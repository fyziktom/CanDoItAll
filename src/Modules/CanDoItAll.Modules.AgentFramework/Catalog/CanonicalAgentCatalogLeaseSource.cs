using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.Configuration;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Projects;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Modules.AgentFramework;

public sealed partial class CanonicalAgentCatalogLeaseSource : IAgentCatalogReadLeaseStore {
    private readonly FileSandboxWorkspaceStore store;
    private readonly ProjectDeletionSourceRecovery.CanonicalOrganizationCatalog sourceRecovery;

    public CanonicalAgentCatalogLeaseSource(ICanonicalRuntimeDatabase database, IOptions<StorageOptions> storageOptions,
        IHostEnvironment environment, ProjectWriteAdmissionService projectAdmissions) {
        var profile = database.Profile;
        var paths = new WorkspacePathResolver(storageOptions, environment, new BoundProfile(profile));
        sourceRecovery = new(profile.Profile.Id, HostBoundPathPolicy.BindCurrent(paths.ResolveWorkspaceRoot(), DateTimeOffset.UtcNow));
        store = new(sourceRecovery.RootPath, WorkspaceScopeDescriptor.Organization(profile.Profile.Id.ToString("N")),
            new AgentProjectAccessCatalogPolicy(projectAdmissions, profile.Profile.Id));
    }

    public Task<IAgentCatalogReadLease> AcquireAgentReadLeaseAsync(Guid agentId, CancellationToken cancellationToken = default)
        => AcquireWithRecoveryAsync(store.AcquireAgentReadLeaseAsync(agentId, cancellationToken));

    public Task<IAgentCatalogReadLease> AcquireAgentsReadLeaseAsync(IReadOnlyList<Guid> agentIds, CancellationToken cancellationToken = default)
        => AcquireWithRecoveryAsync(store.AcquireAgentsReadLeaseAsync(agentIds, cancellationToken));

    public Task<IAgentCatalogReadLease> AcquireAgentAuthoritySnapshotAsync(IReadOnlyList<Guid> agentIds,
        CancellationToken cancellationToken = default) {
        sourceRecovery.RequireCurrentHost();
        return store.AcquireAgentAuthoritySnapshotAsync(agentIds, cancellationToken);
    }

    public Task<IAgentPlanningPublicationSnapshotLease> AcquirePlanningAuthoritySnapshotAsync(
        AgentPlanningPublicationExpectation expected, CancellationToken cancellationToken = default) {
        sourceRecovery.RequireCurrentHost();
        return store.AcquirePlanningAuthoritySnapshotAsync(expected, cancellationToken);
    }

    private sealed class BoundProfile(ResolvedDatabaseProfile profile) : IActiveDatabaseProfileResolver {
        public ResolvedDatabaseProfile ResolveCurrentProfile() => profile;
    }
}
