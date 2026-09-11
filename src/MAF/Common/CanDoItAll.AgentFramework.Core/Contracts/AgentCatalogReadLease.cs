using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public interface IAgentCatalogReadLeaseStore {
    Task<IAgentCatalogReadLease> AcquireAgentReadLeaseAsync(Guid agentId, CancellationToken cancellationToken = default);
}

public interface IAgentCatalogReadLease : IAsyncDisposable {
    WorkspaceScopeDescriptor Scope { get; }
    CatalogDataRevision Revision { get; }
    AgentDefinition? Agent { get; }
}
