using CanDoItAll.Components.Gantt;

namespace CanDoItAll.Modules.Workbench;

public sealed record ProjectStructureGanttTaskEditModel(
    GanttTaskId TaskId,
    string Title,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    int ProgressPercent,
    ProjectTaskEstimate Estimate,
    ProjectStructureTaskResourceSelection? Assignee,
    bool ScheduleReadOnly = false);

public sealed record ProjectStructureTaskEditDialogResult(
    GanttTaskId TaskId,
    string Title,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    int ProgressPercent,
    ProjectTaskEstimate Estimate,
    bool AssigneeChanged,
    ProjectStructureTaskResourceSelection? Assignee);

public sealed record ProjectStructureTaskDetailsUpdateRequest(
    GanttTaskId TaskId,
    string CurrentTitle,
    string ProposedTitle,
    int CurrentProgressPercent,
    int ProposedProgressPercent,
    ProjectTaskEstimate CurrentEstimate,
    ProjectTaskEstimate ProposedEstimate,
    GanttTaskScheduleChangeRequest? ScheduleChange,
    bool AssigneeChanged,
    ProjectStructureTaskResourceSelection? ProposedAssignee);

public enum ProjectStructureTaskDetailsErrorCode
{
    InvalidRequest,
    AssignmentConflict,
    AssignmentCompensationFailed
}

public sealed class ProjectStructureTaskDetailsException : Exception
{
    public ProjectStructureTaskDetailsException(
        ProjectStructureTaskDetailsErrorCode code,
        string message,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Code = code;
    }

    public ProjectStructureTaskDetailsErrorCode Code { get; }
}
