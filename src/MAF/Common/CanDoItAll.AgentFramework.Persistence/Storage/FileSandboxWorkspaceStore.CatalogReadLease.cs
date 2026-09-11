using System.Collections.Immutable;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Persistence;

public sealed partial class FileSandboxWorkspaceStore : IAgentCatalogReadLeaseStore {
    public async Task<IAgentCatalogReadLease> AcquireAgentReadLeaseAsync(Guid agentId, CancellationToken cancellationToken = default) {
        if (agentId == Guid.Empty) {
            throw new ArgumentException("An agent identifier is required for a catalog read lease.", nameof(agentId));
        }
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        IAsyncDisposable? workspaceLock = null;
        try {
            workspaceLock = await crossProcessLock.AcquireAsync(cancellationToken).ConfigureAwait(false);
            await EnsureCatalogReadCoreAsync(cancellationToken).ConfigureAwait(false);
            var catalog = await LoadNormalizedCatalogCoreAsync(cancellationToken).ConfigureAwait(false);
            var agent = catalog.Agents.SingleOrDefault(item => item.Id == agentId);
            if (agent is not null) {
                agent = agent with {
                    Permissions = agent.Permissions with { AllowedSecrets = agent.Permissions.NormalizedAllowedSecrets.ToImmutableArray() },
                    Capabilities = agent.Capabilities.ToImmutableArray(),
                    Tags = agent.Tags.ToImmutableArray()
                };
            }
            return new AgentCatalogReadLease(gate, workspaceLock, layout.Scope, catalog.CatalogDataRevision, agent);
        } catch {
            try {
                if (workspaceLock is not null) {
                    await workspaceLock.DisposeAsync().ConfigureAwait(false);
                }
            } finally {
                gate.Release();
            }
            throw;
        }
    }

    private sealed class AgentCatalogReadLease(SemaphoreSlim gate, IAsyncDisposable workspaceLock,
        WorkspaceScopeDescriptor scope, CatalogDataRevision revision, AgentDefinition? agent) : IAgentCatalogReadLease {
        private IAsyncDisposable? heldLock = workspaceLock;

        public WorkspaceScopeDescriptor Scope {
            get {
                RequireActive();
                return scope;
            }
        }

        public CatalogDataRevision Revision {
            get {
                RequireActive();
                return revision;
            }
        }

        public AgentDefinition? Agent {
            get {
                RequireActive();
                return agent;
            }
        }

        public async ValueTask DisposeAsync() {
            var acquired = Interlocked.Exchange(ref heldLock, null);
            if (acquired is null) {
                return;
            }
            try {
                await acquired.DisposeAsync().ConfigureAwait(false);
            } finally {
                gate.Release();
            }
        }

        private void RequireActive() => ObjectDisposedException.ThrowIf(Volatile.Read(ref heldLock) is null, this);
    }
}
