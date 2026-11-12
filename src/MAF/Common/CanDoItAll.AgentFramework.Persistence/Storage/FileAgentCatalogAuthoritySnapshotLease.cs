using System.Collections.Immutable;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Persistence;

internal sealed class FileAgentCatalogAuthoritySnapshotLease(IAsyncDisposable owner, WorkspaceScopeDescriptor scope,
    CatalogDataRevision revision, Guid primaryAgentId, ImmutableArray<AgentDefinition> agents) : IAgentCatalogReadLease {
    private IAsyncDisposable? held = owner;

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
            return agents.SingleOrDefault(item => item.Id == primaryAgentId);
        }
    }

    public ImmutableArray<AgentDefinition> Agents {
        get {
            RequireActive();
            return agents;
        }
    }

    public async ValueTask DisposeAsync() {
        if (Interlocked.Exchange(ref held, null) is { } acquired) {
            await acquired.DisposeAsync().ConfigureAwait(false);
        }
    }

    private void RequireActive() => ObjectDisposedException.ThrowIf(Volatile.Read(ref held) is null, this);
}
