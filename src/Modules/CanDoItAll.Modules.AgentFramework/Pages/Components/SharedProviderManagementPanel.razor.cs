using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using CanDoItAll.SharedProviders.Abstractions;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components;

public partial class SharedProviderManagementPanel : IDisposable {
    [Inject] public ISharedProviderManagementService ManagementService { get; set; } = default!;
    [Inject] public SharedProviderRecovery Recovery { get; set; } = default!;
    [Inject] public NotificationService NotificationService { get; set; } = default!;
    [Parameter] public Guid? ProviderProfileId { get; set; }
    [Parameter] public long Revision { get; set; }
    [Parameter] public EventCallback<SharedProviderChange> ProvidersChanged { get; set; }

    private SharedProviderProfileSharingSnapshot? profileState;
    private CancellationTokenSource? owner;
    private Guid? loadedProviderProfileId;
    private long loadedRevision = -1;
    private long generation;
    private bool disposed;
    private bool isLoading;
    private bool isBusy;
    private bool mutationUnconfirmed => Recovery.FindTarget(ProviderProfileId) is not null;
    private string? warning;
    private string confirmationTitle = string.Empty;
    private string confirmationMessage = string.Empty;
    private string confirmationActionText = string.Empty;
    private ConfirmationAction confirmationAction;
    private bool confirmationDialogOpen;

    protected override async Task OnParametersSetAsync() {
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
        warning = mutationUnconfirmed ? "A sharing attempt needs canonical verification before another change." : null;
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
            var state = targetId is { } id
                ? await ManagementService.GetProfileSharingAsync(id, token) : null;
            if (!IsCurrent(operation, token)) {
                return;
            }
            if (state is not null && state.ProviderProfileId != ProviderProfileId) {
                throw new InvalidOperationException("The sharing response has a different provider identity.");
            }
            profileState = state;
            if (verify && state is not null && state.ProviderProfileId == targetId) {
                warning = null;
                if (unresolved is not null && Recovery.CompleteTarget(unresolved)) {
                    var change = Recovery.KnownChange(unresolved.AttemptId) ?? VerifiedChange(unresolved, state);
                    if (change is not null && Recovery.ClaimPublication(unresolved.AttemptId)) {
                        await ProvidersChanged.InvokeAsync(change);
                    }
                }
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
        if (owner is null || disposed || isBusy) {
            return;
        }
        await LoadAsync(++generation, owner.Token, verify: true);
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
        return RunMutationAsync(token => ManagementService.UpdateImportedProfileAsync(request, token), "Imported provider updated", SharedProviderTargetMutationKind.ImportedSettings);
    }

    private Task RetireImportedProfileAsync() {
        if (profileState?.Import is not { } import) {
            return Task.CompletedTask;
        }
        var request = new SharedProviderImportedProfileRetireRequest(import.ImportId, import.ProviderProfileId,
            import.ImportConcurrencyToken, import.ProviderConcurrencyToken);
        return RunMutationAsync(token => ManagementService.RetireImportedProfileAsync(request, token), "Imported provider retired", SharedProviderTargetMutationKind.Retirement);
    }

    private async Task RunMutationAsync(
        Func<CancellationToken, Task<SharedProviderProfileSharingSnapshot>> mutation, string title, SharedProviderTargetMutationKind kind) {
        if (disposed || owner is null || isBusy || isLoading || mutationUnconfirmed ||
            profileState?.ProviderProfileId != ProviderProfileId) {
            return;
        }
        var attempt = Recovery.BeginTarget(ProviderProfileId!.Value, kind, profileState!);
        var operation = ++generation;
        var token = owner.Token;
        isBusy = true;
        warning = null;
        SharedProviderChange? committed = null;
        try {
            var result = await mutation(token);
            committed = result.Change;
            Recovery.RecordCommit(attempt.AttemptId, result.Change);
            if (!IsCurrent(operation, token)) {
                return;
            }
            if (result.ProviderProfileId != ProviderProfileId) {
                throw new InvalidOperationException("The sharing response has a different provider identity.");
            }
            Recovery.CompleteTarget(attempt);
            profileState = result;
            if (result.Change is { } change && Recovery.ClaimPublication(attempt.AttemptId)) {
                warning = change.Warning;
                await ProvidersChanged.InvokeAsync(change);
            }
            if (IsCurrent(operation, token)) {
                if (warning is null) {
                    NotificationService.Success(title, "The authoritative sharing state was saved.");
                } else {
                    NotificationService.Warning(title, warning);
                }
            }
        } catch (SharedProviderCommittedException exception) {
            Recovery.RecordCommit(attempt.AttemptId, exception.Change);
            if (IsCurrent(operation, token)) {
                warning = exception.Change.Warning;
                profileState = null;
                Recovery.CompleteTarget(attempt);
                if (Recovery.ClaimPublication(attempt.AttemptId)) {
                    await ProvidersChanged.InvokeAsync(exception.Change);
                }
            }
        } catch (OperationCanceledException) when (token.IsCancellationRequested) {
        } catch (Exception exception) {
            if (IsCurrent(operation, token)) {
                if (committed is not null) {
                    warning = "The sharing change is saved, but its workspace refresh did not complete.";
                    return;
                }
                var rejected = exception is SharedProviderConcurrencyException or SharedProviderPublicationEligibilityException
                    or ArgumentException or KeyNotFoundException;
                if (rejected) {
                    Recovery.CompleteTarget(attempt);
                }
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
        CloseConfirmationDialog();
    }

    private static SharedProviderChange? VerifiedChange(
        SharedProviderTargetAttempt attempt, SharedProviderProfileSharingSnapshot current) {
        var before = attempt.Before;
        if (attempt.Kind is SharedProviderTargetMutationKind.Publish or SharedProviderTargetMutationKind.Unpublish) {
            return before.Publication?.ConcurrencyToken != current.Publication?.ConcurrencyToken
                ? new(SharedProviderChangeKind.Publication, [attempt.ProviderId]) : null;
        }
        if (before.Import?.ImportConcurrencyToken == current.Import?.ImportConcurrencyToken &&
            before.Import?.ProviderConcurrencyToken == current.Import?.ProviderConcurrencyToken) {
            return null;
        }
        var retired = current.Import?.SelectionState == SharedProviderSelectionState.Retired;
        return new(retired ? SharedProviderChangeKind.ImportRetirement : SharedProviderChangeKind.ImportedSettings,
            [attempt.ProviderId], retired ? [attempt.ProviderId] : [], catalogMembershipMayHaveChanged: retired);
    }

    private enum ConfirmationAction { None, Unpublish, RetireImport }
}
