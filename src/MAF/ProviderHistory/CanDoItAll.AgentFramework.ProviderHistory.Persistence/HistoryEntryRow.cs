using CanDoItAll.Infrastructure.Persistence;

namespace CanDoItAll.AgentFramework.ProviderHistory.Persistence;

public sealed class HistoryEntryRow : IHasConcurrencyToken {
    public Guid Id { get; set; }
    public Guid PartitionId { get; set; }
    public Guid? RequestId { get; set; }
    public Guid? AttemptId { get; set; }
    public Guid? CaptureHostId { get; set; }
    public HistoryGranularity Granularity { get; set; }
    public DateTimeOffset SortAtUtc { get; set; }
    public HistoryTimeBasis TimeBasis { get; set; }
    public DateTimeOffset? StartedAtUtc { get; set; }
    public DateTimeOffset? FinishedAtUtc { get; set; }
    public Guid? ProviderId { get; set; }
    public string ProviderName { get; set; } = "";
    public string ProviderKind { get; set; } = "";
    public string? RequestedModel { get; set; }
    public string? ResolvedModel { get; set; }
    public HistoryOperation Operation { get; set; }
    public HistoryWorkload Workload { get; set; }
    public HistoryOutcome Outcome { get; set; }
    public HistoryAuthenticationKind AuthenticationKind { get; set; }
    public Guid? CredentialId { get; set; }
    public string? Issuer { get; set; }
    public string? Subject { get; set; }
    public string? CallerName { get; set; }
    public string? CorrelationId { get; set; }
    public HistoryUsageState UsageState { get; set; }
    public long? InputTokens { get; set; }
    public long? OutputTokens { get; set; }
    public long? CachedInputTokens { get; set; }
    public long? CacheWriteTokens { get; set; }
    public long? ReasoningTokens { get; set; }
    public int? ImageCount { get; set; }
    public HistoryPriceState PriceState { get; set; }
    public decimal? Amount { get; set; }
    public string? Currency { get; set; }
    public string? PriceHash { get; set; }
    public string? PriceVersion { get; set; }
    public string? PriceSourceRevision { get; set; }
    public HistoryMetadataAuthority MetadataAuthority { get; set; }
    public HistoryRetentionAuthority RetentionAuthority { get; set; }
    public HistoryDetailState DetailState { get; set; }
    public Guid? InputDetailId { get; set; }
    public DateTimeOffset? ExpiresAtUtc { get; set; }
    public Guid? RemoteSourceId { get; set; }
    public string? RemoteRequestId { get; set; }
    public bool IsVisible { get; set; } = true;
    public long Version { get; set; }
    public Guid ConcurrencyToken { get; set; }
}
