using CanDoItAll.Components.Gantt;

namespace CanDoItAll.Modules.Workbench;

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
