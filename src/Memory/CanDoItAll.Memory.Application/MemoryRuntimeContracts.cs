using CanDoItAll.Memory.Abstractions;

namespace CanDoItAll.Memory.Application;

public interface IMemoryProviderProfileStore
{
    Task UpsertAsync(
        MemoryProviderProfile profile,
        DateTimeOffset updatedAtUtc,
        CancellationToken cancellationToken = default);

    Task<MemoryProviderProfile?> GetAsync(
        MemoryProviderInstanceId providerId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MemoryProviderProfile>> ListAsync(CancellationToken cancellationToken = default);
}

public interface IMemoryOperationLedgerStore
{
    Task CreateAsync(
        MemoryOperationRecord record,
        CancellationToken cancellationToken = default);

    Task<MemoryOperationRecord?> GetAsync(
        MemoryOperationId operationId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MemoryOperationRecord>> ListDueForPollingAsync(
        DateTimeOffset nowUtc,
        TimeSpan staleAfter,
        int take,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MemoryOperationRecord>> ListByProviderAsync(
        MemoryProviderInstanceId providerInstanceId,
        int take = 50,
        CancellationToken cancellationToken = default);

    Task<MemoryOperationRecord> TransitionAsync(
        MemoryOperationId operationId,
        MemoryLedgerStatus nextStatus,
        DateTimeOffset transitionedAtUtc,
        string reason,
        CancellationToken cancellationToken = default);

    Task<MemoryOperationRecord> TransitionAsync(
        MemoryOperationId operationId,
        MemoryLedgerStatus nextStatus,
        DateTimeOffset transitionedAtUtc,
        string reason,
        MemoryExtensionData extensions,
        CancellationToken cancellationToken = default);

    Task<MemoryOperationRecord> DeferAsync(
        MemoryOperationId operationId,
        DateTimeOffset deferredAtUtc,
        string reason,
        bool incrementRetry,
        CancellationToken cancellationToken = default);
}

public interface IMemoryFeedbackLedgerStore
{
    Task SubmitAsync(
        MemoryFeedbackRecord record,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MemoryFeedbackRecord>> ListDueForDeliveryAsync(
        DateTimeOffset nowUtc,
        TimeSpan staleAfter,
        int take,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MemoryFeedbackRecord>> ListByProviderAsync(
        MemoryProviderInstanceId providerInstanceId,
        CancellationToken cancellationToken = default);

    Task<MemoryFeedbackRecord> TransitionAsync(
        MemoryFeedbackRecordId feedbackRecordId,
        MemoryLedgerStatus nextStatus,
        DateTimeOffset transitionedAtUtc,
        string reason,
        CancellationToken cancellationToken = default);

    Task<MemoryFeedbackRecord> DeferAsync(
        MemoryFeedbackRecordId feedbackRecordId,
        DateTimeOffset deferredAtUtc,
        bool incrementRetry,
        CancellationToken cancellationToken = default);
}

public interface IMemoryEventLedgerStore
{
    Task EnqueueInboxAsync(
        MemoryEventInboxRecord record,
        CancellationToken cancellationToken = default);

    Task EnqueueOutboxAsync(
        MemoryEventOutboxRecord record,
        CancellationToken cancellationToken = default);

    Task<bool> ContainsInboxDedupeKeyAsync(
        MemoryEventDedupeKey dedupeKey,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MemoryEventInboxRecord>> ListPendingInboxAsync(
        MemoryProviderInstanceId providerInstanceId,
        int take = 100,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MemoryEventOutboxRecord>> ListPendingOutboxAsync(
        MemoryProviderInstanceId providerInstanceId,
        int take = 100,
        CancellationToken cancellationToken = default);

    Task<MemoryEventInboxRecord> TransitionInboxAsync(
        MemoryEventInboxRecordId inboxRecordId,
        MemoryLedgerStatus nextStatus,
        DateTimeOffset transitionedAtUtc,
        string reason,
        CancellationToken cancellationToken = default);

    Task<MemoryEventInboxRecord> DeferInboxAsync(
        MemoryEventInboxRecordId inboxRecordId,
        DateTimeOffset deferredAtUtc,
        string reason,
        bool incrementRetry,
        CancellationToken cancellationToken = default);

    Task<MemoryEventOutboxRecord> TransitionOutboxAsync(
        MemoryEventOutboxRecordId outboxRecordId,
        MemoryLedgerStatus nextStatus,
        DateTimeOffset transitionedAtUtc,
        CancellationToken cancellationToken = default);

    Task<MemoryEventOutboxRecord> DeferOutboxAsync(
        MemoryEventOutboxRecordId outboxRecordId,
        DateTimeOffset deferredAtUtc,
        bool incrementRetry,
        CancellationToken cancellationToken = default);
}

public interface IMemorySourceRequestLedgerStore
{
    Task EnqueueAsync(
        MemorySourceIngestionJobRecord record,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MemorySourceIngestionJobRecord>> ListByProviderAsync(
        MemoryProviderInstanceId providerInstanceId,
        CancellationToken cancellationToken = default);
}

public sealed record MemoryRetentionCandidate(
    string LedgerName,
    string RecordId,
    MemoryLedgerRetentionDecision Decision,
    DateTimeOffset DueAtUtc);

public sealed record MemoryRetentionApplicationResult(
    MemoryRetentionCandidate Candidate,
    MemoryLedgerStatus AppliedStatus,
    bool IpfsUnpinRequested);

public interface IMemoryRetentionProjectionStore
{
    Task<IReadOnlyList<MemoryRetentionCandidate>> ListDueAsync(
        DateTimeOffset nowUtc,
        int take,
        CancellationToken cancellationToken = default);

    Task<MemoryRetentionApplicationResult> ApplyAsync(
        MemoryRetentionCandidate candidate,
        DateTimeOffset appliedAtUtc,
        string reason,
        CancellationToken cancellationToken = default);
}
