using CanDoItAll.AgentFramework.SharedProviders.UI;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using Microsoft.AspNetCore.Components;
using CanDoItAll.Editing;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components;

public partial class SharedProviderManagementPanel : ISharedProviderSharingView, IDisposable, IAsyncDisposable {
    [Inject] public ISharedProviderManagementService ManagementService { get; set; } = default!;
    [Inject] public SharedProviderRecovery Recovery { get; set; } = default!;
    [Inject] public NotificationService NotificationService { get; set; } = default!;
    [Inject] public ProviderEditingChanges Editing { get; set; } = default!;
    [Parameter] public ProviderEditingOpening? OriginalOpening { get; set; }
    [Parameter] public Guid? ProviderProfileId { get; set; }
    [Parameter] public long Revision { get; set; }
    [Parameter] public Guid? ProviderRevision { get; set; }
    [Parameter] public EventCallback<SharedProviderChangeDelivery> ProvidersChanged { get; set; }

    private SharedProviderProfileSharingSnapshot? profileState;
    private SharedProviderImportDraft? importDraft;
    private ProviderEditingOpening? opening;
    private bool admitted;
    private long changeSequence;
    private (Guid AttemptId, long Sequence)? verifiedAttempt;
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
        profileState?.Import is { } import ? SharedProviderPresentationMapper.Baseline(import) : null) {
            ProviderConfigurationToken = OriginalOpening is null ? profileState?.ProviderConcurrencyToken : ProviderRevision
        };

    private bool NeedsProviderReview => OriginalOpening is not null && profileState?.Ownership == SharedProviderProfileOwnership.Local &&
        (ProviderRevision is null || profileState.ProviderConcurrencyToken != ProviderRevision);

    public SharedProviderSharingPresentation Presentation => new(Origin, admitted ? SharedProviderPresentationMapper.Sharing(profileState) : null,
        admitted ? importDraft : null, isLoading, !admitted || isBusy || isLoading || readFailed || hasPendingAttempt || NeedsProviderReview,
        warning ?? (NeedsProviderReview ? "Provider configuration changed. Review it in the provider editor before changing publication." : null), confirmation) {
        ReviewBlocked = !admitted || isBusy || isLoading || readFailed,
        CanAcknowledgeCurrent = admitted && !isBusy && !isLoading && !readFailed && importDraft?.HasConflict != true &&
            Recovery.FindTarget(ProviderProfileId) is { } attempt && Recovery.PendingDelivery(attempt.AttemptId) is null &&
            verifiedAttempt == (attempt.AttemptId, changeSequence)
    };

    private bool CanWrite(SharedProviderSharingOrigin origin) => origin == Origin && !disposed && admitted && owner is not null &&
        !isBusy && !isLoading && !readFailed && !hasPendingAttempt && !NeedsProviderReview && profileState?.ProviderProfileId == ProviderProfileId;

    protected override async Task OnInitializedAsync() {
        if (!RendererInfo.IsInteractive) {
            return;
        }
        opening = Editing.Open(viewId, () => !disposed, parent: OriginalOpening);
        try {
            await opening.StartAsync(OnChangedAsync, () => {
                RetireAccess();
                return InvokeAsync(StateHasChanged);
            });
            Recovery.Bind(new(opening.Scope, opening.PrincipalStamp));
            admitted = true;
        } catch (UnauthorizedAccessException) {
            RetireAccess();
        }
    }

    private Task OnChangedAsync(EditingChangeBatch batch) => InvokeAsync(() => {
        if (!admitted || disposed || !batch.RequiresResync && !batch.Changes.Any(change =>
                change.OriginEditorId != viewId && change.Resource.ResourceId == ProviderProfileId &&
                change.Kind != EditingChangeKind.MetadataChanged)) {
            return;
        }
        changeSequence++;
        verifiedAttempt = null;
        importDraft?.Invalidate();
        readFailed = true;
        CloseConfirmationDialog();
        warning = "This provider changed. Read its current sharing state before another change; your draft is retained.";
        StateHasChanged();
    });

    private void RetireAccess() {
        admitted = false;
        opening?.Stop();
        profileState = null;
        verifiedAttempt = null;
        CloseConfirmationDialog();
        isLoading = false;
        isBusy = false;
        warning = "Original sharing access has ended. Reopen the provider with current access.";
    }

    protected override async Task OnParametersSetAsync() {
        if (disposed || !admitted) {
            return;
        }
        if (loadedProviderProfileId == ProviderProfileId && loadedRevision == Revision && loadedProviderRevision == ProviderRevision) {
            return;
        }
        var targetChanged = loadedProviderProfileId != ProviderProfileId;
        if (!targetChanged && isBusy) {
            return;
        }
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
            importDraft = Recovery.TargetDraft(ProviderProfileId);
            verifiedAttempt = null;
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
        !disposed && admitted && operation == generation && !token.IsCancellationRequested;

    private async Task LoadAsync(long operation, CancellationToken token, bool verify = false) {
        using var request = CancellationTokenSource.CreateLinkedTokenSource(token);
        token = request.Token;
        var targetId = ProviderProfileId;
        var unresolved = Recovery.FindTarget(targetId);
        var coveredSequence = changeSequence;
        isLoading = true;
        readFailed = false;
        try {
            var state = targetId is { } id ? await (opening ?? throw new UnauthorizedAccessException("Original sharing access is unavailable."))
                .ReadAsync(ct => ManagementService.GetProfileSharingAsync(id, ct), token) : null;
            if (!IsCurrent(operation, token)) {
                return;
            }
            if (state is not null && state.ProviderProfileId != targetId) {
                throw new InvalidOperationException("The sharing response has a different provider identity.");
            }
            AcceptProfile(state);
            readFailed = coveredSequence != changeSequence;
            if (readFailed) {
                importDraft?.Invalidate();
                warning = "The provider changed during this read. Read the current sharing state again.";
                return;
            }
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
            verifiedAttempt = (unresolved.AttemptId, coveredSequence);
            warning = "Current sharing state was read. The earlier outcome remains unconfirmed. Review any changed local settings, then acknowledge current state; no write is repeated.";
        } catch (UnauthorizedAccessException) {
            RetireAccess();
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
        if (origin != Origin || owner is null || disposed || !admitted || isBusy || isLoading) {
            return;
        }
        var operation = ++generation;
        using var request = CancellationTokenSource.CreateLinkedTokenSource(owner.Token);
        var token = request.Token;
        if (Recovery.FindTarget(ProviderProfileId) is { } pending && Recovery.PendingDelivery(pending.AttemptId) is not null) {
            isBusy = true;
            try {
                await (opening ?? throw new UnauthorizedAccessException("Original sharing access is unavailable."))
                    .ReadAsync(_ => Task.FromResult(true), token);
                await DeliverAsync(pending, operation, token);
                if (IsCurrent(operation, token) && Recovery.FindTarget(ProviderProfileId) is null) {
                    await LoadAsync(operation, token);
                }
            } catch (UnauthorizedAccessException) {
                RetireAccess();
            } finally {
                if (IsCurrent(operation, token)) {
                    isBusy = false;
                    await OnParametersSetAsync();
                }
            }
        } else {
            await LoadAsync(operation, token, verify: true);
        }
    }

    public async Task AcknowledgeCurrentAsync(SharedProviderSharingOrigin origin) {
        if (origin != Origin || !Presentation.CanAcknowledgeCurrent || owner is null ||
            Recovery.FindTarget(ProviderProfileId) is not { } attempt) {
            return;
        }
        try {
            await (opening ?? throw new UnauthorizedAccessException("Original sharing access is unavailable."))
                .ReadAsync(_ => Task.FromResult(true), owner.Token);
            if (origin == Origin && Presentation.CanAcknowledgeCurrent && Recovery.AcknowledgeTargetCurrent(attempt)) {
                verifiedAttempt = null;
                warning = "Current state acknowledged. The earlier outcome remains unconfirmed; no operation was repeated.";
            }
        } catch (UnauthorizedAccessException) {
            RetireAccess();
        }
    }

    public Task PublishAsync(SharedProviderSharingOrigin origin) =>
        profileState?.Eligibility?.IsEligible == true && profileState.Publication?.IsPublished != true
            ? ChangePublicationAsync(origin, SharedProviderPublicationAction.Publish) : Task.CompletedTask;

    private Task ChangePublicationAsync(SharedProviderSharingOrigin origin, SharedProviderPublicationAction action) {
        if (!CanWrite(origin) || profileState is not { Ownership: SharedProviderProfileOwnership.Local } ||
            origin.ProviderConfigurationToken is not { } original || original == Guid.Empty) {
            return Task.CompletedTask;
        }
        return RunMutationAsync(token => ManagementService.SetPublicationAsync(
            origin.ProviderId!.Value, action, origin.PublicationToken, original, token), "Publication updated",
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
        if (importDraft is null || importDraft.Baseline.ImportId != import.ImportId) {
            importDraft = new(baseline);
        } else if (submission is not null && submission.DraftId == importDraft.Id) {
            importDraft.Accept(submission, baseline);
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
        if (disposed || !admitted || owner is null || isBusy || isLoading || readFailed || hasPendingAttempt ||
            profileState?.ProviderProfileId != ProviderProfileId) {
            return;
        }
        SharedProviderTargetAttempt? attempt = null;
        var operation = ++generation;
        var submittedSequence = changeSequence;
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(owner.Token);
        var token = lifetime.Token;
        isBusy = true;
        warning = null;
        try {
            var result = await (opening ?? throw new UnauthorizedAccessException("Original sharing access is unavailable."))
                .WriteAsync(() => {
                    attempt = Recovery.BeginTarget(ProviderProfileId!.Value, kind, profileState!, request);
                    Recovery.RetainTargetDraft(attempt, importDraft);
                    verifiedAttempt = null;
                    return mutation(token);
                }, () => IsCurrent(operation, token), token);
            if (attempt is null) {
                throw new InvalidOperationException("The sharing operation did not capture its original attempt.");
            }
            if (result.ProviderProfileId != attempt.ProviderId) {
                throw new InvalidOperationException("The sharing response has a different provider identity.");
            }
            Recovery.RecordCommit(attempt.AttemptId, result.Change);
            result = await opening.ReadAsync(_ => Task.FromResult(result), token);
            if (!IsCurrent(operation, token)) {
                return;
            }
            AcceptProfile(result, submission);
            if (submittedSequence != changeSequence) {
                readFailed = true;
                importDraft?.Invalidate();
            }
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
            if (attempt is null) {
                throw;
            }
            Recovery.RecordCommit(attempt.AttemptId, exception.Change);
            if (IsCurrent(operation, token)) {
                profileState = null;
                await DeliverAsync(attempt, operation, token);
                if (IsCurrent(operation, token) && Recovery.FindTarget(ProviderProfileId) is null) {
                    warning = exception.Change.Warning;
                }
            }
        } catch (UnauthorizedAccessException) {
            if (attempt is not null && Recovery.PendingDelivery(attempt.AttemptId) is null) {
                Recovery.CompleteTarget(attempt);
            }
            RetireAccess();
        } catch (OperationCanceledException) when (token.IsCancellationRequested || !admitted) {
        } catch (Exception exception) {
            var rejected = exception is SharedProviderConcurrencyException or SharedProviderPublicationEligibilityException
                or ArgumentException or KeyNotFoundException;
            if (rejected && attempt is not null) {
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
                await OnParametersSetAsync();
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
        admitted = false;
        opening?.Stop();
        owner?.Cancel();
        owner?.Dispose();
        owner = null;
        CloseConfirmationDialog();
    }

    public async ValueTask DisposeAsync() {
        Dispose();
        if (opening is not null) {
            await opening.DisposeAsync();
        }
    }

}
