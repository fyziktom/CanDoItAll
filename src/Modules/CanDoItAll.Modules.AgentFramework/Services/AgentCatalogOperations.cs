using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using IProviderRuntimeAdministrationService = CanDoItAll.Modules.AgentFramework.ProviderManagement.IProviderRuntimeAdministrationService;

namespace CanDoItAll.Modules.AgentFramework;

public sealed record AgentCatalogLoadRequest(
    bool Repair,
    IReadOnlyList<AgentDefinition>? Agents = null,
    IReadOnlyList<ProviderProfile>? Providers = null,
    IReadOnlyList<AgentTeamDefinition>? Teams = null);

public interface IAgentCatalogOperations {
    Task<AgentCatalogSnapshot> LoadAsync(AgentCatalogLoadRequest request, CancellationToken cancellationToken = default);
    Task DeleteTeamAsync(Guid teamId, CancellationToken cancellationToken = default);
    Task UpdateMembersAsync(Guid teamId, IReadOnlyList<Guid> agentIds, CancellationToken cancellationToken = default);
}

public sealed class AgentCatalogOperations(
    IAgentFrameworkWorkspaceService workspace,
    IProviderRuntimeAdministrationService providers,
    IAgentFrameworkOrganizationCatalogRepairService repair) : IAgentCatalogOperations {
    public async Task<AgentCatalogSnapshot> LoadAsync(AgentCatalogLoadRequest request, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Repair) {
            await repair.EnsureCurrentOrganizationCatalogAsync(cancellationToken);
        }
        var agentsTask = request.Agents is null
            ? workspace.ListAgentsAsync(includeTemplates: false, cancellationToken)
            : Task.FromResult(request.Agents);
        var providersTask = request.Providers is null
            ? providers.ListProvidersAsync(cancellationToken)
            : Task.FromResult(request.Providers);
        var teamsTask = request.Teams is null
            ? workspace.ListAgentTeamsAsync(cancellationToken)
            : Task.FromResult(request.Teams);
        await Task.WhenAll(agentsTask, providersTask, teamsTask);
        return new((await agentsTask).ToArray(), (await teamsTask).ToArray(),
            (await providersTask).ToDictionary(provider => provider.Id, provider => provider.IsPrivateProvider));
    }

    public Task DeleteTeamAsync(Guid teamId, CancellationToken cancellationToken = default)
        => workspace.DeleteAgentTeamAsync(teamId, cancellationToken);

    public Task UpdateMembersAsync(Guid teamId, IReadOnlyList<Guid> agentIds, CancellationToken cancellationToken = default)
        => workspace.UpdateAgentTeamMembersAsync(teamId, agentIds, cancellationToken);
}
