using CanDoItAll.Memory.Abstractions;

namespace CanDoItAll.Memory.Application;

public sealed record MemoryRuntimeOperationRequest(
    MemoryProviderSelectionPolicy SelectionPolicy,
    MemoryProviderSelectionContext SelectionContext,
    MemoryOperationKind OperationKind,
    MemoryLedgerRequester Requester,
    MemoryCorrelationId CorrelationId,
    MemoryCausationId CausationId,
    IReadOnlyList<MemorySourceSnapshotId> SourceSnapshotIds,
    MemoryLedgerRetentionPolicy Retention)
{
    public MemoryExtensionData Extensions { get; init; } = MemoryExtensionData.Empty;
}

public sealed record MemoryRuntimeOperationResult(
    MemoryProviderSelectionResult Selection,
    MemoryOperationRecord? OperationRecord,
    MemoryContextPack? ContextPack,
    MemoryOperationAccepted? AcceptedOperation,
    bool DriverDispatchAttempted,
    string Diagnostic);

public enum MemoryProviderDriverResultKind
{
    ContextPack = 0,
    OperationAccepted = 1,
    ProviderError = 2,
    Timeout = 3,
    Unavailable = 4,
    UnsupportedCapability = 5
}

public sealed record MemoryProviderDriverResult(
    MemoryProviderDriverResultKind Kind,
    MemoryContextPack? ContextPack,
    MemoryOperationAccepted? AcceptedOperation,
    MemoryLedgerStatus LedgerStatus,
    string Diagnostic)
{
    public static MemoryProviderDriverResult ContextPackResult(
        MemoryContextPack contextPack,
        string diagnostic) =>
        new(
            MemoryProviderDriverResultKind.ContextPack,
            contextPack,
            AcceptedOperation: null,
            MemoryLedgerStatus.Completed,
            diagnostic);

    public static MemoryProviderDriverResult Accepted(
        MemoryOperationAccepted acceptedOperation,
        string diagnostic) =>
        new(
            MemoryProviderDriverResultKind.OperationAccepted,
            ContextPack: null,
            acceptedOperation,
            MemoryLedgerStatus.Running,
            diagnostic);

    public static MemoryProviderDriverResult Failed(
        MemoryProviderDriverResultKind kind,
        string diagnostic) =>
        new(
            kind,
            ContextPack: null,
            AcceptedOperation: null,
            kind == MemoryProviderDriverResultKind.Timeout ? MemoryLedgerStatus.TimedOut : MemoryLedgerStatus.Failed,
            diagnostic);
}

public interface IMemoryProviderHealthDriver
{
    MemoryProviderDriverKind DriverKind { get; }

    Task<MemoryProviderHealth> GetHealthAsync(
        MemoryProviderProfile provider,
        CancellationToken cancellationToken = default);
}

public interface IMemoryProviderDriver
{
    MemoryProviderDriverKind DriverKind { get; }

    Task<MemoryProviderDriverResult> ExecuteContextQueryAsync(
        MemoryProviderProfile provider,
        MemoryOperationRecord operation,
        MemoryContextQueryRequest request,
        CancellationToken cancellationToken = default);
}

public interface IMemoryProviderOperationStatusDriver
{
    MemoryProviderDriverKind DriverKind { get; }

    Task<MemoryProviderOperationPollResult> PollOperationAsync(
        MemoryProviderProfile provider,
        MemoryOperationRecord operation,
        CancellationToken cancellationToken = default);
}

public interface IMemoryProviderFeedbackDeliveryDriver
{
    MemoryProviderDriverKind DriverKind { get; }

    Task<MemoryProviderQueueDispatchResult> DeliverFeedbackAsync(
        MemoryProviderProfile provider,
        MemoryFeedbackRecord feedback,
        CancellationToken cancellationToken = default);
}

public interface IMemoryProviderEventPollDriver
{
    MemoryProviderDriverKind DriverKind { get; }

    Task<MemoryProviderEventPollResult> PollEventsAsync(
        MemoryProviderProfile provider,
        CancellationToken cancellationToken = default);
}

public interface IMemoryProviderEventOutboxDriver
{
    MemoryProviderDriverKind DriverKind { get; }

    Task<MemoryProviderQueueDispatchResult> DeliverOutboxAsync(
        MemoryProviderProfile provider,
        MemoryEventOutboxRecord outbox,
        CancellationToken cancellationToken = default);
}

public interface IMemoryRuntimeService
{
    Task<MemoryRuntimeOperationResult> ExecuteContextQueryAsync(
        MemoryRuntimeOperationRequest request,
        MemoryContextQueryRequest query,
        CancellationToken cancellationToken = default);
}
