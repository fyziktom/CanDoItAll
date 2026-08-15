using CanDoItAll.Modules.LlmChats.Common;
using CanDoItAll.Modules.LlmChats.Ports;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.LlmChats.Application;

public sealed class LlmChatOperationDispatcher(
    ILlmChatOperationReadStore operationReadStore,
    LlmChatProfileScopeRunner profileScopeRunner,
    LlmChatExecutionLeaseService leaseService,
    LlmChatOperationExecutor executor,
    LlmChatExecutionLeaseOptions options,
    TimeProvider timeProvider,
    ILogger<LlmChatOperationDispatcher> logger)
{
    public async Task<bool> DispatchNextAsync(
        LlmChatExecutionOwnerId ownerId,
        CancellationToken cancellationToken = default)
    {
        var scopeResult = await profileScopeRunner.ExecuteAsync(
            LlmChatOperationId.New(),
            async token =>
            {
                var candidates = await operationReadStore.ListDispatchCandidatesAsync(
                    timeProvider.GetUtcNow(),
                    options.CandidateBatchSize,
                    token).ConfigureAwait(false);
                foreach (var operationId in candidates)
                {
                    var claim = await leaseService.TryClaimAsync(operationId, ownerId, token)
                        .ConfigureAwait(false);
                    if (claim.Recovered)
                    {
                        return Result<bool>.Success(true);
                    }

                    if (!claim.Claimed)
                    {
                        continue;
                    }

                    await executor.ExecuteAsync(claim, token).ConfigureAwait(false);
                    return Result<bool>.Success(true);
                }

                return Result<bool>.Success(false);
            },
            cancellationToken).ConfigureAwait(false);
        if (scopeResult.IsSuccess)
        {
            return scopeResult.Value;
        }

        logger.LogWarning(
            "LLM Chat dispatcher stopped its current profile pass. ErrorCodes={ErrorCodes}.",
            string.Join(',', scopeResult.Errors.Select(error => error.Code)));
        return false;
    }
}
