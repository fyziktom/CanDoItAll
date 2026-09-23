using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.AgentFramework;

public sealed record AgentCapabilitiesCatalog(
    IReadOnlyList<AgentDefinition> Agents,
    IReadOnlyList<CapabilityCatalogItem> Capabilities);

public interface IAgentCapabilitiesReads {
    Task<AgentCapabilitiesCatalog> LoadCatalogAsync(CancellationToken cancellationToken = default);
    Task<AgentEditorModel> ReadEditorAsync(Guid agentId, CancellationToken cancellationToken = default);
}

public sealed class AgentCapabilitiesReads(IAgentFrameworkWorkspaceService workspace) : IAgentCapabilitiesReads {
    public async Task<AgentCapabilitiesCatalog> LoadCatalogAsync(CancellationToken cancellationToken = default) {
        var agents = await workspace.ListAgentsAsync(includeTemplates: false, cancellationToken);
        var capabilities = await workspace.ListCapabilitiesAsync(cancellationToken);
        return new(agents, capabilities);
    }

    public Task<AgentEditorModel> ReadEditorAsync(Guid agentId, CancellationToken cancellationToken = default)
        => workspace.GetAgentEditorAsync(agentId, cancellationToken);
}
