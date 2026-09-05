using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Components;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Conversations.Components.Presentation;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components;

public sealed record AgentDetailsDialogResult(Guid? AgentId, bool Deleted);

public partial class AgentDetailsDialog : IDisposable
{
    [Parameter]
    public Guid? AgentId { get; set; }

    [Parameter]
    public IReadOnlyList<ProviderProfile>? InitialProviders { get; set; }

    [Parameter]
    public EventCallback<AgentDetailsDialogResult> Saved { get; set; }

    [Parameter]
    public AgentEditorSection Section { get; set; } = AgentEditorSection.Identity;

    [Parameter]
    public EventCallback<AgentEditorSection> SectionChanged { get; set; }

    [Parameter]
    public EventCallback<AgentEditorTarget> TargetChanged { get; set; }

    public AgentEditorTarget CurrentTarget => session.Target;

    [Inject]
    public IAgentEditorCommands EditorCommands { get; set; } = default!;

    [Inject]
    public IAgentEditorReads EditorReads { get; set; } = default!;

    [Inject]
    public NotificationService NotificationService { get; set; } = default!;

    [Inject]
    public DialogService DialogService { get; set; } = default!;

    [CascadingParameter]
    public DialogReference? DialogReference { get; set; }

    private AgentEditorSession session = new(AgentEditorTarget.Create);
    private AgentEditorModel editorModel => session.Draft;
    private bool targetApplied;
    private Guid? appliedTargetId;
    private bool isDisposed;
    private IReadOnlyList<AgentDefinition> agents = [];
    private IReadOnlyList<ProviderProfile> providers = [];
    private IReadOnlyList<CapabilityCatalogItem> capabilities = [];
    private IReadOnlyList<AgentEditorProject> projectStructureProjects = [];
    private IReadOnlyList<AgentEditorSecret> secrets = [];
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
    private bool isLoadingSecrets => isLoading;
    private string? providerLoadErrorMessage;
    private string? projectStructureProjectsErrorMessage;
    private string? secretsErrorMessage;
    private Task? projectStructureProjectsLoadTask;
    private int selectedTabIndex => (int)Section;
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

    private async Task RefreshRuntimeProvidersAsync(AgentEditorSession owner) {
        if (!IsCurrent(owner)) {
            return;
        }
        var refreshedProviders = await EditorReads.ReadProvidersAsync(owner.CancellationToken);
        if (!IsCurrent(owner)) {
            return;
        }
        providers = refreshedProviders;
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

    protected override async Task OnParametersSetAsync() {
        if (!Enum.IsDefined(Section)) {
            throw new ArgumentOutOfRangeException(nameof(Section), Section, "Unknown agent editor section.");
        }
        if (targetApplied && (appliedTargetId == AgentId || session.Target.AgentId == AgentId)) {
            appliedTargetId = AgentId;
            return;
        }
        targetApplied = true;
        appliedTargetId = AgentId;
        ReplaceSession(new(AgentId));
        providers = [];
        capabilities = [];
        agents = [];
        secrets = [];
        projectStructureProjects = [];
        areProvidersLoaded = false;
        areSecretsLoaded = false;
        areProjectStructureProjectsLoaded = false;
        projectStructureProjectsRequested = false;
        providerLoadErrorMessage = null;
        secretsErrorMessage = null;
        projectStructureProjectsErrorMessage = null;
        await LoadAsync();
    }

    private async Task LoadAsync() {
        var owner = session;
        isLoading = true;
        try {
            var loaded = await EditorReads.LoadAsync(owner.Target, InitialProviders, owner.CancellationToken);
            if (!IsCurrent(owner)) {
                return;
            }
            owner.Load(loaded.Draft);
            agents = loaded.Agents;
            capabilities = loaded.Capabilities;
            providers = loaded.Providers.Items;
            secrets = loaded.Secrets.Items;
            linkedPartyId = loaded.LinkedPartyId;
            areProvidersLoaded = loaded.Providers.Error is null;
            areSecretsLoaded = loaded.Secrets.Error is null;
            ApplyDerivedEditorState();
            if (loaded.Providers.Error is { } providerError) {
                providerLoadErrorMessage = $"Failed to load providers. {providerError}";
                NotificationService.Error("Providers failed to load", providerError);
            }
            if (loaded.Secrets.Error is { } secretError) {
                secretsErrorMessage = $"Failed to load secrets. {secretError}";
                NotificationService.Error("Secrets failed to load", secretError);
            }
            await TargetChanged.InvokeAsync(owner.Target);
        } catch (Exception exception) {
            if (IsCurrent(owner)) {
                NotificationService.Error("Agent editor failed to load", exception.Message);
            }
        } finally {
            if (IsCurrent(owner)) {
                isLoading = false;
            }
        }
    }

    private async Task HandleSelectedTabIndexChanged(int index) {
        var section = (AgentEditorSection)index;
        if (!Enum.IsDefined(section)) {
            throw new ArgumentOutOfRangeException(nameof(index), index, "Unknown agent editor section.");
        }
        Section = section;
        await SectionChanged.InvokeAsync(section);
    }

    private bool IsCurrent(AgentEditorSession owner) => !isDisposed && ReferenceEquals(session, owner);

    private void ReplaceSession(AgentEditorTarget target) {
        session.Dispose();
        session = new(target);
        tagValues = [];
        linkedPartyId = null;
        isBusy = false;
        isConfirmingDelete = false;
        isConfirmingAutoApproval = false;
        isOpeningCapabilityWizard = false;
        isLoadingProjectStructureProjects = false;
        projectStructureProjectsLoadTask = null;
        autoApprovalInputVersion++;
    }

    public void Dispose() {
        if (isDisposed) {
            return;
        }
        isDisposed = true;
        session.Dispose();
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

        var pendingLoad = LoadProjectStructureProjectsAsync();
        projectStructureProjectsLoadTask = pendingLoad.IsCompleted ? null : pendingLoad;
        return pendingLoad;
    }

    private async Task LoadProjectStructureProjectsAsync() {
        var owner = session;
        isLoadingProjectStructureProjects = true;
        projectStructureProjectsErrorMessage = null;
        await InvokeAsync(StateHasChanged);
        try {
            var projects = await EditorReads.ReadProjectsAsync(owner.CancellationToken);
            if (IsCurrent(owner)) {
                projectStructureProjects = projects;
                areProjectStructureProjectsLoaded = true;
            }
        } catch (Exception exception) {
            if (IsCurrent(owner)) {
                projectStructureProjectsErrorMessage = $"Failed to load projects. {exception.Message}";
                NotificationService.Error("Project list failed to load", exception.Message);
            }
        } finally {
            if (IsCurrent(owner)) {
                isLoadingProjectStructureProjects = false;
                projectStructureProjectsLoadTask = null;
                await InvokeAsync(StateHasChanged);
            }
        }
    }

    private bool IsMutationBlocked => isBusy || !session.CanWrite;

    private Task SaveAgentAsync()
        => SaveCurrentDraftAsync("Agent saved", "Technical agent saved.", "Agent save failed");

    private async Task<bool> SaveCurrentDraftAsync(string successTitle, string successDetail, string failureTitle) {
        if (IsMutationBlocked) {
            return false;
        }
        var owner = session;
        isBusy = true;
        try {
            var submission = AgentEditorDraftPolicy.Capture(owner.Draft, tagValues, providers);
            var outcome = await EditorCommands.SaveAsync(submission.Request, owner.CancellationToken);
            if (!IsCurrent(owner)) {
                return false;
            }
            switch (outcome) {
                case AgentEditorSaveOutcome.Rejected rejected:
                    NotificationService.Error(rejected.IsConflict ? "Agent changed elsewhere" : failureTitle, rejected.Message);
                    return false;
                case AgentEditorSaveOutcome.Unconfirmed unconfirmed:
                    owner.MarkWriteUnconfirmed();
                    NotificationService.Error("Agent save could not be confirmed", unconfirmed.Message);
                    return false;
                case AgentEditorSaveOutcome.Committed committed:
                    owner.AcknowledgeMutation(committed.AgentId, submission);
                    try {
                        await TargetChanged.InvokeAsync(owner.Target);
                    } catch (Exception exception) {
                        if (IsCurrent(owner)) {
                            NotificationService.Error("Agent saved, but the editor target update failed", exception.Message);
                        }
                    }
                    if (!IsCurrent(owner)) {
                        return false;
                    }
                    return await ReconcileSaveAsync(owner, successTitle, successDetail);
                default:
                    throw new InvalidOperationException("Unknown agent save outcome.");
            }
        } catch (Exception exception) {
            if (IsCurrent(owner)) {
                NotificationService.Error(failureTitle, exception.Message);
            }
            return false;
        } finally {
            if (IsCurrent(owner)) {
                isBusy = false;
            }
        }
    }

    private async Task<bool> ReconcileSaveAsync(AgentEditorSession owner, string successTitle, string successDetail) {
        var pending = owner.PendingRefresh ?? throw new InvalidOperationException("There is no acknowledged save to refresh.");
        try {
            var refreshed = await EditorCommands.ReconcileAsync(pending.AgentId, providers, owner.CancellationToken);
            if (!IsCurrent(owner)) {
                return false;
            }
            ApplyReconciledEditor(owner, pending.Submission, refreshed);
            owner.CompleteReconciliation();
        } catch (Exception exception) {
            if (IsCurrent(owner)) {
                NotificationService.Error(pending.Kind == AgentEditorMutationKind.Save
                    ? "Agent saved, but the editor refresh failed"
                    : "Capability verified, but the editor refresh failed", exception.Message);
            }
            return false;
        }
        NotificationService.Success(successTitle, successDetail);
        if (pending.Kind != AgentEditorMutationKind.Save) {
            return true;
        }
        try {
            await Saved.InvokeAsync(new AgentDetailsDialogResult(pending.AgentId, Deleted: false));
        } catch (Exception exception) {
            if (IsCurrent(owner)) {
                NotificationService.Error("Agent saved, but the catalog refresh failed", exception.Message);
            }
        }
        return true;
    }

    private void ApplyReconciledEditor(AgentEditorSession owner, AgentEditorSubmission submission,
        AgentEditorCatalogRefresh refreshed) {
        if (refreshed.Draft.Id != owner.Target.AgentId || !refreshed.Draft.ExpectedUpdatedAtUtc.HasValue) {
            throw new InvalidOperationException("The refreshed agent identity or version is missing.");
        }
        agents = refreshed.Agents;
        capabilities = refreshed.Capabilities;
        linkedPartyId = refreshed.LinkedPartyId;
        if (submission.HasLaterEdits(owner.Draft, tagValues)) {
            owner.Draft.ExpectedUpdatedAtUtc = refreshed.Draft.ExpectedUpdatedAtUtc;
        } else {
            owner.Load(refreshed.Draft);
            ApplyDerivedEditorState();
        }
    }

    private async Task RetrySavedRefreshAsync() {
        if (isBusy || session.PendingRefresh is null) {
            return;
        }
        var owner = session;
        isBusy = true;
        try {
            await ReconcileSaveAsync(owner,
                owner.PendingRefresh.Kind == AgentEditorMutationKind.Save ? "Agent saved" : "Capability verified",
                owner.PendingRefresh.Kind == AgentEditorMutationKind.Save ? "Technical agent saved." : "Capability verification completed.");
        } finally {
            if (IsCurrent(owner)) {
                isBusy = false;
            }
        }
    }

    private async Task DeleteAgentAsync() {
        if (!editorModel.Id.HasValue || IsMutationBlocked || isConfirmingDelete || IsManagedSeedAgent) {
            return;
        }
        var owner = session;
        var deletedAgentId = owner.Draft.Id!.Value;
        var deletedAgentName = string.IsNullOrWhiteSpace(owner.Draft.Name) ? "Unnamed agent" : owner.Draft.Name.Trim();
        var confirmed = false;
        try {
            isConfirmingDelete = true;
            confirmed = await DialogService.OpenAsync<AgentDeleteConfirmationDialog>(
                "Delete agent?",
                new Dictionary<string, object?> {
                    [nameof(AgentDeleteConfirmationDialog.AgentName)] = deletedAgentName
                },
                new DialogOptions {
                    Eyebrow = "Danger action",
                    Subtitle = "This action cannot be undone.",
                    Size = ModalSize.Compact,
                    DenseChrome = true,
                    AriaLabel = $"Confirm deletion of agent {deletedAgentName}",
                    TestId = "agents-catalog-delete-confirmation"
                }) is true;
        } catch (Exception exception) {
            if (IsCurrent(owner)) {
                NotificationService.Error("Agent delete confirmation failed", exception.Message);
            }
        } finally {
            if (IsCurrent(owner)) {
                isConfirmingDelete = false;
            }
        }
        if (!IsCurrent(owner) || !confirmed || IsMutationBlocked) {
            return;
        }
        isBusy = true;
        try {
            await EditorCommands.DeleteAsync(deletedAgentId, owner.CancellationToken);
        } catch (Exception exception) {
            if (IsCurrent(owner)) {
                NotificationService.Error("Agent delete failed", exception.Message);
            }
            return;
        } finally {
            if (IsCurrent(owner)) {
                isBusy = false;
            }
        }
        if (!IsCurrent(owner)) {
            return;
        }
        NotificationService.Success("Agent deleted", $"Technical agent '{deletedAgentName}' deleted.");
        var result = new AgentDetailsDialogResult(deletedAgentId, Deleted: true);
        try {
            if (DialogReference is not null) {
                await DialogReference.CloseAsync(result);
            } else {
                await Saved.InvokeAsync(result);
            }
        } catch (Exception exception) {
            if (IsCurrent(owner)) {
                NotificationService.Error("Agent deleted, but the catalog refresh failed", exception.Message);
            }
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

    private async Task ResetAgentAsync() {
        ReplaceSession(AgentEditorTarget.Create);
        projectStructureProjectsRequested = areProjectStructureProjectsLoaded;
        projectStructureProjectsErrorMessage = null;
        isLoading = false;
        Section = AgentEditorSection.Identity;
        await TargetChanged.InvokeAsync(session.Target);
        await SectionChanged.InvokeAsync(Section);
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

    private async Task ToggleCapabilityAsync(Guid capabilityId) {
        if (IsMutationBlocked) {
            return;
        }
        var selected = editorModel.SelectedCapabilityIds.ToList();
        if (!selected.Remove(capabilityId)) {
            selected.Add(capabilityId);
        }
        editorModel.SelectedCapabilityIds = selected.Distinct().OrderBy(id => id).ToList();
        if (!editorModel.Id.HasValue) {
            NotificationService.Info("Capability staged", "Save the new agent to persist capability assignments.");
            return;
        }
        await SaveCurrentDraftAsync("Capability assignment updated",
            "Agent capability assignment saved.", "Capability assignment failed");
    }

    private async Task OpenCapabilityWizardAsync(CapabilityKind initialKind) {
        if (IsMutationBlocked || isOpeningCapabilityWizard) {
            return;
        }
        var owner = session;
        isOpeningCapabilityWizard = true;
        var created = false;
        try {
            var result = await DialogService.OpenAsync<CapabilitySetupWizardDialog>(
                ResolveCapabilityWizardTitle(initialKind),
                new Dictionary<string, object?> {
                    [nameof(CapabilitySetupWizardDialog.InitialKind)] = initialKind,
                    [nameof(CapabilitySetupWizardDialog.TagSuggestions)] = AvailableCapabilityTags
                },
                new DialogOptions {
                    Eyebrow = "Capability setup",
                    Subtitle = "Create a skill, tool, or MCP capability and assign it to this agent.",
                    Size = ModalSize.Wide,
                    DenseChrome = true,
                    AriaLabel = "Capability setup wizard",
                    TestId = "agents-details-capability-setup-dialog"
                });
            if (!IsCurrent(owner) || result is not CapabilityDetailsDialogResult capability) {
                return;
            }
            created = true;
            isBusy = true;
            var refreshedCapabilities = await EditorCommands.ReadCapabilitiesAsync(owner.CancellationToken);
            if (!IsCurrent(owner)) {
                return;
            }
            capabilities = refreshedCapabilities;
            owner.Draft.SelectedCapabilityIds = owner.Draft.SelectedCapabilityIds
                .Append(capability.CapabilityId).Distinct().OrderBy(id => id).ToList();
            isBusy = false;
            if (owner.Draft.Id.HasValue) {
                var reconciled = await SaveCurrentDraftAsync("Capability created",
                    "Capability was created and assigned.", "Capability was created, but assignment failed");
                if (!reconciled && IsCurrent(owner)) {
                    NotificationService.Info("Capability created", owner.PendingRefresh is not null
                        ? "Agent assignment was saved; the editor refresh still needs attention."
                        : owner.HasUnconfirmedWrite
                            ? "The agent assignment could not be confirmed."
                            : "The agent assignment was not saved.");
                }
            } else {
                NotificationService.Success("Capability created",
                    "Capability was created and staged for assignment when the new agent is saved.");
            }
        } catch (Exception exception) {
            if (IsCurrent(owner)) {
                NotificationService.Error(created ? "Capability created, but assignment setup failed" : "Capability setup failed", exception.Message);
            }
        } finally {
            if (IsCurrent(owner)) {
                isBusy = false;
                isOpeningCapabilityWizard = false;
            }
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

    private async Task VerifyCapabilityAsync(Guid capabilityId) {
        if (!editorModel.Id.HasValue || IsMutationBlocked) {
            return;
        }
        var owner = session;
        isBusy = true;
        try {
            var submission = AgentEditorDraftPolicy.Capture(owner.Draft, tagValues, providers);
            await EditorCommands.VerifyCapabilityAsync(owner.Draft.Id!.Value, capabilityId, owner.CancellationToken);
            if (!IsCurrent(owner)) {
                return;
            }
            owner.AcknowledgeMutation(owner.Draft.Id.Value, submission, AgentEditorMutationKind.CapabilityVerification);
            await ReconcileSaveAsync(owner, "Capability verified", "Capability verification completed.");
        } catch (Exception exception) {
            if (IsCurrent(owner)) {
                NotificationService.Error("Capability verification failed", exception.Message);
            }
        } finally {
            if (IsCurrent(owner)) {
                isBusy = false;
            }
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

    private void ToggleAllowedSecret(AgentEditorSecret secret, object? rawValue)
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
            ? AgentEditorDraftPolicy.CopyWorkspaceAccess(current)
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

    private Task HandleAvatarChangedAsync(AgentEditorSession owner, string? value)
    {
        if (!IsCurrent(owner)) {
            return Task.CompletedTask;
        }
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

        var owner = session;
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
            if (confirmed && IsCurrent(owner))
            {
                editorModel.Permissions = editorModel.Permissions with
                {
                    AutoApproveExternalCallsByDefault = true
                };
            }
        }
        catch (Exception exception)
        {
            if (IsCurrent(owner)) {
                NotificationService.Error("Auto-approval confirmation failed", exception.Message);
            }
        }
        finally
        {
            if (IsCurrent(owner)) {
                isConfirmingAutoApproval = false;
                if (!confirmed) {
                    autoApprovalInputVersion++;
                }
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

    private void ApplyExternalWorkspaceRootSelection(AgentEditorSession owner, ExternalWorkspaceRootSelection selection)
    {
        if (!IsCurrent(owner)) {
            return;
        }
        editorModel.WorkspaceToolAccess.AllowedExternalTargetAliases = selection.AllowedAliases.ToList();
        editorModel.WorkspaceToolAccess.ExternalTargetRootBindings = selection.RootBindings.ToList();
        NormalizeWorkspaceToolAccess();
    }

    private void ApplyStorageCatalogSelection(AgentEditorSession owner, IReadOnlyList<Guid> catalogIds)
    {
        if (!IsCurrent(owner)) {
            return;
        }
        editorModel.WorkspaceToolAccess.AllowedStorageCatalogIds = catalogIds
            .Where(catalogId => catalogId != Guid.Empty)
            .Distinct()
            .OrderBy(catalogId => catalogId)
            .ToList();
        NormalizeWorkspaceToolAccess();
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

    private void ApplyDerivedEditorState()
    {
        tagValues = NormalizeVisibleTags(editorModel.Tags);
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
