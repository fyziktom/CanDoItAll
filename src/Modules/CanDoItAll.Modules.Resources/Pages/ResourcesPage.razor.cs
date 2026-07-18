using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Security;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.Modules.Workspace.Pages.Components;
using CanDoItAll.SharedKernel;
using CanDoItAll.SharedKernel.Configuration;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Modules.Resources.Pages;

public partial class ResourcesPage
{
    [SupplyParameterFromQuery(Name = "resourceId")]
    public Guid? ResourceIdQuery { get; set; }

    [SupplyParameterFromQuery(Name = "projectId")]
    public Guid? ProjectIdQuery { get; set; }

    [Inject]
    public NotificationService NotificationService { get; set; } = default!;

    [Inject]
    public NavigationManager Navigation { get; set; } = default!;

    private IReadOnlyList<ResourceSummary> resources = [];
    private IReadOnlyList<ProjectSummary> projects = [];
    private IReadOnlyList<SecretListItem> secrets = [];
    private IReadOnlyList<ProjectPartyOption> responsiblePartyOptions = [];
    private IReadOnlyList<ConnectorPluginManifest> resourceManifests = [];
    private ResourceEditorModel editor = new();
    private string resourceSearch = string.Empty;
    private string projectFilter = string.Empty;
    private string connectorFilter = string.Empty;
    private string validationFilter = string.Empty;
    private int resourcesTabIndex;
    private ResourceBrowseAgentChatContextState browseContextState = ResourceBrowseAgentChatContextState.Loading;
    private AgentChatContextAccessState agentChatContextAccessState = AgentChatContextAccessState.Loading;
    private readonly ResourcesPageLoadGeneration agentChatContextLoads = new();
    private bool resourcePageStateLoaded;

    private AgentChatContextSurface AgentChatSurface => ResourcesAgentChatContextBuilder.Build(
        resourcesTabIndex == 1 ? ResourcesAgentChatView.Browse : ResourcesAgentChatView.Registry,
        editor,
        SelectedProjectName,
        SelectedResourceConnectorLabel,
        browseContextState.Position);

    private AgentChatContextAccessState CurrentAgentChatContextAccessState
        => resourcesTabIndex == 1
            ? browseContextState.AccessState
            : agentChatContextAccessState;

    private AgentChatNavigationIdentity AgentChatNavigationFence
        => AgentChatNavigationIdentity.CreateForLocation(
            Navigation.BaseUri,
            Navigation.Uri,
            [
                new("resourceId", ResourceIdQuery?.ToString("D")),
                new("projectId", ProjectIdQuery?.ToString("D"))
            ]);

    private ConnectorPluginManifest? SelectedResourceManifest => resourceManifests.FirstOrDefault(manifest =>
            string.Equals(manifest.PluginKey, editor.ConnectorPluginKey, StringComparison.OrdinalIgnoreCase))
        ?? resourceManifests.FirstOrDefault();

    private IReadOnlyList<ConfigurationFieldDescriptor> SelectedResourceFields => SelectedResourceManifest?.ConfigurationSchema.Fields ?? [];

    private IReadOnlyList<ConnectorPluginManifest> EditableResourceManifests => resourceManifests
        .Where(manifest => !string.Equals(
            manifest.PluginKey,
            StorageObjectResourceConnectorPlugin.PluginKey,
            StringComparison.OrdinalIgnoreCase))
        .ToArray();

    private IReadOnlyList<ResourceSummary> FilteredResources => resources
        .Where(resource =>
            (string.IsNullOrWhiteSpace(resourceSearch) ||
             resource.Name.Contains(resourceSearch, StringComparison.OrdinalIgnoreCase) ||
             resource.ProjectName.Contains(resourceSearch, StringComparison.OrdinalIgnoreCase) ||
             resource.LocationOrIdentifier.Contains(resourceSearch, StringComparison.OrdinalIgnoreCase) ||
             resource.ConnectorDisplayName.Contains(resourceSearch, StringComparison.OrdinalIgnoreCase)) &&
            (string.IsNullOrWhiteSpace(projectFilter) || string.Equals(resource.ProjectId.ToString(), projectFilter, StringComparison.OrdinalIgnoreCase)) &&
            (string.IsNullOrWhiteSpace(connectorFilter) || string.Equals(resource.ConnectorPluginKey, connectorFilter, StringComparison.OrdinalIgnoreCase)) &&
            (string.IsNullOrWhiteSpace(validationFilter) || string.Equals(resource.ValidationStatus.ToString(), validationFilter, StringComparison.OrdinalIgnoreCase)))
        .OrderBy(resource => resource.ProjectName)
        .ThenBy(resource => resource.Name)
        .ToList();

    private string PreviewLocation => ResourcesService.BuildLocationPreview(editor);

    private string EditorTitle => editor.Id.HasValue ? editor.Name : "New resource";

    private string SelectedResourceConnectorLabel => SelectedResourceManifest?.DisplayName ?? "Connector";

    private string? SelectedProjectName => projects.FirstOrDefault(project => project.Id == editor.ProjectId)?.Name;

    private bool IsGovernedStorageObject => string.Equals(
        editor.ConnectorPluginKey,
        StorageObjectResourceConnectorPlugin.PluginKey,
        StringComparison.OrdinalIgnoreCase);

    protected override async Task OnParametersSetAsync()
    {
        await LoadAsync(ResourceIdQuery, ProjectIdQuery);
    }

    private async Task LoadAsync(Guid? resourceId = null, Guid? projectId = null)
    {
        var loadGeneration = BeginAgentChatContextLoad();
        try
        {
            var loadedState = await LoadPageStateAsync(resourceId, projectId);
            TryCompleteAgentChatContextLoad(
                loadGeneration,
                () => ApplyLoadedPageState(loadedState),
                loadedState.RouteContextSelection.IsResolved
                    ? AgentChatContextAccessState.Ready
                    : AgentChatContextAccessState.Failed);
        }
        catch
        {
            FailAgentChatContextLoad(loadGeneration);
            throw;
        }
    }

    private async Task CreateNewAsync()
    {
        var loadGeneration = BeginAgentChatContextLoad();
        try
        {
            if (!resourcePageStateLoaded)
            {
                var loadedState = await LoadPageStateAsync(null, ProjectIdQuery);
                TryCompleteAgentChatContextLoad(loadGeneration, () =>
                {
                    ApplyLoadedPageState(loadedState);
                    resourcesTabIndex = 0;
                }, ResolveRouteContextAccessState(loadedState));
                return;
            }

            var loadedEditor = CreateNewEditor(ProjectIdQuery, resourceManifests);
            var loadedResponsiblePartyOptions = await LoadResponsiblePartyOptionsAsync(
                ResponsiblePartySelection.From(loadedEditor));
            TryCompleteAgentChatContextLoad(loadGeneration, () =>
            {
                resourcesTabIndex = 0;
                editor = loadedEditor;
                responsiblePartyOptions = loadedResponsiblePartyOptions;
            });
        }
        catch
        {
            FailAgentChatContextLoad(loadGeneration);
            throw;
        }
    }

    private void OpenBrowseTab() => resourcesTabIndex = 1;

    private async Task HandleResourcePromotedAsync(Guid resourceId)
    {
        var loadGeneration = BeginAgentChatContextLoad();
        try
        {
            var loadedResources = await ResourcesService.ListAsync();
            TryCompleteAgentChatContextLoad(loadGeneration, () => resources = loadedResources);
        }
        catch
        {
            FailAgentChatContextLoad(loadGeneration);
            throw;
        }
    }

    private Task HandleBrowseContextStateChanged(ResourceBrowseAgentChatContextState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        browseContextState = state;
        return Task.CompletedTask;
    }

    private async Task EditAsync(Guid id)
    {
        var loadGeneration = BeginAgentChatContextLoad();
        try
        {
            if (!resourcePageStateLoaded)
            {
                var loadedState = await LoadPageStateAsync(id, ProjectIdQuery);
                TryCompleteAgentChatContextLoad(
                    loadGeneration,
                    () => ApplyLoadedPageState(loadedState),
                    ResolveRouteContextAccessState(loadedState));
                return;
            }

            var loadedEditor = await ResourcesService.GetAsync(id);
            NormalizeResourceEditor(loadedEditor, resourceManifests);
            var loadedResponsiblePartyOptions = await LoadResponsiblePartyOptionsAsync(
                ResponsiblePartySelection.From(loadedEditor));
            TryCompleteAgentChatContextLoad(loadGeneration, () =>
            {
                editor = loadedEditor;
                responsiblePartyOptions = loadedResponsiblePartyOptions;
            });
        }
        catch
        {
            FailAgentChatContextLoad(loadGeneration);
            throw;
        }
    }

    private ResourcesPageLoadStamp BeginAgentChatContextLoad()
    {
        agentChatContextAccessState = AgentChatContextAccessState.Loading;
        return agentChatContextLoads.Begin();
    }

    private bool CompleteAgentChatContextLoad(ResourcesPageLoadStamp loadGeneration)
    {
        return agentChatContextLoads.TryCommit(
            loadGeneration,
            () => agentChatContextAccessState = AgentChatContextAccessState.Ready);
    }

    private bool TryCompleteAgentChatContextLoad(
        ResourcesPageLoadStamp loadGeneration,
        Action commit,
        AgentChatContextAccessState completedState = AgentChatContextAccessState.Ready)
    {
        return agentChatContextLoads.TryCommit(loadGeneration, () =>
        {
            commit();
            agentChatContextAccessState = completedState;
        });
    }

    private bool FailAgentChatContextLoad(ResourcesPageLoadStamp loadGeneration)
    {
        return agentChatContextLoads.TryCommit(
            loadGeneration,
            () => agentChatContextAccessState = AgentChatContextAccessState.Failed);
    }

    private async Task SaveAsync()
    {
        var loadGeneration = BeginAgentChatContextLoad();
        var editorToSave = CloneEditor(editor);
        try
        {
            var result = await ResourcesService.SaveAsync(editorToSave);
            if (!result.IsSuccess)
            {
                if (CompleteAgentChatContextLoad(loadGeneration))
                {
                    NotificationService.Warning("Resource was not saved", DescribeErrors(result.Errors));
                }

                return;
            }

            var loadedResourcesTask = ResourcesService.ListAsync();
            var loadedEditorTask = ResourcesService.GetAsync(result.Value);
            await Task.WhenAll(loadedResourcesTask, loadedEditorTask);

            var loadedResources = await loadedResourcesTask;
            var loadedEditor = await loadedEditorTask;
            NormalizeResourceEditor(loadedEditor, resourceManifests);
            var loadedResponsiblePartyOptions = await LoadResponsiblePartyOptionsAsync(
                ResponsiblePartySelection.From(loadedEditor));
            if (TryCompleteAgentChatContextLoad(loadGeneration, () =>
                {
                    resources = loadedResources;
                    editor = loadedEditor;
                    responsiblePartyOptions = loadedResponsiblePartyOptions;
                }))
            {
                NotificationService.Success("Resource saved", "Resource saved.");
            }
        }
        catch (Exception exception)
        {
            if (FailAgentChatContextLoad(loadGeneration))
            {
                NotificationService.Error("Resource save failed", exception.Message);
            }
        }
    }

    private async Task DeleteAsync()
    {
        if (!editor.Id.HasValue)
        {
            return;
        }

        var loadGeneration = BeginAgentChatContextLoad();
        var resourceId = editor.Id.Value;
        var projectId = ProjectIdQuery;
        try
        {
            await ResourcesService.DeleteAsync(resourceId);
            if (!agentChatContextLoads.IsCurrent(loadGeneration))
            {
                return;
            }

            var loadedState = await LoadPageStateAsync(null, projectId);
            if (TryCompleteAgentChatContextLoad(
                    loadGeneration,
                    () => ApplyLoadedPageState(loadedState),
                    ResolveRouteContextAccessState(loadedState)))
            {
                NotificationService.Success("Resource deleted", "Resource deleted.");
            }
        }
        catch (Exception exception)
        {
            if (FailAgentChatContextLoad(loadGeneration))
            {
                NotificationService.Error("Resource delete failed", exception.Message);
            }
        }
    }

    private static string DescribeErrors(IEnumerable<Error> errors)
        => string.Join(" ", errors.Select(error => error.Message));

    private void ResetFilters()
    {
        resourceSearch = string.Empty;
        projectFilter = string.Empty;
        connectorFilter = string.Empty;
        validationFilter = string.Empty;
    }

    private string BuildResourceMeta(ResourceSummary resource)
    {
        return $"{resource.ProjectName} / {resource.ConnectorDisplayName}";
    }

    private Task HandleConnectorPluginChangedAsync()
    {
        NormalizeResourceEditorForCurrentPlugin();
        return Task.CompletedTask;
    }

    private async Task RefreshResponsiblePartyOptionsAsync()
    {
        var loadGeneration = BeginAgentChatContextLoad();
        var selection = ResponsiblePartySelection.From(editor);
        try
        {
            var loadedResponsiblePartyOptions = await LoadResponsiblePartyOptionsAsync(selection);
            TryCompleteAgentChatContextLoad(
                loadGeneration,
                () => responsiblePartyOptions = loadedResponsiblePartyOptions);
        }
        catch
        {
            FailAgentChatContextLoad(loadGeneration);
            throw;
        }
    }

    private async Task<IReadOnlyList<ProjectPartyOption>> LoadResponsiblePartyOptionsAsync(
        ResponsiblePartySelection selection)
    {
        var options = selection.ProjectId.HasValue
            ? (await ProjectPartyIntegrationBridge.ListPartyOptionsAsync(selection.ProjectId.Value)).ToList()
            : [];
        var missingPartyIds = new[] { selection.OwnerPartyId, selection.MaintainerPartyId }
            .Where(partyId => partyId.HasValue)
            .Select(partyId => partyId!.Value)
            .Distinct()
            .Where(partyId => options.All(option => option.PartyId != partyId))
            .ToArray();
        if (missingPartyIds.Length == 0)
        {
            return options;
        }

        var missingPartyTasks = missingPartyIds
            .Select(partyId => ProjectPartyIntegrationBridge.GetPartyOptionAsync(partyId))
            .ToArray();
        var missingParties = await Task.WhenAll(missingPartyTasks);
        return options
            .Concat(missingParties.OfType<ProjectPartyOption>())
            .DistinctBy(option => option.PartyId)
            .OrderBy(option => option.DisplayName)
            .ToList();
    }

    private async Task<LoadedResourcePageState> LoadPageStateAsync(Guid? resourceId, Guid? projectId)
    {
        var loadedResourceManifests = ResourcesService.ListConnectorManifests();
        var loadedResourcesTask = ResourcesService.ListAsync();
        var loadedProjectsTask = ProjectsService.ListAsync();
        var loadedSecretsTask = SecretService.ListForPickerAsync();
        await Task.WhenAll(loadedResourcesTask, loadedProjectsTask, loadedSecretsTask);

        var loadedResources = await loadedResourcesTask;
        var loadedProjects = await loadedProjectsTask;
        var routeContextSelection = ResourceRouteContextSelection.Resolve(
            resourceId,
            projectId,
            loadedResources,
            loadedProjects);
        var loadedEditor = routeContextSelection.IsResolved && resourceId.HasValue
            ? await ResourcesService.GetAsync(resourceId.Value)
            : routeContextSelection.IsResolved
                ? CreateNewEditor(projectId, loadedResourceManifests)
                : CreateUnresolvedRouteEditor(
                    resourceId,
                    projectId,
                    routeContextSelection.Resource,
                    loadedResourceManifests);
        NormalizeResourceEditor(loadedEditor, loadedResourceManifests);
        var loadedResponsiblePartyOptions = routeContextSelection.IsResolved
            ? await LoadResponsiblePartyOptionsAsync(ResponsiblePartySelection.From(loadedEditor))
            : [];
        return new LoadedResourcePageState(
            loadedResourceManifests,
            loadedResources,
            loadedProjects,
            await loadedSecretsTask,
            loadedResponsiblePartyOptions,
            loadedEditor,
            routeContextSelection);
    }

    private void ApplyLoadedPageState(LoadedResourcePageState loadedState)
    {
        resourceManifests = loadedState.ResourceManifests;
        resources = loadedState.Resources;
        projects = loadedState.Projects;
        secrets = loadedState.Secrets;
        responsiblePartyOptions = loadedState.ResponsiblePartyOptions;
        editor = loadedState.Editor;
        resourcePageStateLoaded = true;
    }

    private static AgentChatContextAccessState ResolveRouteContextAccessState(
        LoadedResourcePageState loadedState)
        => loadedState.RouteContextSelection.IsResolved
            ? AgentChatContextAccessState.Ready
            : AgentChatContextAccessState.Failed;

    private static ResourceEditorModel CreateUnresolvedRouteEditor(
        Guid? resourceId,
        Guid? projectId,
        ResourceSummary? resource,
        IReadOnlyList<ConnectorPluginManifest> manifests)
    {
        var model = CreateNewEditor(projectId, manifests);
        model.Id = resourceId;
        model.Name = resource?.Name ?? string.Empty;
        return model;
    }

    private static ResourceEditorModel CloneEditor(ResourceEditorModel source)
    {
        return new ResourceEditorModel
        {
            Id = source.Id,
            ProjectId = source.ProjectId,
            OwnerPartyId = source.OwnerPartyId,
            MaintainerPartyId = source.MaintainerPartyId,
            Name = source.Name,
            Description = source.Description,
            ConnectorPluginKey = source.ConnectorPluginKey,
            ConfigSchemaVersion = source.ConfigSchemaVersion,
            LocationOrIdentifier = source.LocationOrIdentifier,
            ConfigJson = source.ConfigJson,
            Configuration = source.Configuration?.Clone() ?? new ConnectorConfigState(),
            LinkedSecretId = source.LinkedSecretId,
            ValidationStatus = source.ValidationStatus,
            Sensitivity = source.Sensitivity,
            SupportsPreview = source.SupportsPreview,
            SupportsIndexing = source.SupportsIndexing
        };
    }

    private static ResourceEditorModel CreateNewEditor(
        Guid? projectId,
        IReadOnlyList<ConnectorPluginManifest> manifests)
    {
        var model = new ResourceEditorModel
        {
            ProjectId = projectId,
            ConnectorPluginKey = manifests.FirstOrDefault()?.PluginKey ?? ResourceConnectorPluginKeys.Repository
        };
        NormalizeResourceEditor(model, manifests);
        return model;
    }

    private static void NormalizeResourceEditor(
        ResourceEditorModel model,
        IReadOnlyList<ConnectorPluginManifest> manifests)
    {
        var manifest = manifests.FirstOrDefault(candidate =>
                string.Equals(candidate.PluginKey, model.ConnectorPluginKey, StringComparison.OrdinalIgnoreCase))
            ?? manifests.FirstOrDefault();
        if (manifest is null)
        {
            return;
        }

        model.ConnectorPluginKey = manifest.PluginKey;
        model.ConfigSchemaVersion = manifest.ConfigurationSchema.Version;

        var existingConfiguration = model.Configuration?.Clone() ?? new ConnectorConfigState();
        existingConfiguration.KeepOnly(manifest.ConfigurationSchema.Fields.Select(field => field.Key));
        model.Configuration = existingConfiguration;
    }

    private sealed record LoadedResourcePageState(
        IReadOnlyList<ConnectorPluginManifest> ResourceManifests,
        IReadOnlyList<ResourceSummary> Resources,
        IReadOnlyList<ProjectSummary> Projects,
        IReadOnlyList<SecretListItem> Secrets,
        IReadOnlyList<ProjectPartyOption> ResponsiblePartyOptions,
        ResourceEditorModel Editor,
        ResourceRouteContextSelection RouteContextSelection);

    private readonly record struct ResponsiblePartySelection(
        Guid? ProjectId,
        Guid? OwnerPartyId,
        Guid? MaintainerPartyId)
    {
        public static ResponsiblePartySelection From(ResourceEditorModel model)
        {
            return new ResponsiblePartySelection(
                model.ProjectId,
                model.OwnerPartyId,
                model.MaintainerPartyId);
        }
    }

    private static string ResolveTone(ResourceValidationStatus status)
    {
        return status switch
        {
            ResourceValidationStatus.Valid => "success",
            ResourceValidationStatus.Warning => "warning",
            ResourceValidationStatus.Invalid => "danger",
            _ => "neutral"
        };
    }

    private void NormalizeResourceEditorForCurrentPlugin()
    {
        NormalizeResourceEditor(editor, resourceManifests);
    }

    private static string? ResolveResourceFieldTestId(
        ConfigurationFieldDescriptor field,
        IReadOnlyList<ConfigurationFieldDescriptor> fields)
    {
        var primaryFieldKey = fields.FirstOrDefault()?.Key;
        if (!string.Equals(field.Key, primaryFieldKey, StringComparison.OrdinalIgnoreCase))
        {
            return $"resource-config-{field.Key}";
        }

        return "resource-primary-input";
    }
}
