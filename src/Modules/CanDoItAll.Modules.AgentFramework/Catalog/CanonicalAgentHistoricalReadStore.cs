using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.Infrastructure.Configuration;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Modules.AgentFramework;

internal sealed class CanonicalAgentHistoricalReadStore : IAgentHistoricalCatalogReadStore, ISandboxWorkspaceHistoricalReadStore {
    private readonly FileSandboxWorkspaceHistoricalReadStore store;
    private readonly IDatabaseRuntimeState runtimeState;
    private readonly DatabaseRuntimeSnapshot runtimeSnapshot;

    internal CanonicalAgentHistoricalReadStore(ResolvedDatabaseProfile profile, DatabaseRuntimeSnapshot snapshot,
        IDatabaseRuntimeState runtimeState, IOptions<StorageOptions> storageOptions, IHostEnvironment environment) {
        if (snapshot.ActiveProfileId != profile.Profile.Id || snapshot.ActiveProfileId == Guid.Empty ||
            string.IsNullOrWhiteSpace(snapshot.ActiveFingerprint) || snapshot.ActiveFingerprint != profile.Profile.Runtime.Fingerprint ||
            snapshot != runtimeState.GetSnapshot()) {
            throw new NotSupportedException("Historical receipt review requires an initialized unchanged canonical profile.");
        }
        this.runtimeState = runtimeState;
        runtimeSnapshot = snapshot;
        Fence = new(new(profile.Profile.Id, profile.Profile.Runtime.Fingerprint, new(snapshot.Generation)),
            WorkspaceScopeDescriptor.Organization(profile.Profile.Id.ToString("N")));
        var paths = new WorkspacePathResolver(storageOptions, environment, new BoundProfile(profile));
        store = new(paths.ResolveWorkspaceRootForRead(), Fence.Scope);
    }

    internal AgentHistoricalReceiptReadFence Fence { get; }

    internal bool IsCurrent(AgentHistoricalReceiptReadFence fence)
        => fence == Fence && runtimeState.GetSnapshot() == runtimeSnapshot;

    public Task<AgentHistoricalAgentRead> ReadHistoricalAgentAsync(Guid agentId, CancellationToken cancellationToken = default) {
        RequireCurrent();
        return store.ReadHistoricalAgentAsync(agentId, cancellationToken);
    }

    public Task<AgentHistoricalRunRead> ReadHistoricalRunAsync(Guid executionRunId, CancellationToken cancellationToken = default) {
        RequireCurrent();
        return store.ReadHistoricalRunAsync(executionRunId, cancellationToken);
    }

    private void RequireCurrent() {
        if (!IsCurrent(Fence)) {
            throw new AgentHistoricalReceiptContextChangedException();
        }
    }

    private sealed class BoundProfile(ResolvedDatabaseProfile profile) : IActiveDatabaseProfileResolver {
        public ResolvedDatabaseProfile ResolveCurrentProfile() => profile;
    }
}