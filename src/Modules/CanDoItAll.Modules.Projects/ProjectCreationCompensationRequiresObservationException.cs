namespace CanDoItAll.Modules.Projects;

public sealed class ProjectCreationCompensationRequiresObservationException(ProjectCreationCompensationOutcome outcome)
    : Exception(outcome is ProjectCreationCompensationOutcome.Committed
        ? "The original creation compensation committed, but its completion needs observation. Retain the exact original completion binding."
        : "The original creation compensation is unconfirmed. Observe its original address before any further mutation.") {
    public ProjectCreationCompensationOutcome Outcome { get; } = outcome;
}
