namespace CanDoItAll.Modules.Projects;

public enum ProjectCreationCompensationUnavailableReason { OriginalContextChanged, InvalidStoredEvidence, ReadUnavailable }

public abstract record ProjectCreationCompensationObservation {
    private ProjectCreationCompensationObservation() {
    }

    public sealed record Committed(ProjectDeletionCompletionBinding Completion) : ProjectCreationCompensationObservation;
    public sealed record NotObserved : ProjectCreationCompensationObservation;
    public sealed record Unavailable(ProjectCreationCompensationUnavailableReason Reason,
        bool DiagnosticsUnavailable = false) : ProjectCreationCompensationObservation;
}
