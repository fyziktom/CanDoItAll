namespace CanDoItAll.AgentFramework.ProviderHistory;

public sealed class UnavailableProviderHistoryAccess : IProviderHistoryAccess {
    public Task<HistoryAccessContext> AuthorizeAsync(HistoryPermission permission, CancellationToken cancellationToken) => throw Denied();
    public Task EnsureCurrentAsync(HistoryAccessContext context, HistoryPermission permission, CancellationToken cancellationToken) => throw Denied();
    public Task AuthorizeOwnerAsync(HistoryAccessContext context, CanonicalEvidenceReference owner, CancellationToken cancellationToken) => throw Denied();

    private static ProviderHistoryException Denied() =>
        new(HistoryFailure.Denied, "This host has not supplied trusted provider-history authority.");
}
