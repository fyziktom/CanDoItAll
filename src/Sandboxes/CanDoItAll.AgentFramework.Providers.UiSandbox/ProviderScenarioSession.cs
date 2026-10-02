using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Providers.UI;
using CanDoItAll.Modules.AgentFramework;
using Microsoft.AspNetCore.Components.Forms;

namespace CanDoItAll.AgentFramework.Providers.UiSandbox;

public enum ProviderScenario { Realistic, Loading, Empty, Large, Missing, PartialSecrets, ReadOnly, Warning, Unknown, Held }

public sealed class ProviderScenarioSession : IProviderProfilesView {
    private readonly Dictionary<Guid, ProviderProfileEditorModel> saved = [];
    private TaskCompletionSource? held;
    public ProviderScenario Scenario { get; }
    public ProviderProfilesState State { get; private set; } = new();
    public ProviderEditorDraft Editor { get; private set; } = ProviderEditorDraft.For(new(new ProviderProfileEditorModel()));
    public long Activation { get; private set; }
    public IReadOnlyList<ProviderProfile> Providers => saved.Values.Select(Project).ToArray();
    public IReadOnlyList<ProviderSecretChoice> Secrets { get; } = [new("secret:11111111-1111-4111-8111-111111111111", "Sandbox credential reference")];
    public ProviderProfile? SelectedProvider => Providers.SingleOrDefault(provider => provider.Id == State.ProviderId);
    public ProviderProfilesLoadState CatalogLoadState { get; private set; } = ProviderProfilesLoadState.Ready;
    public bool CanEdit => CatalogLoadState == ProviderProfilesLoadState.Ready && Error is null;
    public bool SourceManaged => Scenario == ProviderScenario.ReadOnly && State.ProviderId.HasValue;
    public bool IsBusy { get; private set; }
    public bool WritesBlocked => HasPendingReconciliation || IsWriteUnconfirmed;
    public bool HasPendingReconciliation { get; private set; }
    public bool IsWriteUnconfirmed { get; private set; }
    public bool HasVerifiedRetry { get; private set; }
    public string? Error { get; private set; }
    public string? MetadataWarning { get; private set; }
    public string? SecretMetadataError => Scenario == ProviderScenario.PartialSecrets ? "Fixture metadata read unavailable" : null;
    public string Outcome { get; private set; } = "Local fixture; no network or persistent storage.";
    public int SaveCount { get; private set; }
    public int HealthCount { get; private set; }
    public int DiscoveryCount { get; private set; }
    public event Action? Changed;

    public ProviderScenarioSession(ProviderScenario scenario = ProviderScenario.Realistic) {
        Scenario = scenario;
        if (scenario != ProviderScenario.Empty) {
            for (var index = 1; index <= (scenario == ProviderScenario.Large ? 80 : 3); index++) {
                var id = new Guid(index, 0, 0, new byte[8]);
                saved.Add(id, new() {
                    Id = id, Name = $"Provider {index:00}", Kind = ProviderKind.Ollama,
                    BaseUrl = "http://127.0.0.1:11434", DefaultModel = "qwen3:8b",
                    Transport = ProviderTransportKind.ChatCompletions, IsPrivateProvider = true,
                    SuggestedModels = ["qwen3:8b", "unpriced-model", .. Enumerable.Range(1, 24).Select(number => $"model-{number}")],
                    ConfigurationJson = "{\"timeoutSeconds\":90,\"customExtension\":{\"retained\":true}}",
                    Notes = "Fixture notes", Tags = [index % 2 == 0 ? "cloud" : "local", "chat"],
                    ModelPrices = [new() { Model = "qwen3:8b", InputPerMillionTokensUsd = 1.25m, OutputPerMillionTokensUsd = 3.5m,
                        CachedInputPerMillionTokensUsd = 0.2m, CacheWritePerMillionTokensUsd = 1.5m,
                        ImageInputPerMillionTokensUsd = 2m, CachedImageInputPerMillionTokensUsd = 0.4m,
                        LongContextThresholdTokens = 128000, LongContextInputPerMillionTokensUsd = 2.5m,
                        LongContextCachedInputPerMillionTokensUsd = 0.5m, LongContextCacheWritePerMillionTokensUsd = 3m,
                        LongContextOutputPerMillionTokensUsd = 7m }]
                });
            }
            Acquire(saved.Keys.First());
        }
        if (scenario == ProviderScenario.Loading) {
            CatalogLoadState = ProviderProfilesLoadState.Loading;
        }
        if (scenario == ProviderScenario.Missing) {
            Error = "The selected fixture provider is missing. Retry reads the catalog again.";
        }
        MetadataWarning = scenario == ProviderScenario.Warning ? "Saved fixture change; reference refresh is unavailable." : null;
        HasPendingReconciliation = scenario == ProviderScenario.Warning;
        IsWriteUnconfirmed = scenario == ProviderScenario.Unknown;
    }

    public void SelectSection(ProviderEditorSection section) => State = State with { Section = section };
    public void SetSharedConnectionsOpen(bool open) => State = State with { SharedConnectionsOpen = open };
    public Task RefreshAsync() {
        CatalogLoadState = ProviderProfilesLoadState.Ready;
        Error = null;
        Changed?.Invoke();
        return Task.CompletedTask;
    }
    public Task SelectAsync(Guid providerId) {
        if (State.ProviderId != providerId || !CanEdit) {
            Acquire(providerId);
        }
        return Task.CompletedTask;
    }
    public Task NewAsync() {
        Acquire(null);
        return Task.CompletedTask;
    }
    public void Release() => held?.TrySetResult();
    public ProviderProfileEditorModel ReadSaved(Guid id) => ProviderEditorDraft.Copy(saved[id]);

    public async Task ExecuteAsync(ProviderEditorIntent intent) {
        if (!Current(intent.Target) || IsBusy || !CanEdit) {
            return;
        }
        if (intent.Action == ProviderEditorAction.Reconcile) {
            HasPendingReconciliation = false;
            MetadataWarning = null;
            Outcome = "Fixture read-back reconciled; write was not replayed.";
            Changed?.Invoke();
            return;
        }
        if (intent.Action == ProviderEditorAction.Verify) {
            IsWriteUnconfirmed = false;
            HasVerifiedRetry = true;
            Outcome = "Fixture verifier reports absence. No write was replayed.";
            Changed?.Invoke();
            return;
        }
        if (WritesBlocked || SourceManaged) {
            return;
        }
        var editor = Editor;
        var captured = ProviderEditorDraft.Copy(editor.Model);
        if (intent.Action is ProviderEditorAction.Save or ProviderEditorAction.RetryVerified && !editor.Validate()) {
            Outcome = "Fix the retained invalid fields before saving.";
            return;
        }
        IsBusy = true;
        Changed?.Invoke();
        try {
            if (Scenario == ProviderScenario.Held) {
                held = new(TaskCreationOptions.RunContinuationsAsynchronously);
                await held.Task;
            }
            switch (intent.Action) {
                case ProviderEditorAction.Save:
                case ProviderEditorAction.RetryVerified:
                    captured.Id ??= Guid.NewGuid();
                    saved[captured.Id.Value] = captured;
                    SaveCount++;
                    if (Current(intent.Target)) {
                        editor.Model.Id = captured.Id;
                        State = State with { ProviderId = captured.Id };
                        HasVerifiedRetry = false;
                        Outcome = "Captured fixture draft saved; later typing remains local.";
                    }
                    break;
                case ProviderEditorAction.Delete when captured.Id is { } id:
                    saved.Remove(id);
                    if (Current(intent.Target)) {
                        Acquire(null);
                        Outcome = "Fixture provider deleted.";
                    }
                    break;
                case ProviderEditorAction.Health when captured.Id is { } id:
                    _ = saved[id];
                    HealthCount++;
                    if (Current(intent.Target)) {
                        Outcome = "Checked the saved fixture provider; unsaved inputs were not saved.";
                    }
                    break;
                case ProviderEditorAction.DiscoverModels:
                    DiscoveryCount++;
                    if (Current(intent.Target) && editor.Model.ConfigurationJson == captured.ConfigurationJson &&
                        editor.Model.SuggestedModels.SequenceEqual(captured.SuggestedModels)) {
                        editor.Model.SuggestedModels = [.. captured.SuggestedModels, "discovered-fixture-model"];
                        editor.Notify(nameof(editor.Model.SuggestedModels));
                        Outcome = "Fixture discovery prepared model data in the draft only.";
                    }
                    break;
            }
        } finally {
            IsBusy = false;
            held = null;
            Changed?.Invoke();
        }
    }

    private bool Current(ProviderEditorTarget target) => target.Activation == Activation && ReferenceEquals(target.Context, Editor.Context);
    private void Acquire(Guid? id) {
        Activation++;
        State = State with { ProviderId = id };
        Editor = ProviderEditorDraft.For(new EditContext(id is { } value ? ReadSaved(value) : new ProviderProfileEditorModel()));
        Error = null;
        Changed?.Invoke();
    }
    private ProviderProfile Project(ProviderProfileEditorModel model) => new(model.Id!.Value, model.Name, model.Kind, model.BaseUrl,
        model.ApiKeyEnvironmentVariable, model.DefaultModel, model.Transport, model.IsEnabled, model.SupportsStreaming,
        model.SupportsTools, model.PreferFrameworkManagedChatHistory, model.SupportsBackgroundResponses, model.ConfigurationJson,
        model.Notes, "Not checked", null, model.SuggestedModels, model.Purpose) {
        Tags = model.Tags, IsPrivateProvider = model.IsPrivateProvider,
        ModelPrices = ProviderPricingDefaults.FromEditorModels(model.ModelPrices),
        CredentialBinding = Scenario == ProviderScenario.ReadOnly
            ? new(Guid.Parse("11111111-1111-4111-8111-111111111111"), ProviderCredentialPurpose.SourceAccessToken,
                ProviderCredentialConsumerKind.Source, Guid.Parse("22222222-2222-4222-8222-222222222222")) : null,
        ModelCatalog = Scenario == ProviderScenario.ReadOnly
            ? model.SuggestedModels.Select(id => new ProviderModelDisplayMetadata(id, $"Shared {id}")).ToArray() : []
    };
}
