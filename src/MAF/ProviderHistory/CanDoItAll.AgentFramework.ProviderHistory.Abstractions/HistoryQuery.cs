namespace CanDoItAll.AgentFramework.ProviderHistory;

public abstract record HistoryProviderScope {
    private HistoryProviderScope() { }
    public sealed record AllAuthorized : HistoryProviderScope;
    public sealed record SingleProvider(ProviderIdentity Provider) : HistoryProviderScope;
}

public sealed record ProviderRequestHistoryQuery(
    HistoryProviderScope Scope, DateTimeOffset FromUtc, DateTimeOffset ToUtc) {
    public ProviderModelIdentity? Model { get; init; }
    public HistoryWorkload? Workload { get; init; }
    public HistoryOperation? Operation { get; init; }
    public HistoryOutcome? Outcome { get; init; }
    public HistoryPriceState? PriceState { get; init; }
    public ManagedCredentialId? CredentialId { get; init; }
    public string? Issuer { get; init; }
    public string? Subject { get; init; }
    public ProviderRequestId? RequestId { get; init; }
    public ProviderAttemptId? AttemptId { get; init; }
    public string? CorrelationId { get; init; }
    public HistoryExternalReference? ExternalReference { get; init; }
    public int PageSize { get; init; } = 50;
    public string? Cursor { get; init; }
}

public sealed record HistoryPage(
    IReadOnlyList<HistoryEntry> Entries, string? NextCursor,
    HistoryCoverage Coverage, DateTimeOffset QueriedAtUtc);

public sealed record HistoryMetadata(HistoryEntry Entry, IReadOnlyList<HistoryOwnerLink> Owners, bool HasMoreOwners = false);

public sealed record HistoryAccessContext(
    HistoryPartition Partition, HistoryExecutionFence Fence,
    HistoryCaller Caller, IReadOnlySet<ProviderIdentity>? AllowedProviders) {
    public string AuthorizationStamp { get; init; } = "";
}

public interface IProviderHistoryAccess {
    Task<HistoryAccessContext> AuthorizeAsync(HistoryPermission permission, CancellationToken cancellationToken);
    Task EnsureCurrentAsync(HistoryAccessContext context, HistoryPermission permission, CancellationToken cancellationToken);
    Task AuthorizeOwnerAsync(HistoryAccessContext context, CanonicalEvidenceReference owner, CancellationToken cancellationToken);
}

public interface IProviderRequestHistory {
    Task<HistoryPage> SearchAsync(ProviderRequestHistoryQuery query, CancellationToken cancellationToken);
    Task<HistoryMetadata?> GetMetadataAsync(HistoryEntryId entryId, CancellationToken cancellationToken);
    Task<HistoryDetail> GetDetailAsync(HistoryEntryId entryId, CanonicalEvidenceReference? owner, CancellationToken cancellationToken);
}
