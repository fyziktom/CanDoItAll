using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.Gantt;
using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Modules.Workbench;

/// <summary>
/// Task update request: the task state the caller last read (the current members) paired with the values it wants
/// (the proposed members), sent as plain JSON with string task identifiers. Every current member is an edit
/// precondition copied unchanged from the owner's latest read, never a guess and never the desired value; the owner
/// rejects the whole update with HTTP 409 when any of them no longer matches the stored task. To leave a field
/// unchanged, send the same value as its current and its proposed member. Enum members are JSON integers. The agent
/// task update tool accepts the same shape but supplies its own project admission.
/// </summary>
/// <param name="TaskId">
/// String node identifier of the canonical task, exactly as returned in <c>nodes[].id</c> by the structure read,
/// for example <c>custom:3f2504e04f8911d39a0c0305e82c3301</c>. It must equal the <c>taskId</c> route value (ordinal,
/// case-sensitive comparison); a different value is rejected with HTTP 400 <c>TaskRouteMismatch</c>. It is not the
/// task title and not an object with a <c>value</c> member.
/// </param>
/// <param name="CurrentTitle">
/// Task title from the caller's latest read (<c>nodes[].title</c>). It must match the stored title exactly
/// (ordinal comparison); otherwise the update is rejected as stale with HTTP 409 <c>StaleTask</c>. Cannot be blank.
/// </param>
/// <param name="ProposedTitle">
/// Title the task should have after the update. Send the current title unchanged when not renaming. Cannot be blank.
/// </param>
/// <param name="CurrentProgressPercent">
/// Progress from the caller's latest read (<c>nodes[].progressPercent</c>): -1 means progress is untracked, otherwise
/// a value from 0 through 100. It is a precondition compared with the stored value, not the requested progress.
/// </param>
/// <param name="ProposedProgressPercent">
/// Requested tracked progress from 0 through 100. -1 (untracked) is not accepted here even when the current value is
/// -1; send 0 to start tracking an untracked task without reporting progress.
/// </param>
/// <param name="CurrentEstimate">
/// Estimate from the caller's latest read, taken from <c>workItem</c> in the task's <c>metadataJson</c>
/// (<c>expectedEffortHours</c>, <c>expectedEffortUnit</c>, <c>expectedCostAmount</c>,
/// <c>expectedCostCurrencyCode</c>; a member missing there is null). The owner compares it, after normalization,
/// with the stored estimate.
/// </param>
/// <param name="ProposedEstimate">
/// Requested estimate, validated and normalized by the same policy as the current estimate. While the task has not
/// started, a changed cost amount and currency are accepted; once work has started or finished, the stored cost
/// amount and currency are kept and only the effort changes.
/// </param>
/// <param name="ScheduleChange">
/// Requested schedule move, or null to leave the planned interval unchanged. When present it must describe the edited
/// task and every task whose interval moves with it, each with the interval the caller read.
/// </param>
/// <param name="AssigneeChanged">
/// True to apply <c>proposedAssignee</c> as the task's direct assignee; false to leave direct assignments untouched,
/// in which case <c>proposedAssignee</c> is ignored.
/// </param>
/// <param name="ProposedAssignee">
/// Direct assignee to set when <c>assigneeChanged</c> is true: a person or agent with a nonempty
/// <c>resourceId</c> and no <c>versionId</c>. Null together with <c>assigneeChanged: true</c> removes the direct
/// assignee. A task with more than one direct assignee cannot be changed through this request (HTTP 409
/// <c>AssignmentConflict</c>). Workflows and processes are attached through the task resource operation instead.
/// </param>
/// <param name="CurrentExecution">
/// Execution state from the caller's latest read, taken from <c>workItem.executionState</c>,
/// <c>workItem.actualStartedAtUtc</c> and <c>workItem.actualEndedAtUtc</c> in the task's <c>metadataJson</c>.
/// Required; it is compared with the stored execution state.
/// </param>
/// <param name="ProposedExecution">
/// Requested execution state and actual timestamps. Allowed transitions: from NotStarted to Started, Completed or
/// Cancelled; from Started to Completed or Cancelled; from Unknown to any state; and to the same state. Completed
/// and Cancelled are final. The timestamps must match the requested state.
/// </param>
/// <param name="CurrentCostBasis">
/// Expected-cost basis from the caller's latest read, taken from <c>workItem.expectedCostBasis</c> in the task's
/// <c>metadataJson</c>. This member must always be present: send null when the read returned no basis. Omitting it is
/// rejected before the update is attempted. Do not construct a substitute basis.
/// </param>
/// <param name="CurrentDirectAssignmentRevision">
/// Revision of the task's direct assignments from the caller's latest read, taken from
/// <c>workItem.directAssignmentRevision</c> in the task's <c>metadataJson</c> (0 when absent). It counts committed
/// assignment changes; it is not the number of assignees. Must not be negative.
/// </param>
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
    /// Project write admission returned as <c>expectedProjectAdmission</c> by the structure read, sent back unchanged.
    /// The HTTP operation requires it even though the schema allows null: when it is omitted, null or names another
    /// project, the update is rejected with HTTP 409 <c>ProjectLifetimeRefreshRequired</c> and nothing is written.
    /// Read the structure again to obtain a fresh admission; never construct one.
    /// </summary>
    /// <remarks>
    /// The property is nullable because the agent task update tool replaces it with the admission captured for its
    /// own invocation.
    /// </remarks>
    public ProjectWriteAdmission? ExpectedProjectAdmission { get; init; }

    /// <summary>
    /// Converts the plain task identifiers into the validated Gantt contract; an invalid identifier, gesture or
    /// interval becomes the HTTP 400 <c>TaskUpdateRequestInvalid</c> rejection before anything is read or written.
    /// </summary>
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
/// Schedule change for the updated task: how the interval was edited and every task whose planned interval moves,
/// each with the interval the caller read and the interval it proposes.
/// </summary>
/// <param name="Gesture">
/// How the planned interval is being edited, as a JSON integer: 0 Move (shift the whole interval), 1 ResizeStart
/// (change only the start), 2 ResizeEnd (change only the end), 3 SetInterval (set an explicit start and end).
/// Other values are rejected with HTTP 400 <c>TaskUpdateRequestInvalid</c>.
/// </param>
/// <param name="AffectedTasks">
/// Every task whose interval changes, including the edited task itself. Only the edited task and tasks that depend
/// on it may appear, each at most once.
/// </param>
/// <param name="CriticalTaskIds">
/// Optional node identifiers the client considers critical-path tasks, as plain strings. The owner does not use them
/// to validate or store the schedule; null or omitted means an empty list, and a blank identifier is rejected with
/// HTTP 400 <c>TaskUpdateRequestInvalid</c>.
/// </param>
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

/// <summary>
/// Planned-interval change for one task in a schedule change: the interval the caller read and the interval it
/// proposes. Timestamps are ISO 8601 instants with an offset.
/// </summary>
/// <param name="TaskId">
/// String node identifier of the task whose interval moves, as returned in <c>nodes[].id</c>.
/// </param>
/// <param name="PreviousStart">
/// Planned start from the caller's latest read (<c>nodes[].startUtc</c>). It must equal the stored start exactly;
/// otherwise the update is rejected as stale with HTTP 409 <c>StaleTask</c>.
/// </param>
/// <param name="PreviousEnd">
/// Planned end from the caller's latest read (<c>nodes[].endUtc</c>), compared exactly like the previous start.
/// </param>
/// <param name="ProposedStart">Requested planned start. It must be earlier than the proposed end.</param>
/// <param name="ProposedEnd">
/// Requested planned end. It must be later than the proposed start; an empty or inverted interval is rejected with
/// HTTP 400 <c>TaskUpdateRequestInvalid</c>.
/// </param>
/// <param name="IsCritical">
/// Client-side critical-path marker carried with the change; the owner does not use it. Defaults to false when
/// omitted.
/// </param>
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
