using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.Modules.LlmChats.Common;
using CanDoItAll.Modules.LlmChats.Operations;
using CanDoItAll.Modules.LlmChats.Ports;

namespace CanDoItAll.Modules.LlmChats.Application;

public sealed class LlmChatOperationEventJournal(
    ILlmChatOperationRepository operationRepository,
    ILlmChatOperationEventRepository eventRepository,
    ILlmChatUnitOfWork unitOfWork,
    ILlmChatOperationEventSignal signal,
    ILlmChatOperationScopeAccessor operationScope,
    LlmChatStreamingOptions options,
    TimeProvider timeProvider)
{
    public Task<LlmChatOperationEvent> AppendStateChangedAsync(
        LlmChatOperation operation,
        string model = "",
        LlmUsage? usage = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return AppendAsync(
            operation.Id,
            sequence => new LlmChatOperationStateChangedEvent(
                operation.Id,
                sequence,
                operation.Status,
                ResolveStateTimestamp(operation),
                operation.FailureCode,
                model,
                usage),
            cancellationToken);
    }

    public Task<LlmChatOperationEvent> AppendAttemptStartedAsync(
        LlmChatOperationId operationId,
        LlmStreamingAttemptStarted started,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(started);
        return AppendAsync(
            operationId,
            sequence => new LlmChatOperationAttemptStartedEvent(
                operationId,
                sequence,
                started.AttemptOrdinal,
                started.Model,
                started.DeliveryMode,
                started.StartedAtUtc),
            cancellationToken);
    }

    public Task<LlmChatOperationEvent> AppendAttemptFinishedAsync(
        LlmChatInvocationRecord record,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        return AppendAsync(
            record.OperationId,
            sequence => new LlmChatOperationAttemptFinishedEvent(
                record.OperationId,
                sequence,
                record.Ordinal,
                record.Outcome,
                record.Usage,
                record.CompletedAtUtc,
                record.FailureCode),
            cancellationToken);
    }

    public Task<LlmChatOperationEvent> AppendTextDeltaAsync(
        LlmChatOperationId operationId,
        int attemptOrdinal,
        string text,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(text);
        return unitOfWork.ExecuteAsync(async token =>
        {
            var operation = await operationRepository.TryGetForUpdateAsync(operationId, token)
                .ConfigureAwait(false)
                ?? throw new InvalidOperationException("The LLM Chat operation event target does not exist.");
            EnsureDeltaCanBeAppended(operation, occurredAtUtc);
            return await AppendCoreAsync(
                operationId,
                sequence => new LlmChatOperationTextDeltaEvent(
                    operationId,
                    sequence,
                    attemptOrdinal,
                    text,
                    occurredAtUtc),
                token).ConfigureAwait(false);
        }, cancellationToken);
    }

    public Task<LlmChatOperationEventPage?> ListAfterAsync(
        LlmChatOperationId operationId,
        long afterSequence,
        int take,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(afterSequence);
        ArgumentOutOfRangeException.ThrowIfLessThan(take, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(take, options.MaximumReplayPageSize);
        return eventRepository.ListAfterAsync(operationId, afterSequence, take, cancellationToken);
    }

    public ValueTask WaitAsync(
        LlmChatOperationId operationId,
        long afterSequence,
        TimeSpan maximumDelay,
        CancellationToken cancellationToken = default)
    {
        var identity = operationScope.Current?.RuntimeIdentity
            ?? throw new InvalidOperationException("An LLM Chat runtime operation scope is required for event waits.");
        return signal.WaitAsync(identity, operationId, afterSequence, maximumDelay, cancellationToken);
    }

    public Task<int> DeleteExpiredTerminalEventsAsync(CancellationToken cancellationToken = default)
    {
        options.Validate();
        return unitOfWork.ExecuteAsync(
            token => eventRepository.DeleteExpiredTerminalEventsAsync(
                timeProvider.GetUtcNow() - options.EventRetention,
                options.CleanupBatchSize,
                token),
            cancellationToken);
    }

    private Task<LlmChatOperationEvent> AppendAsync(
        LlmChatOperationId operationId,
        Func<long, LlmChatOperationEvent> createEvent,
        CancellationToken cancellationToken)
        => unitOfWork.ExecuteAsync(
            token => AppendCoreAsync(operationId, createEvent, token),
            cancellationToken);

    private async Task<LlmChatOperationEvent> AppendCoreAsync(
        LlmChatOperationId operationId,
        Func<long, LlmChatOperationEvent> createEvent,
        CancellationToken cancellationToken)
    {
        var appended = await eventRepository.AppendAsync(
            operationId,
            createEvent,
            cancellationToken).ConfigureAwait(false);
        if (operationScope.Current?.RuntimeIdentity is { } identity)
        {
            unitOfWork.RegisterPostCommit(() => signal.Publish(identity, operationId, appended.Sequence));
        }

        return appended;
    }

    private void EnsureDeltaCanBeAppended(LlmChatOperation operation, DateTimeOffset observedAtUtc)
    {
        var lease = operationScope.Current?.ExecutionLease
            ?? throw new InvalidOperationException("An LLM Chat execution lease is required for text deltas.");
        if (operation.Status == LlmChatOperationStatus.CancellationRequested)
        {
            throw new OperationCanceledException("The durable LLM Chat operation was cancelled.");
        }

        if (operation.Status != LlmChatOperationStatus.Running)
        {
            throw new InvalidOperationException("Text deltas can only be appended to a running LLM Chat operation.");
        }

        if (lease.OperationId != operation.Id ||
            operation.ExecutionOwnerId != lease.OwnerId ||
            operation.ExecutionEpoch != lease.Epoch ||
            operation.LeaseExpiresAtUtc <= observedAtUtc)
        {
            throw new OperationCanceledException("The LLM Chat execution lease no longer owns the operation.");
        }
    }

    private static DateTimeOffset ResolveStateTimestamp(LlmChatOperation operation)
        => operation.Status switch
        {
            LlmChatOperationStatus.Pending => operation.StartedAtUtc,
            LlmChatOperationStatus.Running => operation.ClaimedAtUtc ?? operation.StartedAtUtc,
            LlmChatOperationStatus.CancellationRequested =>
                operation.CancellationRequestedAtUtc ?? operation.StartedAtUtc,
            LlmChatOperationStatus.Succeeded or
                LlmChatOperationStatus.Failed or
                LlmChatOperationStatus.Cancelled => operation.CompletedAtUtc ?? operation.StartedAtUtc,
            LlmChatOperationStatus.RecoveryRequired =>
                operation.ProviderDispatchReturnedAtUtc ??
                operation.ProviderDispatchStartedAtUtc ??
                operation.StartedAtUtc,
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation.Status, "Unknown operation status.")
        };
}
