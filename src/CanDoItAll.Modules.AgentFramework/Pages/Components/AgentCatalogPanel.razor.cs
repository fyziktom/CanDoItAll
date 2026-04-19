using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components;

public partial class AgentCatalogPanel
{
    [Parameter]
    public Guid? RequestedAgentId { get; set; }

    [Parameter]
    public IReadOnlyList<AgentDefinition>? InitialAgents { get; set; }

    [Parameter]
    public IReadOnlyList<ProviderProfile>? InitialProviders { get; set; }

    [Parameter]
    public bool SkipCatalogRepair { get; set; }

    [Inject]
    public IAgentFrameworkWorkspaceService WorkspaceService { get; set; } = default!;

    [Inject]
    public IAgentFrameworkOrganizationCatalogRepairService OrganizationCatalogRepairService { get; set; } = default!;

    [Inject]
    public ProjectsService ProjectsService { get; set; } = default!;

    private AgentEditorModel editorModel = new();
    private IReadOnlyList<AgentDefinition> agents = [];
    private IReadOnlyList<ProviderProfile> providers = [];
    private IReadOnlyList<ProjectSummary> projectStructureProjects = [];
    private string tagText = string.Empty;
    private string agentSearch = string.Empty;
    private string? message;
    private Guid? linkedPartyId;
    private bool hasLoaded;
    private bool isLoading = true;
    private bool interactiveReloadAttempted;
    private Task? loadTask;

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
        await EnsureLoadedAsync();
    }

    protected override async Task OnParametersSetAsync()
    {
        await EnsureLoadedAsync();

        if (RequestedAgentId.HasValue &&
            editorModel.Id != RequestedAgentId)
        {
            await EditAgentAsync(RequestedAgentId.Value);
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender ||
            interactiveReloadAttempted ||
            hasLoaded)
        {
            return;
        }

        interactiveReloadAttempted = true;
        await EnsureLoadedAsync();
        StateHasChanged();
    }

    private Task EnsureLoadedAsync()
    {
        if (hasLoaded)
        {
            return Task.CompletedTask;
        }

        if (loadTask is not null)
        {
            return loadTask;
        }

        loadTask = LoadAsync();
        return loadTask;
    }

    private async Task LoadAsync()
    {
        isLoading = true;

        try
        {
            if (!SkipCatalogRepair)
            {
                await OrganizationCatalogRepairService.EnsureCurrentOrganizationCatalogAsync();
            }

            var agentsTask = InitialAgents is null
                ? WorkspaceService.ListAgentsAsync(includeTemplates: false)
                : Task.FromResult(InitialAgents);
            var providersTask = InitialProviders is null
                ? WorkspaceService.ListProvidersAsync()
                : Task.FromResult(InitialProviders);
            var projectsTask = ProjectsService.ListAsync();

            await Task.WhenAll(agentsTask, providersTask, projectsTask);

            agents = (await agentsTask).ToList();
            providers = (await providersTask).ToList();
            projectStructureProjects = (await projectsTask)
                .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            ResetEditorState();
            hasLoaded = true;
        }
        finally
        {
            isLoading = false;
            loadTask = null;
        }
    }

    private async Task SaveAgentAsync()
    {
        editorModel.Tags = tagText
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var agentId = await WorkspaceService.SaveAgentAsync(editorModel);
        await ReloadAgentsAsync(agentId);
        message = "Technical agent saved.";
    }

    private Task EditAgentAsync(
        Guid agentId)
    {
        var definition = agents.FirstOrDefault(item => item.Id == agentId);
        if (definition is null)
        {
            ResetEditorState();
            return Task.CompletedTask;
        }

        ApplySelectedAgent(definition);
        return Task.CompletedTask;
    }

    private Task ResetAgentAsync()
    {
        ResetEditorState();
        message = null;
        return Task.CompletedTask;
    }

    private async Task DeleteAgentAsync()
    {
        if (!editorModel.Id.HasValue)
        {
            return;
        }

        await WorkspaceService.DeleteAgentAsync(editorModel.Id.Value);
        await ReloadAgentsAsync();
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

    private static int CountAllowedProjectStructureProjects(AgentEditorModel editor)
    {
        return editor.ProjectStructureAccess.AllowedProjectIds
            .Where(projectId => projectId != Guid.Empty)
            .Distinct()
            .Count();
    }

    private void ToggleProjectStructureRead(object? rawValue)
    {
        var isEnabled = rawValue is bool value && value;
        editorModel.ProjectStructureAccess.CanRead = isEnabled;
        if (!isEnabled)
        {
            editorModel.ProjectStructureAccess.CanWrite = false;
        }
    }

    private void ToggleProjectStructureWrite(object? rawValue)
    {
        var isEnabled = rawValue is bool value && value;
        editorModel.ProjectStructureAccess.CanWrite = isEnabled;
        if (isEnabled)
        {
            editorModel.ProjectStructureAccess.CanRead = true;
        }
    }

    private bool HasProjectStructureProjectAccess(Guid projectId)
    {
        return editorModel.ProjectStructureAccess.AllowedProjectIds.Contains(projectId);
    }

    private void ToggleProjectStructureProject(Guid projectId, object? rawValue)
    {
        var selectedProjects = editorModel.ProjectStructureAccess.AllowedProjectIds.ToList();
        var isEnabled = rawValue is bool value && value;
        if (isEnabled)
        {
            if (!selectedProjects.Contains(projectId))
            {
                selectedProjects.Add(projectId);
            }
        }
        else
        {
            selectedProjects.RemoveAll(item => item == projectId);
        }

        editorModel.ProjectStructureAccess.AllowedProjectIds = selectedProjects
            .Distinct()
            .OrderBy(item => item)
            .ToList();
    }

    private void SelectAllProjectStructureProjects()
    {
        editorModel.ProjectStructureAccess.AllowedProjectIds = projectStructureProjects
            .Select(item => item.Id)
            .Distinct()
            .OrderBy(item => item)
            .ToList();
    }

    private void ClearProjectStructureProjects()
    {
        editorModel.ProjectStructureAccess.AllowedProjectIds = [];
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

    private async Task ReloadAgentsAsync(Guid? selectedAgentId = null)
    {
        agents = await WorkspaceService.ListAgentsAsync(includeTemplates: false);
        if (selectedAgentId.HasValue)
        {
            var definition = agents.FirstOrDefault(item => item.Id == selectedAgentId.Value);
            if (definition is not null)
            {
                ApplySelectedAgent(definition);
                return;
            }
        }

        ResetEditorState();
    }

    private void ApplySelectedAgent(AgentDefinition definition)
    {
        editorModel = AgentEditorModel.FromDefinition(definition);
        tagText = string.Join(", ", editorModel.Tags);
        linkedPartyId = AgentFrameworkCrmHrMetadata.Read(definition.ConfigurationJson)?.PartyId;
    }

    private void ResetEditorState()
    {
        editorModel = new AgentEditorModel();
        tagText = string.Empty;
        linkedPartyId = null;
    }
}
