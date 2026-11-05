using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Providers.UI;
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
    private long peerSequence;
    private CancellationTokenSource targetLifetime = new();

    public ProviderProfilesState State { get; private set; } = new();
    public ProviderProfilesCatalog Catalog { get; private set; } = new([], new([]));
    public EditContext EditContext { get; private set; } = new(CreateNewProviderEditor());
    public ProviderEditorDraft Editor => ProviderEditorDraft.For(EditContext);
    public ProviderProfileEditorModel Draft => (ProviderProfileEditorModel)EditContext.Model;
    public ProviderProfileEditorModel? Original { get; private set; }
    public bool NeedsComparison { get; private set; }
    public bool MissingAcceptedSnapshot { get; private set; }
    public ProviderEditorComparison? Comparison { get; private set; }
    public ProviderRecoveryReview? RecoveryReview { get; private set; }
    private ProviderEditorSubmission? comparisonSubmission;
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
        State = State with { Section = section, SharingActivated = State.SharingActivated || section == ProviderEditorSection.Sharing };
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

    public Task<bool> RefreshCatalogAsync() => RefreshCatalogCoreAsync();
    public Task<bool> RefreshMetadataAsync(CancellationToken cancellationToken = default) => RefreshCatalogCoreAsync(cancellationToken);

    private async Task<bool> RefreshCatalogCoreAsync(CancellationToken cancellationToken = default) {
        if (disposed) {
            return false;
        }
        var generation = ++catalogGeneration;
        catalogRead?.Cancel();
        using var owner = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        catalogRead = owner;
        var retainReady = CatalogLoadState == ProviderProfilesLoadState.Ready;
        if (!retainReady) {
            CatalogLoadState = ProviderProfilesLoadState.Loading;
        }
        CatalogError = null;
        MetadataWarning = null;
        long? importedReadVersion = null;
        try {
            var loaded = await reads.LoadCatalogAsync(owner.Token);
            if (disposed || generation != catalogGeneration || owner.IsCancellationRequested) {
                return false;
            }
            var selected = loaded.Providers.FirstOrDefault(provider => provider.Id == State.ProviderId);
            if (selected?.ConnectorPluginKey == ProviderConnectorKeys.SharedImport && Draft.Id == selected.Id &&
                EditorLoadState == ProviderProfilesLoadState.Ready) {
                var context = EditContext;
                importedReadVersion = selectionVersion;
                if (Draft.ExpectedConcurrencyToken != ImportedRevision(loaded, selected.Id)) {
                    using var importedRead = CancellationTokenSource.CreateLinkedTokenSource(owner.Token, TargetCancellationToken);
                    MetadataWarning = "Refreshing the imported provider snapshot.";
                    var draft = await reads.LoadEditorAsync(selected.Id, importedRead.Token);
                    if (disposed || generation != catalogGeneration || importedRead.IsCancellationRequested ||
                        !IsCurrentSelection(importedReadVersion.Value) || !ReferenceEquals(context, EditContext)) {
                        return false;
                    }
                    ValidateImportedEditor(loaded, selected.Id, draft);
                    editorRead?.Cancel();
                    SetContext(new(draft));
                    EditorError = null;
                    MetadataWarning = null;
                }
            }
            Catalog = loaded;
            if (selected is not null && selected.ConnectorPluginKey != ProviderConnectorKeys.SharedImport && Draft.Id == selected.Id &&
                loaded.Revisions.TryGetValue(selected.Id, out var revision) && revision.Value != Draft.ExpectedConcurrencyToken) {
                RequireComparison();
            }
            CatalogLoadState = ProviderProfilesLoadState.Ready;
            if (State.ProviderId.HasValue && SelectedProvider is null) {
                EditorError = "The selected provider is no longer in the catalog. Retry to reload it.";
                EditorLoadState = ProviderProfilesLoadState.Failed;
            }
            return true;
        } catch (OperationCanceledException) when (owner.IsCancellationRequested) {
            return false;
        } catch (Exception) {
            if (importedReadVersion.HasValue && !IsCurrentSelection(importedReadVersion.Value)) {
                return false;
            }
            if (!disposed && generation == catalogGeneration) {
                if (importedReadVersion.HasValue && IsCurrentSelection(importedReadVersion.Value)) {
                    EditorError = "The updated imported provider snapshot could not be loaded. Retry to read the same target.";
                    EditorLoadState = ProviderProfilesLoadState.Failed;
                    MetadataWarning = null;
                } else if (retainReady) {
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

    public Task<bool> SelectAsync(Guid? providerId) => AcquireAsync(providerId, replace: false);

    private async Task<bool> AcquireAsync(Guid? providerId, bool replace) {
        if (disposed) {
            return false;
        }
        if (!replace && selectionVersion > 0 && State.ProviderId == providerId &&
            EditorLoadState == ProviderProfilesLoadState.Ready && Draft.Id == providerId) {
            return true;
        }
        var version = ++selectionVersion;
        targetLifetime.Cancel();
        targetLifetime.Dispose();
        targetLifetime = new();
        editorRead?.Cancel();
        State = State with { ProviderId = providerId, SharingActivated = State.Section == ProviderEditorSection.Sharing };
        EditorError = null;
        MetadataWarning = null;
        Original = null;
        Comparison = null;
        RecoveryReview = null;
        NeedsComparison = false;
        MissingAcceptedSnapshot = false;
        if (providerId is null) {
            var unresolved = Recovery.Find(null);
            SetContext(unresolved?.Context ?? new(CreateNewProviderEditor()));
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
            if (IsSourceManaged) {
                ValidateImportedEditor(Catalog, providerId.Value, draft);
            }
            SetContext(new(draft));
            Original = ProviderEditorSubmission.Copy(draft);
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
        if (!attempt.Attempt.IsCreate || State.ProviderId is { } id && id != attempt.Attempt.ProviderId) {
            throw new InvalidOperationException("Only the matching pending provider creation can resume its draft.");
        }
        if (!ReferenceEquals(EditContext, attempt.Context)) {
            SetContext(attempt.Context);
            State = State with { Section = attempt.Section };
        }
    }

    public void BindCommittedIdentity(Guid providerId, Guid? concurrencyToken) {
        State = State with { ProviderId = providerId };
        Draft.Id = providerId;
        Draft.ExpectedConcurrencyToken = concurrencyToken;
        MissingAcceptedSnapshot = true;
        NeedsComparison = true;
    }

    public void AcceptCommitted(ProviderEditorSubmission submission, ProviderProfileEditorModel? accepted) {
        if (accepted is null || accepted.Id != State.ProviderId || accepted.ExpectedConcurrencyToken is not { } token || token == Guid.Empty) {
            MissingAcceptedSnapshot = true;
            NeedsComparison = true;
            return;
        }
        submission.Reconcile(Draft, accepted);
        Original = ProviderEditorSubmission.Copy(accepted);
        MissingAcceptedSnapshot = false;
        NeedsComparison = false;
    }

    public void RequireComparison() {
        peerSequence++;
        NeedsComparison = true;
        Comparison?.Invalidate();
        RecoveryReview?.Invalidate();
    }

    public async Task ReviewCurrentAsync(Guid providerId, CancellationToken cancellationToken) {
        if (!CanEdit || IsSourceManaged || Draft.Id is { } id && id != providerId) {
            return;
        }
        var version = selectionVersion;
        var sequence = peerSequence;
        var submission = ProviderEditorSubmission.Capture(Draft, Editor);
        var current = await reads.LoadEditorAsync(providerId, cancellationToken);
        if (!IsCurrentSelection(version) || sequence != peerSequence || submission.HasLaterEdits(Draft) || cancellationToken.IsCancellationRequested) {
            return;
        }
        if (current.Id != providerId) {
            throw new InvalidOperationException("The provider read returned a different recovery identity.");
        }
        comparisonSubmission = submission;
        RecoveryReview = new(Draft, current);
    }

    public bool ApplyCurrentReview() {
        if (RecoveryReview is not { CanApply: true } review || comparisonSubmission is not { } submission) {
            return false;
        }
        if (submission.HasLaterEdits(Draft)) {
            RecoveryReview?.Invalidate();
            return false;
        }
        var current = review.CurrentSnapshot();
        State = State with { ProviderId = current.Id };
        Draft.Id = current.Id;
        Draft.ExpectedConcurrencyToken = current.ExpectedConcurrencyToken;
        Original = current;
        NeedsComparison = false;
        MissingAcceptedSnapshot = false;
        CancelComparison();
        return true;
    }

    public async Task CompareAsync(CancellationToken cancellationToken = default) {
        if (!CanEdit || Original is null || State.ProviderId is not { } id || IsSourceManaged) {
            return;
        }
        var version = selectionVersion;
        var sequence = peerSequence;
        var submission = ProviderEditorSubmission.Capture(Draft, Editor);
        var current = await reads.LoadEditorAsync(id, cancellationToken);
        if (!IsCurrentSelection(version) || sequence != peerSequence || submission.HasLaterEdits(Draft) || cancellationToken.IsCancellationRequested) {
            return;
        }
        comparisonSubmission = submission;
        Comparison = new(Original, Draft, current, !Editor.Validate());
    }

    public bool ApplyComparison() {
        if (Comparison is not { CanApply: true } review || comparisonSubmission is not { } submission || submission.HasLaterEdits(Draft)) {
            Comparison?.Invalidate();
            return false;
        }
        var result = review.Result();
        submission.Reconcile(Draft, result);
        if (!review.HasIncompleteSettings) {
            Draft.ModelPrices = result.ModelPrices;
        }
        Original = review.CurrentSnapshot();
        NeedsComparison = false;
        MissingAcceptedSnapshot = false;
        CancelComparison();
        return true;
    }

    public void CancelComparison() {
        Comparison = null;
        RecoveryReview = null;
        comparisonSubmission = null;
    }

    private void SetContext(EditContext context) {
        EditContext.OnFieldChanged -= DraftChanged;
        EditContext = context;
        EditContext.OnFieldChanged += DraftChanged;
    }

    private void DraftChanged(object? sender, FieldChangedEventArgs args) {
        Comparison?.Invalidate();
        RecoveryReview?.Invalidate();
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
            if (MissingAcceptedSnapshot && submission is not null && Draft.ExpectedConcurrencyToken is { } witness &&
                witness != Guid.Empty && authoritative.ExpectedConcurrencyToken == witness) {
                AcceptCommitted(submission, authoritative);
            } else if (authoritative.ExpectedConcurrencyToken != Draft.ExpectedConcurrencyToken) {
                RequireComparison();
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
        var context = EditContext;
        if (!await RefreshMetadataAsync() || !IsCurrentSelection(version)) {
            return new(false);
        }
        if (change.UnknownScope || change.CommitState == ProviderManagement.SharedProviderCommitState.Unconfirmed) {
            MetadataWarning = "Shared-provider state may have changed. Refresh to verify the catalog before another change.";
            return new(true, !ReferenceEquals(context, EditContext));
        }
        if (!selectedId.HasValue) {
            return new(true);
        }
        if (change.RetiredProviderProfileIds.Contains(selectedId.Value)) {
            if (SelectedProvider is null) {
                MarkTargetUnavailable("The selected imported provider was retired. Its metadata is unavailable; retain the target for a later read.");
            } else {
                MetadataWarning = "The selected imported provider was retired. Its metadata remains available for audit; reactivate it from Shared provider connections.";
            }
            return new(true, !ReferenceEquals(context, EditContext));
        }
        if ((wasImported || IsSourceManaged) && change.AffectedProviderProfileIds.Contains(selectedId.Value)) {
            if (SelectedProvider is null) {
                return new(true);
            }
            var replaced = !ReferenceEquals(context, EditContext) || await AcquireAsync(selectedId, replace: true);
            return new(replaced, replaced);
        }
        return new(true);
    }

    private static Guid ImportedRevision(ProviderProfilesCatalog catalog, Guid providerId) =>
        catalog.Revisions.TryGetValue(providerId, out var revision) ? revision.Value
            : throw new InvalidOperationException("The imported provider snapshot has no native revision.");

    private static void ValidateImportedEditor(ProviderProfilesCatalog catalog, Guid providerId, ProviderProfileEditorModel draft) {
        if (draft.Id != providerId || draft.ExpectedConcurrencyToken != ImportedRevision(catalog, providerId)) {
            throw new InvalidOperationException("The imported editor and catalog belong to different snapshots. Refresh the provider.");
        }
    }

    public async Task NewAsync() {
        SelectSection(ProviderEditorSection.Connection);
        await AcquireAsync(null, replace: true);
    }

    public void Dispose() {
        if (disposed) {
            return;
        }
        disposed = true;
        EditContext.OnFieldChanged -= DraftChanged;
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
