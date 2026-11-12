using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Modules.Workbench;

public sealed record ProjectCreationPartialCompletion(Guid ProjectId, bool CreationObserved, bool RequestedOperationCompleted = false,
    ProjectDeletionRecovery? CompensationDeletion = null) {
    internal ProjectCreationReceipt? CreationReceipt { get; init; }
}

internal static class ProjectCreationPartialCompletionFailure {
    internal static ProjectStructureAgentException Create(Guid projectId, bool created, Exception original,
        ProjectCreationReceipt? creationReceipt = null, ProjectDeletionRecovery? compensationDeletion = null) => ProjectStructureAgentException.CreateMapped(
        409, "ProjectCreationRequiresObservation",
        compensationDeletion is not null
            ? $"Project '{projectId:D}' was created and its compensating deletion committed, but the requested operation and deletion completion did not both finish. {compensationDeletion.RetryGuidance}"
            : created
            ? $"Project '{projectId:D}' was created, but the requested operation did not complete. Original creation evidence was retained. Inspect the current project state before retrying."
            : $"Creation of project '{projectId:D}' was not acknowledged and it may already exist. No rollback was inferred. Inspect the project and original reservation before retrying.",
        new ProjectCreationPartialCompletion(projectId, created, CompensationDeletion: compensationDeletion) { CreationReceipt = creationReceipt }, isSafeToExpose: true, canRetryWithCorrectedInput: false,
        innerException: original, effectState: AgentToolEffectState.Unknown);
}
