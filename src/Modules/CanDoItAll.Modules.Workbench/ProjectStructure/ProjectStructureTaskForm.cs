using System.Globalization;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Components.Gantt;
using CanDoItAll.Workbench.Planning.UI;

namespace CanDoItAll.Modules.Workbench;

internal static class ProjectStructureTaskForm {
    public static CanvasWorkbenchCreateActionRequest StructureRequest(ProjectStructureTaskSnapshot snapshot,
        CanvasWorkbenchCreateActionRequest original) {
        if (!ProjectStructureCanvasCatalog.TryResolveCreateDefinition(ProjectStructureCanvasCatalog.WorkTaskActionId, out var definition)) {
            throw new InvalidOperationException("The Task editor definition is unavailable.");
        }
        var inputValues = ProjectStructureNodeEditor.BuildInputValues(definition, snapshot.Task).ToList();
        if (Repository(snapshot.Task) is { } repository) {
            inputValues.Add(new() { Key = ProjectTaskInputKeys.RepositoryRef, Value = repository });
        }
        return original with { Title = snapshot.Task.Title, Subtitle = snapshot.Task.Subtitle, Notes = snapshot.Task.Notes,
            InputValues = inputValues };
    }

    public static PlanningTaskEditorInput Input(ProjectStructureTaskSnapshot snapshot, bool gantt, string defaultCurrency) {
        var estimate = snapshot.State.Estimate;
        var rawEstimate = new TaskEstimateInputText(Number(ProjectTaskEstimatePolicy.ToInputValue(estimate)), estimate.ExpectedEffortUnit.ToString(),
            Number(estimate.ExpectedCostAmount), estimate.ExpectedCostAmount.HasValue ? estimate.ExpectedCostCurrencyCode : defaultCurrency);
        var rawExecution = new TaskExecutionInputText(snapshot.State.Execution.State, Date(snapshot.State.Execution.ActualStartedAtUtc),
            Date(snapshot.State.Execution.ActualEndedAtUtc));
        var representative = snapshot.Assignees.Representative;
        var selected = snapshot.Assignees.CanChangeDirectAssignee ? representative : null;
        if (gantt) {
            var projected = snapshot.Schedule!.Tasks.Single(task => task.Id == new GanttTaskId(snapshot.Task.Id));
            return new PlanningTaskEditorInput.Gantt(snapshot.Task.Title, Date(projected.Start), Date(projected.End),
                ((projected.End - projected.Start).Ticks / (decimal)TimeSpan.TicksPerHour).ToString(CultureInfo.InvariantCulture),
                snapshot.Task.ProgressPercent.ToString(CultureInfo.InvariantCulture), rawEstimate, rawExecution, representative, null, selected);
        }
        var metadata = ProjectObjectMetadataSerializer.Parse(snapshot.Task.MetadataJson);
        return new PlanningTaskEditorInput.Structure(snapshot.Task.Title, snapshot.Task.Subtitle, snapshot.Task.Notes,
            Date(metadata.WorkItem?.DueUtc), Repository(snapshot.Task), rawEstimate, rawExecution, representative, null, selected);
    }

    public static ProjectStructureGanttTaskEditModel GanttModel(ProjectStructureTaskSnapshot snapshot) {
        var id = new GanttTaskId(snapshot.Task.Id);
        var projected = snapshot.Schedule!.Tasks.Single(task => task.Id == id);
        return new(id, snapshot.Task.Title, projected.Start, projected.End, snapshot.Task.ProgressPercent,
            snapshot.State.Estimate, snapshot.Assignees.Representative, snapshot.Schedule.IsProjectionOnly(projected),
            snapshot.Assignees.CanChangeDirectAssignee, snapshot.State.Execution, snapshot.State.CostBasis, snapshot.State.DirectAssignmentRevision);
    }

    private static string? Repository(ProjectStructureNode task) => task.NodeReferences?.WorkItemRepositoryResourceId is { } id
        ? $"custom:{id:N}" : null;
    private static string Number(decimal? value) => value?.ToString("0.############################", CultureInfo.InvariantCulture) ?? string.Empty;
    private static string Date(DateTimeOffset? value) => value?.ToUniversalTime().ToString(value.Value.Ticks % TimeSpan.TicksPerMinute == 0
        ? "yyyy-MM-ddTHH:mm" : "yyyy-MM-ddTHH:mm:ss.FFFFFFF", CultureInfo.InvariantCulture) ?? string.Empty;
}
