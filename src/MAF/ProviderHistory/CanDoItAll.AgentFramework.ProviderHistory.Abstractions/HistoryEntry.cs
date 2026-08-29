namespace CanDoItAll.AgentFramework.ProviderHistory;

public sealed record HistoryEntry(
    HistoryEntryId Id,
    HistoryPartition Partition,
    ProviderRequestId? RequestId,
    ProviderAttemptId? AttemptId,
    HistoryGranularity Granularity,
    DateTimeOffset SortAtUtc,
    HistoryTimeBasis TimeBasis,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? FinishedAtUtc,
    HistoryProvider Provider,
    HistoryOperation Operation,
    HistoryWorkload Workload,
    HistoryOutcome Outcome,
    HistoryCaller Caller,
    HistoryUsage Usage,
    HistoryPrice Price,
    HistoryMetadataAuthority MetadataAuthority,
    HistoryRetentionAuthority RetentionAuthority,
    HistoryDetailState DetailState) {
    public string? CorrelationId { get; init; }
    public RemoteRequestReference? RemoteRequest { get; init; }
    public DateTimeOffset? ExpiresAtUtc { get; init; }
    public long Version { get; init; }
}

public sealed record HistoryAttemptStart(
    HistoryEntryId EntryId,
    HistoryPartition Partition,
    HistoryExecutionFence Fence,
    ProviderRequestId RequestId,
    ProviderAttemptId AttemptId,
    DateTimeOffset StartedAtUtc,
    HistoryProvider Provider,
    HistoryOperation Operation,
    HistoryWorkload Workload,
    HistoryCaller Caller,
    HistoryPolicySnapshot Policy,
    CanonicalEvidenceReference? ContentOwner = null,
    string? CorrelationId = null);

public sealed record HistoryAttemptCompletion(
    HistoryOutcome Outcome,
    DateTimeOffset FinishedAtUtc,
    HistoryUsage Usage,
    HistoryPrice Price,
    RemoteRequestReference? RemoteRequest = null) {
    public long? ResponseOriginalBytes { get; init; }
}

public sealed record HistoryCurrentTurn(string Input, long InputRevision);
public sealed record HistoryCapturedText(
    string Text, long OriginalBytes, int CapturedBytes, HistoryDetailFlags Flags);

public sealed record HistoryDetail(
    HistoryEntryId EntryId,
    HistoryDetailState State,
    HistoryCapturedText? Input = null,
    HistoryCapturedText? Response = null,
    DateTimeOffset? ExpiresAtUtc = null);
