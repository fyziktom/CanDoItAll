using System.Collections.Immutable;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Modules.AgentFramework;

public sealed partial class CanonicalAgentCatalogLeaseSource {
    private async Task<IAgentCatalogReadLease> AcquireWithRecoveryAsync(Task<IAgentCatalogReadLease> acquisition) {
        var held = await acquisition;
        try {
            if (held.Scope != WorkspaceScopeDescriptor.Organization(sourceRecovery.DatabaseProfileId.ToString("N"))) {
                throw new InvalidOperationException("The acquired catalog does not match its original organization scope.");
            }
            sourceRecovery.RequireCurrentHost();
            return new RecoveryCatalogLease(held, sourceRecovery);
        } catch (Exception failure) {
            try {
                await held.DisposeAsync();
            } catch (Exception cleanupFailure) {
                throw new AggregateException("Catalog recovery capture and lease release both failed.", failure, cleanupFailure);
            }
            throw;
        }
    }

    private sealed class RecoveryCatalogLease(IAgentCatalogReadLease held,
        ProjectDeletionSourceRecovery.CanonicalOrganizationCatalog original) : IAgentCatalogReadLease, IProjectDeletionSourceRecoveryLease {
        private readonly object releaseSync = new();
        private Task? release;

        public WorkspaceScopeDescriptor Scope => held.Scope;
        public CatalogDataRevision Revision => held.Revision;
        public AgentDefinition? Agent => held.Agent;
        public ImmutableArray<AgentDefinition> Agents => held.Agents;
        public ImmutableArray<CapabilityCatalogItem> Capabilities => held.Capabilities;
        public Task<IAgentExecutionAdmissionReadLease> AcquireExecutionAdmissionReadLeaseAsync(ISandboxWorkspaceExecutionRunStore executions,
            Guid executionRunId, CancellationToken cancellationToken = default)
            => held.AcquireExecutionAdmissionReadLeaseAsync(executions, executionRunId, cancellationToken);

        public ProjectDeletionSourceRecovery CaptureDeletionRecoverySource() {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref release) is not null, this);
            if (held.Scope != WorkspaceScopeDescriptor.Organization(original.DatabaseProfileId.ToString("N"))) {
                throw new InvalidOperationException("The acquired catalog scope changed before recovery capture.");
            }
            original.RequireCurrentHost();
            return original;
        }

        public ValueTask DisposeAsync() {
            lock (releaseSync) {
                return new(release ??= ReleaseAsync());
            }
        }

        private async Task ReleaseAsync() => await held.DisposeAsync();
    }
}
