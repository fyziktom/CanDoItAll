using CanDoItAll.AgentFramework.SharedProviders.UI;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using CanDoItAll.Modules.Security;
using CanDoItAll.SharedProviders.Abstractions;
using Microsoft.AspNetCore.Components;
using CanDoItAll.Editing;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components;

public partial class SharedProviderSourcesDialog : ISharedProviderSourcesView, IDisposable, IAsyncDisposable {
    [Inject]
    public ISharedProviderManagementService ManagementService { get; set; } = default!;

    [Inject] public SharedProviderRecovery Recovery { get; set; } = default!;
    [Inject] public ProviderEditingChanges Editing { get; set; } = default!;

    [Inject]
    public NotificationService NotificationService { get; set; } = default!;

    [Parameter]
    public IReadOnlyList<SecretListItem> Secrets { get; set; } = [];

    [Parameter] public string? SecretMetadataError { get; set; }
    [Parameter] public ProviderEditingOpening? OriginalOpening { get; set; }

    [Parameter]
    public EventCallback<SharedProviderChangeDelivery> ProvidersChanged { get; set; }

    [Parameter]
    public EventCallback OnClose { get; set; }

    private IReadOnlyList<SharedProviderSourceManagementSnapshot> sources = [];
    private SharedProviderSourceDraft? sourceEditor;
    private SharedProviderCatalogSelection? catalog;
    private SharedProviderSourceConfirmation? confirmation;
    private readonly Guid viewId = Guid.NewGuid();
    private Guid snapshotId = Guid.NewGuid();
    private (SharedProviderSourceMutationAttempt Attempt, SharedProviderSourceDraft Draft, SharedProviderSourceSubmission Submission)? sourceSubmission;
    private bool sourceWriteCommitted;
    private (SharedProviderSourceMutationAttempt Attempt, SharedProviderCatalogSelection Dialog, SharedProviderCatalogSubmission Submission)? catalogSubmission;
    private string sourceDialogError = string.Empty;
    private string loadError = string.Empty;
    private bool isLoading;
    private bool readFailed;
    private bool operationBusy;
    private bool hasPendingAttempt => Recovery.Source is not null;
    private bool isBusy => !admitted || operationBusy || hasPendingAttempt || readFailed || sourceWriteCommitted;
    private readonly CancellationTokenSource lifetime = new();
    private CancellationToken ownerToken;
    private long generation;
    private long readGeneration;
    private bool disposed;
    private bool sourceDialogOpen;
    private ProviderEditingOpening? opening;
    private bool admitted;
    private bool catalogChanged;
    private long sourceChangeSequence;
    private Guid? verifiedSourceAttempt;
    private long verifiedSourceSequence;

    public SharedProviderSourcesPresentation Presentation => new(viewId,
        admitted ? sources.Select(item => SharedProviderPresentationMapper.Source(Origin(item), item)).ToArray() : [],
        admitted ? Secrets.Select(secret => new SharedProviderCredentialChoice(secret.Id, secret.Name)).ToArray() : [], SecretMetadataError,
        isLoading, operationBusy, isBusy || isLoading, loadError, sourceDialogOpen && admitted ? sourceEditor : null, sourceDialogError, admitted ? catalog : null,
        admitted && Recovery.Source is { } pending ? new(pending.AttemptId, pending.SourceId,
            Recovery.PendingDelivery(pending.AttemptId) is not null, CanAcknowledgeStatus(pending),
            Recovery.PendingDelivery(pending.AttemptId) is null &&
                (pending.Kind is SharedProviderSourceMutationKind.Create or SharedProviderSourceMutationKind.Update) &&
                sources.Any(source => source.Source.Id == pending.SourceId), Recovery.SourceCatalog is not null) : null, confirmation) { CatalogChanged = catalogChanged };

    private SharedProviderSourceOrigin Origin(SharedProviderSourceManagementSnapshot source) => new(viewId, snapshotId,
        source.Source.Id, source.Source.ConcurrencyToken) { Observation = source.Observation };

    private SharedProviderSourceManagementSnapshot? Resolve(SharedProviderSourceOrigin origin) =>
        !disposed && origin.ViewId == viewId && origin.SnapshotId == snapshotId
            ? sources.SingleOrDefault(item => item.Source.Id == origin.SourceId && item.Source.ConcurrencyToken == origin.ConcurrencyToken) : null;

    public Task RefreshAsync(Guid origin) => origin == viewId && !operationBusy ? LoadAsync() : Task.CompletedTask;
    public Task CloseAsync(Guid origin) => origin == viewId && !disposed ? CloseOverlayAsync() : Task.CompletedTask;
    public Task VerifyAsync(Guid attemptId) => Recovery.Source?.AttemptId == attemptId ? VerifySourceAsync() : Task.CompletedTask;
    private bool CanAcknowledgeStatus(SharedProviderSourceMutationAttempt attempt) => !readFailed &&
        verifiedSourceAttempt == attempt.AttemptId && verifiedSourceSequence == sourceChangeSequence &&
        Recovery.PendingDelivery(attempt.AttemptId) is null &&
        (attempt.Kind is SharedProviderSourceMutationKind.Enablement or SharedProviderSourceMutationKind.Delete or SharedProviderSourceMutationKind.Test ||
            attempt.Kind == SharedProviderSourceMutationKind.Synchronize && Recovery.SourceCatalog is null);

    public async Task AcknowledgeCurrentAsync(Guid attemptId) {
        if (disposed || operationBusy || Recovery.Source is not { } attempt || attempt.AttemptId != attemptId ||
            !CanAcknowledgeStatus(attempt) || !await AuthorizeLocalReviewAsync() || !CanAcknowledgeStatus(attempt)) {
            return;
        }
        if (Recovery.AcknowledgeSourceStatus(attempt)) {
            verifiedSourceAttempt = null;
            loadError = "Current state acknowledged. The earlier outcome remains unconfirmed; no operation was repeated.";
        }
    }

    protected override async Task OnInitializedAsync() {
        ownerToken = lifetime.Token;
        if (!RendererInfo.IsInteractive) {
            RetireAccess();
            return;
        }
        opening = Editing.Open(viewId, () => !disposed, EditingOwner.SharedProviderSource, OriginalOpening);
        try {
            await opening.StartAsync(OnSourcesChangedAsync, () => {
                RetireAccess();
                return InvokeAsync(StateHasChanged);
            });
            Recovery.Bind(new(opening.Scope, opening.PrincipalStamp));
            admitted = true;
            catalog = Recovery.SourceCatalog;
            await LoadAsync();
        } catch (UnauthorizedAccessException) {
            RetireAccess();
        }
    }

    private Task OnSourcesChangedAsync(EditingChangeBatch batch) => InvokeAsync(() => {
        if (disposed || !admitted || !batch.RequiresResync && !batch.Changes.Any(change => change.OriginEditorId != viewId)) {
            return;
        }
        sourceChangeSequence++;
        catalogChanged = true;
        if (catalog is { } selection && (batch.RequiresResync || batch.Changes.Any(change =>
            change.OriginEditorId != viewId && change.Resource.ResourceId == selection.Origin.SourceId))) {
            selection.ReadbackConflict = true;
            selection.Comparison?.Invalidate();
        }
        if (sourceEditor is { } draft && (draft.Original is not null || Recovery.Source?.SourceId == draft.SourceId) &&
            (batch.RequiresResync || batch.Changes.Any(change => change.OriginEditorId != viewId && change.Resource.ResourceId == draft.SourceId))) {
            draft.ReadbackConflict = true;
            draft.Comparison?.Invalidate();
        }
        StateHasChanged();
    });

    private void RetireAccess() {
        admitted = false;
        opening?.Stop();
        sources = [];
        sourceDialogOpen = false;
        catalog = null;
        confirmation = null;
        readFailed = true;
        loadError = "The original source workspace no longer has access. Reopen it with current access; retained attempts are not replayed.";
    }

    private Task<T> ReadAsync<T>(Func<CancellationToken, Task<T>> read, CancellationToken token)
        => (opening ?? throw new UnauthorizedAccessException("Original source access is unavailable.")).ReadAsync(read, token);

    private async Task LoadAsync() {
        if (disposed || !admitted) {
            return;
        }
        using var request = CancellationTokenSource.CreateLinkedTokenSource(ownerToken);
        var read = ++readGeneration;
        var coveredSequence = sourceChangeSequence;
        isLoading = true;
        readFailed = false;
        loadError = string.Empty;
        try {
            var result = await ReadAsync(ManagementService.ListSourcesAsync, request.Token);
            if (!disposed && read == readGeneration) {
                sources = result;
                snapshotId = Guid.NewGuid();
                confirmation = null;
                ReconcileSourceDraft();
                ReconcileCatalog();
                catalogChanged = coveredSequence != sourceChangeSequence;
            }
        } catch (OperationCanceledException) when (ownerToken.IsCancellationRequested) {
        } catch (Exception) {
            if (!disposed && read == readGeneration) {
                loadError = "Shared-provider connections could not be loaded.";
                readFailed = true;
            }
        } finally {
            if (!disposed && read == readGeneration) {
                isLoading = false;
            }
        }
    }

    private bool IsCurrent(long operation) => !disposed && admitted && operation == generation;

    private async Task CloseOverlayAsync() {
        Dispose();
        await OnClose.InvokeAsync();
    }

    public void Dispose() {
        if (disposed) {
            return;
        }
        disposed = true;
        opening?.Stop();
        lifetime.Cancel();
        lifetime.Dispose();
        sourceDialogOpen = false;
        catalog = null;
        confirmation = null;
    }

    public async ValueTask DisposeAsync() {
        Dispose();
        if (opening is not null) {
            await opening.DisposeAsync();
        }
    }

    public void NewSource(Guid origin) {
        if (origin != viewId || disposed || isBusy || isLoading) {
            return;
        }
        sourceEditor = new(Guid.NewGuid(), Guid.Empty, new(string.Empty, string.Empty,
            SecretMetadataError is null && Secrets.Count == 1 ? Secrets[0].Id : Guid.Empty, true, false));
        sourceDialogError = string.Empty;
        sourceDialogOpen = true;
    }

    private void OpenEditSourceDialog(SharedProviderSourceSnapshot source) {
        if (disposed) {
            return;
        }
        sourceEditor = new(source.Id, source.ConcurrencyToken, SharedProviderPresentationMapper.SourceValues(source));
        sourceDialogError = string.Empty;
        sourceDialogOpen = true;
    }

    public void CloseEditor(Guid draftId) {
        if (sourceEditor?.Id != draftId) {
            return;
        }
        sourceDialogOpen = false;
        sourceDialogError = string.Empty;
    }

    public async Task SaveAsync(SharedProviderSourceSubmission submission) {
        if (disposed || isBusy || isLoading || !sourceDialogOpen || sourceEditor is not { } draft || !draft.CanSubmit(submission)) {
            return;
        }
        sourceDialogError = string.Empty;
        if (SecretMetadataError is not null || !Secrets.Any(secret => secret.Id == submission.Values.CredentialReference)) {
            sourceDialogError = SecretMetadataError is not null ? "Credential metadata is unavailable. Refresh it before saving."
                : "The stored credential reference is unavailable. Select an available credential.";
            return;
        }
        var values = submission.Values;
        var request = new SharedProviderSourceEditorRequest(
            submission.SourceId, submission.ExpectedToken, values.Name, new Uri(values.BaseUri.Trim(), UriKind.Absolute),
            values.CredentialReference, values.IsEnabled, values.AllowPrivateNetwork);
        var attempt = new SharedProviderSourceMutationAttempt(request.Id!.Value,
            request.ExpectedConcurrencyToken is null || request.ExpectedConcurrencyToken == Guid.Empty ? SharedProviderSourceMutationKind.Create : SharedProviderSourceMutationKind.Update,
            sources.SingleOrDefault(source => source.Source.Id == request.Id), request);
        sourceSubmission = (attempt, draft, submission);
        sourceWriteCommitted = false;
        await SaveSourceAttemptAsync(attempt);
    }

    private Task SaveSourceAttemptAsync(SharedProviderSourceMutationAttempt attempt) =>
        RunSourceMutationAsync(async token => {
            var result = await ManagementService.SaveSourceAsync(attempt.Request!, token);
            if (result.Id != attempt.SourceId) {
                throw new InvalidOperationException("The source result has a different identity.");
            }
            Recovery.RecordCommit(attempt.AttemptId, result.Change);
            result = await ReadAsync(_ => Task.FromResult(result), token);
            if (sourceSubmission is { } submitted && submitted.Attempt.AttemptId == attempt.AttemptId) {
                sourceWriteCommitted = true;
                if (!disposed && ReferenceEquals(sourceEditor, submitted.Draft)) {
                    submitted.Draft.AcceptIdentity(submitted.Submission, result.Id, result.ConcurrencyToken);
                    if (result.AcceptedSource is { } accepted && accepted.Id == result.Id && accepted.ConcurrencyToken == result.ConcurrencyToken) {
                        var unchanged = submitted.Draft.Accept(submitted.Submission, accepted.ConcurrencyToken,
                            SharedProviderPresentationMapper.SourceValues(accepted));
                        sourceDialogOpen = sourceDialogOpen && !unchanged;
                    } else {
                        submitted.Draft.ReadbackConflict = true;
                    }
                }
            }
            return result.Change;
        }, "Source saved", attempt);

    private void ReconcileSourceDraft() {
        if (sourceEditor is { } draft && sources.SingleOrDefault(source => source.Source.Id == draft.SourceId)?.Source is { } current) {
            draft.Observe(current.ConcurrencyToken, SharedProviderPresentationMapper.SourceValues(current));
        }
        if (sourceWriteCommitted && sourceEditor?.ReadbackConflict == true) {
            sourceDialogError = "The source is saved, but its current version requires comparison. Your draft is retained.";
        }
        sourceSubmission = null;
        sourceWriteCommitted = false;
    }

    public async Task CompareSourceAsync(Guid draftId) {
        if (disposed || operationBusy || sourceEditor?.Id != draftId) {
            return;
        }
        var sequence = sourceChangeSequence;
        await LoadAsync();
        if (!disposed && admitted && !readFailed && sequence == sourceChangeSequence && sourceEditor?.Id == draftId) {
            sourceEditor.Compare();
        }
    }

    public async Task ApplySourceComparisonAsync(Guid draftId) {
        if (disposed || operationBusy || sourceEditor?.Id != draftId || !await AuthorizeLocalReviewAsync()) {
            return;
        }
        if (!readFailed && sourceEditor.ApplyComparison()) {
            if (Recovery.Source is { } pending && pending.SourceId == sourceEditor.SourceId && Recovery.AcknowledgeSourceCurrent(pending)) {
                sourceSubmission = null;
                sourceWriteCommitted = false;
                loadError = "The earlier source outcome remains unconfirmed. Review this draft and use Save separately.";
            }
            sourceDialogError = string.Empty;
        }
    }

    public void CancelSourceComparison(Guid draftId) {
        if (sourceEditor?.Id == draftId) {
            sourceEditor.CancelComparison();
        }
    }

    public async Task ReviewPendingSourceAsync(Guid attemptId) {
        if (disposed || operationBusy || Recovery.Source is not { } attempt || attempt.AttemptId != attemptId ||
            attempt.Kind is not (SharedProviderSourceMutationKind.Create or SharedProviderSourceMutationKind.Update)) {
            return;
        }
        await LoadAsync();
        if (disposed || readFailed || Recovery.Source?.AttemptId != attemptId ||
            sources.SingleOrDefault(source => source.Source.Id == attempt.SourceId)?.Source is not { } current) {
            return;
        }
        var draft = Recovery.SourceDraft;
        if (draft is null) {
            if (attempt.Request is not { } submitted) {
                loadError = "The original source submission is unavailable. This attempt remains unconfirmed.";
                return;
            }
            var local = new SharedProviderSourceValues(submitted.Name, submitted.BaseUri.AbsoluteUri,
                submitted.ApiTokenSecretId, submitted.IsEnabled, submitted.AllowInsecurePrivateNetwork);
            draft = new(attempt.SourceId, attempt.Before?.Source.ConcurrencyToken ?? Guid.Empty,
                attempt.Before is { } before ? SharedProviderPresentationMapper.SourceValues(before.Source) : local) {
                Name = local.Name,
                BaseUri = local.BaseUri,
                CredentialReference = local.CredentialReference,
                IsEnabled = local.IsEnabled,
                AllowPrivateNetwork = local.AllowPrivateNetwork
            };
            Recovery.RetainSourceDraft(attempt, draft);
        }
        sourceEditor = draft;
        sourceDialogOpen = true;
        draft.Observe(current.ConcurrencyToken, SharedProviderPresentationMapper.SourceValues(current));
        draft.Compare();
    }

    public Task ExecuteAsync(SharedProviderSourceIntent intent) {
        if (Resolve(intent.Origin) is not { } source || (intent.Action != SharedProviderSourceAction.Edit && (isBusy || isLoading))) {
            return Task.CompletedTask;
        }
        switch (intent.Action) {
            case SharedProviderSourceAction.Edit:
                OpenEditSourceDialog(source.Source);
                return Task.CompletedTask;
            case SharedProviderSourceAction.Test:
                return TestSourceAsync(source.Source.Id);
            case SharedProviderSourceAction.Discover when source.Source.IsEnabled:
                return DiscoverSourceAsync(source);
            case SharedProviderSourceAction.Synchronize when source.Source.IsEnabled && source.Imports.Count > 0:
                return SynchronizeExistingAsync(source);
            case SharedProviderSourceAction.ToggleEnabled:
                return ToggleSourceAsync(source.Source);
            case SharedProviderSourceAction.Delete when source.Imports.Count == 0:
                confirmation = new(Guid.NewGuid(), intent.Origin, source.Source.Name);
                return Task.CompletedTask;
            default:
                return Task.CompletedTask;
        }
    }

    public Task ConfirmDeleteAsync(SharedProviderSourceConfirmation requested) => requested == confirmation &&
        !isBusy && !isLoading && Resolve(requested.Origin) is { Imports.Count: 0 } source
            ? DeleteSourceAsync(source.Source) : Task.CompletedTask;

    public void CloseConfirmation(SharedProviderSourceConfirmation requested) {
        if (requested == confirmation) {
            confirmation = null;
        }
    }

    private Task ToggleSourceAsync(SharedProviderSourceSnapshot source) {
        var attempt = new SharedProviderSourceMutationAttempt(source.Id, SharedProviderSourceMutationKind.Enablement,
            sources.Single(item => item.Source.Id == source.Id), intendedEnabled: !source.IsEnabled);
        return RunSourceMutationAsync(async token =>
            (await ManagementService.SetSourceEnabledAsync(source.Id, source.ConcurrencyToken, !source.IsEnabled, token)).Change,
            source.IsEnabled ? "Source disabled" : "Source enabled", attempt);
    }

    private Task DeleteSourceAsync(SharedProviderSourceSnapshot source) {
        var attempt = new SharedProviderSourceMutationAttempt(source.Id, SharedProviderSourceMutationKind.Delete,
            sources.Single(item => item.Source.Id == source.Id));
        return RunSourceMutationAsync(async token =>
            (await ManagementService.DeleteSourceAsync(source.Id, source.ConcurrencyToken, token)).Change, "Source deleted", attempt);
    }

    private async Task RunSourceMutationAsync(
        Func<CancellationToken, Task<SharedProviderChange?>> mutation, string successTitle,
        SharedProviderSourceMutationAttempt attempt) {
        if (disposed || operationBusy || hasPendingAttempt) {
            return;
        }
        using var request = CancellationTokenSource.CreateLinkedTokenSource(ownerToken);
        var operation = ++generation;
        operationBusy = true;
        SharedProviderChange? committed = null;
        try {
            var change = await opening!.WriteAsync(async () => {
                Recovery.BeginSource(attempt);
                if (sourceSubmission is { } submitted && submitted.Attempt.AttemptId == attempt.AttemptId) {
                    Recovery.RetainSourceDraft(attempt, submitted.Draft);
                }
                return await mutation(request.Token);
            }, () => IsCurrent(operation), request.Token);
            committed = change;
            Recovery.RecordCommit(attempt.AttemptId, change);
            if (!IsCurrent(operation)) {
                return;
            }
            Recovery.CompleteSource(attempt);
            if (!await PublishChangeAsync(change, operation, attempt.AttemptId) || !IsCurrent(operation)) {
                return;
            }
            await LoadAsync();
            if (IsCurrent(operation)) {
                if (change?.Warning is { } warning) {
                    NotificationService.Warning(successTitle, warning);
                } else {
                    NotificationService.Success(successTitle, "The authoritative source state was saved.");
                }
            }
        } catch (OperationCanceledException) when (ownerToken.IsCancellationRequested) {
        } catch (Exception exception) {
            if (exception is UnauthorizedAccessException && Recovery.PendingDelivery(attempt.AttemptId) is null) {
                Recovery.CompleteSource(attempt);
            }
            if (exception is SharedProviderCommittedException saved) {
                Recovery.RecordCommit(attempt.AttemptId, saved.Change);
            }
            if (IsCurrent(operation)) {
                if (committed is not null) {
                    loadError = "The shared-provider change is saved, but its workspace refresh did not complete.";
                } else {
                    await HandleOperationFailureAsync(exception, operation, attempt);
                }
            }
        } finally {
            if (IsCurrent(operation)) {
                operationBusy = false;
            }
        }
    }

    private async Task<bool> PublishChangeAsync(SharedProviderChange? change, long operation, Guid? attemptId = null) {
        if (!IsCurrent(operation)) {
            return false;
        }
        if (change is null) {
            return true;
        }
        if (!attemptId.HasValue) {
            try {
                await ProvidersChanged.InvokeAsync(new SharedProviderChangeDelivery(Guid.NewGuid(), change));
                return IsCurrent(operation);
            } catch (Exception) {
                if (IsCurrent(operation)) {
                    loadError = "The write remains unconfirmed and its advisory workspace refresh did not complete. Verify the source attempt.";
                }
                return false;
            }
        }
        if (Recovery.Source is not { } attempt || attempt.AttemptId != attemptId) {
            return false;
        }
        var result = await Recovery.DeliverSourceAsync(attempt, attempt.SourceId, () => IsCurrent(operation),
            delivery => ProvidersChanged.InvokeAsync(delivery));
        if (IsCurrent(operation) && result != SharedProviderDeliveryDisposition.Acknowledged) {
            loadError = DeliveryPendingMessage;
        }
        return result == SharedProviderDeliveryDisposition.Acknowledged;
    }

    private const string DeliveryPendingMessage =
        "The source write is resolved, but workspace reconciliation delivery is pending. Retry delivery without repeating the write.";

    private async Task HandleOperationFailureAsync(Exception exception, long operation, SharedProviderSourceMutationAttempt attempt) {
        if (exception is SharedProviderCommittedException committed) {
            sourceWriteCommitted = sourceSubmission?.Attempt.AttemptId == attempt.AttemptId;
            if (catalogSubmission?.Attempt.AttemptId == attempt.AttemptId && ReferenceEquals(catalog, catalogSubmission.Value.Dialog)) {
                catalog!.ReadbackConflict = true;
            }
            Recovery.RecordCommit(attempt.AttemptId, committed.Change);
            Recovery.CompleteSource(attempt);
            loadError = committed.Change.Warning!;
            await PublishChangeAsync(committed.Change, operation, attempt.AttemptId);
            if (IsCurrent(operation)) {
                await LoadAsync();
            }
            return;
        }
        var rejected = exception is SharedProviderSourceDeletionBlockedException or ArgumentException or KeyNotFoundException or UnauthorizedAccessException ||
            (exception is SharedProviderConcurrencyException && attempt.Kind != SharedProviderSourceMutationKind.Create);
        if (rejected) {
            Recovery.CompleteSource(attempt);
            if (sourceSubmission?.Attempt.AttemptId == attempt.AttemptId) {
                sourceWriteCommitted = false;
            }
            if (catalogSubmission?.Attempt.AttemptId == attempt.AttemptId) {
                if (ReferenceEquals(catalog, catalogSubmission.Value.Dialog)) {
                    catalog!.ReadbackConflict = true;
                    catalog.Comparison?.Invalidate();
                }
                catalogSubmission = null;
            }
        }
        var message = rejected ? "The source change was rejected. Reload current source state and correct the request."
            : "The source outcome is unconfirmed. Verify its state before repeating the operation.";
        loadError = message;
        if (sourceSubmission is { } submitted && submitted.Attempt.AttemptId == attempt.AttemptId && ReferenceEquals(sourceEditor, submitted.Draft)) {
            submitted.Draft.ReadbackConflict = true;
            sourceDialogError = message;
        }
        NotificationService.Warning("Source change needs attention", message);
        if (!rejected) {
            await PublishChangeAsync(new(SharedProviderChangeKind.SourceAvailability, [],
                commitState: SharedProviderCommitState.Unconfirmed, unknownScope: true, warning: message), operation);
        }
    }

    private async Task TestSourceAsync(Guid sourceId) {
        var operation = generation + 1;
        var result = await RunSourceOperationAsync(
            token => ManagementService.TestSourceAsync(sourceId, token),
            new(sourceId, SharedProviderSourceMutationKind.Test, sources.Single(item => item.Source.Id == sourceId)));
        if (!IsCurrent(operation)) {
            return;
        }
        if (result?.Outcome == SharedProviderSourceOperationOutcome.Succeeded) {
            NotificationService.Success(
                "Source connection passed",
                $"The catalog contains {result.Catalog!.Providers.Count} published provider(s).");
        }
    }

    private async Task DiscoverSourceAsync(SharedProviderSourceManagementSnapshot source) {
        var operation = generation + 1;
        var result = await RunSourceOperationAsync(
            token => ManagementService.TestSourceAsync(source.Source.Id, token),
            new(source.Source.Id, SharedProviderSourceMutationKind.Test, source));
        if (!IsCurrent(operation)) {
            return;
        }
        if (result?.Outcome != SharedProviderSourceOperationOutcome.Succeeded ||
            result.Catalog is null) {
            return;
        }

        if (readFailed || sources.SingleOrDefault(item => item.Source.Id == source.Source.Id) is not { } accepted) {
            return;
        }
        catalog = new(Origin(accepted), accepted.Source.Name, result.Catalog.Providers,
            accepted.Imports.Where(import => import.SelectionState == SharedProviderSelectionState.Selected)
                .Select(import => import.RemotePublicationId));
    }

    private async Task SynchronizeExistingAsync(SharedProviderSourceManagementSnapshot source) {
        var operation = generation + 1;
        var selected = source.Imports
            .Where(import => import.SelectionState == SharedProviderSelectionState.Selected)
            .Select(import => import.RemotePublicationId)
            .ToHashSet();
        var result = await RunSourceOperationAsync(
            token => ManagementService.SynchronizeSourceAsync(source.Source.Id, source.Observation, selected, token),
            new(source.Source.Id, SharedProviderSourceMutationKind.Synchronize, source, selection: selected));
        if (!IsCurrent(operation)) {
            return;
        }
        if (result?.IsSuccessful == true) {
            NotificationService.Success("Source synchronized", DescribeSourceOperation(result));
        }
    }

    public async Task ApplyCatalogAsync(SharedProviderCatalogSubmission submission) {
        if (isBusy || isLoading || catalog is not { } dialog || !dialog.IsCurrent(submission) ||
            sources.SingleOrDefault(item => item.Source.Id == submission.Origin.SourceId) is not { Source.IsEnabled: true } source) {
            return;
        }

        var operation = generation + 1;
        var attempt = new SharedProviderSourceMutationAttempt(submission.Origin.SourceId,
            SharedProviderSourceMutationKind.Synchronize, source, selection: submission.Selection);
        catalogSubmission = (attempt, dialog, submission);
        var result = await RunSourceOperationAsync(
            token => ManagementService.SynchronizeSourceAsync(submission.Origin.SourceId,
                submission.Origin.Observation ?? throw new InvalidOperationException("The original source selection is unavailable."), submission.Selection, token), attempt);
        if (!IsCurrent(operation)) {
            return;
        }
        if (result?.IsSuccessful != true) {
            return;
        }

        NotificationService.Success("Shared providers imported", DescribeSourceOperation(result));
    }

    private void ReconcileCatalog() {
        if (catalog is not { } dialog) {
            return;
        }
        var current = sources.SingleOrDefault(item => item.Source.Id == dialog.Origin.SourceId);
        if (current is null) {
            dialog.ReadbackConflict = true;
            dialog.Comparison?.Invalidate();
        } else {
            dialog.Observe(Origin(current));
        }
    }

    public async Task CompareCatalogAsync(Guid dialogId) {
        if (disposed || operationBusy || catalog?.Id != dialogId) {
            return;
        }
        var sequence = sourceChangeSequence;
        await LoadAsync();
        if (!disposed && admitted && !readFailed && sequence == sourceChangeSequence && catalog is { } dialog && dialog.Id == dialogId &&
            sources.SingleOrDefault(item => item.Source.Id == dialog.Origin.SourceId) is { } current) {
            dialog.Compare(Origin(current), current.Imports.Where(import => import.SelectionState == SharedProviderSelectionState.Selected)
                .Select(import => import.RemotePublicationId));
        }
    }

    public async Task ReviewPendingCatalogAsync(Guid attemptId) {
        if (disposed || operationBusy || Recovery.Source?.AttemptId != attemptId || Recovery.SourceCatalog is not { } retained) {
            return;
        }
        catalog = retained;
        await CompareCatalogAsync(retained.Id);
    }

    public async Task ApplyCatalogComparisonAsync(Guid dialogId) {
        if (disposed || operationBusy || readFailed || catalog?.Id != dialogId || !await AuthorizeLocalReviewAsync() ||
            Recovery.Source is { } pendingDelivery && Recovery.PendingDelivery(pendingDelivery.AttemptId) is not null ||
            !catalog.ApplyComparison()) {
            return;
        }
        if (Recovery.Source is { Kind: SharedProviderSourceMutationKind.Synchronize } pending && pending.SourceId == catalog.Origin.SourceId) {
            Recovery.AcknowledgeSourceCurrent(pending);
            loadError = "The earlier synchronization remains unconfirmed. Your reviewed selection is local; Apply selection starts a separate operation.";
        }
        catalogSubmission = null;
    }

    public void CancelCatalogComparison(Guid dialogId) {
        if (catalog?.Id == dialogId) {
            catalog.CancelComparison();
        }
    }

    private async Task<SharedProviderSourceOperationResult?> RunSourceOperationAsync(
        Func<CancellationToken, Task<SharedProviderSourceOperationResult>> run,
        SharedProviderSourceMutationAttempt attempt) {
        if (disposed || operationBusy || hasPendingAttempt) {
            return null;
        }
        using var request = CancellationTokenSource.CreateLinkedTokenSource(ownerToken);
        var operation = ++generation;
        operationBusy = true;
        SharedProviderChange? committed = null;
        try {
            var result = await opening!.WriteAsync(async () => {
                Recovery.BeginSource(attempt);
                if (catalogSubmission is { } submitted && submitted.Attempt.AttemptId == attempt.AttemptId) {
                    Recovery.RetainSourceCatalog(attempt, submitted.Dialog);
                }
                var completed = await run(request.Token);
                Recovery.RecordCommit(attempt.AttemptId, completed.Change);
                return await ReadAsync(_ => Task.FromResult(completed), request.Token);
            }, () => IsCurrent(operation), request.Token);
            committed = result.Change;
            if (!IsCurrent(operation)) {
                return null;
            }
            if (catalogSubmission?.Attempt.AttemptId == attempt.AttemptId) {
                if (result.IsSuccessful && result.AcceptedObservation is { } accepted && ReferenceEquals(catalog, catalogSubmission.Value.Dialog)) {
                    var submitted = catalogSubmission.Value;
                    if (submitted.Dialog.Accept(submitted.Submission,
                        new(viewId, Guid.NewGuid(), attempt.SourceId, accepted.ConcurrencyToken) { Observation = accepted })) {
                        catalog = null;
                    }
                } else if (catalog is { } retained && ReferenceEquals(retained, catalogSubmission.Value.Dialog)) {
                    retained.ReadbackConflict = true;
                }
                catalogSubmission = null;
            }
            Recovery.RecordCommit(attempt.AttemptId, result.Change);
            if (!IsCurrent(operation)) {
                return null;
            }
            Recovery.CompleteSource(attempt);
            if (!await PublishChangeAsync(result.Change, operation, attempt.AttemptId) || !IsCurrent(operation)) {
                return null;
            }
            await LoadAsync();
            if (!IsCurrent(operation)) {
                return null;
            }
            if (result.Change?.Warning is { } warning) {
                NotificationService.Warning("Shared-provider change saved", warning);
            }
            if (!result.IsSuccessful) {
                NotificationService.Warning("Shared-provider source is unavailable",
                    result.Failure?.SanitizedMessage ?? FormatStatus(result.Outcome));
            }
            return result;
        } catch (OperationCanceledException) when (ownerToken.IsCancellationRequested) {
            return null;
        } catch (Exception exception) {
            if (exception is UnauthorizedAccessException && Recovery.PendingDelivery(attempt.AttemptId) is null) {
                Recovery.CompleteSource(attempt);
            }
            if (exception is SharedProviderCommittedException saved) {
                Recovery.RecordCommit(attempt.AttemptId, saved.Change);
            }
            if (IsCurrent(operation)) {
                if (committed is not null) {
                    loadError = "The shared-provider change is saved, but its workspace refresh did not complete.";
                } else {
                    await HandleOperationFailureAsync(exception, operation, attempt);
                }
            }
            return null;
        } finally {
            if (IsCurrent(operation)) {
                operationBusy = false;
            }
        }
    }

    private async Task VerifySourceAsync() {
        if (disposed || operationBusy || Recovery.Source is not { } attempt) {
            return;
        }
        using var request = CancellationTokenSource.CreateLinkedTokenSource(ownerToken);
        var operation = ++generation;
        operationBusy = true;
        var coveredSequence = sourceChangeSequence;
        try {
            if (Recovery.PendingDelivery(attempt.AttemptId) is { } delivery) {
                if (!await AuthorizeLocalReviewAsync()) {
                    return;
                }
                if (await PublishChangeAsync(delivery.Change, operation, attempt.AttemptId) && IsCurrent(operation)) {
                    await LoadAsync();
                }
                return;
            }
            var result = await ReadAsync(token => ManagementService.VerifySourceAsync(attempt, token), request.Token);
            if (!IsCurrent(operation) || result.SourceId != attempt.SourceId || Recovery.Source?.AttemptId != attempt.AttemptId) {
                return;
            }
            sources = result.Sources;
            snapshotId = Guid.NewGuid();
            readFailed = false;
            confirmation = null;
            verifiedSourceAttempt = coveredSequence == sourceChangeSequence ? attempt.AttemptId : null;
            verifiedSourceSequence = coveredSequence;
            loadError = result.CurrentStateMatchesIntent
                ? "Current values match the requested state, but the earlier outcome remains unconfirmed. Review current state explicitly; verification did not repeat the operation."
                : "Current state was read. The earlier outcome remains unconfirmed. Review it before a separate operation; verification did not repeat it.";
            ReconcileSourceDraft();
            ReconcileCatalog();
        } catch (OperationCanceledException) when (ownerToken.IsCancellationRequested) {
        } catch (Exception) {
            if (IsCurrent(operation)) {
                loadError = "Canonical source verification failed. This attempt remains blocked; no source write was repeated.";
            }
        } finally {
            if (IsCurrent(operation)) {
                operationBusy = false;
            }
        }
    }

    private async Task<bool> AuthorizeLocalReviewAsync() {
        try {
            return await ReadAsync(_ => Task.FromResult(true), ownerToken);
        } catch (UnauthorizedAccessException) {
            RetireAccess();
            return false;
        }
    }

    public void CloseCatalog(Guid dialogId) {
        if (catalog?.Id == dialogId) {
            catalog = null;
        }
    }

    private static string DescribeSourceOperation(SharedProviderSourceOperationResult result) {
        if (result.Outcome == SharedProviderSourceOperationOutcome.NotModified) {
            return "The remote catalog has not changed.";
        }

        return $"Updated {result.AffectedProviderProfileIds.Count} profile(s) and retired {result.RetiredProviderProfileIds.Count} profile(s).";
    }

    private static string FormatStatus<T>(T value) where T : struct, Enum {
        var text = value.ToString();
        return string.Concat(text.Select((character, index) =>
            index > 0 && char.IsUpper(character)
                ? $" {char.ToLowerInvariant(character)}"
                : char.ToLowerInvariant(character).ToString()));
    }

}
