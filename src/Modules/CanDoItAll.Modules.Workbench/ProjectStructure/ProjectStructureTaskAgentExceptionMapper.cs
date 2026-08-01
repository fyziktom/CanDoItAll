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
        return new ProjectStructureAgentException(
            statusCode,
            exception.Code.ToString(),
            exception.Message,
            new { exception.Code });
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
        return new ProjectStructureAgentException(
            statusCode,
            exception.Code.ToString(),
            exception.Message,
            new { exception.Code });
    }
}
