using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Security;
using CanDoItAll.Modules.Workspace;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Modules.Resources.Pages;

public partial class ResourcesPage
{
    [SupplyParameterFromQuery(Name = "resourceId")]
    public Guid? ResourceIdQuery { get; set; }

    [SupplyParameterFromQuery(Name = "projectId")]
    public Guid? ProjectIdQuery { get; set; }

    private IReadOnlyList<ResourceSummary> resources = [];
    private IReadOnlyList<ProjectSummary> projects = [];
    private IReadOnlyList<SecretListItem> secrets = [];
    private IReadOnlyList<ProjectPartyOption> responsiblePartyOptions = [];
    private IReadOnlyList<ConnectorPluginManifest> resourceManifests = [];
    private ResourceEditorModel editor = new();
    private string? message;
    private string resourceSearch = string.Empty;
    private string projectFilter = string.Empty;
    private string connectorFilter = string.Empty;
    private string validationFilter = string.Empty;

    private ConnectorPluginManifest? SelectedResourceManifest => resourceManifests.FirstOrDefault(manifest =>
            string.Equals(manifest.PluginKey, editor.ConnectorPluginKey, StringComparison.OrdinalIgnoreCase))
        ?? resourceManifests.FirstOrDefault();

    private IReadOnlyList<ConnectorConfigFieldDescriptor> SelectedResourceFields => SelectedResourceManifest?.ConfigurationSchema.Fields ?? [];

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
        EnsureLegacyResourceKind(editor);
        await LoadResponsiblePartyOptionsAsync();
    }

    private async Task CreateNewAsync()
    {
        editor = NewEditor(ProjectIdQuery);
        message = null;
        await LoadResponsiblePartyOptionsAsync();
    }

    private async Task EditAsync(Guid id)
    {
        editor = await ResourcesService.GetAsync(id);
        EnsureLegacyResourceKind(editor);
        await LoadResponsiblePartyOptionsAsync();
        message = null;
    }

    private async Task SaveAsync()
    {
        EnsureLegacyResourceKind(editor);
        var result = await ResourcesService.SaveAsync(editor);
        message = result.IsSuccess ? "Resource saved." : string.Join(" ", result.Errors.Select(error => error.Message));
        resources = await ResourcesService.ListAsync();
        if (!result.IsSuccess)
        {
            return;
        }

        editor = await ResourcesService.GetAsync(result.Value);
        EnsureLegacyResourceKind(editor);
        await LoadResponsiblePartyOptionsAsync();
    }

    private async Task DeleteAsync()
    {
        if (!editor.Id.HasValue)
        {
            return;
        }

        await ResourcesService.DeleteAsync(editor.Id.Value);
        await LoadAsync(null, ProjectIdQuery);
        message = "Resource deleted.";
    }

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
        EnsureLegacyResourceKind(editor);
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
        EnsureLegacyResourceKind(model);
        return model;
    }

    private void EnsureLegacyResourceKind(ResourceEditorModel model)
    {
        model.ResourceKind = ResolveLegacyResourceKind(model.ConnectorPluginKey, model.ResourceKind);
        if (string.IsNullOrWhiteSpace(model.ConfigSchemaVersion))
        {
            var manifest = resourceManifests.FirstOrDefault(candidate =>
                string.Equals(candidate.PluginKey, model.ConnectorPluginKey, StringComparison.OrdinalIgnoreCase));
            if (manifest is not null)
            {
                model.ConfigSchemaVersion = manifest.ConfigurationSchema.Version;
            }
        }
    }

    private ResourceKind ResolveLegacyResourceKind(string? connectorPluginKey, ResourceKind fallback)
    {
        if (string.IsNullOrWhiteSpace(connectorPluginKey))
        {
            return fallback;
        }

        var manifest = resourceManifests.FirstOrDefault(candidate =>
            string.Equals(candidate.PluginKey, connectorPluginKey, StringComparison.OrdinalIgnoreCase));
        if (manifest is null)
        {
            return fallback;
        }

        return manifest.PluginKey switch
        {
            ResourceConnectorPluginKeys.Repository => ResourceKind.Repository,
            ResourceConnectorPluginKeys.Folder => ResourceKind.Folder,
            ResourceConnectorPluginKeys.File => ResourceKind.File,
            ResourceConnectorPluginKeys.WebLink => ResourceKind.WebLink,
            ResourceConnectorPluginKeys.Ftp => ResourceKind.Ftp,
            ResourceConnectorPluginKeys.Ssh => ResourceKind.Ssh,
            ResourceConnectorPluginKeys.PowerShellScript => ResourceKind.PowerShellScript,
            ResourceConnectorPluginKeys.DockerCompose => ResourceKind.DockerCompose,
            ResourceConnectorPluginKeys.SecretLink => ResourceKind.SecretLink,
            ResourceConnectorPluginKeys.PromptLink => ResourceKind.PromptLink,
            ResourceConnectorPluginKeys.WebhookEndpoint => ResourceKind.WebLink,
            _ => fallback
        };
    }
}
