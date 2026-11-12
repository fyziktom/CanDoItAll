namespace CanDoItAll.Modules.Projects;

public enum ProjectLifecycleRefusal {
    UnsupportedInterpretation, ContextChanged, IntentMismatch, TargetChanged, EdgeChanged, Cycle,
    WriteDenied, AuthorityChanged, AuthorityUnavailable, AdmissionClosed, Expired, UnknownFutureEpoch,
    TransactionConflict, ActiveCreationReservation
}

public enum ProjectLifecycleRecoveryReason { ReadUnavailable, ReceiptUnavailable, OwnerReleaseUnconfirmed, CommitUnconfirmed }
public enum ProjectLifecycleCompletionIssue { OwnerReleaseUnconfirmed, ReleaseRecordUnconfirmed, PostCommitProcessing, DiagnosticsUnavailable }

public abstract record ProjectLifecycleOutcome {
    private ProjectLifecycleOutcome() {
    }

    public sealed record Accepted(ProjectLifecycleAccepted Result, bool Replayed,
        ProjectLifecycleCompletionIssue? CompletionIssue = null, bool DiagnosticsUnavailable = false) : ProjectLifecycleOutcome;
    public sealed record Refused(ProjectLifecycleSubmission Original, ProjectLifecycleRefusal Reason,
        bool DiagnosticsUnavailable = false) : ProjectLifecycleOutcome;
    public sealed record Unconfirmed(ProjectLifecycleSubmission Original, ProjectLifecycleRecoveryReason Reason,
        bool DiagnosticsUnavailable = false) : ProjectLifecycleOutcome;
}
