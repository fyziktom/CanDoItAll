using CanDoItAll.AgentFramework.Models;
using ProviderConnectorKeys = CanDoItAll.Modules.AgentFramework.ProviderManagement.ProviderConnectorKeys;
using CanDoItAll.SharedKernel.Configuration;
using Microsoft.AspNetCore.Components.Forms;
using ProviderProfileEditorModel = CanDoItAll.AgentFramework.Models.ProviderProfileEditorModel;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class ProviderProfilesSession(IProviderProfilesReads reads) : IDisposable {
    private CancellationTokenSource? catalogRead;
    private CancellationTokenSource? editorRead;
    private long catalogGeneration;
    private long selectionVersion;
    private bool disposed;

    public ProviderProfilesState State { get; private set; } = new();
    public ProviderProfilesCatalog Catalog { get; private set; } = new([], new([]));
    public EditContext EditContext { get; private set; } = new(CreateNewProviderEditor());
    public ProviderProfileEditorModel Draft => (ProviderProfileEditorModel)EditContext.Model;
    public ProviderProfilesLoadState CatalogLoadState { get; private set; } = ProviderProfilesLoadState.Loading;
    public ProviderProfilesLoadState EditorLoadState { get; private set; } = ProviderProfilesLoadState.Loading;
    public string? CatalogError { get; private set; }
    public string? EditorError { get; private set; }
    public string? Error => CatalogError ?? EditorError;
    public bool CanEdit => !disposed && CatalogLoadState == ProviderProfilesLoadState.Ready
        && EditorLoadState == ProviderProfilesLoadState.Ready;
    public long SelectionVersion => selectionVersion;
    public ProviderProfile? SelectedProvider => Catalog.Providers.FirstOrDefault(provider => provider.Id == State.ProviderId);
    public bool IsSourceManaged => SelectedProvider?.ConnectorPluginKey == ProviderConnectorKeys.SharedImport;

    public bool IsCurrentSelection(long version) => !disposed && version == selectionVersion;

    public void SelectSection(ProviderEditorSection section) {
        _ = ProviderEditorSections.IndexOf(section);
        State = State with { Section = section };
    }

    public void SetSharedConnectionsOpen(bool open) => State = State with { SharedConnectionsOpen = open };

    public async Task<bool> RefreshAsync() {
        var version = selectionVersion;
        if (!await RefreshCatalogAsync() || !IsCurrentSelection(version)) {
            return false;
        }
        if (version == 0) {
            return await SelectAsync(Catalog.Providers.FirstOrDefault()?.Id);
        }
        return State.ProviderId.HasValue && await SelectAsync(State.ProviderId);
    }

    public async Task<bool> RefreshCatalogAsync() {
        if (disposed) {
            return false;
        }
        var generation = ++catalogGeneration;
        catalogRead?.Cancel();
        using var owner = new CancellationTokenSource();
        catalogRead = owner;
        CatalogLoadState = ProviderProfilesLoadState.Loading;
        CatalogError = null;
        try {
            var loaded = await reads.LoadCatalogAsync(owner.Token);
            if (disposed || generation != catalogGeneration) {
                return false;
            }
            Catalog = loaded;
            CatalogLoadState = ProviderProfilesLoadState.Ready;
            if (State.ProviderId.HasValue && SelectedProvider is null) {
                EditorError = "The selected provider is no longer in the catalog. Retry to reload it.";
                EditorLoadState = ProviderProfilesLoadState.Failed;
            }
            return true;
        } catch (OperationCanceledException) when (owner.IsCancellationRequested) {
            return false;
        } catch (Exception exception) {
            if (!disposed && generation == catalogGeneration) {
                CatalogError = exception.Message;
                CatalogLoadState = ProviderProfilesLoadState.Failed;
            }
            return false;
        } finally {
            if (ReferenceEquals(catalogRead, owner)) {
                catalogRead = null;
            }
        }
    }

    public async Task<bool> SelectAsync(Guid? providerId) {
        if (disposed) {
            return false;
        }
        var version = ++selectionVersion;
        editorRead?.Cancel();
        State = State with { ProviderId = providerId };
        EditorError = null;
        if (providerId is null) {
            EditContext = new(CreateNewProviderEditor());
            EditorLoadState = ProviderProfilesLoadState.Ready;
            return true;
        }

        using var owner = new CancellationTokenSource();
        editorRead = owner;
        EditorLoadState = ProviderProfilesLoadState.Loading;
        try {
            if (SelectedProvider is null) {
                throw new InvalidOperationException("The selected provider is no longer in the catalog. Retry to reload it.");
            }
            var draft = await reads.LoadEditorAsync(providerId.Value, owner.Token);
            if (!IsCurrentSelection(version)) {
                return false;
            }
            if (SelectedProvider is null) {
                throw new InvalidOperationException("The selected provider is no longer in the catalog. Retry to reload it.");
            }
            if (draft.Id != providerId) {
                throw new InvalidOperationException("The provider read returned a different editor identity.");
            }
            EditContext = new(draft);
            EditorLoadState = ProviderProfilesLoadState.Ready;
            return true;
        } catch (OperationCanceledException) when (owner.IsCancellationRequested) {
        } catch (Exception exception) {
            if (IsCurrentSelection(version)) {
                EditorError = exception.Message;
                EditorLoadState = ProviderProfilesLoadState.Failed;
            }
        } finally {
            if (ReferenceEquals(editorRead, owner)) {
                editorRead = null;
            }
        }
        return false;
    }

    public async Task NewAsync() {
        SelectSection(ProviderEditorSection.Connection);
        await SelectAsync(null);
    }

    public void Dispose() {
        if (disposed) {
            return;
        }
        disposed = true;
        catalogRead?.Cancel();
        editorRead?.Cancel();
    }

    private static ProviderProfileEditorModel CreateNewProviderEditor() {
        return new ProviderProfileEditorModel {
            Name = "New OpenAI provider",
            Kind = ProviderKind.OpenAi,
            BaseUrl = ManagedSeedProviderFallbacks.OpenAiBaseUrl,
            ApiKeyEnvironmentVariable = string.Empty,
            DefaultModel = string.Empty,
            Transport = ProviderTransportKind.Responses,
            Purpose = ProviderProfilePurpose.Chat,
            IsEnabled = true,
            SupportsStreaming = true,
            SupportsTools = true,
            SupportsBackgroundResponses = true,
            PreferFrameworkManagedChatHistory = false,
            ConfigurationJson = "{}",
            SuggestedModels = [],
            IsPrivateProvider = ProviderPricingDefaults.ResolveIsPrivateProvider(ProviderKind.OpenAi, null),
            ModelPrices = [],
            Tags = ["openai", "cloud", "chat", "responses"]
        };
    }
}
