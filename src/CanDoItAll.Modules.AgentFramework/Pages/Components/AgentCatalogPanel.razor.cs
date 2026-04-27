using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Processes;
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

    [Inject]
    public ProcessesService ProcessesService { get; set; } = default!;

    private AgentEditorModel editorModel = new();
    private IReadOnlyList<AgentDefinition> agents = [];
    private IReadOnlyList<ProviderProfile> providers = [];
    private IReadOnlyList<ProjectAccessListItem> projectStructureProjects = [];
    private IReadOnlyList<ProcessDefinitionListItem> processDefinitions = [];
    private string tagText = string.Empty;
    private string agentSearch = string.Empty;
    private string? message;
    private Guid? linkedPartyId;
    private bool hasLoaded;
    private bool isLoading = true;
    private bool areProvidersLoaded;
    private bool areProjectStructureProjectsLoaded;
    private bool isLoadingProjectStructureProjects;
    private bool projectStructureProjectsRequested;
    private bool areProcessDefinitionsLoaded;
    private bool isLoadingProcessDefinitions;
    private bool processDefinitionsRequested;
    private string? providerLoadErrorMessage;
    private string? projectStructureProjectsErrorMessage;
    private string? processDefinitionsErrorMessage;
    private bool interactiveReloadAttempted;
    private Task? loadTask;
    private Task? projectStructureProjectsLoadTask;
    private Task? processDefinitionsLoadTask;

    private IReadOnlyList<AgentDefinition> FilteredAgents => agents
        .Where(agent =>
            string.IsNullOrWhiteSpace(agentSearch) ||
            agent.Name.Contains(agentSearch, StringComparison.OrdinalIgnoreCase) ||
            agent.RoleTitle.Contains(agentSearch, StringComparison.OrdinalIgnoreCase) ||
            agent.Summary.Contains(agentSearch, StringComparison.OrdinalIgnoreCase) ||
            agent.Tags.Any(tag => tag.Contains(agentSearch, StringComparison.OrdinalIgnoreCase)))
        .OrderBy(agent => agent.Name)
        .ToList();

    private ProviderProfile? SelectedRuntimeProvider => editorModel.ProviderProfileId.HasValue
        ? providers.FirstOrDefault(item => item.Id == editorModel.ProviderProfileId.Value)
        : null;

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
                : Task.FromResult<IReadOnlyList<ProviderProfile>>(InitialProviders);

            agents = (await agentsTask).ToList();
            ResetEditorState();
            hasLoaded = true;

            _ = LoadProvidersAsync(providersTask);
        }
        finally
        {
            isLoading = false;
            loadTask = null;
        }
    }

    private async Task LoadProvidersAsync(Task<IReadOnlyList<ProviderProfile>> providersTask)
    {
        try
        {
            providers = (await providersTask).ToList();
            areProvidersLoaded = true;
        }
        catch (Exception exception)
        {
            providerLoadErrorMessage = $"Failed to load providers. {exception.Message}";
        }

        await InvokeAsync(StateHasChanged);
    }

    private Task RequestProjectStructureProjectsAsync()
    {
        projectStructureProjectsRequested = true;
        return EnsureProjectStructureProjectsLoadedAsync();
    }

    private Task RequestProcessDefinitionsAsync()
    {
        processDefinitionsRequested = true;
        return EnsureProcessDefinitionsLoadedAsync();
    }

    private Task EnsureProjectStructureProjectsLoadedAsync()
    {
        if (areProjectStructureProjectsLoaded)
        {
            return Task.CompletedTask;
        }

        if (projectStructureProjectsLoadTask is not null)
        {
            return projectStructureProjectsLoadTask;
        }

        projectStructureProjectsLoadTask = LoadProjectStructureProjectsAsync();
        return projectStructureProjectsLoadTask;
    }

    private async Task LoadProjectStructureProjectsAsync()
    {
        isLoadingProjectStructureProjects = true;
        projectStructureProjectsErrorMessage = null;
        await InvokeAsync(StateHasChanged);

        try
        {
            projectStructureProjects = await ProjectsService.ListAccessListAsync();
            areProjectStructureProjectsLoaded = true;
        }
        catch (Exception exception)
        {
            projectStructureProjectsErrorMessage = $"Failed to load projects. {exception.Message}";
        }
        finally
        {
            isLoadingProjectStructureProjects = false;
            projectStructureProjectsLoadTask = null;
            await InvokeAsync(StateHasChanged);
        }
    }

    private Task EnsureProcessDefinitionsLoadedAsync()
    {
        if (areProcessDefinitionsLoaded)
        {
            return Task.CompletedTask;
        }

        if (processDefinitionsLoadTask is not null)
        {
            return processDefinitionsLoadTask;
        }

        processDefinitionsLoadTask = LoadProcessDefinitionsAsync();
        return processDefinitionsLoadTask;
    }

    private async Task LoadProcessDefinitionsAsync()
    {
        isLoadingProcessDefinitions = true;
        processDefinitionsErrorMessage = null;
        await InvokeAsync(StateHasChanged);

        try
        {
            processDefinitions = await ProcessesService.ListDefinitionsAsync(cancellationToken: default);
            areProcessDefinitionsLoaded = true;
        }
        catch (Exception exception)
        {
            processDefinitionsErrorMessage = $"Failed to load processes. {exception.Message}";
        }
        finally
        {
            isLoadingProcessDefinitions = false;
            processDefinitionsLoadTask = null;
            await InvokeAsync(StateHasChanged);
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

        if (!areProvidersLoaded &&
            string.IsNullOrWhiteSpace(providerLoadErrorMessage))
        {
            return "Loading provider...";
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

    private static string DescribeProjectStructureScope(AgentEditorModel editor)
    {
        return editor.ProjectStructureAccess.AllowAllProjects
            ? "All current and future projects"
            : $"{CountAllowedProjectStructureProjects(editor)} selected";
    }

    private static int CountAllowedProcesses(AgentEditorModel editor)
    {
        return editor.ProcessAccess.AllowedDefinitionIds
            .Where(definitionId => definitionId != Guid.Empty)
            .Distinct()
            .Count();
    }

    private static string DescribeProcessScope(AgentEditorModel editor)
    {
        return editor.ProcessAccess.AllowAllDefinitions
            ? "All current and future processes"
            : $"{CountAllowedProcesses(editor)} selected";
    }

    private string DescribeRuntimeParameterPolicy(ProviderProfile provider)
    {
        var model = ResolveEditorRuntimeModel(provider);
        var modelLabel = string.IsNullOrWhiteSpace(model)
            ? "the selected model"
            : $"model '{model}'";

        if (!AgentProviderModelParameterPolicy.IsOpenAiLikeProvider(provider.Kind))
        {
            return $"Configured model parameters are sent for {modelLabel}.";
        }

        if (AgentProviderModelParameterPolicy.ShouldOmitTemperature(provider.Kind, model))
        {
            return $"Temperature will be omitted for {modelLabel}. Provider defaults apply.";
        }

        return $"Temperature is sent for {modelLabel}. If the provider rejects it, the runtime retries once without temperature.";
    }

    private string ResolveEditorRuntimeModel(ProviderProfile provider)
    {
        return string.IsNullOrWhiteSpace(editorModel.Model)
            ? provider.DefaultModel.Trim()
            : editorModel.Model.Trim();
    }

    private void ToggleProjectStructureRead(object? rawValue)
    {
        var isEnabled = rawValue is bool value && value;
        editorModel.ProjectStructureAccess.CanRead = isEnabled;
        if (isEnabled)
        {
            projectStructureProjectsRequested = true;
            _ = EnsureProjectStructureProjectsLoadedAsync();
        }

        if (!isEnabled)
        {
            editorModel.ProjectStructureAccess.CanWrite = false;
            editorModel.ProjectStructureAccess.AllowAllProjects = false;
        }
    }

    private void ToggleProjectStructureWrite(object? rawValue)
    {
        var isEnabled = rawValue is bool value && value;
        editorModel.ProjectStructureAccess.CanWrite = isEnabled;
        if (isEnabled)
        {
            editorModel.ProjectStructureAccess.CanRead = true;
            projectStructureProjectsRequested = true;
            _ = EnsureProjectStructureProjectsLoadedAsync();
        }
    }

    private void ToggleProjectStructureAllowAll(object? rawValue)
    {
        var isEnabled = rawValue is bool value && value;
        editorModel.ProjectStructureAccess.AllowAllProjects = isEnabled;
        if (isEnabled)
        {
            editorModel.ProjectStructureAccess.CanRead = true;
        }
    }

    private void ToggleProcessRead(object? rawValue)
    {
        var isEnabled = rawValue is bool value && value;
        editorModel.ProcessAccess.CanRead = isEnabled;
        if (isEnabled)
        {
            processDefinitionsRequested = true;
            _ = EnsureProcessDefinitionsLoadedAsync();
        }

        if (!isEnabled)
        {
            editorModel.ProcessAccess.CanWrite = false;
            editorModel.ProcessAccess.AllowAllDefinitions = false;
        }
    }

    private void ToggleProcessWrite(object? rawValue)
    {
        var isEnabled = rawValue is bool value && value;
        editorModel.ProcessAccess.CanWrite = isEnabled;
        if (isEnabled)
        {
            editorModel.ProcessAccess.CanRead = true;
            processDefinitionsRequested = true;
            _ = EnsureProcessDefinitionsLoadedAsync();
        }
    }

    private void ToggleProcessAllowAll(object? rawValue)
    {
        var isEnabled = rawValue is bool value && value;
        editorModel.ProcessAccess.AllowAllDefinitions = isEnabled;
        if (isEnabled)
        {
            editorModel.ProcessAccess.CanRead = true;
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

    private bool HasProcessAccess(Guid definitionId)
    {
        return editorModel.ProcessAccess.AllowedDefinitionIds.Contains(definitionId);
    }

    private void ToggleProcess(Guid definitionId, object? rawValue)
    {
        var selectedDefinitions = editorModel.ProcessAccess.AllowedDefinitionIds.ToList();
        var isEnabled = rawValue is bool value && value;
        if (isEnabled)
        {
            if (!selectedDefinitions.Contains(definitionId))
            {
                selectedDefinitions.Add(definitionId);
            }
        }
        else
        {
            selectedDefinitions.RemoveAll(item => item == definitionId);
        }

        editorModel.ProcessAccess.AllowedDefinitionIds = selectedDefinitions
            .Distinct()
            .OrderBy(item => item)
            .ToList();
    }

    private void SelectAllProcesses()
    {
        editorModel.ProcessAccess.AllowedDefinitionIds = processDefinitions
            .Select(item => item.Id)
            .Distinct()
            .OrderBy(item => item)
            .ToList();
    }

    private void ClearProcesses()
    {
        editorModel.ProcessAccess.AllowedDefinitionIds = [];
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
