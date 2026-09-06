using CanDoItAll.AgentFramework.Models;
using ProviderConnectorKeys = CanDoItAll.Modules.AgentFramework.ProviderManagement.ProviderConnectorKeys;
using CanDoItAll.SharedKernel.Configuration;
using Microsoft.AspNetCore.Components.Forms;
using ProviderProfileEditorModel = CanDoItAll.AgentFramework.Models.ProviderProfileEditorModel;

namespace CanDoItAll.Modules.AgentFramework;

public sealed record ProviderSharedReconciliationResult(bool Completed, bool EditorReplaced = false);

public sealed class ProviderProfilesSession(IProviderProfilesReads reads, ProviderEditorRecovery? recovery = null) : IDisposable {
    public ProviderEditorRecovery Recovery { get; } = recovery ?? new();
    private CancellationTokenSource? catalogRead;
    private CancellationTokenSource? editorRead;
    private long catalogGeneration;
    private long selectionVersion;
    private bool disposed;
    private CancellationTokenSource targetLifetime = new();

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
    public CancellationToken TargetCancellationToken => targetLifetime.Token;
    public string? MetadataWarning { get; private set; }
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

    public Task<bool> RefreshCatalogAsync() => RefreshCatalogCoreAsync(preserveEditor: false);
    public Task<bool> RefreshMetadataAsync(CancellationToken cancellationToken = default) => RefreshCatalogCoreAsync(preserveEditor: true, cancellationToken);

    private async Task<bool> RefreshCatalogCoreAsync(bool preserveEditor, CancellationToken cancellationToken = default) {
        if (disposed) {
            return false;
        }
        var generation = ++catalogGeneration;
        catalogRead?.Cancel();
        using var owner = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        catalogRead = owner;
        var retainReady = preserveEditor && CatalogLoadState == ProviderProfilesLoadState.Ready;
        if (!retainReady) {
            CatalogLoadState = ProviderProfilesLoadState.Loading;
        }
        CatalogError = null;
        MetadataWarning = null;
        try {
            var loaded = await reads.LoadCatalogAsync(owner.Token);
            if (disposed || generation != catalogGeneration || owner.IsCancellationRequested) {
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
        } catch (Exception) {
            if (!disposed && generation == catalogGeneration) {
                if (retainReady) {
                    MetadataWarning = "The provider catalog could not be refreshed. Your draft is retained.";
                } else {
                    CatalogError = "The provider catalog could not be loaded.";
                    CatalogLoadState = ProviderProfilesLoadState.Failed;
                }
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
        targetLifetime.Cancel();
        targetLifetime.Dispose();
        targetLifetime = new();
        editorRead?.Cancel();
        State = State with { ProviderId = providerId };
        EditorError = null;
        if (providerId is null) {
            var unresolved = Recovery.Find(null);
            EditContext = unresolved?.Context ?? new(CreateNewProviderEditor());
            if (unresolved is not null) {
                State = State with { Section = unresolved.Section };
            }
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
        } catch (Exception) {
            if (IsCurrentSelection(version)) {
                EditorError = "The selected provider could not be loaded. Retry to read the same target.";
                EditorLoadState = ProviderProfilesLoadState.Failed;
            }
        } finally {
            if (ReferenceEquals(editorRead, owner)) {
                editorRead = null;
            }
        }
        return false;
    }

    public void ResumeNewAttempt(ProviderUnresolvedAttempt attempt) {
        if (State.ProviderId is not null || !attempt.Attempt.IsCreate) {
            throw new InvalidOperationException("Only a pending New provider can resume its draft.");
        }
        if (!ReferenceEquals(EditContext, attempt.Context)) {
            EditContext = attempt.Context;
            State = State with { Section = attempt.Section };
        }
    }

    public void BindCommittedIdentity(Guid providerId, Guid? concurrencyToken) {
        State = State with { ProviderId = providerId };
        Draft.Id = providerId;
        Draft.ExpectedConcurrencyToken = concurrencyToken;
    }

    public async Task<bool> ReconcileCommittedAsync(ProviderEditorSubmission? submission, CancellationToken token) {
        var version = selectionVersion;
        var providerId = State.ProviderId;
        if (!providerId.HasValue) {
            return false;
        }
        try {
            var authoritative = await reads.LoadEditorAsync(providerId.Value, token);
            if (!IsCurrentSelection(version) || token.IsCancellationRequested) {
                return false;
            }
            if (authoritative.Id != providerId) {
                throw new InvalidOperationException("The provider read returned a different editor identity.");
            }
            if (submission is not null) {
                submission.Reconcile(Draft, authoritative);
            } else {
                Draft.Id = authoritative.Id;
                Draft.ExpectedConcurrencyToken = authoritative.ExpectedConcurrencyToken;
            }
            EditorError = null;
            EditorLoadState = ProviderProfilesLoadState.Ready;
            return true;
        } catch (OperationCanceledException) when (token.IsCancellationRequested) {
            return false;
        } catch (Exception) {
            if (IsCurrentSelection(version)) {
                MetadataWarning = "The provider change is saved, but the current editor revision could not be read.";
            }
            return false;
        }
    }

    public void MarkTargetUnavailable(string message) {
        editorRead?.Cancel();
        EditorLoadState = ProviderProfilesLoadState.Failed;
        EditorError = message;
    }

    public async Task<ProviderSharedReconciliationResult> ReconcileSharedAsync(ProviderManagement.SharedProviderChange change) {
        var version = selectionVersion;
        var selectedId = State.ProviderId;
        var wasImported = IsSourceManaged;
        if (!await RefreshMetadataAsync() || !IsCurrentSelection(version)) {
            return new(false);
        }
        if (change.UnknownScope || change.CommitState == ProviderManagement.SharedProviderCommitState.Unconfirmed) {
            MetadataWarning = "Shared-provider state may have changed. Your draft is retained; refresh to verify the catalog.";
            return new(true);
        }
        if (!selectedId.HasValue) {
            return new(true);
        }
        if (change.RetiredProviderProfileIds.Contains(selectedId.Value)) {
            MarkTargetUnavailable("The selected imported provider was retired. It remains selected for audit and can be reactivated from Shared provider connections.");
            return new(true);
        }
        if ((wasImported || IsSourceManaged) && change.AffectedProviderProfileIds.Contains(selectedId.Value)) {
            if (SelectedProvider is null) {
                return new(true);
            }
            var replaced = await SelectAsync(selectedId);
            return new(replaced, replaced);
        }
        return new(true);
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
        targetLifetime.Cancel();
        targetLifetime.Dispose();
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
