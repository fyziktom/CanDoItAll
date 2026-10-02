using System.Text.Json;
using CanDoItAll.AgentFramework.Editor.UI;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AppComponents;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Conversations.Components.Presentation;
using CanDoItAll.Modules.AgentFramework;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace CanDoItAll.AgentFramework.Editor.UiSandbox;

public sealed class AgentEditorScenarioSession(object receiver, AgentEditorScenario scenario, string name) : IDisposable {
    public static readonly ConversationPresentationKey LocalKey = new("scenario-local");
    public static readonly ConversationPresentationKey SharedKey = new("scenario-shared");
    private const string DefaultChoice = "provider-default";
    private static readonly ConversationProviderOption Local = new(LocalKey, "Fixture reasoning provider", true,
        "reasoning-model", ["reasoning-model", "custom-deployment"]);
    private static readonly ConversationProviderOption Shared = new(SharedKey, "Fixture shared catalog", true,
        "shared-model", ["shared-model"]) {
            AllowsModelOverride = false,
            ModelDisplayNames = new Dictionary<string, string> { ["shared-model"] = "Shared reasoning model" }
        };
    private readonly AgentEditorOrigin origin = new(Guid.NewGuid());
    private readonly EditContext context = new(new AgentEditorModel {
        Name = name, RoleTitle = "Technical reviewer", Summary = "Žluťoučký 東京 — synthetic editor data",
        Instructions = "Review only the supplied fixture. Do not call external services.",
        Tags = ["Fixture", "Favorite"], Status = AgentLifecycleStatus.Active,
        Model = scenario == AgentEditorScenario.UnavailableModel ? "removed-shared-model" : string.Empty,
        ThinkingEffortOverride = scenario == AgentEditorScenario.UnknownEffort ? AgentReasoningEffortLevel.High : null
    });
    private readonly IReadOnlyList<ConversationProviderOption> providers = scenario == AgentEditorScenario.LargeCatalog
        ? Enumerable.Range(0, 150).Select(index => new ConversationProviderOption(new($"fixture-{index}"),
            $"Fixture provider {index:000} · 東京", index % 5 != 0, "model", ["model", "other-model"]))
            .Prepend(Local).Append(Shared).ToArray()
        : [Local, Shared];
    private AgentEditorSection section;
    private AgentEditorLoadState load = scenario switch {
        AgentEditorScenario.Loading => AgentEditorLoadState.Loading,
        AgentEditorScenario.LoadFailure => AgentEditorLoadState.Failed,
        _ => AgentEditorLoadState.Ready
    };
    private ConversationPresentationKey? providerKey = scenario == AgentEditorScenario.UnavailableModel ? SharedKey : LocalKey;
    private ConversationPresentationKey? imageKey;
    private TaskCompletionSource? held;
    private bool retired;
    private bool busy;
    private bool pendingRefresh;
    private bool unconfirmed;
    private int autoApprovalInputVersion;
    private string? warning;
    public AgentEditorModel Draft => (AgentEditorModel)context.Model;
    public int Writes { get; private set; }
    public int Reads { get; private set; }
    public string? SubmittedJson { get; private set; }
    public string? SubmittedName { get; private set; }
    public string Notice { get; private set; } = "Synthetic state. No backend or external model is registered.";
    public bool IsHeld => held is not null;
    public bool IsConfirmingApproval { get; private set; }
    public bool IsClosed { get; private set; }

    public AgentEditorCoreState State => new(origin, context) {
        LoadState = load, Section = section, LoadError = "Injected scenario load failure.",
        CommitWarning = warning, HasUnconfirmedWrite = unconfirmed,
        VerificationMessage = verificationPending ? Notice : null, CanReviewVerification = verificationPending,
        PendingRefreshMessage = pendingRefresh ? "The synthetic write committed; read-back failed." : null,
        IsBusy = busy, IsMutationBlocked = busy || pendingRefresh || unconfirmed || verificationPending,
        IsConfirmingAutoApproval = IsConfirmingApproval,
        AutoApprovalInputVersion = autoApprovalInputVersion,
        CanDelete = Draft.Id.HasValue,
        Delete = EventCallback.Factory.Create(receiver, () => ConfirmDeleteRequested?.Invoke() ?? Task.CompletedTask),
        Tags = Draft.Tags.Where(tag => tag != "Favorite").ToArray(), TagSuggestions = ["Fixture", "Review", "東京"],
        AreProvidersLoaded = scenario != AgentEditorScenario.ProviderFailure,
        ProviderLoadError = scenario == AgentEditorScenario.ProviderFailure ? "Injected provider catalog failure." : null,
        RuntimeProviders = scenario == AgentEditorScenario.ProviderFailure ? [] : providers,
        RuntimeProviderKey = providerKey, RuntimeProvider = providers.SingleOrDefault(item => item.Key == providerKey),
        ThinkingEffort = EffortPresentation(),
        HasIncompatibleThinkingEffort = scenario == AgentEditorScenario.UnknownEffort && Draft.ThinkingEffortOverride is not null,
        RuntimeParameterPolicy = "Presented fixture metadata. Production compatibility policy remains with the original owner.",
        ImageProviders = [Local], ImageProviderKey = imageKey, ImageProvider = imageKey is null ? null : Local,
        ImageProviderPolicy = "A missing selection asks the production owner to choose its recommended image provider on save.",
        Changed = EventCallback.Factory.Create<AgentEditorCoreIntent>(receiver, Change),
        SectionChanged = EventCallback.Factory.Create<AgentEditorSection>(receiver, SelectSection),
        Save = EventCallback.Factory.Create<EditContext>(receiver, SaveAsync),
        RetryLoad = EventCallback.Factory.Create(receiver, RetryLoad),
        RetryRefresh = EventCallback.Factory.Create(receiver, RetryRefresh),
        Close = EventCallback.Factory.Create(receiver, Close)
    };

    private void SelectSection(AgentEditorSection value) {
        if (!retired) {
            section = value;
        }
    }

    private ConversationThinkingEffortPresentation<AgentReasoningEffortLevel?> EffortPresentation() {
        // Fixed scenario metadata; compatibility decisions are supplied by the production policy, never this host.
        ConversationThinkingEffortOption<AgentReasoningEffortLevel?>[] options = scenario == AgentEditorScenario.UnknownEffort
            ? [new(DefaultChoice, null, "Provider default"), new("High", AgentReasoningEffortLevel.High, "High (unavailable)")]
            : [new(DefaultChoice, null, "Provider default"), new("None", AgentReasoningEffortLevel.None, "None"),
                new("Low", AgentReasoningEffortLevel.Low, "Low"), new("High", AgentReasoningEffortLevel.High, "High")];
        return new(options, Draft.ThinkingEffortOverride?.ToString() ?? DefaultChoice,
            scenario == AgentEditorScenario.UnknownEffort && Draft.ThinkingEffortOverride is null,
            scenario == AgentEditorScenario.UnknownEffort ? AlertStyle.Warning : AlertStyle.Info,
            scenario == AgentEditorScenario.UnknownEffort
                ? "Scenario: capability is unknown. The saved override remains visible until explicitly cleared."
                : "Scenario: None is explicit; Provider default inherits the supplied provider setting.");
    }

    private async Task Change(AgentEditorCoreIntent intent) {
        if (retired) {
            return;
        }
        switch (intent) {
            case AgentEditorCoreIntent.Name value:
                Draft.Name = value.Value ?? string.Empty;
                break;
            case AgentEditorCoreIntent.Role value:
                Draft.RoleTitle = value.Value ?? string.Empty;
                break;
            case AgentEditorCoreIntent.Summary value:
                Draft.Summary = value.Value ?? string.Empty;
                break;
            case AgentEditorCoreIntent.Instructions value:
                Draft.Instructions = value.Value ?? string.Empty;
                break;
            case AgentEditorCoreIntent.Tags value:
                Draft.Tags = value.Value.Concat(["Favorite"]).Distinct().ToList();
                break;
            case AgentEditorCoreIntent.RuntimeProvider value:
                providerKey = value.Value;
                Draft.Model = string.Empty;
                break;
            case AgentEditorCoreIntent.RuntimeModel value:
                Draft.Model = value.Value ?? string.Empty;
                break;
            case AgentEditorCoreIntent.ThinkingEffort value:
                Draft.ThinkingEffortOverride = value.Value;
                Draft.IsThinkingEffortOverrideEdited = true;
                break;
            case AgentEditorCoreIntent.ToolUse value:
                Draft.Permissions = Draft.Permissions with { CanUseTools = value.Value };
                break;
            case AgentEditorCoreIntent.ExternalCallApproval value:
                Draft.Permissions = Draft.Permissions with { RequiresApprovalForExternalCalls = value.Value };
                break;
            case AgentEditorCoreIntent.AutoApproval value:
                var revision = ++approvalRevision;
                IsConfirmingApproval = value.Value;
                if (!value.Value) {
                    Draft.Permissions = Draft.Permissions with { AutoApproveExternalCallsByDefault = false };
                }
                if (value.Value && ConfirmApprovalRequested is not null) {
                    var accepted = await ConfirmApprovalRequested();
                    if (!retired && revision == approvalRevision) {
                        ConfirmApproval(accepted);
                    }
                }
                break;
            case AgentEditorCoreIntent.ImageGeneration value:
                Draft.ImageGenerationAccess.CanGenerateImages = value.Value;
                break;
            case AgentEditorCoreIntent.ImageAssetStorage value:
                Draft.ImageGenerationAccess.CanStoreImagesAsProjectAssets = value.Value;
                break;
            case AgentEditorCoreIntent.ImageProvider value:
                imageKey = value.Value;
                Draft.ImageGenerationAccess.DefaultModel = string.Empty;
                break;
            case AgentEditorCoreIntent.ImageModel value:
                Draft.ImageGenerationAccess.DefaultModel = value.Value ?? string.Empty;
                break;
            case AgentEditorCoreIntent.Voice value:
                Draft.VoiceAccess.CanUseVoiceMode = value.Value;
                break;
            default: throw new ArgumentOutOfRangeException(nameof(intent));
        }
    }

    private async Task SaveAsync(EditContext submittedContext) {
        if (retired || busy || pendingRefresh || unconfirmed || verificationPending || !ReferenceEquals(context, submittedContext)) {
            return;
        }
        if (string.IsNullOrWhiteSpace(Draft.Name)) {
            Notice = "Known validation refusal: name is required. Draft retained.";
            return;
        }
        busy = true;
        var submission = AgentEditorDraftSnapshot.Copy(Draft);
        SubmittedJson = JsonSerializer.Serialize(submission);
        SubmittedName = Draft.Name;
        Writes++;
        try {
            if (held is { } pending) {
                await pending.Task;
            }
            if (retired) {
                return;
            }
            if (scenario is not (AgentEditorScenario.SaveRefusal or AgentEditorScenario.UnknownResult)) {
                committed = AgentEditorDraftSnapshot.Copy(submission);
                committed.Id ??= Guid.NewGuid();
                committed.ExpectedUpdatedAtUtc = DateTimeOffset.UtcNow;
                Draft.Id = committed.Id;
                Draft.ExpectedUpdatedAtUtc = committed.ExpectedUpdatedAtUtc;
                Reads++;
                CommittedName = ReadCommitted()!.Name;
            }
            switch (scenario) {
                case AgentEditorScenario.SaveRefusal:
                    Notice = "Known save refusal. Draft retained; no synthetic commit.";
                    break;
                case AgentEditorScenario.CommitWarning:
                    warning = "Synthetic commit confirmed; secondary projection failed.";
                    break;
                case AgentEditorScenario.RefreshFailure:
                    pendingRefresh = true;
                    break;
                case AgentEditorScenario.UnknownResult:
                    unconfirmed = true;
                    break;
                default:
                    Notice = "Synthetic commit confirmed. Captured submission retained.";
                    break;
            }
        } finally {
            busy = false;
        }
    }

    public void Hold() => held ??= new(TaskCreationOptions.RunContinuationsAsynchronously);
    public void Release() {
        var pending = held;
        held = null;
        pending?.TrySetResult();
    }
    public void ConfirmApproval(bool accepted) {
        if (!retired && IsConfirmingApproval && accepted) {
            Draft.Permissions = Draft.Permissions with { AutoApproveExternalCallsByDefault = true };
        }
        IsConfirmingApproval = false;
        autoApprovalInputVersion++;
    }
    private void RetryLoad() {
        if (!retired) {
            Reads++;
            load = AgentEditorLoadState.Ready;
        }
    }
    private void RetryRefresh() {
        if (!retired && verificationPending) {
            ReviewVerification();
            return;
        }
        if (!retired && pendingRefresh) {
            Reads++;
            pendingRefresh = false;
            Notice = "Synthetic read-back complete. No additional write.";
        }
    }
    public void Close() {
        IsClosed = true;
        Dispose();
    }
    public void Dispose() {
        if (retired) {
            return;
        }
        retired = true;
        lifetime.Cancel();
        lifetime.Dispose();
        Release();
        ReleaseVerification();
    }
    private readonly CancellationTokenSource lifetime = new();
    private AgentEditorAccessState? access;
    private AgentMemoryEditorState? memory;
    private AgentEditorModel? committed;
    private long riskRevision;
    private long approvalRevision;
    private int rootSequence;
    private TaskCompletionSource? heldVerification;
    private bool verificationPending;
    private bool verificationUnknown;
    private Guid? verifiedCapabilityId;
    public int Diagnostics { get; private set; }
    public bool IsVerificationHeld => heldVerification is not null;
    private readonly Dictionary<string, string> rootNames = new(StringComparer.Ordinal);
    public string? CommittedName { get; private set; }
    public AgentEditorModel? ReadCommitted() => committed is null ? null : AgentEditorDraftSnapshot.Copy(committed);
    public CancellationToken Token => lifetime.Token;
    public string EntryFormId => $"agent-entry-{origin.Value:N}";
    public AgentRootEntry RootEntry { get; } = new();
    public Func<AgentEditorAccessFlag, Task<bool>>? ConfirmRiskRequested { get; set; }
    public Func<Task>? ConfirmDeleteRequested { get; set; }
    public Func<Task<bool>>? ConfirmApprovalRequested { get; set; }
    public AgentEditorAccessState Access {
        get {
            access ??= CreateAccess();
            access.MutationBlocked = busy || pendingRefresh || unconfirmed || verificationPending;
            return access;
        }
    }
    public AgentMemoryEditorState Memory => memory ??= new(Draft.MemoryAccess) {
        ProvidersLoaded = scenario != AgentEditorScenario.MemoryReadFailure,
        ProviderLoadError = scenario == AgentEditorScenario.MemoryReadFailure ? "Injected memory metadata failure. Bindings are retained." : string.Empty,
        AvailableProviders = scenario is AgentEditorScenario.MemoryUnavailable or AgentEditorScenario.MemoryReadFailure ? [] : MemoryProviders
    };
    private static readonly AgentMemoryProviderOption[] MemoryProviders = [new(MemoryProviderInstanceId.Parse("team-fixture"), "Team memory · 東京"),
        new(MemoryProviderInstanceId.Parse("project-fixture"), "Project memory")];
    public void RetryMemory() {
        if (!retired) {
            Reads++;
            Memory.AvailableProviders = MemoryProviders;
            Memory.ProvidersLoaded = true;
            Memory.ProviderLoadError = string.Empty;
        }
    }
    public IReadOnlyList<SelectedReferenceItem<string>> Roots => Draft.WorkspaceToolAccess.AllowedExternalTargetAliases
        .Select(alias => new SelectedReferenceItem<string>(alias, rootNames.GetValueOrDefault(alias, "Path unavailable in fixture"), alias) {
            CanRemove = true, StatusText = rootNames.ContainsKey(alias) ? "Fixture" : "Unresolved"
        }).ToArray();

    private AgentEditorAccessState CreateAccess() {
        var count = scenario == AgentEditorScenario.LargeCatalog ? 90 : 3;
        var state = new AgentEditorAccessState(origin, Draft) {
            SecretsLoaded = true,
            Secrets = Enumerable.Range(1, count).Select(index => new AgentEditorSecret(new Guid(index, 2, 0, new byte[8]), $"Secret reference {index} · 東京", "Token")).ToArray(),
            Capabilities = Enumerable.Range(1, count).Select(index => new CapabilityCatalogItem(new Guid(index, 3, 0, new byte[8]),
                (index % 3) switch { 0 => CapabilityKind.Tool, 1 => CapabilityKind.Skill, _ => CapabilityKind.McpServer },
                $"fixture-{index}", $"Fixture capability {index} · 東京", "Synthetic catalog metadata", "fixture:local", "{}",
                CapabilityProofStatus.NotRun, string.Empty, null, false)).ToArray(),
            Changed = EventCallback.Factory.Create<AgentEditorAccessIntent>(receiver, ChangeAccessAsync)
        };
        if (scenario == AgentEditorScenario.ReferenceFailure) {
            state.SecretsLoaded = false;
            state.SecretsError = "Injected secret metadata failure. Saved references are retained.";
            Draft.AllowedSecretReferences.Add(new(Guid.NewGuid(), "Unavailable secret", "fixture-purpose"));
            Draft.ProjectStructureAccess.AllowedProjectIds.Add(Guid.NewGuid());
            Draft.WorkspaceToolAccess.AllowedStorageCatalogIds.Add(Guid.NewGuid());
        }
        return state;
    }

    private async Task ChangeAccessAsync(AgentEditorAccessIntent intent) {
        if (retired) {
            return;
        }
        var state = Access;
        switch (intent) {
            case AgentEditorAccessIntent.LoadProjects:
                Reads++;
                if (scenario == AgentEditorScenario.ProjectReadFailure && !state.ProjectsRequested) {
                    state.ProjectsRequested = true;
                    state.ProjectsError = "Injected project metadata failure. Saved selections are retained.";
                    return;
                }
                state.ProjectsRequested = true;
                state.ProjectsLoaded = true;
                state.Projects = Enumerable.Range(1, scenario == AgentEditorScenario.LargeCatalog ? 100 : 3)
                    .Select(index => new AgentEditorProject(new Guid(index, 1, 0, new byte[8]), $"Project {index} · Žluťoučký 東京")).ToArray();
                state.ProjectsError = null;
                return;
            case AgentEditorAccessIntent.RetrySecrets:
                Reads++;
                state.SecretsLoaded = true;
                state.SecretsError = null;
                return;
            case AgentEditorAccessIntent.CreateCapability create:
                var capability = new CapabilityCatalogItem(Guid.NewGuid(), create.Kind, "created-fixture", "Created fixture capability", "Created only in memory", "fixture:local", "{}", CapabilityProofStatus.NotRun, string.Empty, null, false);
                state.Capabilities = state.Capabilities.Append(capability).ToArray();
                state.CreatedCapabilityId = capability.Id;
                state.CreatedCapabilityMessage = "Fixture capability created. Assign to this draft.";
                return;
            case AgentEditorAccessIntent.ReviewCreatedCapability:
                Reads++;
                state.CreatedCapabilityNeedsRead = false;
                return;
            case AgentEditorAccessIntent.AssignCreatedCapability:
                if (state.CreatedCapabilityId is { } createdId) {
                    await AssignCapabilityAsync(createdId);
                    state.CreatedCapabilityId = null;
                    state.CreatedCapabilityMessage = null;
                }
                return;
            case AgentEditorAccessIntent.Flag { Kind: AgentEditorAccessFlag.WorkspaceLocalScripts or AgentEditorAccessFlag.ScriptsReadEnvironment } risk:
                var revision = ++riskRevision;
                var enabled = risk.Value && ConfirmRiskRequested is not null && await ConfirmRiskRequested(risk.Kind);
                if (!retired && revision == riskRevision) {
                    if (risk.Kind == AgentEditorAccessFlag.WorkspaceLocalScripts) {
                        Draft.WorkspaceToolAccess.CanRunLocalScripts = enabled;
                    } else {
                        Draft.WorkspaceToolAccess.CanScriptsReadEnvironment = enabled;
                    }
                    Draft.WorkspaceToolAccess.Profile = AgentWorkspaceToolProfileKind.Custom;
                    Draft.WorkspaceToolAccess = AgentWorkspaceToolAccessMetadata.Normalize(Draft.WorkspaceToolAccess);
                    state.WorkspaceRiskInputVersion++;
                }
                return;
            case AgentEditorAccessIntent.Profile:
                riskRevision++;
                break;
        }
        state.Apply(intent);
    }

    public void ApplyStorage(IReadOnlyList<Guid> ids) {
        if (!retired) {
            Draft.WorkspaceToolAccess.AllowedStorageCatalogIds = ids.ToList();
        }
    }
    public void ChangeRootCandidate(string value) {
        if (!retired) {
            RootEntry.Candidate = value;
            RootEntry.ValidationMessage = null;
        }
    }
    public void AddRoot() {
        if (retired) {
            return;
        }
        var candidate = RootEntry.Candidate.Trim();
        if (!Path.IsPathFullyQualified(candidate)) {
            RootEntry.ValidationMessage = "Enter a native absolute path for this fixture.";
            return;
        }
        if (rootNames.Values.Contains(candidate, StringComparer.OrdinalIgnoreCase)) {
            RootEntry.ValidationMessage = "This fixture root is already selected.";
            return;
        }
        var alias = $"external-target/v1/{++rootSequence:x24}";
        rootNames.Add(alias, candidate);
        Draft.WorkspaceToolAccess.AllowedExternalTargetAliases.Add(alias);
        RootEntry.Candidate = string.Empty;
        RootEntry.ValidationMessage = null;
    }
    public void RemoveRoot(string alias) {
        if (!retired) {
            Draft.WorkspaceToolAccess.AllowedExternalTargetAliases.Remove(alias);
            rootNames.Remove(alias);
        }
    }
    public async Task AssignCapabilityAsync(Guid id) {
        if (retired || busy || pendingRefresh || unconfirmed || verificationPending) {
            return;
        }
        if (!Draft.SelectedCapabilityIds.Remove(id)) {
            Draft.SelectedCapabilityIds.Add(id);
        }
        if (Draft.Id.HasValue && context.Validate()) {
            await SaveAsync(context);
        }
    }
    public async Task VerifyCapabilityAsync(Guid id) {
        if (retired || busy || pendingRefresh || unconfirmed || verificationPending || committed is null || !Draft.SelectedCapabilityIds.Contains(id)) {
            return;
        }
        busy = true;
        Diagnostics++;
        try {
            if (heldVerification is { } pending) {
                await pending.Task;
            }
            if (retired) {
                return;
            }
            committed.ExpectedUpdatedAtUtc = DateTimeOffset.UtcNow;
            verifiedCapabilityId = id;
            verificationPending = true;
            verificationUnknown = scenario == AgentEditorScenario.VerificationUnknown;
            if (scenario == AgentEditorScenario.VerificationReadFailure) {
                Notice = "Fixture proof committed; read-back failed. Retry the read without repeating the diagnostic.";
                return;
            }
            ReviewVerification();
        } finally {
            busy = false;
        }
    }
    private void ReviewVerification() {
        Reads++;
        if (verificationUnknown) {
            Notice = "Fixture publication remains unconfirmed without a receipt. The unsaved draft is retained and writes stay blocked.";
            return;
        }
        Draft.ExpectedUpdatedAtUtc = committed!.ExpectedUpdatedAtUtc;
        Access.Capabilities = Access.Capabilities.Select(item => item.Id == verifiedCapabilityId ? item with { ProofStatus = CapabilityProofStatus.Verified } : item).ToArray();
        verificationPending = false;
        Notice = "Fixture diagnostic published. Unsaved configuration and edit context retained.";
    }
    public void HoldVerification() => heldVerification ??= new(TaskCreationOptions.RunContinuationsAsynchronously);
    public void ReleaseVerification() {
        var pending = heldVerification;
        heldVerification = null;
        pending?.TrySetResult();
    }
    public void DeleteCommitted(bool confirmed) {
        if (!retired && confirmed) {
            committed = null;
            Writes++;
            Close();
        }
    }
}
