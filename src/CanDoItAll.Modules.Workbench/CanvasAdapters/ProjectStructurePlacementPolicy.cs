using CanDoItAll.Components.CanvasLib;

namespace CanDoItAll.Modules.Workbench.CanvasAdapters;

public sealed class ProjectStructurePlacementPolicy
{
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
            var existingSiblings = nodes.Count(node => string.Equals(node.ParentId, sourceNode.ParentId, StringComparison.Ordinal));
            return (
                sourceNode.X + ((existingSiblings % 2) * 24),
                sourceNode.Y + 132);
        }

        var anchorNode = parentNode ?? sourceNode;
        if (anchorNode is null)
        {
            return (request.X, request.Y);
        }

        var existingChildren = nodes.Count(node => string.Equals(node.ParentId, anchorNode.Id, StringComparison.Ordinal));
        var column = existingChildren % 3;
        var row = existingChildren / 3;
        return (
            anchorNode.X + 240 + (column * 46),
            anchorNode.Y - 70 + (row * 118));
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


