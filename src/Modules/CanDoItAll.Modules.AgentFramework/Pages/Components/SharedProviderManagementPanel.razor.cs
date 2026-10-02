using CanDoItAll.AgentFramework.SharedProviders.UI;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components;

public partial class SharedProviderManagementPanel : ISharedProviderSharingView, IDisposable {
    [Inject] public ISharedProviderManagementService ManagementService { get; set; } = default!;
    [Inject] public SharedProviderRecovery Recovery { get; set; } = default!;
    [Inject] public NotificationService NotificationService { get; set; } = default!;
    [Parameter] public Guid? ProviderProfileId { get; set; }
    [Parameter] public long Revision { get; set; }
    [Parameter] public Guid? ProviderRevision { get; set; }
    [Parameter] public EventCallback<SharedProviderChangeDelivery> ProvidersChanged { get; set; }

    private SharedProviderProfileSharingSnapshot? profileState;
    private SharedProviderImportDraft? importDraft;
    private (Guid AttemptId, SharedProviderImportSubmission Submission)? importAttempt;
    private bool importCommitted;
    private CancellationTokenSource? owner;
    private Guid? loadedProviderProfileId;
    private long loadedRevision = -1;
    private Guid? loadedProviderRevision;
    private long generation;
    private bool disposed;
    private bool isLoading;
    private bool readFailed;
    private bool isBusy;
    private bool hasPendingAttempt => Recovery.FindTarget(ProviderProfileId) is not null;
    private string? warning;
    private readonly Guid viewId = Guid.NewGuid();
    private Guid snapshotId = Guid.NewGuid();
    private long activation;
    private SharedProviderConfirmation? confirmation;
    private SharedProviderSharingOrigin Origin => new(viewId, activation, snapshotId, ProviderProfileId,
        profileState?.Publication?.ConcurrencyToken,
        profileState?.Import is { } import ? SharedProviderPresentationMapper.Baseline(import) : null);

    public SharedProviderSharingPresentation Presentation => new(Origin, SharedProviderPresentationMapper.Sharing(profileState),
        importDraft, isLoading, isBusy || isLoading || readFailed || hasPendingAttempt, warning, confirmation);

    private bool CanWrite(SharedProviderSharingOrigin origin) => origin == Origin && !disposed && owner is not null &&
        !isBusy && !isLoading && !readFailed && !hasPendingAttempt && profileState?.ProviderProfileId == ProviderProfileId;

    protected override async Task OnParametersSetAsync() {
        if (disposed) {
            return;
        }
        if (loadedProviderProfileId == ProviderProfileId && loadedRevision == Revision && loadedProviderRevision == ProviderRevision) {
            return;
        }
        var targetChanged = loadedProviderProfileId != ProviderProfileId;
        loadedProviderProfileId = ProviderProfileId;
        loadedRevision = Revision;
        loadedProviderRevision = ProviderRevision;
        owner?.Cancel();
        owner?.Dispose();
        owner = new();
        generation++;
        if (targetChanged) {
            activation++;
            profileState = null;
            importDraft = null;
            importAttempt = null;
            importCommitted = false;
        }
        isBusy = false;
        warning = Recovery.FindTarget(ProviderProfileId) is { } pending
            ? Recovery.PendingDelivery(pending.AttemptId) is null
                ? "The sharing write is unresolved. Verify its exact intended state before another change."
                : DeliveryPendingMessage
            : null;
        CloseConfirmationDialog();
        await LoadAsync(generation, owner.Token);
    }

    private bool IsCurrent(long operation, CancellationToken token) =>
        !disposed && operation == generation && !token.IsCancellationRequested;

    private async Task LoadAsync(long operation, CancellationToken token, bool verify = false) {
        using var request = CancellationTokenSource.CreateLinkedTokenSource(token);
        token = request.Token;
        var targetId = ProviderProfileId;
        var unresolved = Recovery.FindTarget(targetId);
        isLoading = true;
        readFailed = false;
        try {
            var state = targetId is { } id ? await ManagementService.GetProfileSharingAsync(id, token) : null;
            if (!IsCurrent(operation, token)) {
                return;
            }
            if (state is not null && state.ProviderProfileId != targetId) {
                throw new InvalidOperationException("The sharing response has a different provider identity.");
            }
            var verification = verify && unresolved is not null && state is not null
                ? SharedProviderTargetVerification.Evaluate(unresolved, state) : null;
            AcceptProfile(state, verification?.Disposition == SharedProviderTargetVerificationDisposition.Satisfied &&
                importAttempt?.AttemptId == unresolved?.AttemptId ? importAttempt?.Submission : null);
            if (!verify || state is null) {
                return;
            }
            if (unresolved is null) {
                warning = null;
                return;
            }
            if (Recovery.FindTarget(targetId)?.AttemptId != unresolved.AttemptId) {
                return;
            }
            switch (verification!.Disposition) {
                case SharedProviderTargetVerificationDisposition.Satisfied:
                    Recovery.RecordCommit(unresolved.AttemptId, verification.Change);
                    if (Recovery.PendingDelivery(unresolved.AttemptId) is not null) {
                        await DeliverAsync(unresolved, operation, token);
                    } else if (Recovery.CompleteTarget(unresolved)) {
                        warning = null;
                    }
                    break;
                case SharedProviderTargetVerificationDisposition.NotApplied:
                    if (Recovery.CompleteTarget(unresolved)) {
                        warning = "The sharing write was not applied. Current state is unchanged; you can make a deliberate new change.";
                    }
                    break;
                default:
                    warning = "The sharing write remains unresolved. Current state does not establish the requested outcome; no write was repeated.";
                    break;
            }
        } catch (OperationCanceledException) when (token.IsCancellationRequested) {
        } catch (Exception) {
            if (IsCurrent(operation, token)) {
                readFailed = true;
                warning = "Sharing state could not be read. Retry this target.";
            }
        } finally {
            if (IsCurrent(operation, token)) {
                isLoading = false;
            }
        }
    }

    public async Task RetryAsync(SharedProviderSharingOrigin origin) {
        if (origin != Origin || owner is null || disposed || isBusy || isLoading) {
            return;
        }
        var operation = ++generation;
        using var request = CancellationTokenSource.CreateLinkedTokenSource(owner.Token);
        var token = request.Token;
        if (Recovery.FindTarget(ProviderProfileId) is { } pending && Recovery.PendingDelivery(pending.AttemptId) is not null) {
            isBusy = true;
            try {
                await DeliverAsync(pending, operation, token);
                if (IsCurrent(operation, token) && Recovery.FindTarget(ProviderProfileId) is null) {
                    await LoadAsync(operation, token);
                }
            } finally {
                if (IsCurrent(operation, token)) {
                    isBusy = false;
                }
            }
        } else {
            await LoadAsync(operation, token, verify: true);
        }
    }

    public Task PublishAsync(SharedProviderSharingOrigin origin) =>
        profileState?.Eligibility?.IsEligible == true && profileState.Publication?.IsPublished != true
            ? ChangePublicationAsync(origin, SharedProviderPublicationAction.Publish) : Task.CompletedTask;

    private Task ChangePublicationAsync(SharedProviderSharingOrigin origin, SharedProviderPublicationAction action) {
        if (!CanWrite(origin) || profileState is not { Ownership: SharedProviderProfileOwnership.Local }) {
            return Task.CompletedTask;
        }
        return RunMutationAsync(token => ManagementService.SetPublicationAsync(
            origin.ProviderId!.Value, action, origin.PublicationToken, token), "Publication updated",
            action == SharedProviderPublicationAction.Publish ? SharedProviderTargetMutationKind.Publish : SharedProviderTargetMutationKind.Unpublish);
    }

    public Task SaveImportedAsync(SharedProviderSharingOrigin origin, SharedProviderImportSubmission submission) {
        if (!CanWrite(origin) || profileState?.Import is not { } import || importDraft is null || !importDraft.CanSubmit(submission) ||
            submission.Baseline.ImportId != import.ImportId || submission.Baseline.ProviderId != import.ProviderProfileId) {
            return Task.CompletedTask;
        }
        var request = new SharedProviderImportedProfileUpdateRequest(import.ImportId, import.ProviderProfileId,
            submission.Settings.LocalAlias, submission.Settings.IsEnabled,
            submission.Baseline.ImportToken, submission.Baseline.ProviderToken);
        return RunMutationAsync(token => ManagementService.UpdateImportedProfileAsync(request, token),
            "Imported provider updated", SharedProviderTargetMutationKind.ImportedSettings, request, submission);
    }

    private void AcceptProfile(SharedProviderProfileSharingSnapshot? state, SharedProviderImportSubmission? submission = null) {
        profileState = state;
        snapshotId = Guid.NewGuid();
        CloseConfirmationDialog();
        if (state?.Import is not { } import) {
            return;
        }
        var baseline = SharedProviderPresentationMapper.Baseline(import);
        submission ??= importCommitted ? importAttempt?.Submission : null;
        if (importDraft is null || importDraft.Baseline.ImportId != import.ImportId) {
            importDraft = new(baseline);
        } else if (submission is not null && submission.DraftId == importDraft.Id &&
            SharedProviderLocalAliasPolicy.Normalize(submission.Settings.LocalAlias) == import.LocalAlias &&
            submission.Settings.IsEnabled == import.IsEnabled) {
            importDraft.Accept(submission, baseline);
            importAttempt = null;
            importCommitted = false;
        } else {
            importDraft.Reconcile(baseline);
        }
    }

    private Task RetireImportedProfileAsync(SharedProviderSharingOrigin origin) {
        if (!CanWrite(origin) || origin.Import is not { } import ||
            profileState?.Import?.SelectionState != SharedProviderSelectionState.Selected) {
            return Task.CompletedTask;
        }
        var request = new SharedProviderImportedProfileRetireRequest(import.ImportId, import.ProviderId,
            import.ImportToken, import.ProviderToken);
        return RunMutationAsync(token => ManagementService.RetireImportedProfileAsync(request, token),
            "Imported provider retired", SharedProviderTargetMutationKind.Retirement);
    }

    private async Task RunMutationAsync(Func<CancellationToken, Task<SharedProviderProfileSharingSnapshot>> mutation,
        string title, SharedProviderTargetMutationKind kind, SharedProviderImportedProfileUpdateRequest? request = null,
        SharedProviderImportSubmission? submission = null) {
        if (disposed || owner is null || isBusy || isLoading || readFailed || hasPendingAttempt ||
            profileState?.ProviderProfileId != ProviderProfileId) {
            return;
        }
        SharedProviderTargetAttempt attempt;
        try {
            attempt = Recovery.BeginTarget(ProviderProfileId!.Value, kind, profileState!, request);
            importAttempt = submission is null ? null : (attempt.AttemptId, submission);
            importCommitted = false;
        } catch (ArgumentException) {
            warning = "The requested local settings are invalid. Correct the alias and retry.";
            return;
        }
        var operation = ++generation;
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(owner.Token);
        var token = lifetime.Token;
        isBusy = true;
        warning = null;
        try {
            var result = await mutation(token);
            if (result.ProviderProfileId != attempt.ProviderId) {
                throw new InvalidOperationException("The sharing response has a different provider identity.");
            }
            Recovery.RecordCommit(attempt.AttemptId, result.Change);
            if (!IsCurrent(operation, token)) {
                return;
            }
            AcceptProfile(result, submission);
            if (result.Change is null) {
                Recovery.CompleteTarget(attempt);
            } else {
                await DeliverAsync(attempt, operation, token);
            }
            if (IsCurrent(operation, token) && Recovery.FindTarget(ProviderProfileId) is null) {
                warning = result.Change?.Warning;
                if (warning is null) {
                    NotificationService.Success(title, "The authoritative sharing state was saved.");
                } else {
                    NotificationService.Warning(title, warning);
                }
            }
        } catch (SharedProviderCommittedException exception) {
            Recovery.RecordCommit(attempt.AttemptId, exception.Change);
            if (IsCurrent(operation, token)) {
                importCommitted = submission is not null;
                profileState = null;
                await DeliverAsync(attempt, operation, token);
                if (IsCurrent(operation, token) && Recovery.FindTarget(ProviderProfileId) is null) {
                    warning = exception.Change.Warning;
                }
            }
        } catch (OperationCanceledException) when (token.IsCancellationRequested) {
        } catch (Exception exception) {
            var rejected = exception is SharedProviderConcurrencyException or SharedProviderPublicationEligibilityException
                or ArgumentException or KeyNotFoundException;
            if (rejected) {
                Recovery.CompleteTarget(attempt);
            }
            if (IsCurrent(operation, token)) {
                readFailed = rejected;
                warning = rejected
                    ? "The sharing change was rejected. Retry loading the current state before another change."
                    : "The sharing write is unconfirmed. Verify the authoritative state before another change.";
                NotificationService.Warning("Sharing change needs attention", warning);
            }
        } finally {
            if (IsCurrent(operation, token)) {
                isBusy = false;
                CloseConfirmationDialog();
            }
        }
    }

    private async Task DeliverAsync(SharedProviderTargetAttempt attempt, long operation, CancellationToken token) {
        var result = await Recovery.DeliverTargetAsync(attempt, ProviderProfileId,
            () => IsCurrent(operation, token), delivery => ProvidersChanged.InvokeAsync(delivery));
        if (IsCurrent(operation, token)) {
            warning = result == SharedProviderDeliveryDisposition.Acknowledged ? null : DeliveryPendingMessage;
        }
    }

    private const string DeliveryPendingMessage =
        "The sharing write is resolved, but workspace reconciliation delivery is pending. Retry delivery without repeating the write.";

    public void OpenConfirmation(SharedProviderSharingOrigin origin, SharedProviderConfirmationKind kind) {
        if (!CanWrite(origin) || !CanConfirm(kind)) {
            return;
        }
        confirmation = new(Guid.NewGuid(), origin, kind);
    }

    private bool CanConfirm(SharedProviderConfirmationKind kind) => kind switch {
        SharedProviderConfirmationKind.Unpublish => profileState is { Ownership: SharedProviderProfileOwnership.Local, Publication.IsPublished: true },
        SharedProviderConfirmationKind.RetireImport => profileState?.Import?.SelectionState == SharedProviderSelectionState.Selected,
        _ => false
    };

    public Task ConfirmAsync(SharedProviderConfirmation requested) {
        if (requested != confirmation || !CanWrite(requested.Origin) || !CanConfirm(requested.Kind)) {
            return Task.CompletedTask;
        }
        return requested.Kind == SharedProviderConfirmationKind.Unpublish
            ? ChangePublicationAsync(requested.Origin, SharedProviderPublicationAction.Unpublish)
            : RetireImportedProfileAsync(requested.Origin);
    }

    public void CloseConfirmation(SharedProviderConfirmation requested) {
        if (requested == confirmation) {
            CloseConfirmationDialog();
        }
    }

    private void CloseConfirmationDialog() => confirmation = null;

    public void Dispose() {
        if (disposed) {
            return;
        }
        disposed = true;
        owner?.Cancel();
        owner?.Dispose();
        owner = null;
        CloseConfirmationDialog();
    }

}
