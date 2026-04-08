using CanDoItAll.Components.CanvasLib;

namespace CanDoItAll.Modules.Workbench.CanvasAdapters;

public sealed class ProjectStructurePlacementPolicy
{
    private const double SiblingVerticalSpacing = 280d;
    private const double ChildHorizontalSpacing = 480d;
    private const double ChildVerticalStartOffset = -160d;
    private const double ChildVerticalSpacing = 280d;

    public (double? X, double? Y) ResolveCreatePlacement(
        IReadOnlyList<ProjectStructureNode> nodes,
        ProjectStructureNode? sourceNode,
        ProjectStructureNode? parentNode,
        CanvasWorkbenchCreateActionRequest request)
    {
        if (request.X > 0 && request.Y > 0 && string.Equals(request.PlacementKind, "canvas", StringComparison.OrdinalIgnoreCase))
        {
            return (request.X, request.Y);
        }

        if (string.Equals(request.PlacementKind, "sibling", StringComparison.OrdinalIgnoreCase) && sourceNode is not null)
        {
            return (
                sourceNode.X,
                sourceNode.Y + SiblingVerticalSpacing);
        }

        var anchorNode = parentNode ?? sourceNode;
        if (anchorNode is null)
        {
            return (request.X, request.Y);
        }

        var existingChildren = nodes.Count(node => string.Equals(node.ParentId, anchorNode.Id, StringComparison.Ordinal));
        var anchorParent = string.IsNullOrWhiteSpace(anchorNode.ParentId)
            ? null
            : nodes.FirstOrDefault(node => string.Equals(node.Id, anchorNode.ParentId, StringComparison.Ordinal));
        var horizontalDirection = anchorParent is not null && anchorNode.X < anchorParent.X
            ? -1
            : 1;
        return (
            anchorNode.X + (horizontalDirection * ChildHorizontalSpacing),
            anchorNode.Y + ChildVerticalStartOffset + (existingChildren * ChildVerticalSpacing));
    }

    public static string? ResolveParentNodeId(ProjectStructureNode? sourceNode, CanvasWorkbenchCreateActionRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.ParentNodeId))
        {
            return request.ParentNodeId;
        }

        return string.Equals(request.PlacementKind, "sibling", StringComparison.OrdinalIgnoreCase)
            ? sourceNode?.ParentId
            : sourceNode?.Id;
    }
}


