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
        resourceManifests = ResourcesService.ListConnectorManifests();
        resources = await ResourcesService.ListAsync();
        projects = await ProjectsService.ListAsync();
        secrets = await SecretService.ListForPickerAsync();
        editor = resourceId.HasValue
            ? await ResourcesService.GetAsync(resourceId.Value)
            : NewEditor(projectId);
        NormalizeResourceEditorForCurrentPlugin();
        await LoadResponsiblePartyOptionsAsync();
    }

    private async Task CreateNewAsync()
    {
        resourcesTabIndex = 0;
        editor = NewEditor(ProjectIdQuery);
        await LoadResponsiblePartyOptionsAsync();
    }

    private void OpenBrowseTab() => resourcesTabIndex = 1;

    private async Task HandleResourcePromotedAsync(Guid resourceId)
    {
        resources = await ResourcesService.ListAsync();
    }

    private async Task EditAsync(Guid id)
    {
        editor = await ResourcesService.GetAsync(id);
        NormalizeResourceEditorForCurrentPlugin();
        await LoadResponsiblePartyOptionsAsync();
    }

    private async Task SaveAsync()
    {
        try
        {
            var result = await ResourcesService.SaveAsync(editor);
            resources = await ResourcesService.ListAsync();
            if (!result.IsSuccess)
            {
                NotificationService.Warning("Resource was not saved", DescribeErrors(result.Errors));
                return;
            }

            editor = await ResourcesService.GetAsync(result.Value);
            NormalizeResourceEditorForCurrentPlugin();
            await LoadResponsiblePartyOptionsAsync();
            NotificationService.Success("Resource saved", "Resource saved.");
        }
        catch (Exception exception)
        {
            NotificationService.Error("Resource save failed", exception.Message);
        }
    }

    private async Task DeleteAsync()
    {
        if (!editor.Id.HasValue)
        {
            return;
        }

        try
        {
            await ResourcesService.DeleteAsync(editor.Id.Value);
            await LoadAsync(null, ProjectIdQuery);
            NotificationService.Success("Resource deleted", "Resource deleted.");
        }
        catch (Exception exception)
        {
            NotificationService.Error("Resource delete failed", exception.Message);
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

    private async Task LoadResponsiblePartyOptionsAsync()
    {
        responsiblePartyOptions = editor.ProjectId.HasValue
            ? await ProjectPartyIntegrationBridge.ListPartyOptionsAsync(editor.ProjectId.Value)
            : [];

        if (editor.OwnerPartyId.HasValue && responsiblePartyOptions.All(option => option.PartyId != editor.OwnerPartyId.Value))
        {
            var owner = await ProjectPartyIntegrationBridge.GetPartyOptionAsync(editor.OwnerPartyId.Value);
            if (owner is not null)
            {
                responsiblePartyOptions = responsiblePartyOptions.Append(owner)
                    .DistinctBy(option => option.PartyId)
                    .OrderBy(option => option.DisplayName)
                    .ToList();
            }
        }

        if (editor.MaintainerPartyId.HasValue && responsiblePartyOptions.All(option => option.PartyId != editor.MaintainerPartyId.Value))
        {
            var maintainer = await ProjectPartyIntegrationBridge.GetPartyOptionAsync(editor.MaintainerPartyId.Value);
            if (maintainer is not null)
            {
                responsiblePartyOptions = responsiblePartyOptions.Append(maintainer)
                    .DistinctBy(option => option.PartyId)
                    .OrderBy(option => option.DisplayName)
                    .ToList();
            }
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

    private ResourceEditorModel NewEditor(Guid? projectId)
    {
        var model = new ResourceEditorModel
        {
            ProjectId = projectId,
            ConnectorPluginKey = resourceManifests.FirstOrDefault()?.PluginKey ?? ResourceConnectorPluginKeys.Repository
        };
        editor = model;
        NormalizeResourceEditorForCurrentPlugin();
        model = editor;
        return model;
    }

    private void NormalizeResourceEditorForCurrentPlugin()
    {
        var manifest = resourceManifests.FirstOrDefault(candidate =>
                string.Equals(candidate.PluginKey, editor.ConnectorPluginKey, StringComparison.OrdinalIgnoreCase))
            ?? resourceManifests.FirstOrDefault();
        if (manifest is null)
        {
            return;
        }

        editor.ConnectorPluginKey = manifest.PluginKey;
        editor.ConfigSchemaVersion = manifest.ConfigurationSchema.Version;

        var existingConfiguration = editor.Configuration?.Clone() ?? new ConnectorConfigState();
        existingConfiguration.KeepOnly(manifest.ConfigurationSchema.Fields.Select(field => field.Key));
        editor.Configuration = existingConfiguration;
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
