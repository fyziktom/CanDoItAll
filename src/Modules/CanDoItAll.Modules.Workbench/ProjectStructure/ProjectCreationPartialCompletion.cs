using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.Workbench;

public sealed record ProjectCreationPartialCompletion(Guid ProjectId, bool CreationObserved, bool RequestedOperationCompleted = false);

internal static class ProjectCreationPartialCompletionFailure {
    internal static ProjectStructureAgentException Create(Guid projectId, bool created, Exception original) => ProjectStructureAgentException.CreateMapped(
        409, "ProjectCreationRequiresObservation",
        created
            ? $"Project '{projectId:D}' was created, but the requested operation did not complete. Original creation evidence was retained. Inspect the current project state before retrying."
            : $"Creation of project '{projectId:D}' was not acknowledged and it may already exist. No rollback was inferred. Inspect the project and original reservation before retrying.",
        new ProjectCreationPartialCompletion(projectId, created), isSafeToExpose: true, canRetryWithCorrectedInput: false,
        innerException: original, effectState: AgentToolEffectState.Unknown);
}
