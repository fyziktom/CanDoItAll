using CanDoItAll.AgentFramework.SharedProviders.UI;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using CanDoItAll.Modules.Security;
using CanDoItAll.SharedProviders.Abstractions;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components;

public partial class SharedProviderSourcesDialog : ISharedProviderSourcesView, IDisposable {
    [Inject]
    public ISharedProviderManagementService ManagementService { get; set; } = default!;

    [Inject] public SharedProviderRecovery Recovery { get; set; } = default!;

    [Inject]
    public NotificationService NotificationService { get; set; } = default!;

    [Parameter]
    public IReadOnlyList<SecretListItem> Secrets { get; set; } = [];

    [Parameter] public string? SecretMetadataError { get; set; }

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
    private bool catalogWriteCommitted;
    private string sourceDialogError = string.Empty;
    private string loadError = string.Empty;
    private bool isLoading;
    private bool readFailed;
    private bool operationBusy;
    private bool hasPendingAttempt => Recovery.Source is not null;
    private bool isBusy => operationBusy || hasPendingAttempt || readFailed || sourceWriteCommitted || catalogWriteCommitted;
    private readonly CancellationTokenSource lifetime = new();
    private CancellationToken ownerToken;
    private long generation;
    private long readGeneration;
    private bool disposed;
    private bool sourceDialogOpen;

    public SharedProviderSourcesPresentation Presentation => new(viewId,
        sources.Select(item => SharedProviderPresentationMapper.Source(Origin(item.Source), item)).ToArray(),
        Secrets.Select(secret => new SharedProviderCredentialChoice(secret.Id, secret.Name)).ToArray(), SecretMetadataError,
        isLoading, operationBusy, isBusy || isLoading, loadError, sourceDialogOpen ? sourceEditor : null, sourceDialogError, catalog,
        Recovery.Source is { } pending ? new(pending.AttemptId, pending.SourceId,
            Recovery.PendingDelivery(pending.AttemptId) is not null, Recovery.SourceRetryAllowed) : null, confirmation);

    private SharedProviderSourceOrigin Origin(SharedProviderSourceSnapshot source) => new(viewId, snapshotId, source.Id, source.ConcurrencyToken);

    private SharedProviderSourceManagementSnapshot? Resolve(SharedProviderSourceOrigin origin) =>
        !disposed && origin.ViewId == viewId && origin.SnapshotId == snapshotId
            ? sources.SingleOrDefault(item => item.Source.Id == origin.SourceId && item.Source.ConcurrencyToken == origin.ConcurrencyToken) : null;

    public Task RefreshAsync(Guid origin) => origin == viewId && !operationBusy ? LoadAsync() : Task.CompletedTask;
    public Task CloseAsync(Guid origin) => origin == viewId && !disposed ? CloseOverlayAsync() : Task.CompletedTask;
    public Task VerifyAsync(Guid attemptId) => Recovery.Source?.AttemptId == attemptId ? VerifySourceAsync() : Task.CompletedTask;
    public Task RetryVerifiedAsync(Guid attemptId) => Recovery.Source?.AttemptId == attemptId ? RetryVerifiedSourceAsync() : Task.CompletedTask;

    protected override Task OnInitializedAsync() {
        ownerToken = lifetime.Token;
        return LoadAsync();
    }

    private async Task LoadAsync() {
        if (disposed) {
            return;
        }
        using var request = CancellationTokenSource.CreateLinkedTokenSource(ownerToken);
        var read = ++readGeneration;
        isLoading = true;
        readFailed = false;
        loadError = string.Empty;
        try {
            var result = await ManagementService.ListSourcesAsync(request.Token);
            if (!disposed && read == readGeneration) {
                sources = result;
                snapshotId = Guid.NewGuid();
                confirmation = null;
                ReconcileSourceDraft();
                ReconcileCatalog();
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

    private bool IsCurrent(long operation) => !disposed && operation == generation;

    private async Task CloseOverlayAsync() {
        Dispose();
        await OnClose.InvokeAsync();
    }

    public void Dispose() {
        if (disposed) {
            return;
        }
        disposed = true;
        lifetime.Cancel();
        lifetime.Dispose();
        sourceDialogOpen = false;
        catalog = null;
        confirmation = null;
    }

    public void NewSource(Guid origin) {
        if (origin != viewId || disposed || isBusy || isLoading) {
            return;
        }
        sourceEditor = new(Guid.NewGuid(), null, new(string.Empty, string.Empty,
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
            request.ExpectedConcurrencyToken.HasValue ? SharedProviderSourceMutationKind.Update : SharedProviderSourceMutationKind.Create,
            sources.SingleOrDefault(source => source.Source.Id == request.Id), request);
        sourceSubmission = (attempt, draft, submission);
        sourceWriteCommitted = false;
        await SaveSourceAttemptAsync(attempt);
    }

    private Task SaveSourceAttemptAsync(SharedProviderSourceMutationAttempt attempt, bool controlledRetry = false) =>
        RunSourceMutationAsync(async token => {
            var result = await ManagementService.SaveSourceAsync(attempt.Request!, token);
            if (result.Id != attempt.SourceId) {
                throw new InvalidOperationException("The source result has a different identity.");
            }
            if (sourceSubmission is { } submitted && submitted.Attempt.AttemptId == attempt.AttemptId) {
                sourceWriteCommitted = true;
                if (!disposed && ReferenceEquals(sourceEditor, submitted.Draft)) {
                    submitted.Draft.AcceptIdentity(submitted.Submission, result.Id, result.ConcurrencyToken);
                }
            }
            return result.Change;
        }, "Source saved", attempt, controlledRetry);

    private void ReconcileSourceDraft() {
        if (!sourceWriteCommitted || sourceSubmission is not { } submitted) {
            return;
        }
        var accepted = sources.SingleOrDefault(item => item.Source.Id == submitted.Attempt.SourceId)?.Source;
        if (ReferenceEquals(sourceEditor, submitted.Draft)) {
            if (accepted is not null && SharedProviderSourceVerification.Matches(accepted, submitted.Attempt.Request!)) {
                var unchanged = submitted.Draft.Accept(submitted.Submission, accepted.ConcurrencyToken,
                    SharedProviderPresentationMapper.SourceValues(accepted));
                sourceDialogOpen = sourceDialogOpen && !unchanged;
                sourceDialogError = string.Empty;
            } else {
                submitted.Draft.ReadbackConflict = true;
                sourceDialogError = "The source was saved, then its configuration changed or became unavailable. Your text is retained. Reopen the source to review its current state.";
            }
        }
        sourceSubmission = null;
        sourceWriteCommitted = false;
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
        SharedProviderSourceMutationAttempt attempt, bool controlledRetry = false) {
        if (disposed || operationBusy || (hasPendingAttempt && !controlledRetry)) {
            return;
        }
        Recovery.BeginSource(attempt);
        using var request = CancellationTokenSource.CreateLinkedTokenSource(ownerToken);
        var operation = ++generation;
        operationBusy = true;
        SharedProviderChange? committed = null;
        try {
            var change = await mutation(request.Token);
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
            catalogWriteCommitted = catalogSubmission?.Attempt.AttemptId == attempt.AttemptId;
            Recovery.RecordCommit(attempt.AttemptId, committed.Change);
            Recovery.CompleteSource(attempt);
            loadError = committed.Change.Warning!;
            await PublishChangeAsync(committed.Change, operation, attempt.AttemptId);
            if (IsCurrent(operation)) {
                await LoadAsync();
            }
            return;
        }
        var rejected = exception is SharedProviderSourceDeletionBlockedException or ArgumentException or KeyNotFoundException ||
            (exception is SharedProviderConcurrencyException && attempt.Kind != SharedProviderSourceMutationKind.Create);
        if (rejected) {
            Recovery.CompleteSource(attempt);
            if (sourceSubmission?.Attempt.AttemptId == attempt.AttemptId) {
                sourceWriteCommitted = false;
            }
            if (catalogSubmission?.Attempt.AttemptId == attempt.AttemptId) {
                catalogSubmission = null;
                catalogWriteCommitted = false;
            }
        }
        var message = rejected ? "The source change was rejected. Reload current source state and correct the request."
            : "The source outcome is unconfirmed. Verify its state before repeating the operation.";
        loadError = message;
        if (sourceSubmission is { } submitted && submitted.Attempt.AttemptId == attempt.AttemptId && ReferenceEquals(sourceEditor, submitted.Draft)) {
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
        catalog = new(Origin(accepted.Source), accepted.Source.Name, result.Catalog.Providers,
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
            token => ManagementService.SynchronizeSourceAsync(source.Source.Id, selected, token),
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
            Resolve(submission.Origin) is not { Source.IsEnabled: true } source) {
            return;
        }

        var operation = generation + 1;
        var attempt = new SharedProviderSourceMutationAttempt(submission.Origin.SourceId,
            SharedProviderSourceMutationKind.Synchronize, source, selection: submission.Selection);
        catalogSubmission = (attempt, dialog, submission);
        var result = await RunSourceOperationAsync(
            token => ManagementService.SynchronizeSourceAsync(submission.Origin.SourceId, submission.Selection, token), attempt);
        if (!IsCurrent(operation)) {
            return;
        }
        if (result?.IsSuccessful != true) {
            return;
        }

        NotificationService.Success("Shared providers imported", DescribeSourceOperation(result));
    }

    private void ReconcileCatalog() {
        if (!catalogWriteCommitted || catalogSubmission is not { } submitted) {
            return;
        }
        if (ReferenceEquals(catalog, submitted.Dialog)) {
            var accepted = sources.SingleOrDefault(item => item.Source.Id == submitted.Attempt.SourceId);
            if (accepted is not null && submitted.Submission.Selection.SetEquals(accepted.Imports
                    .Where(item => item.SelectionState == SharedProviderSelectionState.Selected).Select(item => item.RemotePublicationId))) {
                if (submitted.Dialog.Accept(submitted.Submission, Origin(accepted.Source))) {
                    catalog = null;
                }
            } else {
                submitted.Dialog.ReadbackConflict = true;
            }
        }
        catalogSubmission = null;
        catalogWriteCommitted = false;
    }

    private async Task<SharedProviderSourceOperationResult?> RunSourceOperationAsync(
        Func<CancellationToken, Task<SharedProviderSourceOperationResult>> run,
        SharedProviderSourceMutationAttempt attempt, bool controlledRetry = false) {
        if (disposed || operationBusy || (hasPendingAttempt && !controlledRetry)) {
            return null;
        }
        Recovery.BeginSource(attempt);
        using var request = CancellationTokenSource.CreateLinkedTokenSource(ownerToken);
        var operation = ++generation;
        operationBusy = true;
        SharedProviderChange? committed = null;
        try {
            var result = await run(request.Token);
            if (catalogSubmission?.Attempt.AttemptId == attempt.AttemptId) {
                catalogWriteCommitted = result.IsSuccessful;
                if (!result.IsSuccessful) {
                    catalogSubmission = null;
                }
            }
            committed = result.Change;
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
        try {
            if (Recovery.PendingDelivery(attempt.AttemptId) is { } delivery) {
                if (await PublishChangeAsync(delivery.Change, operation, attempt.AttemptId) && IsCurrent(operation)) {
                    await LoadAsync();
                }
                return;
            }
            var result = await ManagementService.VerifySourceAsync(attempt, request.Token);
            if (!IsCurrent(operation) || result.SourceId != attempt.SourceId || Recovery.Source?.AttemptId != attempt.AttemptId) {
                return;
            }
            sources = result.Sources;
            snapshotId = Guid.NewGuid();
            readFailed = false;
            confirmation = null;
            if (result.Disposition == ProviderVerificationDisposition.StillUnconfirmed) {
                loadError = "The exact source attempt is still unconfirmed. Verification did not repeat it. Retain this attempt and retry verification when canonical state is available.";
                return;
            }
            loadError = string.Empty;
            if (result.Disposition == ProviderVerificationDisposition.DefinitelyNotCommitted) {
                Recovery.AllowSourceRetry(attempt);
                return;
            }
            var change = result.Change;
            Recovery.RecordCommit(attempt.AttemptId, change);
            Recovery.CompleteSource(attempt);
            sourceWriteCommitted = sourceSubmission?.Attempt.AttemptId == attempt.AttemptId;
            ReconcileSourceDraft();
            catalogWriteCommitted = catalogSubmission?.Attempt.AttemptId == attempt.AttemptId;
            ReconcileCatalog();
            await PublishChangeAsync(change, operation, attempt.AttemptId);
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

    private async Task RetryVerifiedSourceAsync() {
        if (disposed || operationBusy || !Recovery.SourceRetryAllowed || Recovery.Source is not { } attempt) {
            return;
        }
        switch (attempt.Kind) {
            case SharedProviderSourceMutationKind.Create:
            case SharedProviderSourceMutationKind.Update:
                await SaveSourceAttemptAsync(attempt, controlledRetry: true);
                break;
            case SharedProviderSourceMutationKind.Enablement:
                await RunSourceMutationAsync(async token =>
                    (await ManagementService.SetSourceEnabledAsync(attempt.SourceId,
                        attempt.Before!.Source.ConcurrencyToken, attempt.IntendedEnabled!.Value, token)).Change,
                    "Source updated", attempt, controlledRetry: true);
                break;
            case SharedProviderSourceMutationKind.Delete:
                await RunSourceMutationAsync(async token =>
                    (await ManagementService.DeleteSourceAsync(attempt.SourceId, attempt.Before!.Source.ConcurrencyToken, token)).Change,
                    "Source deleted", attempt, controlledRetry: true);
                break;
            case SharedProviderSourceMutationKind.Test:
                await RunSourceOperationAsync(token => ManagementService.TestSourceAsync(attempt.SourceId, token),
                    attempt, controlledRetry: true);
                break;
            case SharedProviderSourceMutationKind.Synchronize:
                await RunSourceOperationAsync(token => ManagementService.SynchronizeSourceAsync(attempt.SourceId, attempt.Selection, token),
                    attempt, controlledRetry: true);
                break;
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
