namespace CanDoItAll.Modules.Projects.Pages.Components;

public sealed class ProjectEditorOperationSession : IDisposable {
    private bool seedAttempted;

    public ProjectEditorOperationSession(ProjectEditorDraft draft, ProjectEditorSubmission submitted,
        ProjectPendingOperationSession pending, bool restored = false) {
        Draft = draft;
        Submitted = submitted;
        Pending = pending;
        Restored = restored;
    }

    public ProjectEditorDraft Draft { get; }
    public ProjectEditorSubmission Submitted { get; }
    public ProjectPendingOperationSession Pending { get; }
    public bool Restored { get; }
    public bool IsRetired { get; private set; }
    public bool IsEvidenceDiscarded { get; private set; }
    public bool HadPreparationFailure { get; private set; }
    public bool SeedOutcomeUnknown { get; private set; }
    public ProjectDefinitionOperation Operation => Pending.Frozen.Submission.Operation;
    public bool CanSeed => !IsRetired && !Restored && !seedAttempted && Submitted.Seeds.Count > 0 && Pending.IsReconciled &&
        Pending.LastOutcome is ProjectPendingDispatchOutcome.Full { Outcome: ProjectDefinitionOutcome<ProjectEditorAcknowledgement>.Accepted { Replayed: false } };

    public async Task PrepareAsync(CancellationToken cancellationToken = default) {
        try {
            await Pending.PrepareAsync(cancellationToken);
        } catch {
            HadPreparationFailure = true;
            throw;
        }
    }

    public void RememberAcceptance() {
        if (Pending.KnownAccepted is ProjectDefinitionAcceptedReceipt.Full full) {
            Draft.RecordAcceptedOutcome(Submitted, full.Result.Acknowledgement);
        }
    }

    public void Reconcile() {
        ObjectDisposedException.ThrowIf(IsRetired, this);
        RememberAcceptance();
        Pending.Reconcile(receipt => {
            if (receipt is not ProjectDefinitionAcceptedReceipt.Full full) {
                throw new InvalidOperationException("The full editor cannot reconcile a scalar-only acknowledgement.");
            }
            Draft.Acknowledge(Submitted, full.Result.Acknowledgement);
        });
    }

    public void BeginSeedAttempt() {
        if (!CanSeed) {
            throw new InvalidOperationException("This operation cannot start a new starter-object effect.");
        }
        seedAttempted = true;
        SeedOutcomeUnknown = true;
        Draft.MarkSeedsDispatched(Submitted);
        Draft.Mutation = ProjectEditorMutationState.Seeding;
    }

    public void CompleteSeedAttempt() {
        if (!seedAttempted || !SeedOutcomeUnknown) {
            throw new InvalidOperationException("There is no outstanding starter-object attempt.");
        }
        Draft.AcknowledgeSeeds(Submitted);
        SeedOutcomeUnknown = false;
    }

    public void RefuseSeedAttempt() {
        if (!seedAttempted || !SeedOutcomeUnknown) {
            throw new InvalidOperationException("There is no outstanding starter-object attempt.");
        }
        SeedOutcomeUnknown = false;
    }

    public void Dispose() {
        IsRetired = true;
        Pending.Dispose();
    }

    public void MarkEvidenceDiscarded() {
        IsEvidenceDiscarded = true;
        Dispose();
    }
}

public enum ProjectPendingEditorAction { Check, Retry, ApplyAccepted, CancelPrepared, ClearResolved, ReadCurrent }

public sealed record ProjectPendingEditorIntent(ProjectDefinitionOperation Operation, ProjectPendingEditorAction Action);

public sealed record ProjectPendingEditorView(ProjectDefinitionOperation Operation, string Status, bool Busy,
    bool CanRetry, bool CanApply, bool CanCancel, bool CanClear, bool CanReadCurrent, bool HadStarterPlan);
