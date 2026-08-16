using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.Modules.LlmChats.Common;
using CanDoItAll.Modules.LlmChats.Operations;
using CanDoItAll.Modules.LlmChats.Ports;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.LlmChats.Application;

public sealed class LlmChatExecutionLeaseService(
    ILlmChatOperationRepository operationRepository,
    ILlmChatUnitOfWork unitOfWork,
    LlmChatExecutionLeaseOptions options,
    TimeProvider timeProvider,
    LlmChatOperationEventJournal eventJournal,
    ILogger<LlmChatExecutionLeaseService> logger)
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

            if (operation.ProviderDispatchStartedAtUtc is null &&
                now - operation.StartedAtUtc >= options.MaximumQueuedAge)
            {
                logger.LogWarning(
                    "LLM Chat operation {OperationId} expired before provider dispatch. QueueAge={QueueAge} MaximumQueuedAge={MaximumQueuedAge} DispatchPhase={DispatchPhase}.",
                    operation.Id.Value,
                    now - operation.StartedAtUtc,
                    options.MaximumQueuedAge,
                    operation.DispatchPhase);
                var expired = LlmChatOperationTransitions.CompleteFailure(
                    operation,
                    now,
                    LlmChatErrorCodes.QueueAgeExceeded);
                await ReplaceRequiredAsync(operation, expired, token).ConfigureAwait(false);
                await eventJournal.AppendStateChangedAsync(
                        expired,
                        usage: LlmUsage.Zero,
                        cancellationToken: token)
                    .ConfigureAwait(false);
                return new LlmChatExecutionClaimResult(expired, null, Recovered: true);
            }

            if (operation.ProviderDispatchStartedAtUtc is not null)
            {
                var durationExceeded = now - operation.StartedAtUtc >= options.MaximumOperationDuration;
                logger.LogWarning(
                    "LLM Chat operation {OperationId} requires recovery after provider dispatch. DurationExceeded={DurationExceeded} OperationAge={OperationAge} MaximumOperationDuration={MaximumOperationDuration} DispatchPhase={DispatchPhase}.",
                    operation.Id.Value,
                    durationExceeded,
                    now - operation.StartedAtUtc,
                    options.MaximumOperationDuration,
                    operation.DispatchPhase);
                var recovery = LlmChatOperationTransitions.RequireRecovery(
                    operation,
                    durationExceeded
                        ? LlmChatErrorCodes.OperationDurationExceeded
                        : LlmChatErrorCodes.OperationRecoveryRequired);
                await ReplaceRequiredAsync(operation, recovery, token).ConfigureAwait(false);
                await eventJournal.AppendStateChangedAsync(recovery, cancellationToken: token)
                    .ConfigureAwait(false);
                return new LlmChatExecutionClaimResult(recovery, null, Recovered: true);
            }

            var claimed = LlmChatOperationTransitions.ClaimExecution(
                operation,
                ownerId,
                now,
                now + options.LeaseDuration);
            await ReplaceRequiredAsync(operation, claimed, token).ConfigureAwait(false);
            if (claimed.Status == LlmChatOperationStatus.Running &&
                operation.Status != LlmChatOperationStatus.Running)
            {
                await eventJournal.AppendStateChangedAsync(claimed, cancellationToken: token)
                    .ConfigureAwait(false);
            }
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
