namespace CanDoItAll.Modules.Projects;

public sealed record ProjectGanttPreview(
    Guid ProjectId,
    string ProjectName,
    string RootNodeId,
    string RootTitle,
    string MermaidText,
    int RowCount,
    int DependencyCount,
    DateOnly AnchorDate);

public interface IProjectGanttPreviewService
{
    Task<ProjectGanttPreview> BuildAsync(Guid projectId, CancellationToken cancellationToken = default);
}
