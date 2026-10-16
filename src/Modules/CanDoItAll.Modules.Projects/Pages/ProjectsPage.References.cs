using CanDoItAll.Modules.Projects.Pages.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Projects.Pages;

public partial class ProjectsPage {
    [Inject] private IProjectPendingReferenceStore ReferenceStore { get; set; } = default!;
    private bool referencesOpen;
    private bool referencesBusy;
    private ProjectPendingReferencePage? referencePage;
    private ProjectPendingReference? selectedReference;
    private string? referenceMessage;

    private async Task OpenReferencesAsync() {
        referencesOpen = true;
        await ReadReferencesAsync(null);
    }

    private Task RefreshReferencesAsync() => ReadReferencesAsync(null);
    private Task NextReferencesAsync() => ReadReferencesAsync(referencePage?.Next);

    private void CloseReferences() {
        if (!referencesBusy) {
            referencesOpen = false;
            selectedReference = null;
        }
    }

    private void SelectReference(ProjectPendingReference reference) {
        if (!referencesBusy && referencePage?.Items.Contains(reference) == true) {
            selectedReference = reference;
        }
    }

    private async Task ReadReferencesAsync(ProjectDefinitionOperation? after) {
        if (disposed || referencesBusy) {
            return;
        }
        referencesBusy = true;
        selectedReference = null;
        try {
            await ReferenceStore.InitializeAsync(readLifetime.Token);
            var page = await ReferenceStore.ListReferencesAsync(PendingLimits.MaximumPageSize, after, readLifetime.Token);
            if (!disposed) {
                referencePage = page;
                referenceMessage = null;
            }
        } catch (OperationCanceledException) when (readLifetime.IsCancellationRequested) {
        } catch (Exception exception) {
            Logger.LogError(exception, "Project retained-reference inventory could not be read.");
            referencePage = null;
            referenceMessage = "Reference metadata could not be read safely. No local recovery data was removed.";
        } finally {
            referencesBusy = false;
        }
    }

    private async Task DiscardReferenceAsync(ProjectPendingReference reference) {
        if (disposed || referencesBusy || selectedReference != reference) {
            return;
        }
        referencesBusy = true;
        selectedReference = null;
        pendingClears++;
        pendingRecoveryGeneration++;
        var affected = editorOperations.Values.Where(session => session.Operation == reference.Operation).ToArray();
        foreach (var session in affected) {
            session.Dispose();
        }
        try {
            var result = await ReferenceStore.DiscardEvidenceAsync(reference, readLifetime.Token);
            if (result.Status == ProjectPendingStoreStatus.Applied) {
                foreach (var session in affected) {
                    session.MarkEvidenceDiscarded();
                    session.RememberAcceptance();
                    if (editorOperations.TryGetValue(session.Draft, out var current) && ReferenceEquals(current, session)) {
                        ReleaseMutation(session.Draft, ProjectEditorMutationState.LocalRecoveryDiscarded);
                        session.Draft.Message = $"Local recovery data discarded for save {session.Operation.Id:D}. This does not cancel or determine its server outcome. Close this draft and inspect current data explicitly; it cannot be resubmitted with a new identity.";
                        session.Draft.IsError = true;
                    }
                }
                referenceMessage = "Local recovery data discarded. Its server effect was not undone or canceled; no replacement save was submitted.";
            } else {
                referenceMessage = result.Status == ProjectPendingStoreStatus.Changed
                    ? "This reference changed, possibly in another tab. Nothing was discarded. Inspect its refreshed metadata and select it again to confirm."
                    : "This local reference is already absent. This does not establish its server outcome.";
                foreach (var session in affected) {
                    session.Draft.Message = "The discard did not remove the observed revision. Reopen the retained save explicitly before further recovery.";
                }
            }
            referencePage = await ReferenceStore.ListReferencesAsync(PendingLimits.MaximumPageSize, cancellationToken: readLifetime.Token);
        } catch (Exception exception) {
            Logger.LogError(exception, "Local Project recovery evidence disposal could not be confirmed for {OperationId}.", reference.Operation.Id);
            referenceMessage = "Local evidence disposal was not confirmed. Refresh reference metadata before another action; no server outcome is implied.";
        } finally {
            pendingClears--;
            pendingRecoveryGeneration++;
            referencesBusy = false;
            if (!disposed) {
                await RefreshPendingSavesAsync();
            }
        }
    }
}
