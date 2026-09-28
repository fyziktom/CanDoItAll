using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.Workbench;

public static class ProjectStructureTaskAgentExceptionMapper
{
    public static ProjectStructureAgentException Map(ProjectStructureTaskCreationException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return new ProjectStructureAgentException(
            exception.CompensationSucceeded ? 409 : 500,
            exception.Code.ToString(),
            exception.Message,
            new
            {
                exception.Code,
                exception.Stage,
                exception.TaskNodeId,
                exception.CompensationSucceeded
            });
    }

    public static ProjectStructureAgentException Map(ProjectStructureTaskDetailsException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        var statusCode = exception.Code switch
        {
            ProjectStructureTaskDetailsErrorCode.InvalidRequest => 400,
            ProjectStructureTaskDetailsErrorCode.ConcurrencyConflict or
            ProjectStructureTaskDetailsErrorCode.AssignmentConflict => 409,
            ProjectStructureTaskDetailsErrorCode.AssignmentCompensationFailed => 500,
            _ => 500
        };
        return MapTaskRejection(
            statusCode,
            exception.Code.ToString(),
            exception.Message,
            new { exception.Code },
            exception,
            correctable: exception.Code is not ProjectStructureTaskDetailsErrorCode.AssignmentCompensationFailed);
    }

    public static ProjectStructureAgentException Map(ProjectStructureGanttMutationException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        var statusCode = exception.Code switch
        {
            ProjectStructureGanttMutationErrorCode.ProjectNotFound or
            ProjectStructureGanttMutationErrorCode.TaskNotFound or
            ProjectStructureGanttMutationErrorCode.DependencyNotFound => 404,
            ProjectStructureGanttMutationErrorCode.StaleTask or
            ProjectStructureGanttMutationErrorCode.DuplicateDependency or
            ProjectStructureGanttMutationErrorCode.SystemManagedDependency or
            ProjectStructureGanttMutationErrorCode.CycleDetected => 409,
            _ => 400
        };
        return MapTaskRejection(
            statusCode,
            exception.Code.ToString(),
            exception.Message,
            new { exception.Code },
            exception,
            correctable: true);
    }

    // Task and schedule rejections are decided before the Gantt mutation saves. They prove no effect only while the
    // tool invocation has saved no project row, for example before an assignee change was committed and compensated.
    private static ProjectStructureAgentException MapTaskRejection(
        int statusCode,
        string errorCode,
        string message,
        object details,
        Exception exception,
        bool correctable)
    {
        var provenNoEffect = correctable &&
                             ProjectStructureToolEffectObservation.Current is { DomainWriteSaved: false };
        return ProjectStructureAgentException.CreateMapped(
            statusCode,
            errorCode,
            message,
            details,
            isSafeToExpose: provenNoEffect,
            canRetryWithCorrectedInput: provenNoEffect,
            exception,
            provenNoEffect ? AgentToolEffectState.NotCommitted : AgentToolEffectState.Unknown);
    }
}
