using CanDoItAll.Memory.Abstractions;

namespace CanDoItAll.Memory.Application;

public sealed record MemoryAsyncWorkerOptions(
    int MaxBatchSize,
    TimeSpan PollingStaleAfter,
    int MaxRetryAttempts,
    TimeSpan EventRetentionExpiresAfter,
    TimeSpan EventRetentionForgetsAfter,
    MemoryEventLoopGuardPolicy EventLoopGuardPolicy)
{
    public static readonly MemoryAsyncWorkerOptions Default = new(
        MaxBatchSize: 25,
        PollingStaleAfter: TimeSpan.FromSeconds(15),
        MaxRetryAttempts: 3,
        EventRetentionExpiresAfter: TimeSpan.FromDays(7),
        EventRetentionForgetsAfter: TimeSpan.FromDays(30),
        MemoryEventLoopGuardPolicy.Default);

    public void Validate()
    {
        if (MaxBatchSize < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxBatchSize), "Worker batch size must be positive.");
        }

        if (PollingStaleAfter < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(PollingStaleAfter), "Polling staleness cannot be negative.");
        }

        if (MaxRetryAttempts < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxRetryAttempts), "At least one retry attempt must be allowed.");
        }

        if (EventRetentionExpiresAfter <= TimeSpan.Zero || EventRetentionForgetsAfter < EventRetentionExpiresAfter)
        {
            throw new ArgumentOutOfRangeException(nameof(EventRetentionForgetsAfter), "Event retention must expire before it is forgotten.");
        }
    }
}

public sealed record MemoryAsyncWorkerRunResult(
    int Scanned,
    int Completed,
    int Retried,
    int DeadLettered,
    int TimedOut,
    int Cancelled,
    int Enqueued,
    int Duplicates,
    int LoopRejected,
    int IpfsUnpinRequests,
    IReadOnlyList<string> Diagnostics)
{
    public static readonly MemoryAsyncWorkerRunResult Empty = new(
        Scanned: 0,
        Completed: 0,
        Retried: 0,
        DeadLettered: 0,
        TimedOut: 0,
        Cancelled: 0,
        Enqueued: 0,
        Duplicates: 0,
        LoopRejected: 0,
        IpfsUnpinRequests: 0,
        Diagnostics: []);
}

public interface IMemoryAsyncOperationWorker
{
    Task<MemoryAsyncWorkerRunResult> PollOperationsAsync(CancellationToken cancellationToken = default);

    Task<MemoryOperationRecord> CancelOperationAsync(
        MemoryOperationId operationId,
        string reason,
        CancellationToken cancellationToken = default);
}

public interface IMemoryFeedbackWorker
{
    Task<MemoryAsyncWorkerRunResult> DeliverPendingFeedbackAsync(CancellationToken cancellationToken = default);
}

public interface IMemoryProviderEventWorker
{
    Task<MemoryAsyncWorkerRunResult> PollProviderEventsAsync(CancellationToken cancellationToken = default);

    Task<MemoryEventAdmissionResult> AdmitProviderEventAsync(
        MemoryProviderProfile provider,
        MemoryProviderEvent providerEvent,
        MemoryEventLoopContext loopContext,
        CancellationToken cancellationToken = default);

    Task<MemoryAsyncWorkerRunResult> DrainInboxAsync(CancellationToken cancellationToken = default);

    Task<MemoryAsyncWorkerRunResult> DrainOutboxAsync(CancellationToken cancellationToken = default);
}

public interface IMemoryRetentionWorker
{
    Task<MemoryAsyncWorkerRunResult> ApplyDueRetentionAsync(CancellationToken cancellationToken = default);
}

public enum MemoryProviderOperationPollResultKind
{
    OperationResult = 0,
    StillRunning = 1,
    RetryableFailure = 2,
    TerminalFailure = 3,
    UnsupportedCapability = 4
}

public sealed record MemoryProviderOperationPollResult(
    MemoryProviderOperationPollResultKind Kind,
    MemoryOperationResult? OperationResult,
    string Diagnostic)
{
    public static MemoryProviderOperationPollResult FromResult(
        MemoryOperationResult operationResult,
        string diagnostic) =>
        new(MemoryProviderOperationPollResultKind.OperationResult, operationResult, diagnostic);

    public static MemoryProviderOperationPollResult StillRunning(string diagnostic) =>
        new(MemoryProviderOperationPollResultKind.StillRunning, OperationResult: null, diagnostic);

    public static MemoryProviderOperationPollResult RetryableFailure(string diagnostic) =>
        new(MemoryProviderOperationPollResultKind.RetryableFailure, OperationResult: null, diagnostic);

    public static MemoryProviderOperationPollResult TerminalFailure(string diagnostic) =>
        new(MemoryProviderOperationPollResultKind.TerminalFailure, OperationResult: null, diagnostic);

    public static MemoryProviderOperationPollResult UnsupportedCapability(string diagnostic) =>
        new(MemoryProviderOperationPollResultKind.UnsupportedCapability, OperationResult: null, diagnostic);
}

public enum MemoryProviderQueueDispatchResultKind
{
    Succeeded = 0,
    RetryableFailure = 1,
    TerminalFailure = 2,
    UnsupportedCapability = 3
}

public sealed record MemoryProviderQueueDispatchResult(
    MemoryProviderQueueDispatchResultKind Kind,
    string Diagnostic)
{
    public static MemoryProviderQueueDispatchResult Succeeded(string diagnostic) =>
        new(MemoryProviderQueueDispatchResultKind.Succeeded, diagnostic);

    public static MemoryProviderQueueDispatchResult RetryableFailure(string diagnostic) =>
        new(MemoryProviderQueueDispatchResultKind.RetryableFailure, diagnostic);

    public static MemoryProviderQueueDispatchResult TerminalFailure(string diagnostic) =>
        new(MemoryProviderQueueDispatchResultKind.TerminalFailure, diagnostic);

    public static MemoryProviderQueueDispatchResult UnsupportedCapability(string diagnostic) =>
        new(MemoryProviderQueueDispatchResultKind.UnsupportedCapability, diagnostic);
}

public enum MemoryProviderEventPollResultKind
{
    Events = 0,
    RetryableFailure = 1,
    TerminalFailure = 2,
    UnsupportedCapability = 3
}

public sealed record MemoryProviderEventPollResult(
    MemoryProviderEventPollResultKind Kind,
    IReadOnlyList<MemoryProviderEvent> Events,
    string Diagnostic)
{
    public static MemoryProviderEventPollResult FromEvents(
        IReadOnlyList<MemoryProviderEvent> events,
        string diagnostic) =>
        new(MemoryProviderEventPollResultKind.Events, events, diagnostic);

    public static MemoryProviderEventPollResult RetryableFailure(string diagnostic) =>
        new(MemoryProviderEventPollResultKind.RetryableFailure, Events: [], diagnostic);

    public static MemoryProviderEventPollResult TerminalFailure(string diagnostic) =>
        new(MemoryProviderEventPollResultKind.TerminalFailure, Events: [], diagnostic);

    public static MemoryProviderEventPollResult UnsupportedCapability(string diagnostic) =>
        new(MemoryProviderEventPollResultKind.UnsupportedCapability, Events: [], diagnostic);
}
