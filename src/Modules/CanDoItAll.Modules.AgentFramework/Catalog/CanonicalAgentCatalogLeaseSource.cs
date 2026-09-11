using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.Infrastructure.Configuration;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Projects;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class CanonicalAgentCatalogLeaseSource : IAgentCatalogReadLeaseStore {
    private readonly FileSandboxWorkspaceStore store;

    public CanonicalAgentCatalogLeaseSource(ICanonicalRuntimeDatabase database, IOptions<StorageOptions> storageOptions,
        IHostEnvironment environment, ProjectWriteAdmissionService projectAdmissions) {
        var profile = database.Profile;
        var paths = new WorkspacePathResolver(storageOptions, environment, new BoundProfile(profile));
        store = new(paths.ResolveWorkspaceRoot(), WorkspaceScopeDescriptor.Organization(profile.Profile.Id.ToString("N")),
            new AgentProjectAccessCatalogPolicy(projectAdmissions, profile.Profile.Id));
    }

    public Task<IAgentCatalogReadLease> AcquireAgentReadLeaseAsync(Guid agentId, CancellationToken cancellationToken = default)
        => store.AcquireAgentReadLeaseAsync(agentId, cancellationToken);

    private sealed class BoundProfile(ResolvedDatabaseProfile profile) : IActiveDatabaseProfileResolver {
        public ResolvedDatabaseProfile ResolveCurrentProfile() => profile;
    }
}
