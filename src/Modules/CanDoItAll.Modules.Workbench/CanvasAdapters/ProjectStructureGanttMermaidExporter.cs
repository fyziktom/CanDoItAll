using CanDoItAll.Workbench.Planning.UI;

namespace CanDoItAll.Modules.Workbench.CanvasAdapters;

internal static class ProjectStructureGanttMermaidExporter {
    public static string Build(string projectName, ProjectStructureGanttProjectionResult projection) {
        ArgumentNullException.ThrowIfNull(projection);
        return PlanningGanttMermaidExporter.Build(projectName, new(Guid.Empty) {
            Tasks = projection.Tasks,
            Dependencies = projection.Dependencies,
            Errors = projection.Issues.Where(issue => issue.Severity == ProjectStructureGanttProjectionIssueSeverity.Error).Select(issue => issue.Message).ToArray(),
            IntervalSynthesizedTaskIds = projection.Issues
                .Where(issue => issue.Code == ProjectStructureGanttProjectionIssueCode.ScheduleSynthesized && issue.TaskId.HasValue)
                .Select(issue => issue.TaskId!.Value).ToHashSet()
        });
    }
}
