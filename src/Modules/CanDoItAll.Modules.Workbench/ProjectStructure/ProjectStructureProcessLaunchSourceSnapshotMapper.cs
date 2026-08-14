using CanDoItAll.Processes.Application;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workbench;

internal static class ProjectStructureProcessLaunchSourceSnapshotMapper
{
    public static ProcessLaunchPreparationContext Create(
        ProjectStructureSurface surface,
        ProjectStructureNode selectedNode,
        string? definitionKey,
        bool isSubprocess,
        string? contextSummary)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(selectedNode);

        var contextItems = surface.Nodes
            .Where(ProjectStructureProcessContextNodeFilter.ShouldIncludeInProcessContext)
            .Select(node => Map(node, isIncludedInProcessContext: true))
            .ToArray();
        var snapshot = new ProcessLaunchSourceSnapshot(
            surface.ProjectId,
            surface.ProjectName,
            Map(
                selectedNode,
                ProjectStructureProcessContextNodeFilter.ShouldIncludeInProcessContext(selectedNode)),
            contextItems,
            contextSummary?.Trim() ?? string.Empty);
        return new ProcessLaunchPreparationContext(
            definitionKey,
            isSubprocess,
            snapshot);
    }

    private static ProcessLaunchSourceItem Map(
        ProjectStructureNode node,
        bool isIncludedInProcessContext)
    {
        return new ProcessLaunchSourceItem(
            node.Id,
            node.Title,
            node.Subtitle,
            node.Notes,
            node.ObjectSubtype,
            node.ArtifactKind,
            node.Badges?.ToArray() ?? [],
            node.ObjectType switch
            {
                ProjectObjectType.ImageAsset => ProcessLaunchSourceItemKind.ImageAsset,
                ProjectObjectType.ProjectBlock or ProjectObjectType.Decision =>
                    ProcessLaunchSourceItemKind.ProductRequirement,
                ProjectObjectType.WorkItem => ProcessLaunchSourceItemKind.WorkItem,
                _ => ProcessLaunchSourceItemKind.Other
            },
            isIncludedInProcessContext);
    }
}
