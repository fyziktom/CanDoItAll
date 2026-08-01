using CanDoItAll.Components.CanvasLib;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workbench.CanvasAdapters;

public sealed record ProjectStructureCreatePlacementPlan(
    (double? X, double? Y) Placement,
    IReadOnlyList<ProjectNodeMoveRequest> FollowUpMoves);

public sealed class ProjectStructurePlacementPolicy
{
    private const double StandardChildGap = 72d;
    private const double StandardSiblingGap = 36d;
    private const double SimpleNoteChildGap = 24d;
    private const double SimpleNoteSiblingGap = 20d;
    private const double CandidateVerticalGap = 32d;
    private const double NoteStackGap = 18d;

    public (double? X, double? Y) ResolveCreatePlacement(
        IReadOnlyList<ProjectStructureNode> nodes,
        ProjectStructureNode? sourceNode,
        ProjectStructureNode? parentNode,
        CanvasWorkbenchCreateActionRequest request,
        ProjectObjectType? objectType = null)
        => ResolveCreatePlacementPlan(nodes, sourceNode, parentNode, request, objectType).Placement;

    public ProjectStructureCreatePlacementPlan ResolveCreatePlacementPlan(
        IReadOnlyList<ProjectStructureNode> nodes,
        ProjectStructureNode? sourceNode,
        ProjectStructureNode? parentNode,
        CanvasWorkbenchCreateActionRequest request,
        ProjectObjectType? objectType = null)
    {
        if (request.X > 0 && request.Y > 0 && string.Equals(request.PlacementKind, "canvas", StringComparison.OrdinalIgnoreCase))
        {
            return new ProjectStructureCreatePlacementPlan((request.X, request.Y), []);
        }

        var resolvedObjectType = objectType ?? ResolveObjectType(request);
        var newNodeSize = ProjectStructureNodeGeometry.Estimate(
            resolvedObjectType,
            request.Title,
            request.Subtitle,
            request.Notes);
        if (IsSimpleNoteQuickCreate(resolvedObjectType, request) && sourceNode is not null)
        {
            return ResolveSimpleNotePlacement(nodes, sourceNode, parentNode ?? sourceNode, request, newNodeSize);
        }

        var anchorNode = parentNode ?? sourceNode;
        if (anchorNode is null)
        {
            return new ProjectStructureCreatePlacementPlan((request.X, request.Y), []);
        }

        if (string.Equals(request.PlacementKind, "sibling", StringComparison.OrdinalIgnoreCase) && sourceNode is not null)
        {
            var sourceSize = ProjectStructureNodeGeometry.Estimate(sourceNode);
            var preferred = (
                sourceNode.X,
                sourceNode.Y + ((sourceSize.Height + newNodeSize.Height) / 2d) + StandardSiblingGap);
            return new ProjectStructureCreatePlacementPlan(
                FindAvailablePlacement(nodes, newNodeSize, preferred, 0, CandidateStep(newNodeSize)),
                []);
        }

        var horizontalDirection = ResolveChildHorizontalDirection(nodes, anchorNode);
        var anchorPosition = ResolveAnchorPosition(anchorNode, request);
        var anchorSize = ProjectStructureNodeGeometry.Estimate(anchorNode);
        var preferredChild = (
            anchorPosition.X + (horizontalDirection * (((anchorSize.Width + newNodeSize.Width) / 2d) + StandardChildGap)),
            anchorPosition.Y);

        return new ProjectStructureCreatePlacementPlan(
            FindAvailablePlacement(nodes, newNodeSize, preferredChild, horizontalDirection, CandidateStep(newNodeSize)),
            []);
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

    private static ProjectStructureCreatePlacementPlan ResolveSimpleNotePlacement(
        IReadOnlyList<ProjectStructureNode> nodes,
        ProjectStructureNode sourceNode,
        ProjectStructureNode anchorNode,
        CanvasWorkbenchCreateActionRequest request,
        ProjectStructureNodeSize newNodeSize)
    {
        var sourcePosition = ResolveAnchorPosition(sourceNode, request, useRequestPoint: false);
        var sourceSize = ProjectStructureNodeGeometry.Estimate(sourceNode);
        if (string.Equals(request.PlacementKind, "sibling", StringComparison.OrdinalIgnoreCase))
        {
            var placement = (
                sourcePosition.X,
                sourcePosition.Y + ((sourceSize.Height + newNodeSize.Height) / 2d) + SimpleNoteSiblingGap);
            var moves = PlanDownwardSimpleNoteMoves(nodes, sourceNode, placement, newNodeSize);
            return new ProjectStructureCreatePlacementPlan(placement, moves);
        }

        var anchorPosition = ResolveAnchorPosition(anchorNode, request, useRequestPoint: false);
        var anchorSize = ProjectStructureNodeGeometry.Estimate(anchorNode);
        var horizontalDirection = ResolveChildHorizontalDirection(nodes, anchorNode);
        var preferredChild = (
            anchorPosition.X + (horizontalDirection * (((anchorSize.Width + newNodeSize.Width) / 2d) + SimpleNoteChildGap)),
            anchorPosition.Y);
        return new ProjectStructureCreatePlacementPlan(
            FindAvailablePlacement(nodes, newNodeSize, preferredChild, horizontalDirection, CandidateStep(newNodeSize, minimum: 94d)),
            []);
    }

    private static int ResolveChildHorizontalDirection(
        IReadOnlyList<ProjectStructureNode> nodes,
        ProjectStructureNode anchorNode)
    {
        var anchorParent = string.IsNullOrWhiteSpace(anchorNode.ParentId)
            ? null
            : nodes.FirstOrDefault(node => string.Equals(node.Id, anchorNode.ParentId, StringComparison.Ordinal));
        return anchorParent is not null && anchorNode.X < anchorParent.X
            ? -1
            : 1;
    }

    private static IReadOnlyList<ProjectNodeMoveRequest> PlanDownwardSimpleNoteMoves(
        IReadOnlyList<ProjectStructureNode> nodes,
        ProjectStructureNode sourceNode,
        (double X, double Y) placement,
        ProjectStructureNodeSize newNodeSize)
    {
        var insertedRect = ProjectStructureNodeBounds.FromCenter(placement.X, placement.Y, newNodeSize);
        var stackBand = insertedRect.Inflate(horizontal: 24d, vertical: 0d);
        var lowerStackNodes = nodes
            .Where(node =>
                !string.Equals(node.Id, sourceNode.Id, StringComparison.Ordinal) &&
                string.Equals(node.ParentId, sourceNode.ParentId, StringComparison.Ordinal) &&
                node.Y > sourceNode.Y + ProjectStructureNodeGeometry.PositionEpsilon &&
                HorizontallyOverlaps(
                    stackBand,
                    ProjectStructureNodeBounds.FromCenter(
                        node.X,
                        node.Y,
                        ProjectStructureNodeGeometry.Estimate(node))))
            .OrderBy(node => node.Y)
            .ThenBy(node => node.X)
            .ThenBy(node => node.Id, StringComparer.Ordinal)
            .ToList();

        if (lowerStackNodes.Count == 0)
        {
            return [];
        }

        var moves = new List<ProjectNodeMoveRequest>();
        var occupiedBottom = insertedRect.Bottom;
        foreach (var node in lowerStackNodes)
        {
            var size = ProjectStructureNodeGeometry.Estimate(node);
            var currentRect = ProjectStructureNodeBounds.FromCenter(node.X, node.Y, size);
            var requiredY = occupiedBottom + NoteStackGap + (size.Height / 2d);
            if (currentRect.Top >= occupiedBottom + NoteStackGap - ProjectStructureNodeGeometry.PositionEpsilon)
            {
                break;
            }

            moves.Add(new ProjectNodeMoveRequest(node.Id, node.X, Math.Round(requiredY, 0, MidpointRounding.AwayFromZero)));
            occupiedBottom = requiredY + (size.Height / 2d);
        }

        return moves;
    }

    private static (double X, double Y) FindAvailablePlacement(
        IReadOnlyList<ProjectStructureNode> nodes,
        ProjectStructureNodeSize newNodeSize,
        (double X, double Y) preferred,
        int horizontalDirection,
        double verticalStep)
    {
        var occupiedRects = nodes
            .Select(node => ProjectStructureNodeBounds.FromCenter(
                node.X,
                node.Y,
                ProjectStructureNodeGeometry.Estimate(node)))
            .ToList();
        foreach (var candidate in EnumeratePlacementCandidates(preferred, horizontalDirection, verticalStep))
        {
            var candidateRect = ProjectStructureNodeBounds.FromCenter(candidate.X, candidate.Y, newNodeSize).Inflate(20d, 20d);
            if (occupiedRects.All(rect => !candidateRect.Intersects(rect)))
            {
                return candidate;
            }
        }

        return preferred;
    }

    private static IEnumerable<(double X, double Y)> EnumeratePlacementCandidates(
        (double X, double Y) preferred,
        int horizontalDirection,
        double verticalStep)
    {
        yield return preferred;

        for (var ring = 1; ring <= 12; ring++)
        {
            var offsetY = ring * verticalStep;
            yield return (preferred.X, preferred.Y + offsetY);
            yield return (preferred.X, preferred.Y - offsetY);

            if (horizontalDirection == 0 || ring > 4)
            {
                continue;
            }

            var offsetX = horizontalDirection * ring * 56d;
            yield return (preferred.X + offsetX, preferred.Y + offsetY);
            yield return (preferred.X + offsetX, preferred.Y - offsetY);
        }
    }

    private static (double X, double Y) ResolveAnchorPosition(
        ProjectStructureNode anchorNode,
        CanvasWorkbenchCreateActionRequest request,
        bool useRequestPoint = true)
    {
        if (useRequestPoint &&
            request.X != 0 &&
            request.Y != 0 &&
            string.Equals(request.SourceNodeId, anchorNode.Id, StringComparison.Ordinal))
        {
            return (request.X, request.Y);
        }

        return (anchorNode.X, anchorNode.Y);
    }

    private static bool IsSimpleNoteQuickCreate(ProjectObjectType? objectType, CanvasWorkbenchCreateActionRequest request)
        => objectType == ProjectObjectType.Note &&
           string.Equals(request.ActionId, "add-note", StringComparison.OrdinalIgnoreCase) &&
           string.Equals(request.CreateMode, "quick-note", StringComparison.OrdinalIgnoreCase);

    private static ProjectObjectType? ResolveObjectType(CanvasWorkbenchCreateActionRequest request)
        => string.Equals(request.ActionId, "add-note", StringComparison.OrdinalIgnoreCase)
            ? ProjectObjectType.Note
            : null;

    private static double CandidateStep(ProjectStructureNodeSize size, double minimum = 118d)
        => Math.Max(minimum, size.Height + CandidateVerticalGap);

    private static bool HorizontallyOverlaps(ProjectStructureNodeBounds first, ProjectStructureNodeBounds second)
        => first.Left < second.Right - ProjectStructureNodeGeometry.PositionEpsilon &&
           first.Right > second.Left + ProjectStructureNodeGeometry.PositionEpsilon;
}


