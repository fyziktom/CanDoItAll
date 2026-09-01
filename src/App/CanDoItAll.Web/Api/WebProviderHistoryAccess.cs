using CanDoItAll.AgentFramework.ProviderHistory;
using CanDoItAll.Infrastructure.Persistence;

namespace CanDoItAll.Web.Api;

internal sealed class WebProviderHistoryAccess(
    WebHistoryPrincipalResolver principals,
    IProviderHistoryPartition partitions,
    IDatabaseRuntimeState runtime,
    IDatabaseRuntimeWriteFence writeFence) : IProviderHistoryAccess {
    public async Task<HistoryAccessContext> AuthorizeAsync(HistoryPermission permission, CancellationToken cancellationToken) {
        var expected = runtime.GetSnapshot();
        var principal = await principals.ResolveAsync(permission, cancellationToken);
        HistoryPartition partition;
        try {
            partition = await writeFence.ExecuteAsync(expected, token => partitions.GetAsync(token), cancellationToken);
        } catch (DatabaseRuntimeProfileChangedException) {
            throw Stale();
        }
        if (runtime.GetSnapshot() != expected) {
            throw Stale();
        }
        return new(partition, new(expected.Generation, principal.Revision), principal.Caller, null) { AuthorizationStamp = principal.Stamp };
    }

    public async Task EnsureCurrentAsync(HistoryAccessContext context, HistoryPermission permission, CancellationToken cancellationToken) {
        var expected = runtime.GetSnapshot();
        if (expected.Generation != context.Fence.ProfileGeneration) {
            throw Stale();
        }
        var principal = await principals.ResolveAsync(permission, cancellationToken);
        var partition = await partitions.GetAsync(cancellationToken);
        if (runtime.GetSnapshot() != expected || partition != context.Partition ||
            principal.Stamp != context.AuthorizationStamp || principal.Caller != context.Caller) {
            throw Stale();
        }
    }

    public async Task AuthorizeOwnerAsync(HistoryAccessContext context, CanonicalEvidenceReference owner, CancellationToken cancellationToken) {
        if (owner.Partition != context.Partition || runtime.GetSnapshot().Generation != context.Fence.ProfileGeneration) {
            throw Stale();
        }
        var principal = await principals.ResolveAsync(HistoryPermission.ReadContent, cancellationToken);
        if (principal.Stamp != context.AuthorizationStamp) {
            throw Stale();
        }
        await principals.RequireOwnerAsync(principal, owner.Kind, cancellationToken);
    }

    private static ProviderHistoryException Stale() =>
        new(HistoryFailure.StaleContext, "The active database or authorization changed. Run Search again.");
}
