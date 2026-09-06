using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using Microsoft.AspNetCore.Components.Forms;

namespace CanDoItAll.Modules.AgentFramework;

public enum ProviderFeedbackKind { Success, Warning, Error }
public sealed record ProviderEditorFeedback(ProviderFeedbackKind Kind, string Title, string Message);

public sealed class ProviderEditorOperations(ProviderProfilesSession session, IProviderEditorCommands commands) {
    private Operation? active;
    private PendingCommit? pending;
    private ProviderUnresolvedAttempt? Unresolved => session.Recovery.Find(session.State.ProviderId);
    private long generation;

    public bool IsBusy => active is { } operation && IsCurrent(operation);
    public bool HasPendingReconciliation => pending is { } commit && session.IsCurrentSelection(commit.Version);
    public bool IsWriteUnconfirmed => Unresolved is { RetryAllowed: false };
    public bool HasVerifiedRetry => Unresolved is { RetryAllowed: true };
    public Guid? CandidateProviderId => Unresolved?.Attempt.ProviderId;
    public bool WritesBlocked => IsBusy || HasPendingReconciliation || IsWriteUnconfirmed;

    public async Task<ProviderEditorFeedback?> SaveAsync() {
        if (WritesBlocked || !session.CanEdit || session.IsSourceManaged) {
            return null;
        }
        var submission = Unresolved is { RetryAllowed: true, Submission: { } retry }
            ? retry : ProviderEditorSubmission.CaptureForSave(session.Draft);
        var request = submission.CreateRequest();
        if (string.IsNullOrWhiteSpace(request.DefaultModel) ||
            (request.SuggestedModels.Count > 0 && !request.SuggestedModels.Contains(request.DefaultModel.Trim(), StringComparer.OrdinalIgnoreCase))) {
            return new(ProviderFeedbackKind.Error, "Provider save rejected",
                "Choose a default model from this provider's model catalog before saving.");
        }
        var operation = Begin(submission.Attempt, submission);
        try {
            var result = await commands.SaveAsync(submission, operation.Token);
            TrackResult(operation, result);
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
            TrackResult(operation, new(ProviderWriteDisposition.Unconfirmed));
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
        var operation = Begin(new(Guid.NewGuid(), providerId, ProviderMutationKind.Delete, session.Draft.ExpectedConcurrencyToken));
        try {
            var result = await commands.DeleteAsync(providerId, operation.Token);
            TrackResult(operation, result);
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
            TrackResult(operation, new(ProviderWriteDisposition.Unconfirmed));
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
        var operation = Begin(sourceManaged ? null : new(Guid.NewGuid(), providerId, ProviderMutationKind.HealthPersistence, session.Draft.ExpectedConcurrencyToken));
        try {
            var result = await commands.CheckHealthAsync(providerId, sourceManaged, operation.Token);
            if (result.Persistence is { } persistence) {
                TrackResult(operation, persistence);
            }
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
            TrackResult(operation, new(ProviderWriteDisposition.Unconfirmed));
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
        TrackResult(operation, result);
        return new(ProviderFeedbackKind.Error, "Provider operation not completed", result.Message ?? "The provider request was rejected.");
    }

    private void TrackResult(Operation operation, ProviderWriteResult result) {
        if (operation.Attempt is not { } attempt) {
            return;
        }
        if (result.Disposition == ProviderWriteDisposition.Unconfirmed ||
            (result.Disposition == ProviderWriteDisposition.Conflict && attempt.IsCreate &&
                session.Recovery.Find(attempt.ProviderId) is not null)) {
            if (result.Attempt is { } receipt && receipt.ProviderId == attempt.ProviderId) {
                attempt = receipt with { AttemptId = attempt.AttemptId, Kind = attempt.Kind };
            }
            session.Recovery.Retain(new(attempt, operation.Submission, operation.Context, operation.Section));
        } else {
            session.Recovery.Complete(attempt);
        }
    }

    public async Task<ProviderEditorFeedback?> VerifyUnconfirmedAsync() {
        if (!session.IsCurrentSelection(session.SelectionVersion) || IsBusy || Unresolved is not { } unresolved) {
            return null;
        }
        if (unresolved.Attempt.IsCreate) {
            session.ResumeNewAttempt(unresolved);
        }
        var operation = Begin();
        try {
            var result = await commands.VerifyAsync(unresolved.Attempt, operation.Token);
            if (!IsCurrent(operation) || result.ProviderId != unresolved.Attempt.ProviderId ||
                session.Recovery.Find(session.State.ProviderId) != unresolved) {
                return null;
            }
            if (result.Disposition == ProviderVerificationDisposition.StillUnconfirmed) {
                return new(ProviderFeedbackKind.Warning, "Provider verification unresolved",
                    "Canonical state could not establish this attempt's outcome. No write was repeated.");
            }
            if (result.Disposition == ProviderVerificationDisposition.DefinitelyNotCommitted) {
                session.Recovery.AllowRetry(unresolved);
                return new(ProviderFeedbackKind.Warning, "Provider write was not committed",
                    "You can retry the verified write. A new provider retry keeps the same candidate identity.");
            }
            session.Recovery.Remove(unresolved);
            var deleted = unresolved.Attempt.Kind == ProviderMutationKind.Delete;
            if (deleted) {
                session.MarkTargetUnavailable("This provider is deleted. Select another provider or create a new draft.");
            } else {
                session.BindCommittedIdentity(result.ProviderId, result.ConcurrencyToken);
            }
            pending = new(operation.Version, result.ProviderId, unresolved.Submission, deleted, RepairProjection: true);
            return await ReconcileAsync(operation, repair: true);
        } catch (OperationCanceledException) when (operation.Token.IsCancellationRequested) {
            return null;
        } catch (Exception) {
            return IsCurrent(operation) ? new(ProviderFeedbackKind.Warning, "Provider verification unresolved",
                "Canonical state could not be read. The attempt remains blocked; no write was repeated.") : null;
        } finally {
            End(operation);
        }
    }

    public Task<ProviderEditorFeedback?> RetryVerifiedAsync() => Unresolved is { RetryAllowed: true } entry
        ? entry.Attempt.Kind switch {
            ProviderMutationKind.Create or ProviderMutationKind.Update => SaveAsync(),
            ProviderMutationKind.Delete => DeleteAsync(),
            ProviderMutationKind.HealthPersistence => CheckHealthAsync(),
            _ => Task.FromResult<ProviderEditorFeedback?>(null)
        }
        : Task.FromResult<ProviderEditorFeedback?>(null);

    private Operation Begin(ProviderMutationAttempt? attempt = null, ProviderEditorSubmission? submission = null) {
        if (attempt is not null) {
            session.Recovery.Begin(attempt);
        }
        return active = new(++generation, session.SelectionVersion, session.TargetCancellationToken,
            attempt, submission, session.EditContext, session.State.Section);
    }
    private bool IsCurrent(Operation operation) => ReferenceEquals(active, operation) &&
        session.IsCurrentSelection(operation.Version) && !operation.Token.IsCancellationRequested;
    private void End(Operation operation) {
        if (ReferenceEquals(active, operation)) {
            active = null;
        }
    }
    private sealed record Operation(long Generation, long Version, CancellationToken Token,
        ProviderMutationAttempt? Attempt, ProviderEditorSubmission? Submission, EditContext Context, ProviderEditorSection Section);
    private sealed record PendingCommit(long Version, Guid ProviderId, ProviderEditorSubmission? Submission, bool Deleted, bool RepairProjection);
}
