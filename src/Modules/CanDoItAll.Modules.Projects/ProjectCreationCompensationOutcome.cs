namespace CanDoItAll.Modules.Projects;

public enum ProjectCreationCompensationRefusal { OriginalCreationChanged, TransactionConflict }
public enum ProjectCreationCompensationRecoveryReason { ReadUnavailable, OwnerReleaseUnconfirmed, CommitUnconfirmed }

public abstract record ProjectCreationCompensationOutcome {
    private ProjectCreationCompensationOutcome() {
    }

    public sealed record Committed(ProjectCreationCompensationRequest Original, ProjectDeletionCompletionBinding Completion,
        bool Replayed, ProjectLifecycleCompletionIssue? CompletionIssue = null, bool DiagnosticsUnavailable = false)
        : ProjectCreationCompensationOutcome;
    public sealed record Declined(ProjectCreationCompensationRequest Original, ProjectCreationCompensationRefusal Reason,
        bool DiagnosticsUnavailable = false) : ProjectCreationCompensationOutcome;
    public sealed record Unconfirmed(ProjectCreationCompensationRequest Original, ProjectCreationCompensationRecoveryReason Reason,
        bool DiagnosticsUnavailable = false) : ProjectCreationCompensationOutcome;
}
