namespace CanDoItAll.Modules.Projects;

public sealed class ProjectReservationCancellationRequiresObservationException(ProjectReservationCancellationOutcome outcome)
    : Exception("Original reservation cancellation needs observation. Retain its exact completion identity and do not revoke a grant independently.") {
    public ProjectReservationCancellationOutcome Outcome { get; } = outcome;
}
