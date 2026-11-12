using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Components;
using CanDoItAll.AgentFramework.Editor.UI;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Editing;
using CanDoItAll.Conversations.Components.Presentation;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components;

public sealed record AgentDetailsDialogResult(Guid? AgentId, bool Deleted);

public partial class AgentDetailsDialog : IAsyncDisposable {
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
    public AgentConfigurationChanges ConfigurationChanges { get; set; } = default!;

    [Inject]
    public IAgentCapabilityCommands CapabilityCommands { get; set; } = default!;

    [Inject]
    public NotificationService NotificationService { get; set; } = default!;

    [Inject]
    public DialogService DialogService { get; set; } = default!;

    [CascadingParameter]
    public DialogReference? DialogReference { get; set; }

    private AgentEditorSession session = new(AgentEditorTarget.Create);
    private AgentConfigurationOpening? configurationOpening;
    private bool hasExternalChanges;
    private long configurationChangeSequence;
    private string? comparisonMessage;
    private bool isComparing;
    private AgentDefinitionComparison? activeComparison;
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
    private Guid? linkedPartyId;
    private AgentEditorLoadState loadState = AgentEditorLoadState.Loading;
    private bool isLoading => loadState == AgentEditorLoadState.Loading;
    private string? coreLoadError;
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
    private long providerSelectionRevision;
    private long editorLoadRevision;
    private int autoApprovalInputVersion;
    private AgentWorkspaceRiskRequest? pendingWorkspaceRisk;
    private bool isConfirmingWorkspaceRisk => pendingWorkspaceRisk is not null;
    private sealed record AgentWorkspaceRiskRequest(AgentEditorOrigin Origin, AgentEditorAccessFlag Kind, long Revision);
    private int workspaceRiskInputVersion;
    private long workspacePermissionRevision;
    private long autoApprovalRevision;

    private ProviderProfile? SelectedRuntimeProvider => editorModel.ProviderProfileId.HasValue
        ? providers.FirstOrDefault(item => item.Id == editorModel.ProviderProfileId.Value)
        : null;

    private async Task RefreshRuntimeProvidersAsync(AgentEditorSession owner, Guid providerId, long selectionRevision) {
        using var request = CancellationTokenSource.CreateLinkedTokenSource(owner.CancellationToken);
        if (!IsCurrent(owner) || providerSelectionRevision != selectionRevision || owner.Draft.ProviderProfileId != providerId) {
            throw new OperationCanceledException(request.Token);
        }
        var refreshedProviders = await ConfigurationFor(owner).ReadAsync(EditorReads.ReadProvidersAsync, request.Token);
        if (!IsCurrent(owner) || providerSelectionRevision != selectionRevision || owner.Draft.ProviderProfileId != providerId) {
            throw new OperationCanceledException(request.Token);
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

    private IReadOnlyList<string> VisibleTagSuggestions => agents
        .Where(agent => !agent.IsTemplate)
        .SelectMany(agent => agent.Tags)
        .Where(tag => !AgentSpecialTags.IsFavorite(tag))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
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
        if (!RendererInfo.IsInteractive) {
            return;
        }
        if (targetApplied && (appliedTargetId == AgentId || session.Target.AgentId == AgentId)) {
            appliedTargetId = AgentId;
            return;
        }
        targetApplied = true;
        appliedTargetId = AgentId;
        await ReplaceSessionAsync(new(AgentId));
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
        var revision = ++editorLoadRevision;
        using var request = CancellationTokenSource.CreateLinkedTokenSource(owner.CancellationToken);
        loadState = AgentEditorLoadState.Loading;
        coreLoadError = null;
        try {
            var opening = await EnsureConfigurationOpeningAsync(owner);
            var loaded = await opening.ReadAsync(token => EditorReads.LoadAsync(owner.Target, InitialProviders, token), request.Token);
            if (!IsCurrent(owner) || revision != editorLoadRevision) {
                return;
            }
            if (loaded.Draft.Id != owner.Target.AgentId) {
                throw new InvalidOperationException("The loaded agent does not match the requested editor target.");
            }
            owner.Load(loaded.Draft, loaded.Agents.FirstOrDefault(agent => agent.Id == loaded.Draft.Id)?.CreatedAtUtc);
            agents = loaded.Agents;
            capabilities = loaded.Capabilities;
            providers = loaded.Providers.Items;
            secrets = loaded.Secrets.Items;
            linkedPartyId = loaded.LinkedPartyId;
            areProvidersLoaded = loaded.Providers.Error is null;
            areSecretsLoaded = loaded.Secrets.Error is null;
            ApplyDerivedEditorState();
            if (loaded.Providers.Error is not null) {
                providerLoadErrorMessage = "Provider references are unavailable. Retry loading the editor.";
                NotificationService.Error("Providers failed to load", providerLoadErrorMessage);
            }
            if (loaded.Secrets.Error is not null) {
                secretsErrorMessage = "Secret references are unavailable. Retry the secret list without changing your draft.";
                NotificationService.Error("Secrets failed to load", secretsErrorMessage);
            }
            loadState = AgentEditorLoadState.Ready;
            await TargetChanged.InvokeAsync(owner.Target);
        } catch (OperationCanceledException) when (request.Token.IsCancellationRequested) {
        } catch (Exception) {
            if (IsCurrent(owner) && revision == editorLoadRevision) {
                loadState = AgentEditorLoadState.Failed;
                coreLoadError = "The requested agent editor could not be loaded. Retry or close the editor.";
                NotificationService.Error("Agent editor failed to load", coreLoadError);
            }
        }
    }

    private Task CloseFailedEditorAsync() => DialogReference?.CloseAsync() ?? Task.CompletedTask;

    private async Task HandleSelectedTabIndexChanged(int index) {
        var section = AgentEditorSections.At(index).Section;
        Section = section;
        await SectionChanged.InvokeAsync(section);
    }

    private bool IsCurrent(AgentEditorSession owner) => !isDisposed && !owner.IsDisposed && ReferenceEquals(session, owner);

    private async Task<AgentConfigurationOpening> EnsureConfigurationOpeningAsync(AgentEditorSession owner) {
        if (configurationOpening is { } current) {
            return current;
        }
        var opening = new AgentConfigurationOpening(ConfigurationChanges, EditingOwner.AgentDefinition,
            owner.Target.AgentId, owner.Origin.Value, () => IsCurrent(owner));
        configurationOpening = opening;
        await opening.StartAsync(batch => InvokeAsync(() => {
            if (!IsCurrent(owner) || (owner.Target.AgentId ?? owner.Draft.Id) is not { } resourceId) {
                return;
            }
            if (batch.RequiresResync || batch.Changes.Any(change => change.OriginEditorId != owner.Origin.Value &&
                change.Resource.ResourceId == resourceId && change.Kind != EditingChangeKind.MetadataChanged)) {
                hasExternalChanges = true;
                configurationChangeSequence++;
                activeComparison?.Invalidate();
                StateHasChanged();
            }
        }), () => InvokeAsync(() => {
            if (!IsCurrent(owner)) {
                return;
            }
            loadState = AgentEditorLoadState.Failed;
            coreLoadError = "Agent editing access changed. Close and reopen this editor with current access.";
            StateHasChanged();
        }), catalog: owner.Target.IsNew);
        return opening;
    }

    private AgentConfigurationOpening ConfigurationFor(AgentEditorSession owner)
        => IsCurrent(owner) && configurationOpening is { IsReady: true } opening && opening.EditorId == owner.Origin.Value
            ? opening
            : throw new UnauthorizedAccessException("The original agent editor access is unavailable.");

    private Task RunFor(AgentEditorSession owner, Func<Task> action)
        => IsCurrent(owner) ? action() : Task.CompletedTask;

    private Task ChangeFor(AgentEditorSession owner, Action action) {
        if (IsCurrent(owner)) {
            action();
        }
        return Task.CompletedTask;
    }

    private AgentEditorCoreState CreateCoreState(AgentEditorSession owner) => new(owner.Origin, owner.Context) {
        LoadState = loadState,
        Section = Section,
        LoadError = coreLoadError,
        CommitWarning = owner.CommitWarning,
        PendingRefreshMessage = owner.PendingRefresh is { } pending
            ? pending.Kind == AgentEditorMutationKind.Save
                ? "The agent was saved, but the editor could not refresh."
                : "The capability was verified, but the editor could not refresh."
            : null,
        HasUnconfirmedWrite = owner.HasUnconfirmedWrite,
        HasExternalChanges = hasExternalChanges,
        ComparisonMessage = comparisonMessage,
        Compare = EventCallback.Factory.Create(this, () => RunFor(owner, CompareAgentAsync)),
        VerificationMessage = owner.Verification?.Message,
        CanReviewVerification = owner.Verification?.NeedsReview == true,
        LinkedPartyId = linkedPartyId,
        IsBusy = isBusy,
        IsMutationBlocked = IsMutationBlocked,
        CanDelete = owner.Draft.Id.HasValue && !IsManagedSeedAgent,
        IsConfirmingDelete = isConfirmingDelete,
        IsConfirmingAutoApproval = isConfirmingAutoApproval,
        AutoApprovalInputVersion = autoApprovalInputVersion,
        HasIncompatibleThinkingEffort = HasIncompatibleThinkingEffortOverride,
        Tags = tagValues,
        PendingTagInput = owner.PendingTagInput,
        TagSuggestions = VisibleTagSuggestions,
        AreProvidersLoaded = areProvidersLoaded,
        ProviderLoadError = providerLoadErrorMessage,
        RuntimeProviders = RuntimeProviderOptions,
        RuntimeProviderKey = SelectedRuntimeProviderKey,
        RuntimeProvider = SelectedRuntimeProvider is { } runtime ? AgentProviderPresentationMapper.Map(runtime) : null,
        ThinkingEffort = AgentThinkingEffortPresentation.Create(SelectedRuntimeProvider, owner.Draft.Model, owner.Draft.ThinkingEffortOverride),
        RuntimeParameterPolicy = SelectedRuntimeProvider is { } provider ? DescribeRuntimeParameterPolicy(provider) : null,
        ImageProviders = ImageGenerationProviderPresentationOptions,
        ImageProviderKey = SelectedImageGenerationProviderKey,
        ImageProvider = SelectedImageGenerationProvider is { } image ? AgentProviderPresentationMapper.Map(image) : null,
        ImageProviderPolicy = DescribeImageGenerationProviderChoice(),
        ImageWarning = ResolveImageGenerationWarning(),
        Changed = EventCallback.Factory.Create<AgentEditorCoreIntent>(this, intent => HandleCoreIntentAsync(owner, intent)),
        InputObserved = EventCallback.Factory.Create<ChangeEventArgs>(this, _ => {
            if (IsCurrent(owner)) {
                owner.NoteInput();
            }
        }),
        SectionChanged = EventCallback.Factory.Create<AgentEditorSection>(this, section => RunFor(owner, () => HandleSelectedTabIndexChanged(AgentEditorSections.IndexOf(section)))),
        Save = EventCallback.Factory.Create<Microsoft.AspNetCore.Components.Forms.EditContext>(this, _ => RunFor(owner, SaveAgentAsync)),
        Clear = EventCallback.Factory.Create(this, () => RunFor(owner, ResetAgentAsync)),
        Delete = EventCallback.Factory.Create(this, () => RunFor(owner, DeleteAgentAsync)),
        RetryLoad = EventCallback.Factory.Create(this, () => RunFor(owner, LoadAsync)),
        RetryRefresh = EventCallback.Factory.Create(this, () => RunFor(owner, RetrySavedRefreshAsync)),
        Close = DialogReference is null ? default : EventCallback.Factory.Create(this, () => RunFor(owner, CloseFailedEditorAsync))
    };

    private Task HandleCoreIntentAsync(AgentEditorSession owner, AgentEditorCoreIntent intent) {
        if (!IsCurrent(owner)) {
            return Task.CompletedTask;
        }
        owner.NoteInput();
        return intent switch {
            AgentEditorCoreIntent.Name value => HandleNameChangedAsync(value.Value),
            AgentEditorCoreIntent.Role value => HandleRoleChangedAsync(value.Value),
            AgentEditorCoreIntent.Summary value => HandleSummaryChangedAsync(value.Value),
            AgentEditorCoreIntent.Instructions value => HandleInstructionsChangedAsync(value.Value),
            AgentEditorCoreIntent.Tags value => HandleTagsChangedAsync(value.Value),
            AgentEditorCoreIntent.PendingTags value => ChangeFor(owner, () => owner.PendingTagInput = value.Value),
            AgentEditorCoreIntent.RuntimeProvider value => HandleRuntimeProviderPresentationChangedAsync(value.Value),
            AgentEditorCoreIntent.RuntimeModel value => HandleRuntimeModelChangedAsync(value.Value),
            AgentEditorCoreIntent.ThinkingEffort value => HandleThinkingEffortChangedAsync(value.Value),
            AgentEditorCoreIntent.ToolUse value => ChangeFor(owner, () => ToggleToolAccess(value.Value)),
            AgentEditorCoreIntent.ExternalCallApproval value => ChangeFor(owner, () => ToggleExternalCallApprovalRequirement(value.Value)),
            AgentEditorCoreIntent.AutoApproval value => HandleAutoApprovalChangedAsync(value.Value),
            AgentEditorCoreIntent.ImageGeneration value => ChangeFor(owner, () => ToggleImageGenerationAccess(value.Value)),
            AgentEditorCoreIntent.ImageAssetStorage value => ChangeFor(owner, () => ToggleImageProjectAssetStorage(value.Value)),
            AgentEditorCoreIntent.ImageProvider value => HandleImageGenerationProviderPresentationChangedAsync(value.Value),
            AgentEditorCoreIntent.ImageModel value => HandleImageGenerationModelChangedAsync(value.Value),
            AgentEditorCoreIntent.Voice value => ChangeFor(owner, () => ToggleVoiceModeAccess(value.Value)),
            _ => throw new ArgumentOutOfRangeException(nameof(intent), intent, "Unknown agent core intent.")
        };
    }

    private AgentEditorAccessState CreateAccessState(AgentEditorSession owner) {
        var state = owner.Access;
        state.Projects = projectStructureProjects;
        state.ProjectsLoaded = areProjectStructureProjectsLoaded;
        state.ProjectsLoading = isLoadingProjectStructureProjects;
        state.ProjectsRequested = projectStructureProjectsRequested;
        state.ProjectsError = projectStructureProjectsErrorMessage;
        state.Secrets = secrets;
        state.SecretsLoaded = areSecretsLoaded;
        state.SecretsLoading = isLoadingSecrets;
        state.SecretsError = secretsErrorMessage;
        state.ConfirmingWorkspaceRisk = isConfirmingWorkspaceRisk;
        state.WorkspaceRiskInputVersion = workspaceRiskInputVersion;
        state.OpeningCapabilityWizard = isOpeningCapabilityWizard;
        state.MutationBlocked = IsMutationBlocked;
        state.Capabilities = capabilities;
        state.Changed = EventCallback.Factory.Create<AgentEditorAccessIntent>(this, intent => HandleAccessIntentAsync(owner, intent));
        return state;
    }

    private async Task HandleAccessIntentAsync(AgentEditorSession owner, AgentEditorAccessIntent intent) {
        if (!IsCurrent(owner)) {
            return;
        }
        switch (intent) {
            case AgentEditorAccessIntent.LoadProjects:
                await RequestProjectStructureProjectsAsync();
                return;
            case AgentEditorAccessIntent.RetrySecrets:
                await LoadSecretsAsync(owner);
                return;
            case AgentEditorAccessIntent.CreateCapability create:
                await OpenCapabilityWizardAsync(create.Kind);
                return;
            case AgentEditorAccessIntent.ReviewCreatedCapability:
                await ReviewCreatedCapabilityAsync(owner);
                return;
            case AgentEditorAccessIntent.AssignCreatedCapability:
                await AssignCreatedCapabilityAsync(owner);
                return;
            case AgentEditorAccessIntent.Flag { Kind: AgentEditorAccessFlag.WorkspaceLocalScripts } scripts:
                workspacePermissionRevision++;
                await HandleWorkspaceLocalScriptsChangedAsync(scripts.Value);
                return;
            case AgentEditorAccessIntent.Flag { Kind: AgentEditorAccessFlag.ScriptsReadEnvironment } environment:
                workspacePermissionRevision++;
                await HandleScriptsReadEnvironmentChangedAsync(environment.Value);
                return;
            case AgentEditorAccessIntent.Profile:
                workspacePermissionRevision++;
                break;
        }
        owner.Access.Apply(intent);
        if (intent is AgentEditorAccessIntent.Flag { Value: true, Kind:
                AgentEditorAccessFlag.ProjectStructureRead or AgentEditorAccessFlag.ProjectCreation or
                AgentEditorAccessFlag.SubprojectCreation or AgentEditorAccessFlag.ProjectStructureNonTaskWrite or
                AgentEditorAccessFlag.ProjectStructureTaskWrite or AgentEditorAccessFlag.ProjectStructureWrite }) {
            await RequestProjectStructureProjectsAsync();
        }
    }

    private async Task LoadSecretsAsync(AgentEditorSession owner) {
        if (isLoadingSecrets) {
            return;
        }
        isLoadingSecrets = true;
        secretsErrorMessage = null;
        try {
            var references = await ConfigurationFor(owner).ReadAsync(EditorReads.ReadSecretsAsync, owner.CancellationToken);
            if (IsCurrent(owner)) {
                secrets = references;
                areSecretsLoaded = true;
            }
        } catch (OperationCanceledException) when (owner.CancellationToken.IsCancellationRequested) {
        } catch (Exception) {
            if (IsCurrent(owner)) {
                secretsErrorMessage = "Secret references could not be loaded. Retry without changing your selections.";
            }
        } finally {
            if (IsCurrent(owner)) {
                isLoadingSecrets = false;
            }
        }
    }

    private async Task ReplaceSessionAsync(AgentEditorTarget target) {
        session.Dispose();
        if (configurationOpening is { } opening) {
            configurationOpening = null;
            await opening.DisposeAsync();
        }
        session = new(target);
        hasExternalChanges = false;
        configurationChangeSequence++;
        comparisonMessage = null;
        isComparing = false;
        activeComparison = null;
        tagValues = [];
        linkedPartyId = null;
        isBusy = false;
        isConfirmingDelete = false;
        isConfirmingAutoApproval = false;
        pendingWorkspaceRisk = null;
        isOpeningCapabilityWizard = false;
        isLoadingProjectStructureProjects = false;
        isLoadingSecrets = false;
        workspacePermissionRevision++;
        autoApprovalRevision++;
        projectStructureProjectsLoadTask = null;
        autoApprovalInputVersion++;
        workspaceRiskInputVersion++;
    }

    public async ValueTask DisposeAsync() {
        if (isDisposed) {
            return;
        }
        isDisposed = true;
        session.Dispose();
        if (configurationOpening is { } opening) {
            await opening.DisposeAsync();
        }
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
        using var request = CancellationTokenSource.CreateLinkedTokenSource(owner.CancellationToken);
        isLoadingProjectStructureProjects = true;
        projectStructureProjectsErrorMessage = null;
        await InvokeAsync(StateHasChanged);
        try {
            var projects = await ConfigurationFor(owner).ReadAsync(EditorReads.ReadProjectsAsync, request.Token);
            if (IsCurrent(owner)) {
                projectStructureProjects = projects;
                areProjectStructureProjectsLoaded = true;
            }
        } catch (Exception) {
            if (IsCurrent(owner)) {
                projectStructureProjectsErrorMessage = "The project list could not be loaded. Retry without changing your selections.";
                NotificationService.Error("Project list failed to load", projectStructureProjectsErrorMessage);
            }
        } finally {
            if (IsCurrent(owner)) {
                isLoadingProjectStructureProjects = false;
                projectStructureProjectsLoadTask = null;
                await InvokeAsync(StateHasChanged);
            }
        }
    }

    private bool IsMutationBlocked => loadState != AgentEditorLoadState.Ready || configurationOpening?.IsReady != true ||
        hasExternalChanges || isComparing || isBusy || !session.CanWrite;

    private Task SaveAgentAsync()
        => SaveCurrentDraftAsync("Agent saved", "Technical agent saved.", "Agent save failed");

    private async Task<bool> SaveCurrentDraftAsync(string successTitle, string successDetail, string failureTitle) {
        if (IsMutationBlocked) {
            return false;
        }
        var owner = session;
        if (string.IsNullOrWhiteSpace(owner.Draft.Name)) {
            NotificationService.Error(failureTitle, "Enter an agent name before saving. Your draft is preserved.");
            return false;
        }
        if (!owner.Context.Validate()) {
            return false;
        }
        using var request = CancellationTokenSource.CreateLinkedTokenSource(owner.CancellationToken);
        isBusy = true;
        try {
            var submission = AgentEditorDraftPolicy.Capture(owner.Draft, tagValues, providers, owner.InputRevision);
            var outcome = await ConfigurationFor(owner).WriteAsync(token => EditorCommands.SaveAsync(submission.Request, token), request.Token);
            if (!IsCurrent(owner)) {
                return false;
            }
            switch (outcome) {
                case AgentEditorSaveOutcome.Rejected rejected:
                    hasExternalChanges |= rejected.IsConflict;
                    NotificationService.Error(rejected.IsConflict ? "Agent changed elsewhere" : failureTitle, rejected.Message);
                    return false;
                case AgentEditorSaveOutcome.Unconfirmed unconfirmed:
                    owner.MarkWriteUnconfirmed();
                    NotificationService.Error("Agent save could not be confirmed", unconfirmed.Message);
                    return false;
                case AgentEditorSaveOutcome.Committed committed:
                    owner.AcknowledgeMutation(committed.AgentId, submission);
                    owner.SetCommitWarning(committed.Warning);
                    if (committed.Accepted is not { } accepted || accepted.Id != committed.AgentId) {
                        NotificationService.Error("Agent saved; accepted configuration unavailable", "The save was acknowledged, but its exact accepted configuration is missing. Keep this draft and reopen with current access before another write.");
                        return false;
                    }
                    var acceptedDraft = AgentEditorModel.FromDefinition(accepted,
                        providers.FirstOrDefault(provider => provider.Id == accepted.ProviderProfileId)?.Kind);
                    ApplyAcceptedEditor(owner, submission, acceptedDraft, accepted.CreatedAtUtc);
                    comparisonMessage = null;
                    try {
                        await TargetChanged.InvokeAsync(owner.Target);
                    } catch (Exception) {
                        if (IsCurrent(owner)) {
                            NotificationService.Error("Agent saved, but the editor target update failed", "The save was acknowledged. Reload the catalog to continue with the saved agent.");
                        }
                    }
                    if (!IsCurrent(owner)) {
                        return false;
                    }
                    return await ReconcileSaveAsync(owner, successTitle, successDetail);
                default:
                    throw new InvalidOperationException("Unknown agent save outcome.");
            }
        } catch (OperationCanceledException) when (request.Token.IsCancellationRequested) {
            return false;
        } catch (Exception) {
            if (IsCurrent(owner)) {
                owner.MarkWriteUnconfirmed();
                NotificationService.Error(failureTitle, "The operation result could not be confirmed. Check the catalog before retrying. Your draft is preserved.");
            }
            return false;
        } finally {
            if (IsCurrent(owner)) {
                isBusy = false;
            }
        }
    }

    private async Task<bool> ReconcileSaveAsync(AgentEditorSession owner, string successTitle, string successDetail) {
        using var request = CancellationTokenSource.CreateLinkedTokenSource(owner.CancellationToken);
        var pending = owner.PendingRefresh ?? throw new InvalidOperationException("There is no acknowledged save to refresh.");
        try {
            var refreshed = await ConfigurationFor(owner).ReadAsync(token => EditorCommands.ReconcileAsync(pending.AgentId, providers, token), request.Token);
            if (!IsCurrent(owner)) {
                return false;
            }
            ApplyCatalogRefresh(owner, refreshed);
            owner.CompleteReconciliation();
        } catch (Exception) {
            if (IsCurrent(owner)) {
                NotificationService.Error(pending.Kind == AgentEditorMutationKind.Save
                    ? "Agent saved, but the editor refresh failed"
                    : "Capability verified, but the editor refresh failed", "The operation was acknowledged, but its current state could not be loaded. Retry the refresh without repeating the operation.");
            }
            return false;
        }
        if (owner.CommitWarning is null) {
            NotificationService.Success(successTitle, owner.InputRevision != pending.Submission.InputRevision
                ? "The submitted configuration was saved. Later typing remains unsaved." : successDetail);
        }
        if (pending.Kind != AgentEditorMutationKind.Save) {
            return true;
        }
        try {
            await Saved.InvokeAsync(new AgentDetailsDialogResult(pending.AgentId, Deleted: false));
        } catch (Exception) {
            if (IsCurrent(owner)) {
                NotificationService.Error("Agent saved, but the catalog refresh failed", "The save was acknowledged. Reload the catalog to see its current state.");
            }
        }
        return true;
    }

    private void ApplyCatalogRefresh(AgentEditorSession owner, AgentEditorCatalogRefresh refreshed) {
        if (refreshed.Draft.Id != owner.Target.AgentId || !refreshed.Draft.ExpectedUpdatedAtUtc.HasValue) {
            throw new InvalidOperationException("The refreshed agent identity or version is missing.");
        }
        agents = refreshed.Agents;
        capabilities = refreshed.Capabilities;
        linkedPartyId = refreshed.LinkedPartyId;
        if (refreshed.Draft.ExpectedUpdatedAtUtc != owner.Original?.ExpectedUpdatedAtUtc) {
            hasExternalChanges = true;
        }
    }

    private void ApplyAcceptedEditor(AgentEditorSession owner, AgentEditorSubmission submission, AgentEditorModel accepted, DateTimeOffset createdAtUtc) {
        if (owner.InputRevision != submission.InputRevision || submission.HasLaterEdits(owner.Draft, tagValues)) {
            owner.Draft.ExpectedUpdatedAtUtc = accepted.ExpectedUpdatedAtUtc;
        } else {
            AgentEditorDraftSnapshot.ApplyTo(accepted, owner.Draft);
            owner.Access.Draft = owner.Draft;
            owner.Memory.UpdateValue(owner.Draft.MemoryAccess);
            owner.Context.MarkAsUnmodified();
            ApplyDerivedEditorState();
        }
        owner.AcceptOriginal(accepted, createdAtUtc);
    }

    private async Task CompareAgentAsync() {
        var owner = session;
        if (isBusy || isComparing || !owner.CanWrite || owner.Target.AgentId is not { } id || owner.Original is not { } original) {
            return;
        }
        var local = AgentEditorDraftPolicy.Copy(owner.Draft);
        local.Tags = AgentEditorDraftPolicy.BuildTags(owner.Draft.Tags, tagValues);
        var sequence = configurationChangeSequence;
        var inputRevision = owner.InputRevision;
        using var request = CancellationTokenSource.CreateLinkedTokenSource(owner.CancellationToken);
        isComparing = true;
        comparisonMessage = null;
        try {
            var opening = ConfigurationFor(owner);
            var refreshed = await opening.ReadAsync(token => EditorCommands.ReconcileAsync(id, providers, token), request.Token);
            var definition = refreshed.Agents.Single(agent => agent.Id == id);
            if (!IsCurrent(owner) || owner.OriginalCreatedAtUtc is null || definition.CreatedAtUtc != owner.OriginalCreatedAtUtc) {
                throw new InvalidOperationException("The original agent lifetime is unavailable or has been replaced.");
            }
            var review = new AgentDefinitionComparison(original, local, refreshed.Draft);
            activeComparison = review;
            if (sequence != configurationChangeSequence) {
                review.Invalidate();
            }
            var apply = await DialogService.OpenAsync<AgentDefinitionComparisonDialog>("Compare agent changes",
                new Dictionary<string, object?> {
                    [nameof(AgentDefinitionComparisonDialog.Review)] = review,
                    [nameof(AgentDefinitionComparisonDialog.ProviderNames)] = providers.ToDictionary(provider => provider.Id, provider => provider.Name),
                    [nameof(AgentDefinitionComparisonDialog.CapabilityNames)] = refreshed.Capabilities.ToDictionary(capability => capability.Id, capability => capability.Name)
                }, new DialogOptions { Size = ModalSize.Wide, DenseChrome = true, TestId = "agents-comparison-dialog" }, request.Token) is true;
            if (!apply || !IsCurrent(owner)) {
                return;
            }
            await opening.AuthorizeAsync(false, request.Token);
            var stillLocal = AgentEditorDraftPolicy.Copy(owner.Draft);
            stillLocal.Tags = AgentEditorDraftPolicy.BuildTags(owner.Draft.Tags, tagValues);
            if (review.IsStale || configurationChangeSequence != sequence || owner.InputRevision != inputRevision || !AgentDefinitionComparison.Same(local, stillLocal)) {
                comparisonMessage = "The saved configuration or your draft changed during comparison. Your draft is retained. Compare again before applying.";
                return;
            }
            AgentEditorDraftSnapshot.ApplyTo(review.Result(), owner.Draft);
            owner.AcceptOriginal(review.Current, definition.CreatedAtUtc);
            owner.Access.Draft = owner.Draft;
            owner.Memory.UpdateValue(owner.Draft.MemoryAccess);
            owner.Context.NotifyValidationStateChanged();
            ApplyDerivedEditorState();
            hasExternalChanges = false;
            comparisonMessage = "Comparison applied to your draft. Review it and Save when ready.";
        } catch (OperationCanceledException) when (request.Token.IsCancellationRequested) {
        } catch (Exception) {
            if (IsCurrent(owner)) {
                comparisonMessage = "The current agent could not be compared, or its original lifetime changed. Your draft is retained; retry the read or close and reopen with current access.";
            }
        } finally {
            if (IsCurrent(owner)) {
                isComparing = false;
                activeComparison = null;
            }
        }
    }

    private async Task RetrySavedRefreshAsync() {
        if (isBusy || session.PendingRefresh is null && session.Verification?.NeedsReview != true) {
            return;
        }
        var owner = session;
        isBusy = true;
        try {
            if (owner.Verification?.NeedsReview == true) {
                await ReconcileVerificationAsync(owner);
                return;
            }
            await ReconcileSaveAsync(owner,
                owner.PendingRefresh!.Kind == AgentEditorMutationKind.Save ? "Agent saved" : "Capability verified",
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
        using var request = CancellationTokenSource.CreateLinkedTokenSource(owner.CancellationToken);
        var deletedAgentId = owner.Draft.Id!.Value;
        var expectedDeleteVersion = owner.Draft.ExpectedUpdatedAtUtc;
        if (expectedDeleteVersion is null) {
            NotificationService.Error("Agent delete unavailable", "Reopen the agent with its original saved version before deleting it.");
            return;
        }
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
                }, request.Token) is true;
        } catch (Exception) {
            if (IsCurrent(owner)) {
                NotificationService.Error("Agent delete confirmation failed", "The confirmation dialog could not be opened. No deletion was requested.");
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
            await ConfigurationFor(owner).WriteAsync(token => EditorCommands.DeleteAsync(deletedAgentId, expectedDeleteVersion.Value, token), request.Token);
        } catch (AgentCatalogConcurrencyException) {
            if (IsCurrent(owner)) {
                hasExternalChanges = true;
                comparisonMessage = "The agent changed before deletion. Nothing was deleted by this request. Compare its current configuration before another action.";
            }
            return;
        } catch (Exception) {
            if (IsCurrent(owner)) {
                NotificationService.Error("Agent delete failed", "The deletion result could not be confirmed. Reload the catalog before retrying.");
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
        } catch (Exception) {
            if (IsCurrent(owner)) {
                NotificationService.Error("Agent deleted, but the catalog refresh failed", "The deletion was acknowledged. Reload the catalog to see its current state.");
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
        if (loadState != AgentEditorLoadState.Ready) {
            return;
        }
        await ReplaceSessionAsync(AgentEditorTarget.Create);
        await EnsureConfigurationOpeningAsync(session);
        projectStructureProjectsRequested = areProjectStructureProjectsLoaded;
        projectStructureProjectsErrorMessage = null;
        loadState = AgentEditorLoadState.Ready;
        Section = AgentEditorSection.Identity;
        await TargetChanged.InvokeAsync(session.Target);
        await SectionChanged.InvokeAsync(Section);
    }

    private string ResolveAvatarImageModel(ProviderProfile provider)
        => string.IsNullOrWhiteSpace(editorModel.ImageGenerationAccess.DefaultModel)
            ? provider.DefaultModel.Trim()
            : editorModel.ImageGenerationAccess.DefaultModel.Trim();

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
        if (IsMutationBlocked || isOpeningCapabilityWizard || session.Access.CreatedCapabilityId.HasValue) {
            return;
        }
        var owner = session;
        using var request = CancellationTokenSource.CreateLinkedTokenSource(owner.CancellationToken);
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
                }, request.Token);
            if (!IsCurrent(owner) || result is not CapabilityDetailsDialogResult capability) {
                return;
            }
            created = true;
            owner.Access.CreatedCapabilityId = capability.CapabilityId;
            owner.Access.CreatedCapabilityNeedsRead = true;
            owner.Access.CreatedCapabilityMessage = "Capability created. Reviewing the catalog before assigning it.";
            await ReviewCreatedCapabilityAsync(owner);
            if (IsCurrent(owner) && !owner.Access.CreatedCapabilityNeedsRead) {
                await AssignCreatedCapabilityAsync(owner);
            }
        } catch (Exception) {
            if (IsCurrent(owner)) {
                NotificationService.Error(created ? "Capability created, but assignment setup failed" : "Capability setup failed", created ? "The capability was created. Reload the capability catalog before assigning it." : "The capability setup could not be completed. Check the capability catalog before retrying.");
            }
        } finally {
            if (IsCurrent(owner)) {
                isBusy = false;
                isOpeningCapabilityWizard = false;
            }
        }
    }

    private async Task ReviewCreatedCapabilityAsync(AgentEditorSession owner) {
        if (!IsCurrent(owner) || owner.Access.CreatedCapabilityId is not { } createdId || isBusy) {
            return;
        }
        isBusy = true;
        try {
            var items = await ConfigurationFor(owner).ReadAsync(EditorReads.ReadCapabilitiesAsync, owner.CancellationToken);
            if (!IsCurrent(owner) || owner.Access.CreatedCapabilityId != createdId) {
                return;
            }
            capabilities = items;
            owner.Access.CreatedCapabilityNeedsRead = !items.Any(item => item.Id == createdId);
            owner.Access.CreatedCapabilityMessage = owner.Access.CreatedCapabilityNeedsRead
                ? "The created capability is not visible yet. Review the catalog again; creation will not be repeated."
                : "Capability created. Assign it with the current whole-agent draft.";
        } catch (OperationCanceledException) when (owner.CancellationToken.IsCancellationRequested) {
        } catch (Exception) {
            if (IsCurrent(owner)) {
                owner.Access.CreatedCapabilityNeedsRead = true;
                owner.Access.CreatedCapabilityMessage = "Capability created, but the catalog read failed. Retry this read before assignment.";
            }
        } finally {
            if (IsCurrent(owner)) {
                isBusy = false;
            }
        }
    }

    private async Task AssignCreatedCapabilityAsync(AgentEditorSession owner) {
        if (!IsCurrent(owner) || IsMutationBlocked || owner.Access.CreatedCapabilityNeedsRead ||
            owner.Access.CreatedCapabilityId is not { } createdId) {
            return;
        }
        owner.Draft.SelectedCapabilityIds = owner.Draft.SelectedCapabilityIds.Append(createdId).Distinct().OrderBy(id => id).ToList();
        if (!owner.Draft.Id.HasValue) {
            owner.Access.CreatedCapabilityMessage = "Capability created and staged. Save the new agent to persist its assignment.";
            owner.Access.CreatedCapabilityId = null;
            return;
        }
        var completed = await SaveCurrentDraftAsync("Capability created", "Capability created and assigned.", "Capability created, but assignment failed");
        if (!IsCurrent(owner)) {
            return;
        }
        if (completed) {
            owner.Access.CreatedCapabilityId = null;
            owner.Access.CreatedCapabilityMessage = null;
        } else {
            owner.Access.CreatedCapabilityMessage = "Capability created. Its assignment needs attention; creation will not be repeated.";
        }
    }

    private async Task VerifyCapabilityAsync(Guid capabilityId) {
        if (!editorModel.Id.HasValue || IsMutationBlocked) {
            return;
        }
        var owner = session;
        var agentId = owner.Draft.Id!.Value;
        var expectedVersion = owner.Draft.ExpectedUpdatedAtUtc;
        using var request = CancellationTokenSource.CreateLinkedTokenSource(owner.CancellationToken);
        isBusy = true;
        try {
            var outcome = await ConfigurationFor(owner).WriteAsync(token => CapabilityCommands.DiagnoseAsync(agentId, capabilityId, token), request.Token);
            if (!IsCurrent(owner)) {
                return;
            }
            owner.Verification = new(agentId, capabilityId, expectedVersion, outcome);
            if (owner.Verification.NeedsReview) {
                await ReconcileVerificationAsync(owner);
            }
        } catch (Exception) {
            if (IsCurrent(owner)) {
                owner.Verification = new(agentId, capabilityId, expectedVersion, new(CapabilityVerificationDisposition.Unconfirmed));
                NotificationService.Error("Capability verification failed", "The verification result could not be confirmed. Refresh the capability evidence before retrying.");
            }
        } finally {
            if (IsCurrent(owner)) {
                isBusy = false;
            }
        }
    }

    private async Task ReconcileVerificationAsync(AgentEditorSession owner) {
        var verification = owner.Verification ?? throw new InvalidOperationException("There is no diagnostic to review.");
        using var request = CancellationTokenSource.CreateLinkedTokenSource(owner.CancellationToken);
        try {
            var refreshed = await ConfigurationFor(owner).ReadAsync(token => EditorCommands.ReconcileAsync(owner.Target.AgentId!.Value, providers, token), request.Token);
            if (!IsCurrent(owner) || !ReferenceEquals(owner.Verification, verification)) {
                return;
            }
            var accepted = verification.Reconcile(owner.Draft, refreshed);
            agents = refreshed.Agents;
            capabilities = refreshed.Capabilities;
            if (accepted) {
                NotificationService.Success("Capability verified", "Proof updated. Unsaved editor changes are retained.");
            }
        } catch (Exception) {
            if (IsCurrent(owner) && ReferenceEquals(owner.Verification, verification)) {
                NotificationService.Error("Capability proof review failed", "Retry the read-back without repeating the diagnostic. Your draft is retained.");
            }
        }
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

    private async Task HandleWorkspaceLocalScriptsChangedAsync(object? rawValue)
    {
        if (rawValue is not true)
        {
            editorModel.WorkspaceToolAccess.Profile = AgentWorkspaceToolProfileKind.Custom;
            editorModel.WorkspaceToolAccess.CanRunLocalScripts = false;
            editorModel.WorkspaceToolAccess = AgentWorkspaceToolAccessMetadata.Normalize(editorModel.WorkspaceToolAccess);
            return;
        }

        if (editorModel.WorkspaceToolAccess.CanRunLocalScripts)
        {
            return;
        }

        if (await ConfirmWorkspaceRiskAsync(
                AgentEditorAccessFlag.WorkspaceLocalScripts,
                "Allow this agent to run local scripts?",
                "Enable local scripts?",
                "The agent can run PowerShell and Python scripts in its workspace with your account's rights. Scripts can change files, start programs and reach the network. Each run still needs approval unless auto-approval is on, and scripts are inspected before they run.",
                "I understand that scripts run with my account's rights on this machine.",
                "Enable local scripts",
                "agents-workspace-scripts-confirmation"))
        {
            editorModel.WorkspaceToolAccess.Profile = AgentWorkspaceToolProfileKind.Custom;
            editorModel.WorkspaceToolAccess.CanRunLocalScripts = true;
            editorModel.WorkspaceToolAccess = AgentWorkspaceToolAccessMetadata.Normalize(editorModel.WorkspaceToolAccess);
        }
    }

    private async Task HandleScriptsReadEnvironmentChangedAsync(object? rawValue)
    {
        if (rawValue is not true)
        {
            editorModel.WorkspaceToolAccess.CanScriptsReadEnvironment = false;
            editorModel.WorkspaceToolAccess = AgentWorkspaceToolAccessMetadata.Normalize(editorModel.WorkspaceToolAccess);
            return;
        }

        if (editorModel.WorkspaceToolAccess.CanScriptsReadEnvironment || !editorModel.WorkspaceToolAccess.CanRunLocalScripts)
        {
            return;
        }

        if (await ConfirmWorkspaceRiskAsync(
                AgentEditorAccessFlag.ScriptsReadEnvironment,
                "Allow this agent's scripts to read environment variables?",
                "Allow environment access?",
                "Scripts will be able to list all environment variables they receive and read any of them, including proxy settings and extra variables the operator passes to processes (proxy URLs can contain credentials). Host secrets such as API keys are still never passed to processes.",
                "I understand that the agent's scripts can read every variable passed to them.",
                "Allow environment access",
                "agents-workspace-environment-confirmation"))
        {
            editorModel.WorkspaceToolAccess.CanScriptsReadEnvironment = true;
            editorModel.WorkspaceToolAccess = AgentWorkspaceToolAccessMetadata.Normalize(editorModel.WorkspaceToolAccess);
        }
    }

    private async Task<bool> ConfirmWorkspaceRiskAsync(
        AgentEditorAccessFlag kind,
        string question,
        string title,
        string warning,
        string acknowledgement,
        string confirmText,
        string testId)
    {
        if (isConfirmingWorkspaceRisk)
        {
            return false;
        }

        var owner = session;
        var decision = new AgentWorkspaceRiskRequest(owner.Origin, kind, workspacePermissionRevision);
        using var request = CancellationTokenSource.CreateLinkedTokenSource(owner.CancellationToken);
        pendingWorkspaceRisk = decision;
        var confirmed = false;
        try
        {
            confirmed = await DialogService.OpenAsync<AgentWorkspaceRiskConfirmationDialog>(
                title,
                new Dictionary<string, object?>
                {
                    [nameof(AgentWorkspaceRiskConfirmationDialog.Question)] = question,
                    [nameof(AgentWorkspaceRiskConfirmationDialog.Warning)] = warning,
                    [nameof(AgentWorkspaceRiskConfirmationDialog.Acknowledgement)] = acknowledgement,
                    [nameof(AgentWorkspaceRiskConfirmationDialog.ConfirmText)] = confirmText,
                    [nameof(AgentWorkspaceRiskConfirmationDialog.TestIdPrefix)] = testId
                },
                new DialogOptions
                {
                    Eyebrow = "Workspace tools",
                    Subtitle = "Confirm that you understand what this permission allows.",
                    Size = ModalSize.Compact,
                    DenseChrome = true,
                    AriaLabel = title,
                    TestId = testId
                },
                cancellationToken: request.Token) is true;
            confirmed = confirmed && IsCurrent(owner) && decision.Revision == workspacePermissionRevision && pendingWorkspaceRisk == decision;
            return confirmed;
        }
        catch (Exception)
        {
            if (IsCurrent(owner))
            {
                NotificationService.Error("Confirmation failed", "The confirmation could not be completed. The permission was not enabled.");
            }

            return false;
        }
        finally
        {
            if (IsCurrent(owner))
            {
                pendingWorkspaceRisk = null;
                if (!confirmed)
                {
                    workspaceRiskInputVersion++;
                }
            }
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
        owner.NoteInput();
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
        providerSelectionRevision++;
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

    private void ToggleToolAccess(object? rawValue) {
        editorModel.Permissions = editorModel.Permissions with {
            CanUseTools = rawValue is bool value && value
        };
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
        var revision = ++autoApprovalRevision;
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
        using var request = CancellationTokenSource.CreateLinkedTokenSource(owner.CancellationToken);
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
                }, cancellationToken: request.Token) is true;
            confirmed = confirmed && IsCurrent(owner) && revision == autoApprovalRevision;
            if (confirmed)
            {
                editorModel.Permissions = editorModel.Permissions with
                {
                    AutoApproveExternalCallsByDefault = true
                };
            }
        }
        catch (Exception)
        {
            if (IsCurrent(owner)) {
                NotificationService.Error("Auto-approval confirmation failed", "The confirmation could not be completed. Auto-approval was not enabled.");
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
        editorModel.WorkspaceToolAccess = AgentWorkspaceToolAccessMetadata.Normalize(editorModel.WorkspaceToolAccess);
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
        editorModel.WorkspaceToolAccess = AgentWorkspaceToolAccessMetadata.Normalize(editorModel.WorkspaceToolAccess);
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

    private static string ResolveCapabilityWizardTitle(CapabilityKind kind)
    {
        return kind switch
        {
            CapabilityKind.McpServer => "New MCP server",
            CapabilityKind.Tool => "New tool",
            _ => "New skill"
        };
    }

    private void ApplyDerivedEditorState()
    {
        tagValues = NormalizeVisibleTags(editorModel.Tags);
    }

}
