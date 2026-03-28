namespace CanDoItAll.Modules.Workbench.CanvasAdapters;

internal sealed record ProjectStructureSubtreeRecompositionPlan(
    string RootNodeId,
    int DescendantCount,
    IReadOnlyList<ProjectNodeMoveRequest> Positions);

internal static class ProjectStructureSubtreeRecompositionEngine
{
    private const double FullTurn = Math.PI * 2d;
    private const double LevelGap = 68d;
    private const double ParentChildGap = 52d;
    private const double RadialPushStep = 28d;
    private const int RotationCandidates = 24;
    private const double SizePaddingX = 40d;
    private const double SizePaddingY = 28d;
    private const double PositionEpsilon = 0.5d;

    public static ProjectStructureSubtreeRecompositionPlan? Recompose(
        IReadOnlyList<ProjectStructureNode> nodes,
        string rootNodeId)
    {
        ArgumentNullException.ThrowIfNull(nodes);

        if (string.IsNullOrWhiteSpace(rootNodeId))
        {
            return null;
        }

        var nodeById = nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);
        if (!nodeById.TryGetValue(rootNodeId, out var root))
        {
            return null;
        }

        var directChildrenByParent = nodes
            .Where(node => !string.IsNullOrWhiteSpace(node.ParentId))
            .GroupBy(node => node.ParentId!, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.ToList(),
                StringComparer.Ordinal);
        var descendantIds = CollectDescendantIds(rootNodeId, directChildrenByParent);
        if (descendantIds.Count == 0)
        {
            return new ProjectStructureSubtreeRecompositionPlan(rootNodeId, 0, []);
        }

        var orderedChildrenCache = new Dictionary<string, IReadOnlyList<ProjectStructureNode>>(StringComparer.Ordinal);
        var spanByNodeId = new Dictionary<string, int>(StringComparer.Ordinal);
        var rootChildren = GetOrderedChildren(root, directChildrenByParent, orderedChildrenCache);
        foreach (var child in rootChildren)
        {
            MeasureLeafSpan(child, directChildrenByParent, orderedChildrenCache, spanByNodeId);
        }

        var totalLeafCount = rootChildren.Sum(child => spanByNodeId[child.Id]);
        if (totalLeafCount == 0)
        {
            return new ProjectStructureSubtreeRecompositionPlan(rootNodeId, descendantIds.Count, []);
        }

        var depthByNodeId = new Dictionary<string, int>(StringComparer.Ordinal);
        var sizeByNodeId = descendantIds.ToDictionary(
            nodeId => nodeId,
            nodeId => ResolveNodeSize(nodeById[nodeId]),
            StringComparer.Ordinal);
        var fixedRects = nodes
            .Where(node => !descendantIds.Contains(node.Id))
            .Select(node => BuildRect(node.X, node.Y, ResolveNodeSize(node)))
            .ToList();
        var rootSize = ResolveNodeSize(root);
        var firstLeaf = ResolveFirstLeaf(rootChildren, directChildrenByParent, orderedChildrenCache);
        var angleStep = FullTurn / totalLeafCount;
        var baseRotation = NormalizeAngle(ResolvePolarAngle(root, firstLeaf) - (angleStep / 2d));
        LayoutCandidate? bestCandidate = null;

        for (var candidateIndex = 0; candidateIndex < RotationCandidates; candidateIndex++)
        {
            var rotation = NormalizeAngle(baseRotation + ((FullTurn / RotationCandidates) * candidateIndex));
            depthByNodeId.Clear();
            var angleByNodeId = new Dictionary<string, double>(StringComparer.Ordinal);
            AssignAngles(root, 0, 1, rotation, directChildrenByParent, orderedChildrenCache, spanByNodeId, depthByNodeId, angleByNodeId, totalLeafCount);

            var candidate = PlaceNodes(root, descendantIds, nodeById, sizeByNodeId, fixedRects, rootSize, depthByNodeId, angleByNodeId);
            if (bestCandidate is null || candidate.Score < bestCandidate.Score - PositionEpsilon)
            {
                bestCandidate = candidate;
            }
        }

        if (bestCandidate is null)
        {
            return new ProjectStructureSubtreeRecompositionPlan(rootNodeId, descendantIds.Count, []);
        }

        return new ProjectStructureSubtreeRecompositionPlan(
            rootNodeId,
            descendantIds.Count,
            bestCandidate.Positions
                .Select(position => new ProjectNodeMoveRequest(position.NodeId, position.X, position.Y))
                .ToList());
    }

    private static HashSet<string> CollectDescendantIds(
        string rootNodeId,
        IReadOnlyDictionary<string, List<ProjectStructureNode>> childrenByParent)
    {
        var descendantIds = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<string>();
        queue.Enqueue(rootNodeId);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!childrenByParent.TryGetValue(current, out var children))
            {
                continue;
            }

            foreach (var child in children)
            {
                if (!descendantIds.Add(child.Id))
                {
                    continue;
                }

                queue.Enqueue(child.Id);
            }
        }

        return descendantIds;
    }

    private static int MeasureLeafSpan(
        ProjectStructureNode node,
        IReadOnlyDictionary<string, List<ProjectStructureNode>> childrenByParent,
        IDictionary<string, IReadOnlyList<ProjectStructureNode>> orderedChildrenCache,
        IDictionary<string, int> spanByNodeId)
    {
        if (spanByNodeId.TryGetValue(node.Id, out var cachedSpan))
        {
            return cachedSpan;
        }

        var children = GetOrderedChildren(node, childrenByParent, orderedChildrenCache);
        if (children.Count == 0)
        {
            spanByNodeId[node.Id] = 1;
            return 1;
        }

        var span = 0;
        foreach (var child in children)
        {
            span += MeasureLeafSpan(child, childrenByParent, orderedChildrenCache, spanByNodeId);
        }

        spanByNodeId[node.Id] = Math.Max(1, span);
        return spanByNodeId[node.Id];
    }

    private static IReadOnlyList<ProjectStructureNode> GetOrderedChildren(
        ProjectStructureNode parent,
        IReadOnlyDictionary<string, List<ProjectStructureNode>> childrenByParent,
        IDictionary<string, IReadOnlyList<ProjectStructureNode>> orderedChildrenCache)
    {
        if (orderedChildrenCache.TryGetValue(parent.Id, out var cachedChildren))
        {
            return cachedChildren;
        }

        if (!childrenByParent.TryGetValue(parent.Id, out var children))
        {
            orderedChildrenCache[parent.Id] = [];
            return orderedChildrenCache[parent.Id];
        }

        var orderedChildren = children
            .OrderBy(child => NormalizeAngle(ResolvePolarAngle(parent, child)))
            .ThenBy(child => ResolveDistanceSquared(parent, child))
            .ThenBy(child => child.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(child => child.Id, StringComparer.Ordinal)
            .ToList();
        orderedChildrenCache[parent.Id] = orderedChildren;
        return orderedChildren;
    }

    private static ProjectStructureNode ResolveFirstLeaf(
        IReadOnlyList<ProjectStructureNode> rootChildren,
        IReadOnlyDictionary<string, List<ProjectStructureNode>> childrenByParent,
        IDictionary<string, IReadOnlyList<ProjectStructureNode>> orderedChildrenCache)
    {
        var current = rootChildren[0];
        while (true)
        {
            var children = GetOrderedChildren(current, childrenByParent, orderedChildrenCache);
            if (children.Count == 0)
            {
                return current;
            }

            current = children[0];
        }
    }

    private static void AssignAngles(
        ProjectStructureNode parent,
        int startIndex,
        int depth,
        double rotation,
        IReadOnlyDictionary<string, List<ProjectStructureNode>> childrenByParent,
        IDictionary<string, IReadOnlyList<ProjectStructureNode>> orderedChildrenCache,
        IReadOnlyDictionary<string, int> spanByNodeId,
        IDictionary<string, int> depthByNodeId,
        IDictionary<string, double> angleByNodeId,
        int totalLeafCount)
    {
        var children = GetOrderedChildren(parent, childrenByParent, orderedChildrenCache);
        var currentIndex = startIndex;

        foreach (var child in children)
        {
            var span = spanByNodeId[child.Id];
            depthByNodeId[child.Id] = depth;
            angleByNodeId[child.Id] = NormalizeAngle(rotation + (((currentIndex + (span / 2d)) / totalLeafCount) * FullTurn));
            AssignAngles(child, currentIndex, depth + 1, rotation, childrenByParent, orderedChildrenCache, spanByNodeId, depthByNodeId, angleByNodeId, totalLeafCount);
            currentIndex += span;
        }
    }

    private static LayoutCandidate PlaceNodes(
        ProjectStructureNode root,
        IReadOnlyCollection<string> descendantIds,
        IReadOnlyDictionary<string, ProjectStructureNode> nodeById,
        IReadOnlyDictionary<string, NodeSize> sizeByNodeId,
        IReadOnlyList<LayoutRect> fixedRects,
        NodeSize rootSize,
        IReadOnlyDictionary<string, int> depthByNodeId,
        IReadOnlyDictionary<string, double> angleByNodeId)
    {
        var maxRadiusByDepth = descendantIds
            .GroupBy(nodeId => depthByNodeId[nodeId])
            .ToDictionary(
                group => group.Key,
                group => group.Max(nodeId => sizeByNodeId[nodeId].Radius));
        var baseRadiusByDepth = new Dictionary<int, double>();
        var maxDepth = maxRadiusByDepth.Keys.Max();

        for (var depth = 1; depth <= maxDepth; depth++)
        {
            if (depth == 1)
            {
                baseRadiusByDepth[depth] = rootSize.Radius + maxRadiusByDepth[depth] + LevelGap;
                continue;
            }

            baseRadiusByDepth[depth] = baseRadiusByDepth[depth - 1] + maxRadiusByDepth[depth - 1] + maxRadiusByDepth[depth] + LevelGap;
        }

        var placements = new Dictionary<string, PlannedPosition>(StringComparer.Ordinal);
        var orderedNodes = descendantIds
            .Select(nodeId => nodeById[nodeId])
            .OrderBy(node => depthByNodeId[node.Id])
            .ThenBy(node => angleByNodeId[node.Id])
            .ThenBy(node => ResolveDistanceSquared(root, node))
            .ThenBy(node => node.Id, StringComparer.Ordinal)
            .ToList();

        var totalExtraShift = 0d;
        foreach (var node in orderedNodes)
        {
            var nodeSize = sizeByNodeId[node.Id];
            var parentSize = string.Equals(node.ParentId, root.Id, StringComparison.Ordinal)
                ? rootSize
                : !string.IsNullOrWhiteSpace(node.ParentId) && sizeByNodeId.TryGetValue(node.ParentId, out var resolvedParentSize)
                    ? resolvedParentSize
                    : rootSize;
            var parentRadiusFromRoot = !string.IsNullOrWhiteSpace(node.ParentId) && placements.TryGetValue(node.ParentId, out var parentPlacement)
                ? parentPlacement.DistanceFromRoot
                : 0d;
            var desiredRadius = Math.Max(
                baseRadiusByDepth[depthByNodeId[node.Id]],
                parentRadiusFromRoot + parentSize.Radius + nodeSize.Radius + ParentChildGap);
            var placement = FindAvailablePlacement(root, node.Id, angleByNodeId[node.Id], desiredRadius, nodeSize, fixedRects, placements.Values);
            totalExtraShift += placement.DistanceFromRoot - desiredRadius;
            placements[node.Id] = placement;
        }

        var bounds = ResolveBounds(placements.Values);
        var maxDistanceFromRoot = placements.Count == 0
            ? 0d
            : placements.Values.Max(position => position.DistanceFromRoot);
        var spanWidth = bounds.Right - bounds.Left;
        var spanHeight = bounds.Bottom - bounds.Top;
        var score = (totalExtraShift * 8d) + maxDistanceFromRoot + Math.Abs(spanWidth - spanHeight);

        return new LayoutCandidate(score, placements.Values.OrderBy(position => position.NodeId, StringComparer.Ordinal).ToList());
    }

    private static PlannedPosition FindAvailablePlacement(
        ProjectStructureNode root,
        string nodeId,
        double angle,
        double desiredRadius,
        NodeSize nodeSize,
        IReadOnlyList<LayoutRect> fixedRects,
        IEnumerable<PlannedPosition> placedNodes)
    {
        var occupiedRects = fixedRects.ToList();
        occupiedRects.AddRange(placedNodes.Select(node => node.Rect));

        var radius = desiredRadius;
        for (var attempt = 0; attempt < 512; attempt++)
        {
            var x = root.X + (Math.Cos(angle) * radius);
            var y = root.Y + (Math.Sin(angle) * radius);
            var rect = BuildRect(x, y, nodeSize);
            if (!occupiedRects.Any(occupied => Intersects(rect, occupied)))
            {
                return new PlannedPosition(nodeId, x, y, radius, rect);
            }

            radius += RadialPushStep;
        }

        var fallbackX = root.X + (Math.Cos(angle) * radius);
        var fallbackY = root.Y + (Math.Sin(angle) * radius);
        return new PlannedPosition(nodeId, fallbackX, fallbackY, radius, BuildRect(fallbackX, fallbackY, nodeSize));
    }

    private static Bounds ResolveBounds(IEnumerable<PlannedPosition> placements)
    {
        var list = placements.ToList();
        if (list.Count == 0)
        {
            return new Bounds(0d, 0d, 0d, 0d);
        }

        return new Bounds(
            list.Min(position => position.Rect.Left),
            list.Min(position => position.Rect.Top),
            list.Max(position => position.Rect.Right),
            list.Max(position => position.Rect.Bottom));
    }

    private static NodeSize ResolveNodeSize(ProjectStructureNode node)
        => node.VisualProfile.Shape switch
        {
            "circle" => new NodeSize(104d + SizePaddingX, 104d + SizePaddingY),
            "pill" => new NodeSize(196d + SizePaddingX, 64d + SizePaddingY),
            _ => new NodeSize(204d + SizePaddingX, 80d + SizePaddingY)
        };

    private static LayoutRect BuildRect(double x, double y, NodeSize nodeSize)
        => new(
            x - (nodeSize.Width / 2d),
            y - (nodeSize.Height / 2d),
            x + (nodeSize.Width / 2d),
            y + (nodeSize.Height / 2d));

    private static bool Intersects(LayoutRect left, LayoutRect right)
        => left.Left < right.Right - PositionEpsilon &&
           left.Right > right.Left + PositionEpsilon &&
           left.Top < right.Bottom - PositionEpsilon &&
           left.Bottom > right.Top + PositionEpsilon;

    private static double ResolvePolarAngle(ProjectStructureNode origin, ProjectStructureNode node)
        => Math.Atan2(node.Y - origin.Y, node.X - origin.X);

    private static double ResolveDistanceSquared(ProjectStructureNode origin, ProjectStructureNode node)
    {
        var deltaX = node.X - origin.X;
        var deltaY = node.Y - origin.Y;
        return (deltaX * deltaX) + (deltaY * deltaY);
    }

    private static double NormalizeAngle(double angle)
    {
        while (angle < 0d)
        {
            angle += FullTurn;
        }

        while (angle >= FullTurn)
        {
            angle -= FullTurn;
        }

        return angle;
    }

    private sealed record LayoutCandidate(double Score, IReadOnlyList<PlannedPosition> Positions);

    private sealed record PlannedPosition(
        string NodeId,
        double X,
        double Y,
        double DistanceFromRoot,
        LayoutRect Rect);

    private readonly record struct NodeSize(double Width, double Height)
    {
        public double Radius => Math.Sqrt((Width * Width) + (Height * Height)) / 2d;
    }

    private readonly record struct LayoutRect(double Left, double Top, double Right, double Bottom);

    private readonly record struct Bounds(double Left, double Top, double Right, double Bottom);
}
