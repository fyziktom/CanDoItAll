using System.Collections.Immutable;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit.AgentFramework;

internal sealed class SourceAuthorityTestCatalogLease(WorkspaceScopeDescriptor scope, AgentDefinition? agent,
    IReadOnlyList<CapabilityCatalogItem> capabilities, Action released) : IAgentCatalogReadLease {
    private bool disposed;
    public WorkspaceScopeDescriptor Scope { get; } = scope;
    public CatalogDataRevision Revision => CatalogDataRevision.Initial;
    public AgentDefinition? Agent { get; } = agent;
    public ImmutableArray<AgentDefinition> Agents { get; } = agent is null ? [] : [agent];
    public ImmutableArray<CapabilityCatalogItem> Capabilities { get; } = capabilities.ToImmutableArray();

    public ValueTask DisposeAsync() {
        if (!disposed) {
            disposed = true;
            released();
        }
        return ValueTask.CompletedTask;
    }
}
