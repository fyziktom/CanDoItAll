using System.Text.Json;
using CanDoItAll.AgentFramework.Editor.UI;
using CanDoItAll.AgentFramework.Models;
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
        PendingRefreshMessage = pendingRefresh ? "The synthetic write committed; read-back failed." : null,
        IsBusy = busy, IsMutationBlocked = busy || pendingRefresh || unconfirmed,
        IsConfirmingAutoApproval = IsConfirmingApproval,
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

    private void Change(AgentEditorCoreIntent intent) {
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
                IsConfirmingApproval = value.Value;
                if (!value.Value) {
                    Draft.Permissions = Draft.Permissions with { AutoApproveExternalCallsByDefault = false };
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
        if (retired || busy || pendingRefresh || unconfirmed || !ReferenceEquals(context, submittedContext)) {
            return;
        }
        if (string.IsNullOrWhiteSpace(Draft.Name)) {
            Notice = "Known validation refusal: name is required. Draft retained.";
            return;
        }
        busy = true;
        SubmittedJson = JsonSerializer.Serialize(Draft);
        SubmittedName = Draft.Name;
        Writes++;
        try {
            if (held is { } pending) {
                await pending.Task;
            }
            if (retired) {
                return;
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
    }
    private void RetryLoad() {
        if (!retired) {
            Reads++;
            load = AgentEditorLoadState.Ready;
        }
    }
    private void RetryRefresh() {
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
        retired = true;
        Release();
    }
}
