using CanDoItAll.Modules.LlmChats.Common;
using CanDoItAll.Modules.LlmChats.Operations;
using CanDoItAll.Modules.LlmChats.Ports;

namespace CanDoItAll.Modules.LlmChats.Application;

public sealed class LlmChatExecutionLeaseService(
    ILlmChatOperationRepository operationRepository,
    ILlmChatUnitOfWork unitOfWork,
    LlmChatExecutionLeaseOptions options,
    TimeProvider timeProvider)
{
    public Task<LlmChatExecutionClaimResult> TryClaimAsync(
        LlmChatOperationId operationId,
        LlmChatExecutionOwnerId ownerId,
        CancellationToken cancellationToken = default)
        => unitOfWork.ExecuteAsync(async token =>
        {
            var operation = await operationRepository.TryGetForUpdateAsync(operationId, token)
                .ConfigureAwait(false);
            if (operation is null || operation.IsTerminal ||
                operation.Status == LlmChatOperationStatus.RecoveryRequired)
            {
                return LlmChatExecutionClaimResult.Unavailable;
            }

            var now = timeProvider.GetUtcNow();
            if (operation.HasLiveExecutionLease(now))
            {
                return LlmChatExecutionClaimResult.Unavailable;
            }

            if (operation.ProviderDispatchStartedAtUtc is not null)
            {
                var recovery = LlmChatOperationTransitions.RequireRecovery(
                    operation,
                    LlmChatErrorCodes.OperationRecoveryRequired);
                await ReplaceRequiredAsync(operation, recovery, token).ConfigureAwait(false);
                return new LlmChatExecutionClaimResult(recovery, null, Recovered: true);
            }

            var claimed = LlmChatOperationTransitions.ClaimExecution(
                operation,
                ownerId,
                now,
                now + options.LeaseDuration);
            await ReplaceRequiredAsync(operation, claimed, token).ConfigureAwait(false);
            return new LlmChatExecutionClaimResult(
                claimed,
                new LlmChatExecutionLeaseIdentity(claimed.Id, ownerId, claimed.ExecutionEpoch),
                Recovered: false);
        }, cancellationToken);

    private async Task ReplaceRequiredAsync(
        LlmChatOperation current,
        LlmChatOperation replacement,
        CancellationToken cancellationToken)
    {
        if (!await operationRepository.TryReplaceAsync(
                replacement,
                current.ConcurrencyToken,
                cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("The LLM Chat execution lease transition lost its locked operation row.");
        }
    }
}

public sealed record LlmChatExecutionClaimResult(
    LlmChatOperation? Operation,
    LlmChatExecutionLeaseIdentity? Lease,
    bool Recovered)
{
    public static LlmChatExecutionClaimResult Unavailable { get; } = new(null, null, false);

    public bool Claimed => Operation is not null && Lease is not null;
}
