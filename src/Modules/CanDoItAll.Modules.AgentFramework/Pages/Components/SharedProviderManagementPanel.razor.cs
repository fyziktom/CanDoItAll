using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components;

public partial class SharedProviderManagementPanel : IDisposable {
    [Inject] public ISharedProviderManagementService ManagementService { get; set; } = default!;
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
    private bool mutationUnconfirmed;
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
        mutationUnconfirmed = false;
        warning = null;
        CloseConfirmationDialog();
        await LoadAsync(generation, owner.Token);
    }

    private bool IsCurrent(long operation, CancellationToken token) =>
        !disposed && operation == generation && !token.IsCancellationRequested;

    private async Task LoadAsync(long operation, CancellationToken token) {
        isLoading = true;
        try {
            var state = ProviderProfileId is { } id
                ? await ManagementService.GetProfileSharingAsync(id, token) : null;
            if (!IsCurrent(operation, token)) {
                return;
            }
            if (state is not null && state.ProviderProfileId != ProviderProfileId) {
                throw new InvalidOperationException("The sharing response has a different provider identity.");
            }
            profileState = state;
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
        await LoadAsync(++generation, owner.Token);
    }

    private Task PublishAsync() => ChangePublicationAsync(SharedProviderPublicationAction.Publish);

    private Task ChangePublicationAsync(SharedProviderPublicationAction action) {
        if (profileState is not { Ownership: SharedProviderProfileOwnership.Local } state) {
            return Task.CompletedTask;
        }
        return RunMutationAsync(token => ManagementService.SetPublicationAsync(
            state.ProviderProfileId, action, state.Publication?.ConcurrencyToken, token), "Publication updated");
    }

    private Task SaveImportedProfileAsync(SharedProviderImportedProfileEditModel model) {
        if (profileState?.Import is not { } import) {
            return Task.CompletedTask;
        }
        var request = new SharedProviderImportedProfileUpdateRequest(import.ImportId, import.ProviderProfileId,
            model.LocalAlias, model.IsEnabled, import.ImportConcurrencyToken, import.ProviderConcurrencyToken);
        return RunMutationAsync(token => ManagementService.UpdateImportedProfileAsync(request, token), "Imported provider updated");
    }

    private Task RetireImportedProfileAsync() {
        if (profileState?.Import is not { } import) {
            return Task.CompletedTask;
        }
        var request = new SharedProviderImportedProfileRetireRequest(import.ImportId, import.ProviderProfileId,
            import.ImportConcurrencyToken, import.ProviderConcurrencyToken);
        return RunMutationAsync(token => ManagementService.RetireImportedProfileAsync(request, token), "Imported provider retired");
    }

    private async Task RunMutationAsync(
        Func<CancellationToken, Task<SharedProviderProfileSharingSnapshot>> mutation, string title) {
        if (disposed || owner is null || isBusy || isLoading || mutationUnconfirmed ||
            profileState?.ProviderProfileId != ProviderProfileId) {
            return;
        }
        var operation = ++generation;
        var token = owner.Token;
        isBusy = true;
        warning = null;
        SharedProviderChange? committed = null;
        try {
            var result = await mutation(token);
            committed = result.Change;
            if (!IsCurrent(operation, token)) {
                return;
            }
            if (result.ProviderProfileId != ProviderProfileId) {
                throw new InvalidOperationException("The sharing response has a different provider identity.");
            }
            profileState = result;
            if (result.Change is { } change) {
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
            if (IsCurrent(operation, token)) {
                warning = exception.Change.Warning;
                profileState = null;
                await ProvidersChanged.InvokeAsync(exception.Change);
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
                mutationUnconfirmed = !rejected;
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
        disposed = true;
        owner?.Cancel();
        owner?.Dispose();
        CloseConfirmationDialog();
    }

    private enum ConfirmationAction { None, Unpublish, RetireImport }
}
