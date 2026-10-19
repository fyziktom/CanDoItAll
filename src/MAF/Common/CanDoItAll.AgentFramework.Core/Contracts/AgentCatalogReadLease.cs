using System.Collections.Immutable;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public interface IAgentCatalogReadLeaseStore {
    Task<IAgentPlanningPublicationSnapshotLease> AcquirePlanningAuthoritySnapshotAsync(AgentPlanningPublicationExpectation expected,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("This catalog store does not support a held original-run authorization snapshot.");

    Task<IAgentCatalogReadLease> AcquireAgentReadLeaseAsync(Guid agentId, CancellationToken cancellationToken = default);
    Task<IAgentCatalogReadLease> AcquireAgentsReadLeaseAsync(IReadOnlyList<Guid> agentIds, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("This catalog store does not support one coherent lease for multiple selected Agents.");
}

public interface IAgentCatalogReadLease : IAsyncDisposable {
    WorkspaceScopeDescriptor Scope { get; }
    CatalogDataRevision Revision { get; }
    AgentDefinition? Agent { get; }
    ImmutableArray<AgentDefinition> Agents => throw new NotSupportedException(
        "This catalog lease does not expose a coherent selected-Agent snapshot.");
    ImmutableArray<CapabilityCatalogItem> Capabilities => throw new NotSupportedException(
        "This catalog lease does not expose a coherent capability snapshot.");
    Task<IAgentExecutionAdmissionReadLease> AcquireExecutionAdmissionReadLeaseAsync(ISandboxWorkspaceExecutionRunStore executions,
        Guid executionRunId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("This catalog lease cannot hold the original execution publication exclusion.");
}
