using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Core;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components;

public partial class AgentCatalogPanel
{
    [Parameter]
    public Guid? RequestedAgentId { get; set; }

    [Inject]
    public IAgentFrameworkWorkspaceService WorkspaceService { get; set; } = default!;

    private AgentEditorModel editorModel = new();
    private IReadOnlyList<AgentDefinition> agents = [];
    private IReadOnlyList<ProviderProfile> providers = [];
    private string tagText = string.Empty;
    private string agentSearch = string.Empty;
    private string? message;
    private Guid? linkedPartyId;

    private IReadOnlyList<AgentDefinition> FilteredAgents => agents
        .Where(agent =>
            string.IsNullOrWhiteSpace(agentSearch) ||
            agent.Name.Contains(agentSearch, StringComparison.OrdinalIgnoreCase) ||
            agent.RoleTitle.Contains(agentSearch, StringComparison.OrdinalIgnoreCase) ||
            agent.Summary.Contains(agentSearch, StringComparison.OrdinalIgnoreCase) ||
            agent.Tags.Any(tag => tag.Contains(agentSearch, StringComparison.OrdinalIgnoreCase)))
        .OrderBy(agent => agent.Name)
        .ToList();

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
    }

    protected override async Task OnParametersSetAsync()
    {
        if (RequestedAgentId.HasValue &&
            editorModel.Id != RequestedAgentId)
        {
            await EditAgentAsync(RequestedAgentId.Value);
        }
    }

    private async Task LoadAsync()
    {
        agents = await WorkspaceService.ListAgentsAsync(includeTemplates: false);
        providers = await WorkspaceService.ListProvidersAsync();
        editorModel = await WorkspaceService.GetAgentEditorAsync();
        tagText = string.Empty;
        linkedPartyId = null;
    }

    private async Task SaveAgentAsync()
    {
        editorModel.Tags = tagText
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var agentId = await WorkspaceService.SaveAgentAsync(editorModel);
        agents = await WorkspaceService.ListAgentsAsync(includeTemplates: false);
        providers = await WorkspaceService.ListProvidersAsync();
        message = "Technical agent saved.";
        await EditAgentAsync(agentId);
    }

    private async Task EditAgentAsync(
        Guid agentId)
    {
        editorModel = await WorkspaceService.GetAgentEditorAsync(agentId);
        tagText = string.Join(", ", editorModel.Tags);
        var definition = agents.FirstOrDefault(item => item.Id == agentId);
        var metadata = definition is null
            ? AgentFrameworkCrmHrMetadata.Read(editorModel.ConfigurationJson)
            : AgentFrameworkCrmHrMetadata.Read(definition.ConfigurationJson);
        linkedPartyId = metadata?.PartyId;
    }

    private async Task ResetAgentAsync()
    {
        editorModel = await WorkspaceService.GetAgentEditorAsync();
        tagText = string.Empty;
        linkedPartyId = null;
        message = null;
    }

    private async Task DeleteAgentAsync()
    {
        if (!editorModel.Id.HasValue)
        {
            return;
        }

        await WorkspaceService.DeleteAgentAsync(editorModel.Id.Value);
        agents = await WorkspaceService.ListAgentsAsync(includeTemplates: false);
        message = "Technical agent deleted.";
        await ResetAgentAsync();
    }

    private void ResetAgentSearch()
    {
        agentSearch = string.Empty;
    }

    private string ResolveProviderLabel(
        AgentDefinition agent)
    {
        if (!agent.ProviderProfileId.HasValue)
        {
            return "No provider";
        }

        return providers.FirstOrDefault(item => item.Id == agent.ProviderProfileId.Value)?.Name
            ?? "Unknown provider";
    }

    private static string ResolveStatusTone(
        AgentLifecycleStatus status)
    {
        return status switch
        {
            AgentLifecycleStatus.Active => "success",
            AgentLifecycleStatus.Suspended => "warning",
            AgentLifecycleStatus.Archived => "neutral",
            _ => "info"
        };
    }
}
