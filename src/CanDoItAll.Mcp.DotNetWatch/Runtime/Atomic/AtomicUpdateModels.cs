using System.Text.Json.Serialization;

namespace CanDoItAll.Mcp.DotNetWatch.Runtime.Atomic;

public sealed record SlotManifest(
    string SlotId,
    string LogicalAppId,
    string PublishHash,
    string EntryPath,
    string WorkingDirectory,
    IReadOnlyList<string> HealthUrls,
    DateTimeOffset CreatedUtc)
{
    public string? ProjectPath { get; init; }

    public DateTimeOffset? LastActivatedUtc { get; init; }
}

public sealed record LogicalAppRecord(
    string LogicalAppId,
    string? ActiveSessionId,
    RuntimeRevisionData? ActiveRevision,
    string? PreviousSessionId,
    RuntimeRevisionData? PreviousRevision,
    string? CurrentSlotId,
    string? LastCommittedTransactionId,
    bool RollbackAvailable);

public sealed record AtomicTransactionRecord(
    string TransactionId,
    string LogicalAppId,
    string SourceSignature,
    string TargetSlotId,
    string? PreviousActiveSessionId,
    RuntimeRevisionData? PreviousActiveRevision,
    string? CandidateSessionId,
    RuntimeRevisionData? CandidateRevision,
    AtomicTransactionState State,
    DateTimeOffset CreatedUtc)
{
    public DateTimeOffset? CandidateReadyUtc { get; init; }

    public DateTimeOffset? CommittedUtc { get; init; }

    public DateTimeOffset? RolledBackUtc { get; init; }

    public string? FailureSummary { get; init; }
}

public sealed record AtomicStatusSnapshot(
    LogicalAppRecord App,
    SlotManifest? ActiveSlot,
    SlotManifest? CandidateSlot,
    AtomicTransactionRecord? ActiveTransaction);

public sealed record RuntimeSlotState(
    LogicalAppRecord App,
    string LogicalAppDirectory,
    string ActivePointerPath,
    string HistoryDirectory,
    string TransactionsDirectory,
    string SlotADirectory,
    string SlotBDirectory);
