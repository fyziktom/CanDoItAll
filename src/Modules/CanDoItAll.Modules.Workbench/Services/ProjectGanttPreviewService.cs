using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectGanttPreviewService(
    ProjectWorkbenchService projectWorkbenchService,
    IClock clock) : IProjectGanttPreviewService
{
    public async Task<ProjectGanttPreview> BuildAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var surface = await projectWorkbenchService.GetStructureAsync(projectId, cancellationToken);
        var rootNode = ResolveRootNode(surface, projectId);
        var summary = ProjectStructureSummaryBuilder.Build(surface, rootNode);
        var anchorDate = ResolveAnchorDate(summary, clock.GetUtcNow());
        var mermaidText = ProjectStructureSummaryExporter.BuildMermaidGantt(summary, anchorDate);
        var dependencyCount = summary.Rows.Sum(row => row.DependencyNodeIds?.Count ?? 0);

        return new ProjectGanttPreview(
            projectId,
            surface.ProjectName,
            rootNode.Id,
            rootNode.Title,
            mermaidText,
            summary.Rows.Count,
            dependencyCount,
            anchorDate);
    }

    private static ProjectStructureNode ResolveRootNode(ProjectStructureSurface surface, Guid projectId)
    {
        var projectRootId = $"project:{projectId}";

        return surface.Nodes.FirstOrDefault(node => string.Equals(node.Id, projectRootId, StringComparison.Ordinal))
            ?? surface.Nodes.FirstOrDefault(node => string.IsNullOrWhiteSpace(node.ParentId))
            ?? surface.Nodes.FirstOrDefault()
            ?? throw new InvalidOperationException("Project structure is empty, so a Mermaid Gantt preview cannot be generated.");
    }

    private static DateOnly ResolveAnchorDate(ProjectStructureSummary summary, DateTimeOffset fallbackUtcNow)
    {
        var firstScheduledRow = summary.Rows
            .Where(row => row.StartUtc.HasValue)
            .Select(row => row.StartUtc!.Value)
            .OrderBy(value => value)
            .FirstOrDefault();

        var resolvedDate = firstScheduledRow == default
            ? fallbackUtcNow.UtcDateTime
            : firstScheduledRow.UtcDateTime;

        return DateOnly.FromDateTime(resolvedDate);
    }
}
