using CanDoItAll.Infrastructure.Persistence;

namespace CanDoItAll.AgentFramework.ProviderHistory.Persistence;

public sealed class HistorySourceRow : IHasConcurrencyToken {
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PartitionId { get; set; }
    public HistorySourceKind Kind { get; set; }
    public string OwnerId { get; set; } = "";
    public string EvidenceId { get; set; } = "";
    public long Version { get; set; }
    public bool IsDeleted { get; set; }
    public string? MutationHash { get; set; }
    public Guid ConcurrencyToken { get; set; }
}

public sealed class HistoryOwnerRow {
    public Guid PartitionId { get; set; }
    public Guid SourceId { get; set; }
    public Guid EntryId { get; set; }
    public HistoryOwnerRole Role { get; set; }
    public HistoryOwnerState State { get; set; }
}

public sealed class HistoryOutboxRow {
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PartitionId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public HistorySourceMutation Mutation { get; set; } = null!;
    public int Attempts { get; set; }
    public DateTimeOffset RetryAfterUtc { get; set; }
    public string? FailureCode { get; set; }
}
