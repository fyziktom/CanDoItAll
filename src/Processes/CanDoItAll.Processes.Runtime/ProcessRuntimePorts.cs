using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Core;

namespace CanDoItAll.Processes.Runtime;

public interface IProcessRuntimeStateStore
{
    Task<ProcessRuntimeStateSnapshot?> LoadAsync(
        ProcessRunId runId,
        CancellationToken cancellationToken = default);
}

public enum ProcessRuntimeActivitySelectionMode
{
    Active,
    RecentFallback
}

public sealed record ProcessRuntimeActivityQuery
{
    public const int DefaultTake = 5;
    public const int MaximumTake = 5;

    public ProcessRuntimeActivityQuery(int take = DefaultTake)
    {
        if (take is < 1 or > MaximumTake)
        {
            throw new ArgumentOutOfRangeException(
                nameof(take),
                take,
                $"Process runtime activity take must be between 1 and {MaximumTake}.");
        }

        Take = take;
    }

    public int Take { get; }
}

public sealed record ProcessRuntimeActivityRow(
    ProcessRunId RootRunId,
    ProcessRunId RunId,
    ProcessRuntimeStatus Status,
    DateTimeOffset UpdatedAtUtc);

public sealed record ProcessRuntimeActivitySelection(
    ProcessRuntimeActivitySelectionMode Mode,
    IReadOnlyList<ProcessRuntimeActivityRow> Runs);

public interface IProcessRuntimeActivityStore
{
    Task<ProcessRuntimeActivitySelection> QueryActivityAsync(
        ProcessRuntimeActivityQuery query,
        CancellationToken cancellationToken = default);
}

public interface IProcessRuntimeRunHierarchyStore
{
    Task<IReadOnlyList<ProcessRunId>> FindCancellableDescendantRunIdsAsync(
        ProcessRunId rootRunId,
        CancellationToken cancellationToken = default);
}

public interface IProcessRuntimeUnitOfWork
{
    Task<ProcessRuntimeCommitResult> CommitAsync(
        ProcessRuntimeCommitRequest request,
        CancellationToken cancellationToken = default);
}

public interface IProcessRuntimeEventStore
{
    Task AppendAsync(
        IReadOnlyList<ProcessRuntimeEventEnvelope> events,
        CancellationToken cancellationToken = default);
}

public interface IProcessOutboxWriter
{
    Task EnqueueAsync(
        IReadOnlyList<ProcessOutboxMessage> messages,
        CancellationToken cancellationToken = default);
}

public interface IProcessArtifactLedgerStore
{
    Task AppendAsync(
        IReadOnlyList<ProcessArtifactLedgerEvent> ledgerEvents,
        CancellationToken cancellationToken = default);
}

public interface IProcessIdempotencyStore
{
    Task<ProcessRuntimeCommitResult?> FindCompletedCommandAsync(
        RuntimeCommandId commandId,
        CancellationToken cancellationToken = default);
}

public enum ProcessOutboxSubscriberKind
{
    RuntimeProjection,
    OperatorNotification,
    ExternalAdapter
}

public sealed record ProcessOutboxMessage(
    RuntimeOutboxMessageId MessageId,
    RuntimeEventId EventId,
    ProcessOutboxSubscriberKind SubscriberKind,
    string PayloadHash);

public sealed record ProcessArtifactLedgerEvent(
    ArtifactLedgerEventId LedgerEventId,
    RuntimeEventId EventId,
    ArtifactSlotId SlotId,
    ArtifactInstanceId ArtifactId,
    string ContentHash);
