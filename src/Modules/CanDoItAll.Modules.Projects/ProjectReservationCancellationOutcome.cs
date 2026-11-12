namespace CanDoItAll.Modules.Projects;

public abstract record ProjectReservationCancellationObservation {
    private ProjectReservationCancellationObservation() {
    }

    public sealed record Committed(ProjectDeletionCompletionBinding Completion) : ProjectReservationCancellationObservation;
    public sealed record NotObserved : ProjectReservationCancellationObservation;
    public sealed record Unavailable(ProjectCreationCompensationUnavailableReason Reason,
        bool DiagnosticsUnavailable = false) : ProjectReservationCancellationObservation;
}

public abstract record ProjectReservationCancellationOutcome {
    private ProjectReservationCancellationOutcome() {
    }

    public sealed record Committed(ProjectReservationCancellationRequest Original, ProjectDeletionCompletionBinding Completion,
        bool Replayed, ProjectLifecycleCompletionIssue? CompletionIssue = null, bool DiagnosticsUnavailable = false)
        : ProjectReservationCancellationOutcome;
    public sealed record Declined(ProjectReservationCancellationRequest Original, bool DiagnosticsUnavailable = false)
        : ProjectReservationCancellationOutcome;
    public sealed record Unconfirmed(ProjectReservationCancellationRequest Original, ProjectCreationCompensationRecoveryReason Reason,
        bool DiagnosticsUnavailable = false) : ProjectReservationCancellationOutcome;
}
