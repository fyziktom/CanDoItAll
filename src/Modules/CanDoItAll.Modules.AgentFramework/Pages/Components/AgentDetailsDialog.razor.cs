using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Components;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Conversations.Components.Presentation;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Security;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components;

using IProviderRuntimeAdministrationService = CanDoItAll.Modules.AgentFramework.ProviderManagement.IProviderRuntimeAdministrationService;

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
    public IProviderRuntimeAdministrationService ProviderRuntimeAdministrationService { get; set; } = default!;

    [Inject]
    public ProjectsService ProjectsService { get; set; } = default!;

    [Inject]
    public SecretService SecretService { get; set; } = default!;

    [Inject]
    public NotificationService NotificationService { get; set; } = default!;

    [Inject]
    public DialogService DialogService { get; set; } = default!;

    [Inject]
    public IExternalTargetPathRegistryFactory ExternalTargetPathRegistryFactory { get; set; } = default!;

    [CascadingParameter]
    public DialogReference? DialogReference { get; set; }

    private AgentEditorModel editorModel = new();
    private IReadOnlyList<AgentDefinition> agents = [];
    private IReadOnlyList<ProviderProfile> providers = [];
    private IReadOnlyList<CapabilityCatalogItem> capabilities = [];
    private IReadOnlyList<ProjectAccessListItem> projectStructureProjects = [];
    private IReadOnlyList<SecretListItem> secrets = [];
    private IReadOnlyList<string> tagValues = [];
    private string capabilitySearch = string.Empty;
    private CapabilityDialogAssignmentFilter capabilityAssignmentFilter = CapabilityDialogAssignmentFilter.All;
    private CapabilityDialogKindFilter capabilityKindFilter = CapabilityDialogKindFilter.All;
    private Guid? linkedPartyId;
    private bool isLoading = true;
    private bool isBusy;
    private bool isOpeningCapabilityWizard;
    private bool isConfirmingAutoApproval;
    private bool isConfirmingDelete;
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
    private int autoApprovalInputVersion;

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

    private async Task RefreshRuntimeProvidersAsync() {
        providers = await ProviderRuntimeAdministrationService.ListProvidersAsync();
        areProvidersLoaded = true;
        providerLoadErrorMessage = null;
    }

    private IReadOnlyList<ConversationProviderOption> RuntimeProviderOptions
        => AgentProviderPresentationMapper.Map(providers);

    private ConversationPresentationKey? SelectedRuntimeProviderKey
        => AgentProviderPresentationMapper.ToPresentationKey(editorModel.ProviderProfileId);

    private bool HasIncompatibleThinkingEffortOverride
    {
        get
        {
            if (SelectedRuntimeProvider is not { } provider)
            {
                return editorModel.ThinkingEffortOverride is not null;
            }

            var model = ResolveEditorRuntimeModel(provider);
            if (editorModel.ThinkingEffortOverride is { } effort)
            {
                return !AgentThinkingEffortPolicy.IsOverrideSupported(provider, model, effort);
            }

            try
            {
                _ = AgentThinkingEffortPolicy.ResolveProviderDefault(provider, model);
                return false;
            }
            catch (InvalidOperationException)
            {
                return true;
            }
        }
    }

    private ProviderProfile? SelectedImageGenerationProvider
        => editorModel.ImageGenerationAccess.PreferredProviderProfileId.HasValue
            ? providers.FirstOrDefault(item => item.Id == editorModel.ImageGenerationAccess.PreferredProviderProfileId.Value)
            : ImageGenerationProviderSelectionPolicy.ResolveDefault(providers, SelectedRuntimeProvider);

    private ProviderProfile? DefaultAvatarImageProvider
        => SelectedImageGenerationProvider is { IsEnabled: true, Purpose: ProviderProfilePurpose.ImageGeneration } provider
            ? provider
            : null;

    private AvatarGenerationSource? AvatarGenerationSource
        => DefaultAvatarImageProvider is { } provider
            ? new(provider.Id, provider.Name, ResolveAvatarImageModel(provider))
            : null;

    private ProviderProfile? ImageCapableRuntimeProvider
        => SelectedRuntimeProvider is { IsEnabled: true, Purpose: ProviderProfilePurpose.ImageGeneration } provider
            ? provider
            : null;

    private IReadOnlyList<ProviderProfile> ImageGenerationProviderOptions => providers
        .Where(provider => provider.Purpose == ProviderProfilePurpose.ImageGeneration)
        .OrderByDescending(provider => provider.IsEnabled)
        .ThenBy(provider => provider.Name, StringComparer.OrdinalIgnoreCase)
        .ToList();

    private IReadOnlyList<ConversationProviderOption> ImageGenerationProviderPresentationOptions
        => AgentProviderPresentationMapper.Map(ImageGenerationProviderOptions);

    private ConversationPresentationKey? SelectedImageGenerationProviderKey
        => AgentProviderPresentationMapper.ToPresentationKey(
            editorModel.ImageGenerationAccess.PreferredProviderProfileId);

    private ConversationAvatarPresentation IdentityAvatar => new(
        ResolveAvatarAltText(),
        editorModel.AvatarImageUrl,
        ResolveAvatarFallbackText(),
        ResolveAvatarSeed());

    private IReadOnlyList<string> VisibleTagSuggestions => agents
        .SelectMany(agent => agent.Tags)
        .Where(tag => !AgentSpecialTags.IsFavorite(tag))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
        .ToList();

    private IReadOnlyList<CapabilityCatalogItem> AssignableCapabilities => capabilities
        .Where(item => item.Kind is CapabilityKind.Tool or CapabilityKind.Skill or CapabilityKind.McpServer ||
                       editorModel.SelectedCapabilityIds.Contains(item.Id))
        .OrderByDescending(item => editorModel.SelectedCapabilityIds.Contains(item.Id))
        .ThenBy(item => item.Kind)
        .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
        .ToList();

    private IReadOnlyList<CapabilityCatalogItem> FilteredAssignableCapabilities => AssignableCapabilities
        .Where(MatchesCapabilitySearch)
        .Where(MatchesCapabilityAssignmentFilter)
        .Where(MatchesCapabilityKindFilter)
        .ToList();

    private IReadOnlyList<string> AvailableCapabilityTags => capabilities
        .SelectMany(item => item.Tags)
        .Where(tag => !string.IsNullOrWhiteSpace(tag))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
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
            ? ProviderRuntimeAdministrationService.ListProvidersAsync()
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
                    ApplyDerivedEditorState();
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
        NormalizeWorkspaceToolAccessForSave();
        NormalizeRuntimeModelSelectionForSave();
        NormalizeImageGenerationAccessForSave();
        editorModel.ProjectStructureAccess =
            AgentProjectStructureAccessMetadata.Normalize(editorModel.ProjectStructureAccess);
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
        if (!editorModel.Id.HasValue ||
            isBusy ||
            isConfirmingDelete ||
            IsManagedSeedAgent)
        {
            return;
        }

        var deletedAgentId = editorModel.Id.Value;
        var deletedAgentName = string.IsNullOrWhiteSpace(editorModel.Name)
            ? "Unnamed agent"
            : editorModel.Name.Trim();
        var confirmed = false;

        try
        {
            isConfirmingDelete = true;
            confirmed = await DialogService.OpenAsync<AgentDeleteConfirmationDialog>(
                "Delete agent?",
                new Dictionary<string, object?>
                {
                    [nameof(AgentDeleteConfirmationDialog.AgentName)] = deletedAgentName
                },
                new DialogOptions
                {
                    Eyebrow = "Danger action",
                    Subtitle = "This action cannot be undone.",
                    Size = ModalSize.Compact,
                    DenseChrome = true,
                    AriaLabel = $"Confirm deletion of agent {deletedAgentName}",
                    TestId = "agents-catalog-delete-confirmation"
                }) is true;
        }
        catch (Exception exception)
        {
            NotificationService.Error("Agent delete confirmation failed", exception.Message);
        }
        finally
        {
            isConfirmingDelete = false;
        }

        if (!confirmed)
        {
            return;
        }

        isBusy = true;
        try
        {
            await WorkspaceService.DeleteAgentAsync(deletedAgentId);
        }
        catch (Exception exception)
        {
            NotificationService.Error("Agent delete failed", exception.Message);
            return;
        }
        finally
        {
            isBusy = false;
        }

        NotificationService.Success("Agent deleted", $"Technical agent '{deletedAgentName}' deleted.");
        var result = new AgentDetailsDialogResult(deletedAgentId, Deleted: true);
        try
        {
            if (DialogReference is not null)
            {
                await DialogReference.CloseAsync(result);
            }
            else
            {
                await Saved.InvokeAsync(result);
            }
        }
        catch (Exception exception)
        {
            NotificationService.Error(
                "Agent deleted, but the catalog refresh failed",
                exception.Message);
        }
    }

    private bool IsManagedSeedAgent
    {
        get
        {
            if (!editorModel.Id.HasValue)
            {
                return false;
            }

            var definition = agents.FirstOrDefault(item => item.Id == editorModel.Id.Value);
            return definition is not null &&
                   ManagedSeedProviderFallbacks.IsManagedSeedAgent(definition);
        }
    }

    private Task ResetAgentAsync()
    {
        ResetEditorState();
        selectedTabIndex = 0;
        return Task.CompletedTask;
    }

    private string ResolveAvatarImageModel(ProviderProfile provider)
        => string.IsNullOrWhiteSpace(editorModel.ImageGenerationAccess.DefaultModel)
            ? provider.DefaultModel.Trim()
            : editorModel.ImageGenerationAccess.DefaultModel.Trim();

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

    private async Task OpenCapabilityWizardAsync(CapabilityKind initialKind)
    {
        if (isBusy || isOpeningCapabilityWizard)
        {
            return;
        }

        isOpeningCapabilityWizard = true;
        try
        {
            var result = await DialogService.OpenAsync<CapabilitySetupWizardDialog>(
                ResolveCapabilityWizardTitle(initialKind),
                new Dictionary<string, object?>
                {
                    [nameof(CapabilitySetupWizardDialog.InitialKind)] = initialKind,
                    [nameof(CapabilitySetupWizardDialog.TagSuggestions)] = AvailableCapabilityTags
                },
                new DialogOptions
                {
                    Eyebrow = "Capability setup",
                    Subtitle = "Create a skill, tool, or MCP capability and assign it to this agent.",
                    Size = ModalSize.Wide,
                    DenseChrome = true,
                    AriaLabel = "Capability setup wizard",
                    TestId = "agents-details-capability-setup-dialog"
                });

            if (result is not CapabilityDetailsDialogResult createdCapability)
            {
                return;
            }

            isBusy = true;
            try
            {
                capabilities = await WorkspaceService.ListCapabilitiesAsync();
                if (!editorModel.SelectedCapabilityIds.Contains(createdCapability.CapabilityId))
                {
                    editorModel.SelectedCapabilityIds = editorModel.SelectedCapabilityIds
                        .Append(createdCapability.CapabilityId)
                        .Distinct()
                        .OrderBy(item => item)
                        .ToList();
                }

                if (editorModel.Id.HasValue)
                {
                    var agentId = await PersistEditorAsync();
                    await ReloadCatalogStateAsync(agentId);
                    await Saved.InvokeAsync(new AgentDetailsDialogResult(agentId, Deleted: false));
                }

                NotificationService.Success(
                    "Capability created",
                    editorModel.Id.HasValue
                        ? "Capability was created and assigned."
                        : "Capability was created and staged for assignment when the new agent is saved.");
            }
            finally
            {
                isBusy = false;
            }
        }
        catch (Exception exception)
        {
            NotificationService.Error("Capability setup failed", exception.Message);
        }
        finally
        {
            isOpeningCapabilityWizard = false;
        }
    }

    private bool MatchesCapabilitySearch(CapabilityCatalogItem capability)
    {
        if (string.IsNullOrWhiteSpace(capabilitySearch))
        {
            return true;
        }

        var search = capabilitySearch.Trim();
        return capability.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
               capability.Key.Contains(search, StringComparison.OrdinalIgnoreCase) ||
               capability.Description.Contains(search, StringComparison.OrdinalIgnoreCase) ||
               capability.EndpointOrPath.Contains(search, StringComparison.OrdinalIgnoreCase) ||
               capability.Tags.Any(tag => tag.Contains(search, StringComparison.OrdinalIgnoreCase));
    }

    private bool MatchesCapabilityAssignmentFilter(CapabilityCatalogItem capability)
    {
        var isAttached = editorModel.SelectedCapabilityIds.Contains(capability.Id);
        return capabilityAssignmentFilter switch
        {
            CapabilityDialogAssignmentFilter.Attached => isAttached,
            CapabilityDialogAssignmentFilter.Available => !isAttached,
            _ => true
        };
    }

    private bool MatchesCapabilityKindFilter(CapabilityCatalogItem capability)
    {
        return capabilityKindFilter switch
        {
            CapabilityDialogKindFilter.Tool => capability.Kind == CapabilityKind.Tool,
            CapabilityDialogKindFilter.Skill => capability.Kind == CapabilityKind.Skill,
            CapabilityDialogKindFilter.Mcp => capability.Kind == CapabilityKind.McpServer,
            _ => true
        };
    }

    private void ResetCapabilityFilters()
    {
        capabilitySearch = string.Empty;
        capabilityAssignmentFilter = CapabilityDialogAssignmentFilter.All;
        capabilityKindFilter = CapabilityDialogKindFilter.All;
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
            ApplyDerivedEditorState();
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
            : $"model '{provider.GetModelDisplayName(model)}'";

        if (!AgentProviderModelParameterPolicy.IsOpenAiLikeProvider(provider.Kind))
        {
            return $"Configured model parameters are sent for {modelLabel}.";
        }

        if (AgentProviderModelParameterPolicy.ShouldOmitTemperature(provider, model))
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
            editorModel.ProjectStructureAccess.AllowedProjectIds = [];
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
            editorModel.ProjectStructureAccess.AllowedProjectIds = [];
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
        next.ExternalTargetRootBindings = current.ExternalTargetRootBindings.ToList();
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

    private void NormalizeWorkspaceToolAccessForSave()
    {
        var normalized = AgentWorkspaceToolAccessMetadata.Normalize(editorModel.WorkspaceToolAccess);
        var externalTargetRegistry = ExternalTargetPathRegistryFactory.Create(
            normalized.ExternalTargetRootBindings);
        var canonicalAliases = normalized.AllowedExternalTargetAliases
            .Select(alias =>
                AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(
                    alias,
                    externalTargetRegistry) ??
                throw new InvalidOperationException(
                    $"External workspace root '{alias}' is not a supported path or alias."))
            .Distinct(ExternalTargetAliasCodec.EqualityComparer)
            .OrderBy(alias => alias, StringComparer.Ordinal)
            .ToList();

        normalized.AllowedExternalTargetAliases = canonicalAliases;
        normalized.ExternalTargetRootBindings = normalized.ExternalTargetRootBindings
            .Concat(externalTargetRegistry.ExportBindings(canonicalAliases))
            .ToList();
        editorModel.WorkspaceToolAccess = AgentWorkspaceToolAccessMetadata.Normalize(normalized);
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
            ExternalTargetRootBindings = source.ExternalTargetRootBindings.ToList(),
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
            editorModel.ProjectStructureAccess.AllowAllProjects = false;
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
        editorModel.ProjectStructureAccess.AllowAllProjects = false;
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

    private Task HandleNameChangedAsync(string? value)
    {
        editorModel.Name = value ?? string.Empty;
        return Task.CompletedTask;
    }

    private Task HandleRoleChangedAsync(string? value)
    {
        editorModel.RoleTitle = value ?? string.Empty;
        return Task.CompletedTask;
    }

    private Task HandleAvatarChangedAsync(string? value)
    {
        editorModel.AvatarImageUrl = value?.Trim() ?? string.Empty;
        return Task.CompletedTask;
    }

    private Task HandleSummaryChangedAsync(string? value)
    {
        editorModel.Summary = value ?? string.Empty;
        return Task.CompletedTask;
    }

    private Task HandleInstructionsChangedAsync(string? value)
    {
        editorModel.Instructions = value ?? string.Empty;
        return Task.CompletedTask;
    }

    private Task HandleRuntimeProviderPresentationChangedAsync(ConversationPresentationKey? key)
        => HandleRuntimeProviderChangedAsync(AgentProviderPresentationMapper.ToProviderId(key));

    private Task HandleRuntimeProviderChangedAsync(Guid? providerId)
    {
        editorModel.ProviderProfileId = providerId;
        editorModel.Model = string.Empty;

        return Task.CompletedTask;
    }

    private Task HandleRuntimeModelChangedAsync(string? model)
    {
        editorModel.Model = string.IsNullOrWhiteSpace(model)
            ? string.Empty
            : model.Trim();
        return Task.CompletedTask;
    }

    private Task HandleThinkingEffortChangedAsync(AgentReasoningEffortLevel? effort)
    {
        editorModel.ThinkingEffortOverride = effort;
        editorModel.IsThinkingEffortOverrideEdited = true;
        return Task.CompletedTask;
    }

    private void ToggleExternalCallApprovalRequirement(object? rawValue)
    {
        editorModel.Permissions = editorModel.Permissions with
        {
            RequiresApprovalForExternalCalls = rawValue is bool value && value
        };
    }

    private async Task HandleAutoApprovalChangedAsync(object? rawValue)
    {
        var shouldEnable = rawValue is bool value && value;
        if (!shouldEnable)
        {
            editorModel.Permissions = editorModel.Permissions with
            {
                AutoApproveExternalCallsByDefault = false
            };
            return;
        }

        if (editorModel.Permissions.AutoApproveExternalCallsByDefault || isConfirmingAutoApproval)
        {
            return;
        }

        isConfirmingAutoApproval = true;
        var confirmed = false;
        try
        {
            confirmed = await DialogService.OpenAsync<AgentAutoApprovalConfirmationDialog>(
                "Enable automatic approval?",
                options: new DialogOptions
                {
                    Eyebrow = "Runtime approval policy",
                    Subtitle = "Confirm that you understand the effect on future agent runs.",
                    Size = ModalSize.Compact,
                    DenseChrome = true,
                    AriaLabel = "Confirm automatic approval for agent tool calls",
                    TestId = "agents-auto-approval-confirmation"
                }) is true;
            if (confirmed)
            {
                editorModel.Permissions = editorModel.Permissions with
                {
                    AutoApproveExternalCallsByDefault = true
                };
            }
        }
        catch (Exception exception)
        {
            NotificationService.Error("Auto-approval confirmation failed", exception.Message);
        }
        finally
        {
            isConfirmingAutoApproval = false;
            if (!confirmed)
            {
                autoApprovalInputVersion++;
            }
        }
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

    private Task HandleImageGenerationProviderPresentationChangedAsync(ConversationPresentationKey? key)
        => HandleImageGenerationProviderChangedAsync(AgentProviderPresentationMapper.ToProviderId(key));

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
        if (editorModel.ImageGenerationAccess.CanGenerateImages &&
            !editorModel.ImageGenerationAccess.PreferredProviderProfileId.HasValue &&
            SelectedImageGenerationProvider is { } provider)
        {
            editorModel.ImageGenerationAccess.PreferredProviderProfileId = provider.Id;
        }

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
        if (access.CanGenerateImages &&
            !access.PreferredProviderProfileId.HasValue &&
            SelectedImageGenerationProvider is { } selectedProvider)
        {
            access.PreferredProviderProfileId = selectedProvider.Id;
        }

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

        return SelectedImageGenerationProvider is { } recommendedProvider
            ? $"Image requests use the recommended provider '{recommendedProvider.Name}'; saving makes that choice explicit."
            : "No enabled image-generation provider is available.";
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

    private void ApplyExternalWorkspaceRootSelection(ExternalWorkspaceRootSelection selection)
    {
        editorModel.WorkspaceToolAccess.AllowedExternalTargetAliases = selection.AllowedAliases.ToList();
        editorModel.WorkspaceToolAccess.ExternalTargetRootBindings = selection.RootBindings.ToList();
        NormalizeWorkspaceToolAccess();
    }

    private void ApplyStorageCatalogSelection(IReadOnlyList<Guid> catalogIds)
    {
        editorModel.WorkspaceToolAccess.AllowedStorageCatalogIds = catalogIds
            .Where(catalogId => catalogId != Guid.Empty)
            .Distinct()
            .OrderBy(catalogId => catalogId)
            .ToList();
        NormalizeWorkspaceToolAccess();
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

    private static string ResolveCapabilityWizardTitle(CapabilityKind kind)
    {
        return kind switch
        {
            CapabilityKind.McpServer => "New MCP server",
            CapabilityKind.Tool => "New tool",
            _ => "New skill"
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

    private void ApplySelectedAgent(AgentDefinition definition)
    {
        var providerKind = definition.ProviderProfileId is { } providerProfileId
            ? providers.FirstOrDefault(provider => provider.Id == providerProfileId)?.Kind
            : null;
        editorModel = AgentEditorModel.FromDefinition(definition, providerKind);
        ApplyDerivedEditorState();
        ResetAvatarGenerationState();
        linkedPartyId = AgentFrameworkCrmHrMetadata.Read(definition.ConfigurationJson)?.PartyId;
    }

    private void ApplyDerivedEditorState()
    {
        tagValues = NormalizeVisibleTags(editorModel.Tags);
    }

    private void ResetEditorState()
    {
        editorModel = new AgentEditorModel();
        tagValues = [];
        ResetAvatarGenerationState();
        linkedPartyId = null;
    }

    private void ResetAvatarGenerationState()
    {
    }

    private enum CapabilityDialogAssignmentFilter
    {
        All,
        Attached,
        Available
    }

    private enum CapabilityDialogKindFilter
    {
        All,
        Tool,
        Skill,
        Mcp
    }
}
