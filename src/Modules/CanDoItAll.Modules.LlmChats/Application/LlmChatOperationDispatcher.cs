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
    LlmChatOperationEventRetentionService eventRetention,
    ILlmChatOperationDispatchSignal dispatchSignal,
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
                await eventRetention.ApplyIfDueAsync(token).ConfigureAwait(false);
                var candidates = await operationReadStore.ListDispatchCandidatesAsync(
                    timeProvider.GetUtcNow(),
                    options.CandidateBatchSize,
                    token).ConfigureAwait(false);
                foreach (var operationId in candidates)
                {
                    LlmChatExecutionClaimResult claim;
                    using (dispatchSignal.BeginProgress())
                    {
                        claim = await leaseService.TryClaimAsync(operationId, ownerId, token)
                            .ConfigureAwait(false);
                        if (claim.Claimed)
                        {
                            await executor.ExecuteAsync(claim, token).ConfigureAwait(false);
                        }
                    }

                    if (claim.Recovered)
                    {
                        return Result<bool>.Success(true);
                    }

                    if (!claim.Claimed)
                    {
                        continue;
                    }

                    return Result<bool>.Success(true);
                }

                if (candidates.Count > 0)
                {
                    var availability = dispatchSignal.Availability;
                    logger.LogDebug(
                        "LLM Chat dispatcher made no progress for {CandidateCount} candidate(s). RegisteredWorkers={RegisteredWorkers} ProgressingWorkers={ProgressingWorkers} Saturated={Saturated}.",
                        candidates.Count,
                        availability.RegisteredWorkers,
                        availability.ProgressingWorkers,
                        availability.IsSaturated);
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
