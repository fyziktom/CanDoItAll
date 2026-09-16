using System.Collections.Immutable;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Tooling;

public interface IAgentWorkspaceToolResultSource {
    ValueTask<AgentToolProtocolEnvelope> CaptureAsync(AgentRuntimeToolProviderContext context,
        WorkspaceScopeDescriptor workspaceScope, CancellationToken cancellationToken = default);

    ValueTask<AgentToolProtocolEnvelope> CompleteAsync(AgentToolProtocolEnvelope original,
        CancellationToken cancellationToken = default);

    ValueTask<IAgentWorkspaceToolResultReadLease> AcquireReadAsync(AgentRuntimeToolProviderContext context,
        WorkspaceScopeDescriptor workspaceScope, AgentToolProtocolEnvelope original,
        CancellationToken cancellationToken = default);
}

public interface IAgentWorkspaceToolResultReadLease : IAsyncDisposable {
    AgentDefinition Agent { get; }
    ImmutableArray<CapabilityCatalogItem> Capabilities { get; }
    void RequireCurrent();
}
