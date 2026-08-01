using CanDoItAll.Components.Gantt;
using System.Text.Json.Serialization;

namespace CanDoItAll.Modules.Workbench;

public sealed record ProjectStructureGanttTaskEditModel(
    GanttTaskId TaskId,
    string Title,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    int ProgressPercent,
    ProjectTaskEstimate Estimate,
    ProjectStructureTaskResourceSelection? Assignee,
    bool ScheduleReadOnly = false,
    bool CanChangeDirectAssignee = true,
    ProjectTaskExecutionSnapshot? Execution = null,
    ProjectTaskExpectedCostBasis? ExpectedCostBasis = null,
    long DirectAssignmentRevision = 0);

public sealed record ProjectStructureTaskEditDialogResult(
    GanttTaskId TaskId,
    string Title,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    int ProgressPercent,
    ProjectTaskEstimate Estimate,
    bool AssigneeChanged,
    ProjectStructureTaskResourceSelection? Assignee,
    ProjectStructureTaskResourceSelection? ResourceToAttach = null,
    ProjectTaskExecutionSnapshot? Execution = null);

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
    ProjectStructureTaskResourceSelection? ProposedAssignee,
    ProjectTaskExecutionSnapshot CurrentExecution,
    ProjectTaskExecutionSnapshot ProposedExecution,
    [property: JsonRequired] ProjectTaskExpectedCostBasis? CurrentCostBasis,
    long CurrentDirectAssignmentRevision);

public sealed record ProjectStructureTaskDetailsMutationRequest(
    GanttTaskId TaskId,
    string CurrentTitle,
    string ProposedTitle,
    int CurrentProgressPercent,
    int ProposedProgressPercent,
    ProjectTaskEstimate CurrentEstimate,
    ProjectTaskEstimate ProposedEstimate,
    GanttTaskScheduleChangeRequest? ScheduleChange,
    ProjectTaskExecutionSnapshot CurrentExecution,
    ProjectTaskExecutionSnapshot ProposedExecution,
    ProjectTaskExpectedCostBasis? CurrentCostBasis,
    ProjectTaskExpectedCostBasis? ProposedCostBasis,
    bool CostBasisChanged,
    long CurrentDirectAssignmentRevision);

public sealed record ProjectStructureTaskDetailsUpdateResult(
    ProjectStructureGanttMutationResult Mutation,
    ProjectStructureTaskEstimateRefreshResult Pricing);

public enum ProjectStructureTaskDetailsErrorCode
{
    InvalidRequest,
    ConcurrencyConflict,
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
