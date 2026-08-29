using CanDoItAll.Infrastructure.Persistence;

namespace CanDoItAll.AgentFramework.ProviderHistory.Persistence;

public sealed class HistoryPartitionRow {
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OriginInstanceId { get; set; } = Guid.NewGuid();
    public string SecurityPartition { get; set; } = HistoryStorageIdentity.DefaultSecurityPartition;
}

public sealed class HistoryStorageIdentity {
    public const int SingletonId = 1;
    public const string DefaultSecurityPartition = "runtime";
    public int Id { get; set; } = SingletonId;
    public Guid PartitionId { get; set; }
}

public sealed class HistoryPolicyRow : IHasConcurrencyToken {
    public Guid PartitionId { get; set; }
    public HistoryCaptureMode CaptureMode { get; set; }
    public int MetadataRetentionDays { get; set; } = 30;
    public int DetailRetentionDays { get; set; } = 7;
    public int MaximumTextBytes { get; set; } = 32 * 1024;
    public long DetailQuotaBytes { get; set; } = 256L * 1024 * 1024;
    public int BatchSize { get; set; } = 500;
    public long UsedDetailBytes { get; set; }
    public long Version { get; set; }
    public Guid ConcurrencyToken { get; set; }
}

public sealed class HistoryPolicyAuditRow {
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PartitionId { get; set; }
    public long Version { get; set; }
    public DateTimeOffset ChangedAtUtc { get; set; }
    public HistoryPolicy Policy { get; set; } = null!;
    public bool AppliedShorterRetention { get; set; }
    public HistoryCaller Caller { get; set; } = new(HistoryAuthenticationKind.Unknown);
}

public sealed class HistoryCheckpointRow : IHasConcurrencyToken {
    public Guid PartitionId { get; set; }
    public HistorySourceKind SourceKind { get; set; }
    public HistoryCoverageState Coverage { get; set; }
    public string? Cursor { get; set; }
    public DateTimeOffset? IndexedThroughUtc { get; set; }
    public string? FailureCode { get; set; }
    public Guid? LeaseOwner { get; set; }
    public DateTimeOffset? LeaseUntilUtc { get; set; }
    public Guid ConcurrencyToken { get; set; }
}
