using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components;

public partial class SharedProviderManagementPanel : IDisposable {
    [Inject] public ISharedProviderManagementService ManagementService { get; set; } = default!;
    [Inject] public SharedProviderRecovery Recovery { get; set; } = default!;
    [Inject] public NotificationService NotificationService { get; set; } = default!;
    [Parameter] public Guid? ProviderProfileId { get; set; }
    [Parameter] public long Revision { get; set; }
    [Parameter] public EventCallback<SharedProviderChangeDelivery> ProvidersChanged { get; set; }

    private SharedProviderProfileSharingSnapshot? profileState;
    private CancellationTokenSource? owner;
    private Guid? loadedProviderProfileId;
    private long loadedRevision = -1;
    private long generation;
    private bool disposed;
    private bool isLoading;
    private bool isBusy;
    private bool hasPendingAttempt => Recovery.FindTarget(ProviderProfileId) is not null;
    private string? warning;
    private string confirmationTitle = string.Empty;
    private string confirmationMessage = string.Empty;
    private string confirmationActionText = string.Empty;
    private ConfirmationAction confirmationAction;
    private bool confirmationDialogOpen;

    protected override async Task OnParametersSetAsync() {
        if (disposed) {
            return;
        }
        if (loadedProviderProfileId == ProviderProfileId && loadedRevision == Revision) {
            return;
        }
        loadedProviderProfileId = ProviderProfileId;
        loadedRevision = Revision;
        owner?.Cancel();
        owner?.Dispose();
        owner = new();
        generation++;
        profileState = null;
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
        var targetId = ProviderProfileId;
        var unresolved = Recovery.FindTarget(targetId);
        isLoading = true;
        try {
            var state = targetId is { } id ? await ManagementService.GetProfileSharingAsync(id, token) : null;
            if (!IsCurrent(operation, token)) {
                return;
            }
            if (state is not null && state.ProviderProfileId != targetId) {
                throw new InvalidOperationException("The sharing response has a different provider identity.");
            }
            profileState = state;
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
            var verification = SharedProviderTargetVerification.Evaluate(unresolved, state);
            switch (verification.Disposition) {
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
                profileState = null;
                warning = "Sharing state could not be read. Retry this target.";
            }
        } finally {
            if (IsCurrent(operation, token)) {
                isLoading = false;
            }
        }
    }

    private async Task RetryAsync() {
        if (owner is null || disposed || isBusy || isLoading) {
            return;
        }
        var operation = ++generation;
        var token = owner.Token;
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

    private Task PublishAsync() => ChangePublicationAsync(SharedProviderPublicationAction.Publish);

    private Task ChangePublicationAsync(SharedProviderPublicationAction action) {
        if (profileState is not { Ownership: SharedProviderProfileOwnership.Local } state) {
            return Task.CompletedTask;
        }
        return RunMutationAsync(token => ManagementService.SetPublicationAsync(
            state.ProviderProfileId, action, state.Publication?.ConcurrencyToken, token), "Publication updated",
            action == SharedProviderPublicationAction.Publish ? SharedProviderTargetMutationKind.Publish : SharedProviderTargetMutationKind.Unpublish);
    }

    private Task SaveImportedProfileAsync(SharedProviderImportedProfileEditModel model) {
        if (profileState?.Import is not { } import) {
            return Task.CompletedTask;
        }
        var request = new SharedProviderImportedProfileUpdateRequest(import.ImportId, import.ProviderProfileId,
            model.LocalAlias, model.IsEnabled, import.ImportConcurrencyToken, import.ProviderConcurrencyToken);
        return RunMutationAsync(token => ManagementService.UpdateImportedProfileAsync(request, token),
            "Imported provider updated", SharedProviderTargetMutationKind.ImportedSettings, request);
    }

    private Task RetireImportedProfileAsync() {
        if (profileState?.Import is not { } import) {
            return Task.CompletedTask;
        }
        var request = new SharedProviderImportedProfileRetireRequest(import.ImportId, import.ProviderProfileId,
            import.ImportConcurrencyToken, import.ProviderConcurrencyToken);
        return RunMutationAsync(token => ManagementService.RetireImportedProfileAsync(request, token),
            "Imported provider retired", SharedProviderTargetMutationKind.Retirement);
    }

    private async Task RunMutationAsync(Func<CancellationToken, Task<SharedProviderProfileSharingSnapshot>> mutation,
        string title, SharedProviderTargetMutationKind kind, SharedProviderImportedProfileUpdateRequest? request = null) {
        if (disposed || owner is null || isBusy || isLoading || hasPendingAttempt ||
            profileState?.ProviderProfileId != ProviderProfileId) {
            return;
        }
        SharedProviderTargetAttempt attempt;
        try {
            attempt = Recovery.BeginTarget(ProviderProfileId!.Value, kind, profileState!, request);
        } catch (ArgumentException) {
            warning = "The requested local settings are invalid. Correct the alias and retry.";
            return;
        }
        var operation = ++generation;
        var token = owner.Token;
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
            profileState = result;
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

    private void OpenUnpublishConfirmation() {
        confirmationAction = ConfirmationAction.Unpublish;
        confirmationTitle = "Unpublish this provider?";
        confirmationMessage = "New remote requests stop. The permanent public identity and deletion protection remain.";
        confirmationActionText = "Unpublish";
        confirmationDialogOpen = true;
    }

    private void OpenRetireConfirmation() {
        confirmationAction = ConfirmationAction.RetireImport;
        confirmationTitle = "Retire this imported provider?";
        confirmationMessage = "The profile remains for audit and can be reactivated through a later catalog import.";
        confirmationActionText = "Retire import";
        confirmationDialogOpen = true;
    }

    private Task ConfirmDestructiveActionAsync() => confirmationAction switch {
        ConfirmationAction.Unpublish => ChangePublicationAsync(SharedProviderPublicationAction.Unpublish),
        ConfirmationAction.RetireImport => RetireImportedProfileAsync(),
        _ => Task.CompletedTask
    };

    private void CloseConfirmationDialog() {
        confirmationDialogOpen = false;
        confirmationAction = ConfirmationAction.None;
    }

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

    private enum ConfirmationAction { None, Unpublish, RetireImport }
}
