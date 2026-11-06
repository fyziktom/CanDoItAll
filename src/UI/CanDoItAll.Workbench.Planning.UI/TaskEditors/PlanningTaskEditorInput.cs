using CanDoItAll.Modules.Workbench;

namespace CanDoItAll.Workbench.Planning.UI;

public sealed record TaskExecutionInputText(ProjectTaskExecutionState State, string Started, string Ended);

public abstract record PlanningTaskEditorInput {
    public sealed record Gantt(string Title, string Start, string End, string Duration, string Progress,
        TaskEstimateInputText? Estimate, TaskExecutionInputText? Execution,
        ProjectStructureTaskResourceSelection? Assignee, ProjectStructureTaskResourceSelection? Attachment,
        ProjectStructureTaskResourceSelection? SelectedResource) : PlanningTaskEditorInput;

    public sealed record Structure(string Title, string Subtitle, string Notes, string Due, string? Repository,
        TaskEstimateInputText? Estimate, TaskExecutionInputText? Execution,
        ProjectStructureTaskResourceSelection? Assignee, ProjectStructureTaskResourceSelection? Attachment,
        ProjectStructureTaskResourceSelection? SelectedResource) : PlanningTaskEditorInput;
}

public sealed record PlanningTaskCloseRequest(Guid OpeningId, PlanningTaskEditorInput Input, bool IsDirty,
    bool IsSaving, PlanningTaskSaveResult? Outcome) {
    public PlanningTaskComparisonState? Comparison { get; init; }
    public bool NeedsDecision => IsDirty || IsSaving || Comparison is { Revision: > 0 } || Outcome is { RequiresReadback: true } or {
        Status: PlanningTaskSaveStatus.Partial or PlanningTaskSaveStatus.Unknown };
}
