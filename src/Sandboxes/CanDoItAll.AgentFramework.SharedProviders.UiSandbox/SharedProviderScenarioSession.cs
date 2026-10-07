using CanDoItAll.AgentFramework.SharedProviders.UI;
using CanDoItAll.SharedProviders.Abstractions;

namespace CanDoItAll.AgentFramework.SharedProviders.UiSandbox;

public sealed class SharedProviderScenarioSession : ISharedProviderSharingView, ISharedProviderSourcesView, ISharedProviderRefreshView {
    private readonly Guid viewId = Guid.NewGuid();
    private Guid sharingSnapshot = Guid.NewGuid();
    private Guid sourceSnapshot = Guid.NewGuid();
    private bool busy;
    private bool readBlocked;
    private bool loading;
    private bool sharingPending;
    private SharedProviderImportSubmission? pendingImport;
    private SharedProviderSourceSubmission? pendingSource;
    private SharedProviderCatalogSubmission? pendingCatalog;
    private SharedProviderSourceRecoveryPresentation? sourceRecovery;
    private SharedProviderConfirmation? confirmation;
    private SharedProviderSourceConfirmation? sourceConfirmation;
    private SharedProviderCatalogSelection? catalog;
    private SharedProviderSourceDraft? sourceDraft;
    private string? warning;
    private string sourceError = string.Empty;
    private string editorError = string.Empty;
    private string? refreshMessage;
    private readonly TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private SharedProviderProfilePresentation? profile;
    private IReadOnlyList<SharedProviderSourcePresentation> sources = [];
    public SharedProviderScenarioStore Store { get; }
    public SharingScenario Scenario { get; }
    public SharedProviderImportDraft ImportDraft { get; }
    public bool SourcesOpen { get; private set; }
    public event Action? Changed;
    public string Outcome { get; private set; } = "No fixture write has occurred.";
    private bool Deferred => Scenario is SharingScenario.Unknown or SharingScenario.CommittedReadFailure or SharingScenario.DeliveryFailure;
    private bool Imported => Scenario is not (SharingScenario.Local or SharingScenario.Ineligible or SharingScenario.RuntimeOnly);
    private SharedProviderSharingOrigin SharingOrigin => new(viewId, 1, sharingSnapshot, Store.ProviderId,
        Imported ? null : Store.PublicationToken, Imported ? profile?.Import?.Baseline : null);
    private SharedProviderRefreshOrigin RefreshOrigin => new(viewId, 1, Store.ProviderId);
    private IReadOnlyList<SharedProviderCredentialChoice> Credentials => Scenario == SharingScenario.MissingCredential ? [] : [new(Store.CredentialId, "Research API token reference")];
    private string? MetadataError => Scenario == SharingScenario.MetadataFailure ? "Fixture metadata read failed." : null;

    public SharedProviderScenarioSession(SharingScenario scenario = SharingScenario.Imported, SharedProviderScenarioStore? store = null) {
        Scenario = scenario;
        Store = store ?? new(scenario);
        ImportDraft = new(Store.Import);
        ReadSharing();
        ReadSources();
        loading = scenario == SharingScenario.Loading;
        readBlocked = scenario == SharingScenario.ReadFailure;
        warning = readBlocked ? "Sharing state could not be read. Retry this target." : null;
        if (loading || readBlocked) {
            profile = null;
        }
    }

    public SharedProviderSharingPresentation Sharing => new(SharingOrigin, profile, Imported ? ImportDraft : null,
        loading, busy || loading || readBlocked || sharingPending, warning, confirmation);
    public SharedProviderSourcesPresentation Sources => new(viewId, sources, Credentials, MetadataError, loading, busy,
        busy || loading || sourceRecovery is not null || readBlocked, sourceError, sourceDraft, editorError, catalog, sourceRecovery, sourceConfirmation);
    public SharedProviderRefreshPresentation Refresh => new(RefreshOrigin, busy, sourceRecovery is not null,
        sourceRecovery?.DeliveryPending == true ? sourceRecovery.AttemptId : null, refreshMessage, Scenario == SharingScenario.Offline);
    SharedProviderSharingPresentation ISharedProviderSharingView.Presentation => Sharing;
    SharedProviderSourcesPresentation ISharedProviderSourcesView.Presentation => Sources;
    SharedProviderRefreshPresentation ISharedProviderRefreshView.Presentation => Refresh;

    public void OpenSources() {
        SourcesOpen = true;
        Notify();
    }
    public void Release() => release.TrySetResult();
    public void CompleteRead() {
        loading = false;
        readBlocked = false;
        warning = null;
        ReadSharing();
        ReadSources();
        Notify();
    }
    public void OtherOperator() {
        Store.ChangeOtherOperator();
        Outcome = "The separate stored fixture now has Operations model, disabled. Refresh an editor to observe it.";
        Notify();
    }
    public void MetadataOnly() {
        Store.ChangeMetadata();
        Outcome = "Stored remote metadata changed; local alias/enabled settings did not.";
        Notify();
    }

    public Task RetryAsync(SharedProviderSharingOrigin origin) {
        if (origin != SharingOrigin || busy) {
            return Task.CompletedTask;
        }
        if (pendingImport is { } submitted) {
            if (Store.Import.Settings != (submitted.Settings with { LocalAlias = submitted.Settings.LocalAlias.Trim() })) {
                warning = "The original attempt remains unconfirmed. Verification did not repeat the write.";
                return Task.CompletedTask;
            }
            ImportDraft.Accept(submitted, Store.Import);
        }
        pendingImport = null;
        sharingPending = false;
        CompleteRead();
        return Task.CompletedTask;
    }

    public async Task PublishAsync(SharedProviderSharingOrigin origin) {
        if (!CanWrite(origin) || profile?.Eligibility?.IsEligible != true || Store.Published) {
            return;
        }
        await WriteSharingAsync(() => Store.SetPublished(true));
    }

    public async Task SaveImportedAsync(SharedProviderSharingOrigin origin, SharedProviderImportSubmission submission) {
        if (!CanWrite(origin) || !ImportDraft.CanSubmit(submission)) {
            return;
        }
        await WriteSharingAsync(() => {
            var accepted = Store.SaveImport(submission);
            if (Deferred) {
                pendingImport = submission;
            } else {
                ImportDraft.Accept(submission, accepted);
            }
        });
    }

    public void OpenConfirmation(SharedProviderSharingOrigin origin, SharedProviderConfirmationKind kind) {
        if (CanWrite(origin) && (kind == SharedProviderConfirmationKind.Unpublish ? Store.Published : Imported && Store.Selected.Contains(Store.PublicationId))) {
            confirmation = new(Guid.NewGuid(), origin, kind);
        }
    }
    public Task ConfirmAsync(SharedProviderConfirmation requested) => requested == confirmation && CanWrite(requested.Origin)
        ? WriteSharingAsync(() => {
            if (requested.Kind == SharedProviderConfirmationKind.Unpublish) {
                Store.SetPublished(false);
            } else {
                Store.RetireImport();
            }
        }) : Task.CompletedTask;
    public void CloseConfirmation(SharedProviderConfirmation requested) {
        if (requested == confirmation) {
            confirmation = null;
        }
    }

    private bool CanWrite(SharedProviderSharingOrigin origin) => origin == SharingOrigin && !Sharing.WritesBlocked;
    private async Task WriteSharingAsync(Action commit) {
        busy = true;
        Notify();
        try {
            await WaitAsync();
            if (Scenario == SharingScenario.Rejected) {
                warning = "The fixture owner rejected the request before a write.";
                return;
            }
            commit();
            sharingPending = Deferred;
            warning = Deferred ? "The original fixture attempt needs verification or delivery. Retry does not repeat its write." : null;
            if (!Deferred) {
                ReadSharing();
            }
            Outcome = $"Stored fixture writes: {Store.Writes}.";
        } catch (InvalidOperationException exception) {
            warning = exception.Message;
        } finally {
            busy = false;
            confirmation = null;
            Notify();
        }
    }

    public Task CloseAsync(Guid origin) {
        if (origin == viewId) {
            SourcesOpen = false;
            sourceDraft = null;
            catalog = null;
            sourceConfirmation = null;
            Notify();
        }
        return Task.CompletedTask;
    }
    public Task RefreshAsync(Guid origin) {
        if (origin == viewId && !busy) {
            readBlocked = false;
            sourceError = string.Empty;
            ReadSources();
        }
        return Task.CompletedTask;
    }
    public void NewSource(Guid origin) {
        if (origin == viewId && !Sources.WritesBlocked) {
            sourceDraft = new(Guid.NewGuid(), null, new(string.Empty, string.Empty,
                MetadataError is null && Credentials.Count == 1 ? Credentials[0].Id : Guid.Empty, true, false));
            editorError = string.Empty;
        }
    }
    public void CloseEditor(Guid draftId) {
        if (sourceDraft?.Id == draftId) {
            sourceDraft = null;
            editorError = string.Empty;
        }
    }
    public async Task SaveAsync(SharedProviderSourceSubmission submission) {
        if (Sources.WritesBlocked || sourceDraft is not { } draft || !draft.CanSubmit(submission)) {
            return;
        }
        if (MetadataError is not null || !Credentials.Any(item => item.Id == submission.Values.CredentialReference)) {
            editorError = "Select an available credential after metadata is loaded.";
            return;
        }
        busy = true;
        Notify();
        try {
            await WaitAsync();
            if (Scenario == SharingScenario.Rejected) {
                if (ReferenceEquals(sourceDraft, draft)) {
                    editorError = "The fixture owner rejected the source before a write.";
                }
                return;
            }
            if (Scenario == SharingScenario.UnknownBeforeCommit) {
                pendingSource = submission;
                sourceRecovery = new(Guid.NewGuid(), submission.SourceId, false, false);
                Outcome = "The source attempt has an unknown result. Verify before retry.";
                return;
            }
            var accepted = Store.SaveSource(submission);
            if (Deferred) {
                pendingSource = submission;
                sourceRecovery = new(Guid.NewGuid(), accepted.Id, Scenario == SharingScenario.DeliveryFailure, false);
                if (ReferenceEquals(draft, sourceDraft) && Scenario != SharingScenario.Unknown) {
                    draft.AcceptIdentity(submission, accepted.Id, accepted.Token);
                }
            } else {
                AcceptSource(submission, accepted);
                ReadSources();
            }
            Outcome = $"Stored fixture writes: {Store.Writes}.";
        } catch (InvalidOperationException exception) {
            sourceError = exception.Message;
        } finally {
            busy = false;
            Notify();
        }
    }

    public Task ExecuteAsync(SharedProviderSourceIntent intent) {
        if (Resolve(intent.Origin) is not { } source || (intent.Action != SharedProviderSourceAction.Edit && Sources.WritesBlocked)) {
            return Task.CompletedTask;
        }
        switch (intent.Action) {
            case SharedProviderSourceAction.Edit:
                sourceDraft = new(source.Id, source.Token, source.Values);
                editorError = string.Empty;
                break;
            case SharedProviderSourceAction.Test:
                sourceError = Scenario == SharingScenario.Offline ? "The fixture source is offline; imported models remain unavailable." : string.Empty;
                Outcome = "Fixture catalog test completed without inference.";
                break;
            case SharedProviderSourceAction.Discover when source.Values.IsEnabled:
                catalog = new(intent.Origin, source.Values.Name, Store.Catalog, Store.Selected);
                break;
            case SharedProviderSourceAction.Synchronize when source.Values.IsEnabled:
                Store.Synchronize(intent.Origin, Store.Selected.ToHashSet());
                ReadSources();
                ReadSharing();
                break;
            case SharedProviderSourceAction.ToggleEnabled:
                Store.ChangeSource(intent.Origin, false);
                ReadSources();
                break;
            case SharedProviderSourceAction.Delete when sources.Single(item => item.Origin == intent.Origin).ImportCount == 0:
                sourceConfirmation = new(Guid.NewGuid(), intent.Origin, source.Values.Name);
                break;
        }
        return Task.CompletedTask;
    }

    public Task VerifyAsync(Guid attemptId) {
        if (sourceRecovery?.AttemptId == attemptId && !busy) {
            if (pendingSource is { } submitted) {
                var accepted = Store.Sources.GetValueOrDefault(submitted.SourceId);
                var expected = submitted.Values with { Name = submitted.Values.Name.Trim(), BaseUri = new Uri(submitted.Values.BaseUri.Trim()).AbsoluteUri };
                if (accepted is null || accepted.Values != expected) {
                    sourceRecovery = sourceRecovery with { RetryAllowed = accepted?.Token == submitted.ExpectedToken };
                    sourceError = sourceRecovery.RetryAllowed ? string.Empty : "The source differs from both the original and submitted values. Keep this attempt unresolved.";
                    return Task.CompletedTask;
                }
                AcceptSource(submitted, accepted);
            }
            if (pendingCatalog is { } selection) {
                if (!Store.Selected.SetEquals(selection.Selection)) {
                    sourceError = "The original catalog selection remains unconfirmed.";
                    return Task.CompletedTask;
                }
                AcceptCatalog(selection);
            }
            sourceRecovery = null;
            pendingSource = null;
            pendingCatalog = null;
            sourceError = string.Empty;
            ReadSources();
            Outcome = "Original fixture receipt reconciled without repeating a write.";
        }
        return Task.CompletedTask;
    }
    public Task RetryVerifiedAsync(Guid attemptId) {
        if (sourceRecovery?.AttemptId == attemptId && sourceRecovery.RetryAllowed && !busy && pendingSource is { } submitted) {
            var accepted = Store.SaveSource(submitted);
            AcceptSource(submitted, accepted);
            pendingSource = null;
            sourceRecovery = null;
            ReadSources();
            Outcome = "Verified retry used the original source candidate and immutable submitted values.";
        }
        return Task.CompletedTask;
    }
    public void CloseCatalog(Guid dialogId) {
        if (catalog?.Id == dialogId) {
            catalog = null;
        }
    }
    public async Task ApplyCatalogAsync(SharedProviderCatalogSubmission submission) {
        if (Sources.WritesBlocked || catalog is not { } dialog || !dialog.IsCurrent(submission) || Resolve(submission.Origin) is not { Values.IsEnabled: true }) {
            return;
        }
        busy = true;
        Notify();
        try {
            await WaitAsync();
            if (Scenario == SharingScenario.Rejected) {
                sourceError = "The fixture owner rejected synchronization before a write.";
                return;
            }
            Store.Synchronize(submission.Origin, submission.Selection);
            if (Deferred) {
                pendingCatalog = submission;
                sourceRecovery = new(Guid.NewGuid(), submission.Origin.SourceId, Scenario == SharingScenario.DeliveryFailure, false);
            } else {
                AcceptCatalog(submission);
            }
            Outcome = "Stored fixture selection synchronized.";
        } catch (InvalidOperationException exception) {
            sourceError = exception.Message;
        } finally {
            busy = false;
            Notify();
        }
    }
    private void AcceptCatalog(SharedProviderCatalogSubmission submission) {
        ReadSources();
        ReadSharing();
        if (catalog?.Id == submission.DialogId && catalog.Accept(submission, sources.Single(item => item.Origin.SourceId == submission.Origin.SourceId).Origin)) {
            catalog = null;
        }
    }
    public Task ConfirmDeleteAsync(SharedProviderSourceConfirmation requested) {
        if (requested == sourceConfirmation && !Sources.WritesBlocked && Resolve(requested.Origin) is not null) {
            Store.ChangeSource(requested.Origin, true);
            sourceConfirmation = null;
            ReadSources();
        }
        return Task.CompletedTask;
    }
    public void CloseConfirmation(SharedProviderSourceConfirmation requested) {
        if (requested == sourceConfirmation) {
            sourceConfirmation = null;
        }
    }
    public Task RefreshAsync(SharedProviderRefreshOrigin origin) {
        if (origin == RefreshOrigin && !busy && sourceRecovery is null) {
            ReadSharing();
            refreshMessage = Scenario == SharingScenario.Offline ? "Fixture source is offline. No fallback was selected." : "Stored fixture metadata refreshed. Local drafts were retained.";
        }
        return Task.CompletedTask;
    }
    public Task RetryDeliveryAsync(SharedProviderRefreshOrigin origin, Guid attemptId) => origin == RefreshOrigin ? VerifyAsync(attemptId) : Task.CompletedTask;

    private void AcceptSource(SharedProviderSourceSubmission submission, ScenarioSource accepted) {
        if (sourceDraft?.Id == submission.DraftId && sourceDraft.Accept(submission, accepted.Token, accepted.Values)) {
            sourceDraft = null;
        }
    }
    private ScenarioSource? Resolve(SharedProviderSourceOrigin origin) => origin.ViewId == viewId && origin.SnapshotId == sourceSnapshot &&
        Store.Sources.TryGetValue(origin.SourceId, out var source) && source.Token == origin.ConcurrencyToken ? source : null;
    private Task WaitAsync() => Scenario == SharingScenario.Held ? release.Task : Task.CompletedTask;
    private void Notify() => Changed?.Invoke();

    private void ReadSharing() {
        sharingSnapshot = Guid.NewGuid();
        ImportDraft.Reconcile(Store.Import);
        var model = Store.Catalog[0];
        profile = Imported ? new(SharedProviderOwnership.Imported, null, null,
            new(Store.Import, "Research instance", Store.RemoteName, SharedProviderPurpose.Chat, SharedProviderTransport.OpenAiCompatible,
                model.DefaultModelId, Scenario != SharingScenario.Retired && Store.Selected.Contains(Store.PublicationId),
                Scenario switch {
                    SharingScenario.Offline or SharingScenario.DisabledSource => SharedProviderImportAvailability.SourceOffline,
                    SharingScenario.AuthorizationFailure => SharedProviderImportAvailability.AuthorizationFailed,
                    SharingScenario.IdentityMismatch => SharedProviderImportAvailability.SourceIdentityMismatch,
                    SharingScenario.Unpublished => SharedProviderImportAvailability.Unpublished,
                    SharingScenario.MissingImport => SharedProviderImportAvailability.Missing,
                    _ => SharedProviderImportAvailability.Available
                },
                Scenario == SharingScenario.UnavailableModel ? [] : model.Models)) :
            new(Scenario == SharingScenario.RuntimeOnly ? SharedProviderOwnership.RuntimeOnly : SharedProviderOwnership.Local,
                new(Store.PublicationId, Store.Published), new(Scenario != SharingScenario.Ineligible,
                    Scenario == SharingScenario.Ineligible ? "Enable this saved provider before publishing." : "This saved provider is eligible for publication.",
                    [new("Native reasoning model 東京", [SharedProviderCapability.Responses])]), null);
    }
    private void ReadSources() {
        sourceSnapshot = Guid.NewGuid();
        sources = Store.Sources.Values.Select(source => new SharedProviderSourcePresentation(
            new(viewId, sourceSnapshot, source.Id, source.Token), source.Values.Name, new(source.Values.BaseUri), source.Values.CredentialReference,
            source.Values.IsEnabled, source.Values.AllowPrivateNetwork,
            Scenario switch {
                SharingScenario.Offline => SharedProviderSourceAvailability.SourceOffline,
                SharingScenario.AuthorizationFailure => SharedProviderSourceAvailability.AuthorizationFailed,
                SharingScenario.IdentityMismatch => SharedProviderSourceAvailability.SourceIdentityMismatch,
                _ => SharedProviderSourceAvailability.Available
            },
            Scenario switch {
                SharingScenario.Offline => "The remote source is unavailable.",
                SharingScenario.AuthorizationFailure => "The remote source refused the credential.",
                SharingScenario.IdentityMismatch => "The responding instance has a different identity.",
                _ => string.Empty
            },
            source.Id == Store.Import.SourceId ? Store.Selected.Count : 0)).ToArray();
    }
}
