using CanDoItAll.Components.Gantt;
using CanDoItAll.Modules.Workbench;

namespace CanDoItAll.Workbench.Planning.UI;

public sealed record PlanningGanttTaskEditModel(
    GanttTaskId TaskId,
    string Title,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    int ProgressPercent,
    ProjectTaskEstimate Estimate,
    ProjectStructureTaskResourceSelection? Assignee,
    bool ScheduleReadOnly,
    bool CanChangeDirectAssignee,
    ProjectTaskExecutionSnapshot? Execution);

public sealed record PlanningGanttTaskCreate(
    string Title,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    string? AfterTaskNodeId,
    ProjectStructureTaskResourceSelection? Resource,
    ProjectTaskEstimate Estimate);

public enum PlanningTaskSaveStatus { Accepted, Committed, Rejected, Partial, Unknown }
public enum PlanningTaskPhase { Task, Assignment, Pricing, Attachment, RowOrder, Compensation }
public enum PlanningTaskPhaseState { NotAttempted, Committed, Rejected, Compensated, Unconfirmed }

public sealed record PlanningTaskPhaseOutcome(PlanningTaskPhase Phase, PlanningTaskPhaseState State, string Message);

public sealed record PlanningTaskSaveResult(
    PlanningTaskSaveStatus Status,
    string Message,
    IReadOnlyList<string> TaskIds,
    IReadOnlyList<string> ResourceNodeIds,
    IReadOnlyList<PlanningTaskPhaseOutcome> Phases,
    bool RequiresReadback = false) {
    public Guid OperationId { get; init; }
    public bool AllowsCorrectedSubmission { get; init; } = true;
    public bool CanRetry => Status == PlanningTaskSaveStatus.Rejected && !RequiresReadback && AllowsCorrectedSubmission;
    public bool ShouldClose => Status is PlanningTaskSaveStatus.Accepted or PlanningTaskSaveStatus.Committed && !RequiresReadback;
    public static PlanningTaskSaveResult Accepted { get; } = new(PlanningTaskSaveStatus.Accepted, "Input accepted.", [], [], []);
}
