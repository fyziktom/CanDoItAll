using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.AgentFramework;

public enum ProviderFeedbackKind { Success, Warning, Error }
public sealed record ProviderEditorFeedback(ProviderFeedbackKind Kind, string Title, string Message);

public sealed class ProviderEditorOperations(ProviderProfilesSession session, IProviderEditorCommands commands) {
    private Operation? active;
    private PendingCommit? pending;
    private long? unconfirmedVersion;
    private long generation;

    public bool IsBusy => active is { } operation && IsCurrent(operation);
    public bool HasPendingReconciliation => pending is { } commit && session.IsCurrentSelection(commit.Version);
    public bool IsWriteUnconfirmed => unconfirmedVersion is { } version && session.IsCurrentSelection(version);
    public bool WritesBlocked => IsBusy || HasPendingReconciliation || IsWriteUnconfirmed;

    public async Task<ProviderEditorFeedback?> SaveAsync() {
        if (WritesBlocked || !session.CanEdit || session.IsSourceManaged) {
            return null;
        }
        var submission = ProviderEditorSubmission.Capture(session.Draft);
        var request = submission.CreateRequest();
        if (string.IsNullOrWhiteSpace(request.DefaultModel) ||
            (request.SuggestedModels.Count > 0 && !request.SuggestedModels.Contains(request.DefaultModel.Trim(), StringComparer.OrdinalIgnoreCase))) {
            return new(ProviderFeedbackKind.Error, "Provider save rejected",
                "Choose a default model from this provider's model catalog before saving.");
        }
        var operation = Begin();
        try {
            var result = await commands.SaveAsync(submission, operation.Token);
            if (!IsCurrent(operation)) {
                return null;
            }
            if (result.Disposition != ProviderWriteDisposition.Committed) {
                return Rejection(operation, result);
            }
            session.BindCommittedIdentity(result.ProviderId!.Value, result.ConcurrencyToken);
            pending = new(operation.Version, result.ProviderId.Value, submission, Deleted: false, RepairProjection: result.Message is not null);
            if (result.Message is not null) {
                return new(ProviderFeedbackKind.Warning, "Provider saved", result.Message);
            }
            return await ReconcileAsync(operation, repair: false);
        } catch (OperationCanceledException) when (operation.Token.IsCancellationRequested) {
            return null;
        } catch (Exception) {
            if (!IsCurrent(operation)) {
                return null;
            }
            return Rejection(operation, new(ProviderWriteDisposition.Unconfirmed,
                Message: "The provider write is unconfirmed. Verify its canonical state before another write."));
        } finally {
            End(operation);
        }
    }

    public async Task<ProviderEditorFeedback?> DeleteAsync() {
        if (WritesBlocked || !session.CanEdit || session.IsSourceManaged || session.State.ProviderId is not { } providerId) {
            return null;
        }
        var operation = Begin();
        try {
            var result = await commands.DeleteAsync(providerId, operation.Token);
            if (!IsCurrent(operation)) {
                return null;
            }
            if (result.Disposition != ProviderWriteDisposition.Committed) {
                return Rejection(operation, result);
            }
            session.MarkTargetUnavailable("This provider was deleted. Select another provider or create a new draft.");
            pending = new(operation.Version, providerId, null, Deleted: true, RepairProjection: result.Message is not null);
            if (result.Message is not null) {
                return new(ProviderFeedbackKind.Warning, "Provider deleted", result.Message);
            }
            return await ReconcileAsync(operation, repair: false);
        } catch (OperationCanceledException) when (operation.Token.IsCancellationRequested) {
            return null;
        } catch (Exception) {
            if (!IsCurrent(operation)) {
                return null;
            }
            return Rejection(operation, new(ProviderWriteDisposition.Unconfirmed,
                Message: "The provider write is unconfirmed. Verify its canonical state before another write."));
        } finally {
            End(operation);
        }
    }

    public async Task<ProviderEditorFeedback?> CheckHealthAsync() {
        if (WritesBlocked || !session.CanEdit || session.State.ProviderId is not { } providerId) {
            return null;
        }
        var sourceManaged = session.IsSourceManaged;
        var operation = Begin();
        try {
            var result = await commands.CheckHealthAsync(providerId, sourceManaged, operation.Token);
            if (!IsCurrent(operation)) {
                return null;
            }
            if (result.Persistence is { Disposition: not ProviderWriteDisposition.Committed } failed) {
                return Rejection(operation, failed);
            }
            if (result.Persistence is { } committed) {
                pending = new(operation.Version, providerId, null, Deleted: false, RepairProjection: committed.Message is not null);
                if (committed.Message is not null) {
                    return new(ProviderFeedbackKind.Warning, "Provider health saved", committed.Message);
                }
                var reconciliation = await ReconcileAsync(operation, repair: false);
                if (!IsCurrent(operation) || HasPendingReconciliation) {
                    return reconciliation;
                }
            }
            if (!IsCurrent(operation)) {
                return null;
            }
            return result.Health is { } health
                ? new(health.Success ? ProviderFeedbackKind.Success : ProviderFeedbackKind.Warning,
                    "Provider health check", health.Summary)
                : new(ProviderFeedbackKind.Warning, "Provider health check", "The diagnostic result could not be read.");
        } catch (OperationCanceledException) when (operation.Token.IsCancellationRequested) {
            return null;
        } catch (Exception) {
            if (!IsCurrent(operation)) {
                return null;
            }
            if (sourceManaged) {
                return new(ProviderFeedbackKind.Warning, "Provider health check", "The diagnostic did not complete. No local provider write was requested.");
            }
            return Rejection(operation, new(ProviderWriteDisposition.Unconfirmed,
                Message: "The provider write is unconfirmed. Verify its canonical state before another write."));
        } finally {
            End(operation);
        }
    }

    public async Task<ProviderEditorFeedback?> DiscoverModelsAsync() {
        if (WritesBlocked || !session.CanEdit || session.IsSourceManaged) {
            return null;
        }
        var submission = ProviderEditorSubmission.Capture(session.Draft);
        var operation = Begin();
        try {
            var result = await commands.DiscoverModelsAsync(submission, operation.Token);
            if (!IsCurrent(operation)) {
                return null;
            }
            if (!result.IsSuccess) {
                return new(ProviderFeedbackKind.Warning, "Provider models unavailable", "Model discovery did not succeed. The draft is retained.");
            }
            if (submission.HasLaterEdits(session.Draft)) {
                return new(ProviderFeedbackKind.Warning, "Provider draft changed", "The discovery result was not applied because the draft changed. Retry discovery when ready.");
            }
            session.Draft.ModelPrices = result.Value!.ModelPrices;
            session.Draft.SuggestedModels = result.Value.Models.ToList();
            if (!session.Draft.SuggestedModels.Contains(session.Draft.DefaultModel, StringComparer.OrdinalIgnoreCase)) {
                session.Draft.DefaultModel = string.Empty;
            }
            return new(ProviderFeedbackKind.Success, "Provider models loaded",
                "Models and pricing were loaded into this draft. Review the default model before saving.");
        } catch (OperationCanceledException) when (operation.Token.IsCancellationRequested) {
            return null;
        } catch (Exception) {
            return IsCurrent(operation) ? new(ProviderFeedbackKind.Error, "Provider models unavailable", "Model discovery failed. The draft is retained.") : null;
        } finally {
            End(operation);
        }
    }

    public async Task<ProviderEditorFeedback?> RetryReconciliationAsync() {
        if (IsBusy || !HasPendingReconciliation) {
            return null;
        }
        var operation = Begin();
        try {
            return await ReconcileAsync(operation, repair: true);
        } catch (OperationCanceledException) when (operation.Token.IsCancellationRequested) {
            return null;
        } finally {
            End(operation);
        }
    }

    private async Task<ProviderEditorFeedback?> ReconcileAsync(Operation operation, bool repair) {
        var commit = pending!;
        try {
            if (repair && commit.RepairProjection) {
                await commands.ReconcileAsync(commit.ProviderId, operation.Token);
                if (!IsCurrent(operation)) {
                    return null;
                }
                pending = commit = commit with { RepairProjection = false };
            }
            var catalogRead = await session.RefreshMetadataAsync(operation.Token);
            if (!IsCurrent(operation)) {
                return null;
            }
            var editorRead = commit.Deleted || await session.ReconcileCommittedAsync(commit.Submission, operation.Token);
            if (!IsCurrent(operation)) {
                return null;
            }
            if (!catalogRead || !editorRead) {
                return new(ProviderFeedbackKind.Warning, "Provider change saved",
                    "The change is committed. Retry reconciliation to refresh its state without repeating the write.");
            }
            pending = null;
            return new(ProviderFeedbackKind.Success, commit.Deleted ? "Provider deleted" : "Provider saved",
                commit.Deleted ? "The provider was deleted." : "The committed provider state is current.");
        } catch (OperationCanceledException) when (operation.Token.IsCancellationRequested) {
            return null;
        } catch (Exception) {
            return IsCurrent(operation) ? new(ProviderFeedbackKind.Warning, "Provider change saved",
                "Reconciliation is still pending. Retrying it does not repeat the write.") : null;
        }
    }

    private ProviderEditorFeedback Rejection(Operation operation, ProviderWriteResult result) {
        if (result.Disposition == ProviderWriteDisposition.Unconfirmed) {
            unconfirmedVersion = operation.Version;
        }
        return new(ProviderFeedbackKind.Error, "Provider operation not completed", result.Message ?? "The provider request was rejected.");
    }

    private Operation Begin() => active = new(++generation, session.SelectionVersion, session.TargetCancellationToken);
    private bool IsCurrent(Operation operation) => ReferenceEquals(active, operation) &&
        session.IsCurrentSelection(operation.Version) && !operation.Token.IsCancellationRequested;
    private void End(Operation operation) {
        if (ReferenceEquals(active, operation)) {
            active = null;
        }
    }
    private sealed record Operation(long Generation, long Version, CancellationToken Token);
    private sealed record PendingCommit(long Version, Guid ProviderId, ProviderEditorSubmission? Submission, bool Deleted, bool RepairProjection);
}
