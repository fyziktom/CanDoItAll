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
    private const double PositionEpsilon = 0.5d;

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
        var newNodeSize = EstimateNewNodeSize(resolvedObjectType, request);
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
            var sourceSize = EstimateNodeSize(sourceNode);
            var preferred = (
                sourceNode.X,
                sourceNode.Y + ((sourceSize.Height + newNodeSize.Height) / 2d) + StandardSiblingGap);
            return new ProjectStructureCreatePlacementPlan(
                FindAvailablePlacement(nodes, newNodeSize, preferred, 0, CandidateStep(newNodeSize)),
                []);
        }

        var horizontalDirection = ResolveChildHorizontalDirection(nodes, anchorNode);
        var anchorPosition = ResolveAnchorPosition(anchorNode, request);
        var anchorSize = EstimateNodeSize(anchorNode);
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
        NodeSize newNodeSize)
    {
        var sourcePosition = ResolveAnchorPosition(sourceNode, request, useRequestPoint: false);
        var sourceSize = EstimateNodeSize(sourceNode);
        if (string.Equals(request.PlacementKind, "sibling", StringComparison.OrdinalIgnoreCase))
        {
            var placement = (
                sourcePosition.X,
                sourcePosition.Y + ((sourceSize.Height + newNodeSize.Height) / 2d) + SimpleNoteSiblingGap);
            var moves = PlanDownwardSimpleNoteMoves(nodes, sourceNode, placement, newNodeSize);
            return new ProjectStructureCreatePlacementPlan(placement, moves);
        }

        var anchorPosition = ResolveAnchorPosition(anchorNode, request, useRequestPoint: false);
        var anchorSize = EstimateNodeSize(anchorNode);
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
        NodeSize newNodeSize)
    {
        var insertedRect = NodeRect.FromCenter(placement.X, placement.Y, newNodeSize);
        var stackBand = insertedRect.Inflate(horizontal: 24d, vertical: 0d);
        var lowerStackNodes = nodes
            .Where(node =>
                !string.Equals(node.Id, sourceNode.Id, StringComparison.Ordinal) &&
                string.Equals(node.ParentId, sourceNode.ParentId, StringComparison.Ordinal) &&
                node.Y > sourceNode.Y + PositionEpsilon &&
                HorizontallyOverlaps(stackBand, NodeRect.FromCenter(node.X, node.Y, EstimateNodeSize(node))))
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
            var size = EstimateNodeSize(node);
            var currentRect = NodeRect.FromCenter(node.X, node.Y, size);
            var requiredY = occupiedBottom + NoteStackGap + (size.Height / 2d);
            if (currentRect.Top >= occupiedBottom + NoteStackGap - PositionEpsilon)
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
        NodeSize newNodeSize,
        (double X, double Y) preferred,
        int horizontalDirection,
        double verticalStep)
    {
        var occupiedRects = nodes
            .Select(node => NodeRect.FromCenter(node.X, node.Y, EstimateNodeSize(node)))
            .ToList();
        foreach (var candidate in EnumeratePlacementCandidates(preferred, horizontalDirection, verticalStep))
        {
            var candidateRect = NodeRect.FromCenter(candidate.X, candidate.Y, newNodeSize).Inflate(20d, 20d);
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

    private static NodeSize EstimateNewNodeSize(ProjectObjectType? objectType, CanvasWorkbenchCreateActionRequest request)
    {
        if (objectType == ProjectObjectType.Note && string.IsNullOrWhiteSpace(request.Subtitle))
        {
            var text = !string.IsNullOrWhiteSpace(request.Notes)
                ? request.Notes
                : request.Title;
            return EstimateInlineNoteSize(text);
        }

        return objectType switch
        {
            ProjectObjectType.ProjectRoot => new NodeSize(288d, 210d),
            ProjectObjectType.Phase or ProjectObjectType.PromptSession or ProjectObjectType.PromptFlow or ProjectObjectType.ProjectBlock or ProjectObjectType.ProcessDefinition => new NodeSize(272d, 196d),
            ProjectObjectType.ProcessRun or ProjectObjectType.ValidationRun or ProjectObjectType.TestPlan or ProjectObjectType.Decision or ProjectObjectType.SecretReference => new NodeSize(248d, 178d),
            _ => new NodeSize(256d, 190d)
        };
    }

    private static NodeSize EstimateNodeSize(ProjectStructureNode node)
    {
        if (node.ObjectType == ProjectObjectType.Note && string.IsNullOrWhiteSpace(node.Subtitle))
        {
            var text = string.IsNullOrWhiteSpace(node.Notes) ? node.Title : node.Notes;
            return EstimateInlineNoteSize(text);
        }

        return EstimateNewNodeSize(node.ObjectType, new CanvasWorkbenchCreateActionRequest(
            string.Empty,
            node.Id,
            node.X,
            node.Y,
            node.ParentId,
            node.Title,
            node.Subtitle,
            node.Notes,
            string.Empty,
            string.Empty,
            node.ObjectSubtype,
            null));
    }

    private static NodeSize EstimateInlineNoteSize(string? text)
    {
        var noteText = string.IsNullOrWhiteSpace(text) ? "Write note" : text.Trim();
        var longestTokenLength = noteText
            .Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(token => token.Length)
            .DefaultIfEmpty(0)
            .Max();
        var widthBias = Math.Clamp((noteText.Length - 18) * 1.45d, 0d, 132d);
        var longWordBias = Math.Max(0d, longestTokenLength - 12d) * 4.5d;
        var width = Math.Clamp(Math.Ceiling(164d + widthBias + longWordBias), 148d, 348d);
        var lines = EstimateWrappedLineCount(noteText, Math.Max(1, (int)Math.Floor((width - 40d) / 7.2d)));
        var height = Math.Clamp(Math.Ceiling(30d + (lines * 20d) + 26d), 76d, 304d);
        return new NodeSize(width, height);
    }

    private static int EstimateWrappedLineCount(string text, int charactersPerLine)
    {
        var paragraphs = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        var lines = 0;
        foreach (var paragraph in paragraphs)
        {
            var length = paragraph.Length;
            lines += Math.Max(1, (int)Math.Ceiling(length / (double)charactersPerLine));
        }

        return Math.Clamp(lines, 1, 12);
    }

    private static double CandidateStep(NodeSize size, double minimum = 118d)
        => Math.Max(minimum, size.Height + CandidateVerticalGap);

    private static bool HorizontallyOverlaps(NodeRect first, NodeRect second)
        => first.Left < second.Right - PositionEpsilon &&
           first.Right > second.Left + PositionEpsilon;

    private readonly record struct NodeSize(double Width, double Height);

    private readonly record struct NodeRect(double Left, double Top, double Right, double Bottom)
    {
        public double Width => Right - Left;

        public double Height => Bottom - Top;

        public static NodeRect FromCenter(double x, double y, NodeSize size)
            => new(
                x - (size.Width / 2d),
                y - (size.Height / 2d),
                x + (size.Width / 2d),
                y + (size.Height / 2d));

        public NodeRect Inflate(double horizontal, double vertical)
            => new(Left - horizontal, Top - vertical, Right + horizontal, Bottom + vertical);

        public bool Intersects(NodeRect other)
            => Left < other.Right - PositionEpsilon &&
               Right > other.Left + PositionEpsilon &&
               Top < other.Bottom - PositionEpsilon &&
               Bottom > other.Top + PositionEpsilon;
    }
}


