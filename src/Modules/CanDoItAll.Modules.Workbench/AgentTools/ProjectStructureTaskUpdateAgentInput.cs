using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.Gantt;
using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Modules.Workbench;

/// <summary>
/// The agent-facing form of <see cref="ProjectStructureTaskDetailsUpdateRequest"/>, used by the task update tool and
/// the Project Structure HTTP API. The Gantt identifier and schedule change are component value types that JSON
/// cannot construct: an identifier bound from JSON stays empty and the schedule change has no bindable constructor.
/// Callers therefore send plain task identifiers, and <see cref="ToRequest"/> builds the Gantt contract through its
/// validating constructors.
/// </summary>
public sealed record ProjectStructureTaskUpdateAgentInput(
    string TaskId,
    string CurrentTitle,
    string ProposedTitle,
    int CurrentProgressPercent,
    int ProposedProgressPercent,
    ProjectTaskEstimate CurrentEstimate,
    ProjectTaskEstimate ProposedEstimate,
    ProjectStructureTaskScheduleAgentChange? ScheduleChange,
    bool AssigneeChanged,
    ProjectStructureTaskResourceSelection? ProposedAssignee,
    ProjectTaskExecutionSnapshot CurrentExecution,
    ProjectTaskExecutionSnapshot ProposedExecution,
    [property: JsonRequired] ProjectTaskExpectedCostBasis? CurrentCostBasis,
    long CurrentDirectAssignmentRevision)
{
    /// <summary>
    /// The project admission an HTTP caller read with the task. The agent tool captures its own admission and replaces
    /// this value.
    /// </summary>
    public ProjectWriteAdmission? ExpectedProjectAdmission { get; init; }

    public ProjectStructureTaskDetailsUpdateRequest ToRequest()
    {
        try
        {
            var taskId = new GanttTaskId(TaskId);
            return new ProjectStructureTaskDetailsUpdateRequest(
                taskId,
                CurrentTitle,
                ProposedTitle,
                CurrentProgressPercent,
                ProposedProgressPercent,
                CurrentEstimate,
                ProposedEstimate,
                ScheduleChange?.ToRequest(taskId),
                AssigneeChanged,
                ProposedAssignee,
                CurrentExecution,
                ProposedExecution,
                CurrentCostBasis,
                CurrentDirectAssignmentRevision)
            {
                ExpectedProjectAdmission = ExpectedProjectAdmission
            };
        }
        catch (ArgumentException exception)
        {
            // The component constructors reject blank identifiers, unknown gestures and inverted intervals before
            // anything is read or written.
            throw ProjectStructureAgentException.CreateAgentVisible(
                400,
                "TaskUpdateRequestInvalid",
                $"The task update request is invalid: {DescribeRejection(exception)}. Read the task again and send its task node id and exact intervals.",
                canRetryWithCorrectedInput: true,
                effectState: AgentToolEffectState.None);
        }
    }

    private static string DescribeRejection(ArgumentException exception)
        => (exception.ParamName is { Length: > 0 } parameter
                ? exception.Message.Replace($" (Parameter '{parameter}')", string.Empty, StringComparison.Ordinal)
                : exception.Message)
            .Split(Environment.NewLine)[0]
            .Trim()
            .TrimEnd('.');
}

/// <summary>
/// A schedule change for the updated task: every task whose interval moves, with its previous and proposed interval.
/// </summary>
public sealed record ProjectStructureTaskScheduleAgentChange(
    GanttScheduleGesture Gesture,
    IReadOnlyList<ProjectStructureTaskDateAgentChange> AffectedTasks,
    IReadOnlyList<string>? CriticalTaskIds = null)
{
    internal GanttTaskScheduleChangeRequest ToRequest(GanttTaskId taskId)
        => new(
            taskId,
            Gesture,
            (AffectedTasks ?? []).Select(change => change.ToRequest()),
            (CriticalTaskIds ?? []).Select(id => new GanttTaskId(id)));
}

public sealed record ProjectStructureTaskDateAgentChange(
    string TaskId,
    DateTimeOffset PreviousStart,
    DateTimeOffset PreviousEnd,
    DateTimeOffset ProposedStart,
    DateTimeOffset ProposedEnd,
    bool IsCritical = false)
{
    internal GanttTaskDateChange ToRequest()
        => new(new GanttTaskId(TaskId), PreviousStart, PreviousEnd, ProposedStart, ProposedEnd, IsCritical);
}
