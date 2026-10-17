using System.Collections.Immutable;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Persistence;

public sealed partial class FileSandboxWorkspaceStore : IAgentCatalogReadLeaseStore {
    private const int MaximumSelectedLeaseAgents = 2;

    public Task<IAgentCatalogReadLease> AcquireAgentReadLeaseAsync(Guid agentId, CancellationToken cancellationToken = default) {
        if (agentId == Guid.Empty) {
            throw new ArgumentException("An agent identifier is required for a catalog read lease.", nameof(agentId));
        }
        return AcquireAgentsReadLeaseAsync([agentId], cancellationToken);
    }

    public async Task<IAgentCatalogReadLease> AcquireAgentsReadLeaseAsync(IReadOnlyList<Guid> agentIds,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(agentIds);
        if (agentIds.Count is < 1 or > MaximumSelectedLeaseAgents || agentIds.Contains(Guid.Empty) || agentIds.Distinct().Count() != agentIds.Count) {
            throw new ArgumentException("A catalog read lease requires one or two distinct nonempty Agent identifiers.", nameof(agentIds));
        }
        var selectedIds = agentIds.ToImmutableArray();
        var operation = await FileSandboxWorkspacePublicationScope.EnterAsync(gate, crossProcessLock, layout,
            runLeaseWriter, cancellationToken).ConfigureAwait(false);
        try {
            await EnsureCatalogReadCoreAsync(operation, cancellationToken).ConfigureAwait(false);
            var catalog = await LoadNormalizedCatalogCoreAsync(operation, cancellationToken).ConfigureAwait(false);
            var agents = selectedIds.Select(id => catalog.Agents.SingleOrDefault(item => item.Id == id))
                .OfType<AgentDefinition>().Select(agent => agent with {
                    Permissions = agent.Permissions with { AllowedSecrets = agent.Permissions.NormalizedAllowedSecrets.ToImmutableArray() },
                    Capabilities = agent.Capabilities.ToImmutableArray(),
                    Tags = agent.Tags.ToImmutableArray()
                }).ToImmutableArray();
            return new AgentCatalogReadLease(operation, layout.Scope, catalog.CatalogDataRevision,
                agents.SingleOrDefault(agent => agent.Id == selectedIds[0]), agents,
                catalog.Capabilities.Select(item => item with { Tags = item.Tags.ToImmutableArray() }).ToImmutableArray());
        } catch (Exception failure) {
            operation.RetainFailure(failure);
            await operation.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private sealed class AgentCatalogReadLease(IAsyncDisposable workspaceLock,
        WorkspaceScopeDescriptor scope, CatalogDataRevision revision, AgentDefinition? agent,
        ImmutableArray<AgentDefinition> agents, ImmutableArray<CapabilityCatalogItem> capabilities) : IAgentCatalogReadLease {
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

        public ImmutableArray<AgentDefinition> Agents {
            get {
                RequireActive();
                return agents;
            }
        }

        public ImmutableArray<CapabilityCatalogItem> Capabilities {
            get {
                RequireActive();
                return capabilities;
            }
        }

        public async ValueTask DisposeAsync() {
            var acquired = Interlocked.Exchange(ref heldLock, null);
            if (acquired is null) {
                return;
            }
            await acquired.DisposeAsync().ConfigureAwait(false);
        }

        private void RequireActive() => ObjectDisposedException.ThrowIf(Volatile.Read(ref heldLock) is null, this);
    }
}
