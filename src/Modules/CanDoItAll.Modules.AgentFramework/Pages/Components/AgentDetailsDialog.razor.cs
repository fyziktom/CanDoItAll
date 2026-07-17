using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Components;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Security;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components;

public sealed record AgentDetailsDialogResult(Guid? AgentId, bool Deleted);

public partial class AgentDetailsDialog
{
    [Parameter]
    public Guid? AgentId { get; set; }

    [Parameter]
    public IReadOnlyList<ProviderProfile>? InitialProviders { get; set; }

    [Parameter]
    public EventCallback<AgentDetailsDialogResult> Saved { get; set; }

    [Inject]
    public IAgentFrameworkWorkspaceService WorkspaceService { get; set; } = default!;

    [Inject]
    public ProjectsService ProjectsService { get; set; } = default!;

    [Inject]
    public SecretService SecretService { get; set; } = default!;

    [Inject]
    public NotificationService NotificationService { get; set; } = default!;

    [CascadingParameter]
    public DialogReference? DialogReference { get; set; }

    private AgentEditorModel editorModel = new();
    private IReadOnlyList<AgentDefinition> agents = [];
    private IReadOnlyList<ProviderProfile> providers = [];
    private IReadOnlyList<CapabilityCatalogItem> capabilities = [];
    private IReadOnlyList<ProjectAccessListItem> projectStructureProjects = [];
    private IReadOnlyList<SecretListItem> secrets = [];
    private IReadOnlyList<string> tagValues = [];
    private string externalWorkspaceRootsText = string.Empty;
    private string allowedStorageCatalogIdsText = string.Empty;
    private string newCapabilityName = string.Empty;
    private string newCapabilityEndpointOrPath = string.Empty;
    private string newCapabilityDescription = string.Empty;
    private string customAvatarFileName = string.Empty;
    private CapabilityKind newCapabilityKind = CapabilityKind.Skill;
    private Guid? linkedPartyId;
    private bool isLoading = true;
    private bool isBusy;
    private bool isAvatarUploadBusy;
    private bool areProvidersLoaded;
    private bool areProjectStructureProjectsLoaded;
    private bool isLoadingProjectStructureProjects;
    private bool projectStructureProjectsRequested;
    private bool areSecretsLoaded;
    private bool isLoadingSecrets;
    private string? providerLoadErrorMessage;
    private string? projectStructureProjectsErrorMessage;
    private string? secretsErrorMessage;
    private Task? projectStructureProjectsLoadTask;
    private int selectedTabIndex;
    private bool avatarSelectorOpen;

    private static IReadOnlyList<string> AvatarOptions => AgentAvatarImageCatalog.BundledAvatarUrls;

    private static IReadOnlyList<AgentWorkspaceToolProfileKind> WorkspaceToolProfileOptions { get; } =
    [
        AgentWorkspaceToolProfileKind.Custom,
        AgentWorkspaceToolProfileKind.ReadOnly,
        AgentWorkspaceToolProfileKind.SoftwareDevelopment,
        AgentWorkspaceToolProfileKind.QualityValidation,
        AgentWorkspaceToolProfileKind.ArchitectureReview,
        AgentWorkspaceToolProfileKind.SecurityReview,
        AgentWorkspaceToolProfileKind.BusinessAnalysis
    ];

    private ProviderProfile? SelectedRuntimeProvider => editorModel.ProviderProfileId.HasValue
        ? providers.FirstOrDefault(item => item.Id == editorModel.ProviderProfileId.Value)
        : null;

    private ProviderProfile? SelectedImageGenerationProvider
        => editorModel.ImageGenerationAccess.PreferredProviderProfileId.HasValue
            ? providers.FirstOrDefault(item => item.Id == editorModel.ImageGenerationAccess.PreferredProviderProfileId.Value)
            : ImageCapableRuntimeProvider;

    private ProviderProfile? ImageCapableRuntimeProvider
        => SelectedRuntimeProvider is { IsEnabled: true, Purpose: ProviderProfilePurpose.ImageGeneration } provider
            ? provider
            : null;

    private IReadOnlyList<ProviderProfile> ImageGenerationProviderOptions => providers
        .Where(provider => provider.Purpose == ProviderProfilePurpose.ImageGeneration)
        .OrderByDescending(provider => provider.IsEnabled)
        .ThenBy(provider => provider.Name, StringComparer.OrdinalIgnoreCase)
        .ToList();

    private IReadOnlyList<string> VisibleTagSuggestions => agents
        .SelectMany(agent => agent.Tags)
        .Where(tag => !AgentSpecialTags.IsFavorite(tag))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
        .ToList();

    private IReadOnlyList<CapabilityCatalogItem> AssignableCapabilities => capabilities
        .Where(item => item.Kind is CapabilityKind.Skill or CapabilityKind.McpServer ||
                       editorModel.SelectedCapabilityIds.Contains(item.Id))
        .OrderByDescending(item => editorModel.SelectedCapabilityIds.Contains(item.Id))
        .ThenBy(item => item.Kind)
        .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
        .ToList();

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        isLoading = true;

        try
        {
            var agentsTask = WorkspaceService.ListAgentsAsync(includeTemplates: false);
            var providersTask = InitialProviders is null
                ? WorkspaceService.ListProvidersAsync()
                : Task.FromResult<IReadOnlyList<ProviderProfile>>(InitialProviders);
            var capabilitiesTask = WorkspaceService.ListCapabilitiesAsync();
            var secretsTask = SecretService.ListForPickerAsync();

            agents = (await agentsTask).ToList();
            capabilities = (await capabilitiesTask).ToList();
            await LoadSecretsAsync(secretsTask);
            await LoadProvidersAsync(providersTask);

            if (AgentId.HasValue)
            {
                var definition = agents.FirstOrDefault(item => item.Id == AgentId.Value);
                if (definition is not null)
                {
                    ApplySelectedAgent(definition);
                }
                else
                {
                    editorModel = await WorkspaceService.GetAgentEditorAsync(AgentId.Value);
                    ApplyEditorTextState();
                    linkedPartyId = null;
                }
            }
            else
            {
                ResetEditorState();
            }
        }
        catch (Exception exception)
        {
            NotificationService.Error("Agent editor failed to load", exception.Message);
        }
        finally
        {
            isLoading = false;
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
            NotificationService.Error("Providers failed to load", exception.Message);
        }
    }

    private async Task LoadSecretsAsync(Task<IReadOnlyList<SecretListItem>> secretsTask)
    {
        isLoadingSecrets = true;
        secretsErrorMessage = null;
        try
        {
            secrets = (await secretsTask).ToList();
            areSecretsLoaded = true;
        }
        catch (Exception exception)
        {
            secretsErrorMessage = $"Failed to load secrets. {exception.Message}";
            NotificationService.Error("Secrets failed to load", exception.Message);
        }
        finally
        {
            isLoadingSecrets = false;
        }
    }

    private Task HandleSelectedTabIndexChanged(int index)
    {
        selectedTabIndex = index;
        return Task.CompletedTask;
    }

    private Task RequestProjectStructureProjectsAsync()
    {
        projectStructureProjectsRequested = true;
        return EnsureProjectStructureProjectsLoadedAsync();
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
            NotificationService.Error("Project list failed to load", exception.Message);
        }
        finally
        {
            isLoadingProjectStructureProjects = false;
            projectStructureProjectsLoadTask = null;
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task SaveAgentAsync()
    {
        if (isBusy)
        {
            return;
        }

        isBusy = true;
        try
        {
            var agentId = await PersistEditorAsync();
            await ReloadCatalogStateAsync(agentId);
            NotificationService.Success("Agent saved", "Technical agent saved.");
            await Saved.InvokeAsync(new AgentDetailsDialogResult(agentId, Deleted: false));
        }
        catch (Exception exception)
        {
            NotificationService.Error("Agent save failed", exception.Message);
        }
        finally
        {
            isBusy = false;
        }
    }

    private async Task<Guid> PersistEditorAsync()
    {
        SyncWorkspaceToolAccessFromEditorText();
        NormalizeRuntimeModelSelectionForSave();
        NormalizeImageGenerationAccessForSave();
        editorModel.Tags = BuildAgentTagsForSave().ToList();
        return await WorkspaceService.SaveAgentAsync(editorModel);
    }

    private async Task ReloadCatalogStateAsync(Guid selectedAgentId)
    {
        agents = await WorkspaceService.ListAgentsAsync(includeTemplates: false);
        capabilities = await WorkspaceService.ListCapabilitiesAsync();
        var definition = agents.FirstOrDefault(item => item.Id == selectedAgentId);
        if (definition is not null)
        {
            ApplySelectedAgent(definition);
        }
    }

    private async Task DeleteAgentAsync()
    {
        if (!editorModel.Id.HasValue || isBusy)
        {
            return;
        }

        isBusy = true;
        var deletedAgentId = editorModel.Id.Value;

        try
        {
            await WorkspaceService.DeleteAgentAsync(deletedAgentId);
            NotificationService.Success("Agent deleted", "Technical agent deleted.");
            await Saved.InvokeAsync(new AgentDetailsDialogResult(deletedAgentId, Deleted: true));
            if (DialogReference is not null)
            {
                await DialogReference.CloseAsync(new AgentDetailsDialogResult(deletedAgentId, Deleted: true));
            }
        }
        catch (Exception exception)
        {
            NotificationService.Error("Agent delete failed", exception.Message);
        }
        finally
        {
            isBusy = false;
        }
    }

    private Task ResetAgentAsync()
    {
        ResetEditorState();
        selectedTabIndex = 0;
        return Task.CompletedTask;
    }

    private Task OpenAvatarSelectorAsync()
    {
        avatarSelectorOpen = true;
        return Task.CompletedTask;
    }

    private Task CloseAvatarSelectorAsync()
    {
        avatarSelectorOpen = false;
        return Task.CompletedTask;
    }

    private Task ClearAvatarAsync()
    {
        editorModel.AvatarImageUrl = string.Empty;
        customAvatarFileName = string.Empty;
        return Task.CompletedTask;
    }

    private async Task LoadCustomAvatarAsync(InputFileChangeEventArgs args)
    {
        var file = args.File;
        customAvatarFileName = file.Name;
        isAvatarUploadBusy = true;

        try
        {
            editorModel.AvatarImageUrl = await AgentAvatarUploadFormatter.BuildDataUrlAsync(file);
            NotificationService.Success("Custom avatar loaded", "Save the agent to persist the loaded avatar.");
        }
        catch (Exception exception)
        {
            customAvatarFileName = string.Empty;
            NotificationService.Error("Avatar upload failed", exception.Message);
        }
        finally
        {
            isAvatarUploadBusy = false;
        }
    }

    private void SelectAvatar(string avatarImageUrl)
    {
        editorModel.AvatarImageUrl = avatarImageUrl.Trim();
        customAvatarFileName = string.Empty;
    }

    private bool IsSelectedAvatar(string avatarImageUrl)
        => string.Equals(editorModel.AvatarImageUrl?.Trim(), avatarImageUrl.Trim(), StringComparison.OrdinalIgnoreCase);

    private bool IsCustomAvatarLoaded()
        => editorModel.AvatarImageUrl.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase);

    private string ResolveAvatarOptionClass(string avatarImageUrl)
        => IsSelectedAvatar(avatarImageUrl)
            ? "agent-details-dialog__avatar-option agent-details-dialog__avatar-option--selected"
            : "agent-details-dialog__avatar-option";

    private string ResolveAvatarSelectionText()
    {
        if (string.IsNullOrWhiteSpace(editorModel.AvatarImageUrl))
        {
            return "Default generated avatar";
        }

        if (AgentAvatarImageCatalog.IsBundledAvatarUrl(editorModel.AvatarImageUrl))
        {
            return "Bundled avatar selected";
        }

        if (editorModel.AvatarImageUrl.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
        {
            return "Custom avatar loaded";
        }

        return "Custom avatar selected";
    }

    private string ResolveAvatarAltText()
        => FirstNonEmpty(editorModel.Name, editorModel.RoleTitle, "Agent avatar");

    private string ResolveAvatarFallbackText()
        => BuildInitials(ResolveAvatarSeed());

    private string ResolveAvatarSeed()
        => FirstNonEmpty(editorModel.Name, editorModel.RoleTitle, "Agent avatar");

    private async Task ToggleCapabilityAsync(Guid capabilityId)
    {
        if (isBusy)
        {
            return;
        }

        var selectedCapabilityIds = editorModel.SelectedCapabilityIds.ToList();
        var isAttached = selectedCapabilityIds.Contains(capabilityId);
        if (isAttached)
        {
            selectedCapabilityIds.Remove(capabilityId);
        }
        else
        {
            selectedCapabilityIds.Add(capabilityId);
        }

        editorModel.SelectedCapabilityIds = selectedCapabilityIds
            .Distinct()
            .OrderBy(item => item)
            .ToList();

        if (!editorModel.Id.HasValue)
        {
            NotificationService.Info("Capability staged", "Save the new agent to persist capability assignments.");
            return;
        }

        isBusy = true;
        try
        {
            var agentId = await PersistEditorAsync();
            await ReloadCatalogStateAsync(agentId);
            NotificationService.Success("Capability assignment updated", "Agent capability assignment saved.");
            await Saved.InvokeAsync(new AgentDetailsDialogResult(agentId, Deleted: false));
        }
        catch (Exception exception)
        {
            NotificationService.Error("Capability assignment failed", exception.Message);
        }
        finally
        {
            isBusy = false;
        }
    }

    private async Task CreateAndAssignCapabilityAsync()
    {
        if (isBusy)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(newCapabilityName))
        {
            NotificationService.Warning("Capability name required", "Enter a capability name before creating it.");
            return;
        }

        isBusy = true;
        try
        {
            var capabilityId = await WorkspaceService.SaveCapabilityAsync(new CapabilityEditorModel
            {
                Kind = newCapabilityKind,
                Key = newCapabilityName,
                Name = newCapabilityName,
                Description = newCapabilityDescription,
                EndpointOrPath = newCapabilityEndpointOrPath,
                ConfigurationJson = string.Empty,
                IsBuiltIn = false
            });

            if (!editorModel.SelectedCapabilityIds.Contains(capabilityId))
            {
                editorModel.SelectedCapabilityIds.Add(capabilityId);
            }

            capabilities = await WorkspaceService.ListCapabilitiesAsync();
            if (editorModel.Id.HasValue)
            {
                var agentId = await PersistEditorAsync();
                await ReloadCatalogStateAsync(agentId);
                await Saved.InvokeAsync(new AgentDetailsDialogResult(agentId, Deleted: false));
            }

            newCapabilityName = string.Empty;
            newCapabilityEndpointOrPath = string.Empty;
            newCapabilityDescription = string.Empty;
            newCapabilityKind = CapabilityKind.Skill;
            NotificationService.Success("Capability created", "Capability was created and assigned.");
        }
        catch (Exception exception)
        {
            NotificationService.Error("Capability create failed", exception.Message);
        }
        finally
        {
            isBusy = false;
        }
    }

    private async Task VerifyCapabilityAsync(Guid capabilityId)
    {
        if (!editorModel.Id.HasValue || isBusy)
        {
            return;
        }

        isBusy = true;
        try
        {
            await WorkspaceService.VerifyCapabilityAsync(editorModel.Id.Value, capabilityId);
            capabilities = await WorkspaceService.ListCapabilitiesAsync();
            editorModel = await WorkspaceService.GetAgentEditorAsync(editorModel.Id.Value);
            ApplyEditorTextState();
            NotificationService.Success("Capability verified", "Capability verification completed.");
        }
        catch (Exception exception)
        {
            NotificationService.Error("Capability verification failed", exception.Message);
        }
        finally
        {
            isBusy = false;
        }
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

    private static string DescribeWorkspaceFileScope(AgentEditorModel editor)
    {
        if (!editor.WorkspaceToolAccess.CanReadFiles &&
            !editor.WorkspaceToolAccess.CanWriteFiles)
        {
            return "File tools disabled";
        }

        var accessMode = editor.WorkspaceToolAccess.CanWriteFiles
            ? "Read/write"
            : "Read-only";
        var externalRootCount = editor.WorkspaceToolAccess.AllowedExternalTargetAliases.Count;
        return externalRootCount == 0
            ? $"{accessMode}; managed workspace only"
            : $"{accessMode}; {externalRootCount} external root(s)";
    }

    private static string DescribeWorkspaceExecutionScope(AgentEditorModel editor)
    {
        var access = editor.WorkspaceToolAccess;
        var enabled = new List<string>();
        if (access.CanRunValidationCommands)
        {
            enabled.Add("build/test/run");
        }

        if (access.CanRunLocalScripts)
        {
            enabled.Add("local scripts");
        }

        if (access.CanScaffoldProjects)
        {
            enabled.Add("project scaffolding");
        }

        if (access.CanManageWorkspacePaths)
        {
            enabled.Add("path management");
        }

        if (access.CanTransformArtifacts)
        {
            enabled.Add("artifact transforms");
        }

        return enabled.Count == 0
            ? "Execution tools disabled"
            : $"Enabled: {string.Join(", ", enabled)}";
    }

    private static string DescribeStorageScope(AgentEditorModel editor)
    {
        if (!editor.WorkspaceToolAccess.CanReadStorage &&
            !editor.WorkspaceToolAccess.CanWriteStorage)
        {
            return "Storage tools disabled";
        }

        var accessMode = editor.WorkspaceToolAccess.CanWriteStorage
            ? "Read/write"
            : "Read-only";

        return editor.WorkspaceToolAccess.AllowAllStorageCatalogs
            ? $"{accessMode}; all storage catalogs"
            : $"{accessMode}; {editor.WorkspaceToolAccess.AllowedStorageCatalogIds.Count} storage catalog(s)";
    }

    private static string DescribeSecretScope(AgentEditorModel editor)
    {
        var count = editor.AllowedSecretReferences
            .Where(item => item.SecretId != Guid.Empty)
            .Select(item => item.SecretId)
            .Distinct()
            .Count();
        return count == 0
            ? "No stored secrets"
            : $"{count} stored secret(s)";
    }

    private bool HasAllowedSecret(Guid secretId)
        => editorModel.AllowedSecretReferences.Any(item => item.SecretId == secretId);

    private void ToggleAllowedSecret(SecretListItem secret, object? rawValue)
    {
        var isEnabled = rawValue is bool value && value;
        editorModel.AllowedSecretReferences.RemoveAll(item => item.SecretId == secret.Id);
        if (!isEnabled)
        {
            return;
        }

        editorModel.AllowedSecretReferences.Add(new AgentAllowedSecretReference(
            secret.Id,
            secret.Name,
            AgentSecretPurposes.GeneralAgentRequest));
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
            editorModel.ProjectStructureAccess.CanWriteNonTaskStructure = false;
            editorModel.ProjectStructureAccess.CanWriteTasks = false;
            editorModel.ProjectStructureAccess.CanCreateProjects = false;
            editorModel.ProjectStructureAccess.CanCreateSubprojects = false;
            editorModel.ProjectStructureAccess.AllowAllProjects = false;
        }
    }

    private void ToggleProjectCreation(object? rawValue)
    {
        var isEnabled = rawValue is bool value && value;
        editorModel.ProjectStructureAccess.CanCreateProjects = isEnabled;
        EnsureProjectAccessLoadedWhenEnabled(isEnabled);
    }

    private void ToggleSubprojectCreation(object? rawValue)
    {
        var isEnabled = rawValue is bool value && value;
        editorModel.ProjectStructureAccess.CanCreateSubprojects = isEnabled;
        EnsureProjectAccessLoadedWhenEnabled(isEnabled);
    }

    private void EnsureProjectAccessLoadedWhenEnabled(bool isEnabled)
    {
        if (!isEnabled)
        {
            return;
        }

        editorModel.ProjectStructureAccess.CanRead = true;
        projectStructureProjectsRequested = true;
        _ = EnsureProjectStructureProjectsLoadedAsync();
    }

    private void ToggleProjectStructureNonTaskWrite(object? rawValue)
    {
        var isEnabled = rawValue is bool value && value;
        editorModel.ProjectStructureAccess.CanWriteNonTaskStructure = isEnabled;
        if (isEnabled)
        {
            editorModel.ProjectStructureAccess.CanRead = true;
            projectStructureProjectsRequested = true;
            _ = EnsureProjectStructureProjectsLoadedAsync();
        }
    }

    private void ToggleProjectStructureTaskWrite(object? rawValue)
    {
        var isEnabled = rawValue is bool value && value;
        editorModel.ProjectStructureAccess.CanWriteTasks = isEnabled;
        if (isEnabled)
        {
            editorModel.ProjectStructureAccess.CanRead = true;
            projectStructureProjectsRequested = true;
            _ = EnsureProjectStructureProjectsLoadedAsync();
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

    private void ChangeWorkspaceToolProfile(object? rawValue)
    {
        if (!Enum.TryParse<AgentWorkspaceToolProfileKind>(rawValue?.ToString(), ignoreCase: true, out var profile) ||
            !Enum.IsDefined(profile))
        {
            return;
        }

        var current = editorModel.WorkspaceToolAccess;
        var next = profile == AgentWorkspaceToolProfileKind.Custom
            ? CloneWorkspaceToolAccess(current)
            : AgentWorkspaceToolAccessProfiles.CreateSettings(profile);
        next.Profile = profile;
        next.AllowedExternalTargetAliases = current.AllowedExternalTargetAliases.ToList();
        next.CanReadStorage = current.CanReadStorage;
        next.CanWriteStorage = current.CanWriteStorage;
        next.AllowAllStorageCatalogs = current.AllowAllStorageCatalogs;
        next.AllowedStorageCatalogIds = current.AllowedStorageCatalogIds.ToList();
        editorModel.WorkspaceToolAccess = AgentWorkspaceToolAccessMetadata.Normalize(next);
    }

    private void ToggleWorkspaceFileRead(object? rawValue)
    {
        var isEnabled = rawValue is bool value && value;
        MarkWorkspaceToolProfileCustom();
        editorModel.WorkspaceToolAccess.CanReadFiles = isEnabled;
        if (!isEnabled)
        {
            editorModel.WorkspaceToolAccess.CanWriteFiles = false;
        }

        NormalizeWorkspaceToolAccess();
    }

    private void ToggleWorkspaceFileWrite(object? rawValue)
    {
        var isEnabled = rawValue is bool value && value;
        MarkWorkspaceToolProfileCustom();
        editorModel.WorkspaceToolAccess.CanWriteFiles = isEnabled;
        if (isEnabled)
        {
            editorModel.WorkspaceToolAccess.CanReadFiles = true;
        }

        NormalizeWorkspaceToolAccess();
    }

    private void ToggleWorkspaceValidationCommands(object? rawValue)
    {
        MarkWorkspaceToolProfileCustom();
        editorModel.WorkspaceToolAccess.CanRunValidationCommands = rawValue is bool value && value;
        NormalizeWorkspaceToolAccess();
    }

    private void ToggleWorkspaceLocalScripts(object? rawValue)
    {
        MarkWorkspaceToolProfileCustom();
        editorModel.WorkspaceToolAccess.CanRunLocalScripts = rawValue is bool value && value;
        NormalizeWorkspaceToolAccess();
    }

    private void ToggleWorkspaceScaffoldProjects(object? rawValue)
    {
        MarkWorkspaceToolProfileCustom();
        editorModel.WorkspaceToolAccess.CanScaffoldProjects = rawValue is bool value && value;
        NormalizeWorkspaceToolAccess();
    }

    private void ToggleWorkspaceManagePaths(object? rawValue)
    {
        MarkWorkspaceToolProfileCustom();
        editorModel.WorkspaceToolAccess.CanManageWorkspacePaths = rawValue is bool value && value;
        NormalizeWorkspaceToolAccess();
    }

    private void ToggleWorkspaceTransformArtifacts(object? rawValue)
    {
        MarkWorkspaceToolProfileCustom();
        editorModel.WorkspaceToolAccess.CanTransformArtifacts = rawValue is bool value && value;
        NormalizeWorkspaceToolAccess();
    }

    private void MarkWorkspaceToolProfileCustom()
    {
        editorModel.WorkspaceToolAccess.Profile = AgentWorkspaceToolProfileKind.Custom;
    }

    private void NormalizeWorkspaceToolAccess()
    {
        editorModel.WorkspaceToolAccess = AgentWorkspaceToolAccessMetadata.Normalize(editorModel.WorkspaceToolAccess);
    }

    private static AgentWorkspaceToolAccessSettings CloneWorkspaceToolAccess(AgentWorkspaceToolAccessSettings source)
    {
        return new AgentWorkspaceToolAccessSettings
        {
            Profile = source.Profile,
            CanReadFiles = source.CanReadFiles,
            CanWriteFiles = source.CanWriteFiles,
            CanRunValidationCommands = source.CanRunValidationCommands,
            CanRunLocalScripts = source.CanRunLocalScripts,
            CanScaffoldProjects = source.CanScaffoldProjects,
            CanManageWorkspacePaths = source.CanManageWorkspacePaths,
            CanTransformArtifacts = source.CanTransformArtifacts,
            AllowedExternalTargetAliases = source.AllowedExternalTargetAliases.ToList(),
            CanReadStorage = source.CanReadStorage,
            CanWriteStorage = source.CanWriteStorage,
            AllowAllStorageCatalogs = source.AllowAllStorageCatalogs,
            AllowedStorageCatalogIds = source.AllowedStorageCatalogIds.ToList()
        };
    }

    private void ToggleStorageRead(object? rawValue)
    {
        var isEnabled = rawValue is bool value && value;
        editorModel.WorkspaceToolAccess.CanReadStorage = isEnabled;
        if (!isEnabled)
        {
            editorModel.WorkspaceToolAccess.CanWriteStorage = false;
            editorModel.WorkspaceToolAccess.AllowAllStorageCatalogs = false;
        }
    }

    private void ToggleStorageWrite(object? rawValue)
    {
        var isEnabled = rawValue is bool value && value;
        editorModel.WorkspaceToolAccess.CanWriteStorage = isEnabled;
        if (isEnabled)
        {
            editorModel.WorkspaceToolAccess.CanReadStorage = true;
        }
    }

    private void ToggleStorageAllowAll(object? rawValue)
    {
        var isEnabled = rawValue is bool value && value;
        editorModel.WorkspaceToolAccess.AllowAllStorageCatalogs = isEnabled;
        if (isEnabled)
        {
            editorModel.WorkspaceToolAccess.CanReadStorage = true;
        }
    }

    private void ToggleVoiceModeAccess(object? rawValue)
    {
        var isEnabled = rawValue is bool value && value;
        editorModel.VoiceAccess.CanUseVoiceMode = isEnabled;
        if (!isEnabled)
        {
            editorModel.VoiceAccess.PreferredVoiceId = string.Empty;
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

    private Task HandleTagsChangedAsync(IReadOnlyList<string> value)
    {
        tagValues = NormalizeVisibleTags(value);
        return Task.CompletedTask;
    }

    private Task HandleRuntimeProviderChangedAsync(Guid? providerId)
    {
        if (editorModel.ProviderProfileId != providerId)
        {
            editorModel.ProviderProfileId = providerId;
            editorModel.Model = string.Empty;
        }

        return Task.CompletedTask;
    }

    private Task HandleRuntimeModelChangedAsync(string? model)
    {
        editorModel.Model = string.IsNullOrWhiteSpace(model)
            ? string.Empty
            : model.Trim();
        return Task.CompletedTask;
    }

    private Task HandleImageGenerationProviderChangedAsync(Guid? providerId)
    {
        if (editorModel.ImageGenerationAccess.PreferredProviderProfileId != providerId)
        {
            editorModel.ImageGenerationAccess.PreferredProviderProfileId = providerId;
            editorModel.ImageGenerationAccess.DefaultModel = string.Empty;
        }

        editorModel.ImageGenerationAccess = AgentImageGenerationAccessMetadata.Normalize(editorModel.ImageGenerationAccess);
        return Task.CompletedTask;
    }

    private Task HandleImageGenerationModelChangedAsync(string? model)
    {
        editorModel.ImageGenerationAccess.DefaultModel = string.IsNullOrWhiteSpace(model)
            ? string.Empty
            : model.Trim();
        return Task.CompletedTask;
    }

    private void ToggleImageGenerationAccess(object? rawValue)
    {
        editorModel.ImageGenerationAccess.CanGenerateImages = rawValue is bool value && value;
        editorModel.ImageGenerationAccess = AgentImageGenerationAccessMetadata.Normalize(editorModel.ImageGenerationAccess);
    }

    private void ToggleImageProjectAssetStorage(object? rawValue)
    {
        editorModel.ImageGenerationAccess.CanStoreImagesAsProjectAssets = rawValue is bool value && value;
        editorModel.ImageGenerationAccess = AgentImageGenerationAccessMetadata.Normalize(editorModel.ImageGenerationAccess);
    }

    private void NormalizeRuntimeModelSelectionForSave()
    {
        editorModel.Model = SelectedRuntimeProvider is { } provider
            ? ProviderModelSelector.NormalizeProviderDefaultModel(editorModel.Model, provider)
            : string.IsNullOrWhiteSpace(editorModel.Model)
                ? string.Empty
                : editorModel.Model.Trim();
    }

    private void NormalizeImageGenerationAccessForSave()
    {
        var access = AgentImageGenerationAccessMetadata.Normalize(editorModel.ImageGenerationAccess);
        access.DefaultModel = SelectedImageGenerationProvider is { } provider
            ? ProviderModelSelector.NormalizeProviderDefaultModel(access.DefaultModel, provider)
            : string.IsNullOrWhiteSpace(access.DefaultModel)
                ? string.Empty
                : access.DefaultModel.Trim();
        editorModel.ImageGenerationAccess = AgentImageGenerationAccessMetadata.Normalize(access);
    }

    private string DescribeImageGenerationProviderChoice()
    {
        if (!editorModel.ImageGenerationAccess.CanGenerateImages)
        {
            return "Image-generation tools are disabled for this agent.";
        }

        if (editorModel.ImageGenerationAccess.PreferredProviderProfileId.HasValue)
        {
            return SelectedImageGenerationProvider is { } provider
                ? $"Image requests use '{provider.Name}'."
                : "The selected image-generation provider is not available.";
        }

        return ImageCapableRuntimeProvider is { } runtimeProvider
            ? $"Image requests use the runtime provider '{runtimeProvider.Name}'."
            : "Image requests use the first enabled image-generation provider unless a provider is selected here.";
    }

    private string ResolveImageGenerationWarning()
    {
        if (!editorModel.ImageGenerationAccess.CanGenerateImages)
        {
            return string.Empty;
        }

        if (editorModel.ImageGenerationAccess.PreferredProviderProfileId.HasValue &&
            SelectedImageGenerationProvider is null)
        {
            return "The selected image-generation provider is missing. Select an enabled image-generation provider before relying on this agent for image delivery.";
        }

        if (SelectedImageGenerationProvider is { IsEnabled: false } disabledProvider)
        {
            return $"Image-generation provider '{disabledProvider.Name}' is disabled.";
        }

        if (ImageCapableRuntimeProvider is null &&
            !editorModel.ImageGenerationAccess.PreferredProviderProfileId.HasValue &&
            !ImageGenerationProviderOptions.Any(provider => provider.IsEnabled))
        {
            return "No enabled image-generation provider is configured.";
        }

        return string.Empty;
    }

    private void SyncWorkspaceToolAccessFromEditorText()
    {
        editorModel.WorkspaceToolAccess.AllowedExternalTargetAliases = SplitEditorLines(externalWorkspaceRootsText)
            .Select(AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias)
            .Where(alias => !string.IsNullOrWhiteSpace(alias))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(alias => alias, StringComparer.OrdinalIgnoreCase)
            .ToList();

        editorModel.WorkspaceToolAccess.AllowedStorageCatalogIds = SplitEditorLines(allowedStorageCatalogIdsText)
            .Where(item => Guid.TryParse(item, out _))
            .Select(Guid.Parse)
            .Where(item => item != Guid.Empty)
            .Distinct()
            .OrderBy(item => item)
            .ToList();

        editorModel.WorkspaceToolAccess = AgentWorkspaceToolAccessMetadata.Normalize(editorModel.WorkspaceToolAccess);
        externalWorkspaceRootsText = string.Join(Environment.NewLine, editorModel.WorkspaceToolAccess.AllowedExternalTargetAliases);
        allowedStorageCatalogIdsText = string.Join(Environment.NewLine, editorModel.WorkspaceToolAccess.AllowedStorageCatalogIds.Select(item => item.ToString("D")));
    }

    private static IReadOnlyList<string> SplitEditorLines(string value)
    {
        return value
            .Split(["\r\n", "\n", "\r", ",", ";"], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .ToList();
    }

    private IReadOnlyList<string> BuildAgentTagsForSave()
    {
        var nextTags = NormalizeVisibleTags(tagValues).ToList();
        if (editorModel.Tags.Any(AgentSpecialTags.IsFavorite))
        {
            nextTags.Add(AgentSpecialTags.Favorite);
        }

        return nextTags
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<string> NormalizeVisibleTags(IEnumerable<string> tags)
    {
        return tags
            .Select(tag => tag.Trim())
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Where(tag => !AgentSpecialTags.IsFavorite(tag))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

    private static string BuildInitials(string value)
    {
        var words = value
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Take(2)
            .ToArray();
        return words.Length == 0
            ? "A"
            : string.Concat(words.Select(word => char.ToUpperInvariant(word[0])));
    }

    private bool IsCapabilityAttached(Guid capabilityId)
        => editorModel.SelectedCapabilityIds.Contains(capabilityId);

    private static string ResolveCapabilityKindLabel(CapabilityKind kind)
    {
        return kind switch
        {
            CapabilityKind.McpServer => "MCP server",
            CapabilityKind.AiContext => "AI context",
            _ => kind.ToString()
        };
    }

    private static string FormatWorkspaceToolProfile(AgentWorkspaceToolProfileKind profile)
    {
        return profile switch
        {
            AgentWorkspaceToolProfileKind.ReadOnly => "Read only",
            AgentWorkspaceToolProfileKind.SoftwareDevelopment => "Software development",
            AgentWorkspaceToolProfileKind.QualityValidation => "Quality validation",
            AgentWorkspaceToolProfileKind.ArchitectureReview => "Architecture review",
            AgentWorkspaceToolProfileKind.SecurityReview => "Security review",
            AgentWorkspaceToolProfileKind.BusinessAnalysis => "Business analysis",
            _ => "Custom"
        };
    }

    private static string ResolveEndpointSummary(CapabilityCatalogItem capability)
    {
        return string.IsNullOrWhiteSpace(capability.EndpointOrPath)
            ? "No endpoint or path is stored for this capability."
            : capability.EndpointOrPath;
    }

    private void ApplySelectedAgent(AgentDefinition definition)
    {
        editorModel = AgentEditorModel.FromDefinition(definition);
        ApplyEditorTextState();
        linkedPartyId = AgentFrameworkCrmHrMetadata.Read(definition.ConfigurationJson)?.PartyId;
    }

    private void ApplyEditorTextState()
    {
        tagValues = NormalizeVisibleTags(editorModel.Tags);
        externalWorkspaceRootsText = string.Join(Environment.NewLine, editorModel.WorkspaceToolAccess.AllowedExternalTargetAliases);
        allowedStorageCatalogIdsText = string.Join(Environment.NewLine, editorModel.WorkspaceToolAccess.AllowedStorageCatalogIds.Select(item => item.ToString("D")));
    }

    private void ResetEditorState()
    {
        editorModel = new AgentEditorModel();
        tagValues = [];
        externalWorkspaceRootsText = string.Empty;
        allowedStorageCatalogIdsText = string.Empty;
        linkedPartyId = null;
    }
}
