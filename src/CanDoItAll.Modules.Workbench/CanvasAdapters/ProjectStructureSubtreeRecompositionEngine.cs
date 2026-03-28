namespace CanDoItAll.Modules.Workbench.CanvasAdapters;

internal sealed record ProjectStructureSubtreeRecompositionPlan(
    string RootNodeId,
    int DescendantCount,
    IReadOnlyList<ProjectNodeMoveRequest> Positions);

internal static class ProjectStructureSubtreeRecompositionEngine
{
    private const double FullTurn = Math.PI * 2d;
    private const double TopAngle = -Math.PI / 2d;
    private const double LevelGap = 92d;
    private const double ParentChildGap = 60d;
    private const double RadialPushStep = 28d;
    private const int MaxPlacementAttempts = 512;
    private const double SizePaddingX = 56d;
    private const double SizePaddingY = 40d;
    private const double BranchBubblePadding = 44d;
    private const double FirstLayerSeparationGap = 72d;
    private const double SectorUsageRatio = 0.76d;
    private const double SingleBranchSectorSpan = Math.PI * 1.5d;
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
        var rootChildren = GetOrderedChildren(root, directChildrenByParent, orderedChildrenCache);
        if (rootChildren.Count == 0)
        {
            return new ProjectStructureSubtreeRecompositionPlan(rootNodeId, descendantIds.Count, []);
        }

        var sizeByNodeId = new Dictionary<string, NodeSize>(StringComparer.Ordinal)
        {
            [root.Id] = ResolveNodeSize(root)
        };
        foreach (var descendantId in descendantIds)
        {
            sizeByNodeId[descendantId] = ResolveNodeSize(nodeById[descendantId]);
        }

        var spanByNodeId = new Dictionary<string, int>(StringComparer.Ordinal);
        var depthByNodeId = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var child in rootChildren)
        {
            MeasureLeafSpan(child, directChildrenByParent, orderedChildrenCache, spanByNodeId);
            AssignDepths(child, 1, directChildrenByParent, orderedChildrenCache, depthByNodeId);
        }

        var branchNodeIdsByRootChildId = rootChildren.ToDictionary(
            child => child.Id,
            child => CollectBranchNodeIds(child.Id, directChildrenByParent),
            StringComparer.Ordinal);
        var angleByNodeId = new Dictionary<string, double>(StringComparer.Ordinal);
        var clockStep = FullTurn / rootChildren.Count;
        var sectorSpan = ResolveSectorSpan(rootChildren.Count, clockStep);

        for (var index = 0; index < rootChildren.Count; index++)
        {
            var child = rootChildren[index];
            var centerAngle = TopAngle + (clockStep * index);
            angleByNodeId[child.Id] = centerAngle;
            AssignAnglesWithinSector(
                child,
                centerAngle - (sectorSpan / 2d),
                centerAngle + (sectorSpan / 2d),
                directChildrenByParent,
                orderedChildrenCache,
                spanByNodeId,
                angleByNodeId);
        }

        var baseRadiusByDepth = ResolveBaseRadiusByDepth(
            root,
            rootChildren,
            descendantIds,
            sizeByNodeId,
            depthByNodeId,
            clockStep);
        var occupiedBubbleRects = nodes
            .Where(node => !descendantIds.Contains(node.Id))
            .Select(node => InflateRect(BuildRect(node.X, node.Y, sizeByNodeId.GetValueOrDefault(node.Id, ResolveNodeSize(node))), BranchBubblePadding))
            .ToList();
        var branchPlacements = new List<BranchPlacement>();
        var orderedBranchSlots = rootChildren
            .Select((child, index) => new BranchSlot(child, index, angleByNodeId[child.Id], branchNodeIdsByRootChildId[child.Id].Count))
            .OrderByDescending(slot => slot.Weight)
            .ThenBy(slot => slot.OrderIndex)
            .ToList();

        foreach (var slot in orderedBranchSlots)
        {
            var branchPlacement = PlaceBranch(
                root,
                slot,
                branchNodeIdsByRootChildId[slot.RootChild.Id],
                nodeById,
                sizeByNodeId,
                depthByNodeId,
                angleByNodeId,
                baseRadiusByDepth,
                occupiedBubbleRects);
            branchPlacements.Add(branchPlacement);
            occupiedBubbleRects.Add(branchPlacement.BubbleRect);
        }

        return new ProjectStructureSubtreeRecompositionPlan(
            rootNodeId,
            descendantIds.Count,
            branchPlacements
                .SelectMany(placement => placement.Positions)
                .OrderBy(position => position.NodeId, StringComparer.Ordinal)
                .Select(position => new ProjectNodeMoveRequest(position.NodeId, position.X, position.Y))
                .ToList());
    }

    private static Dictionary<int, double> ResolveBaseRadiusByDepth(
        ProjectStructureNode root,
        IReadOnlyList<ProjectStructureNode> rootChildren,
        IReadOnlyCollection<string> descendantIds,
        IReadOnlyDictionary<string, NodeSize> sizeByNodeId,
        IReadOnlyDictionary<string, int> depthByNodeId,
        double firstLayerClockStep)
    {
        var maxRadiusByDepth = descendantIds
            .GroupBy(nodeId => depthByNodeId[nodeId])
            .ToDictionary(
                group => group.Key,
                group => group.Max(nodeId => sizeByNodeId[nodeId].Radius));
        var rootSize = sizeByNodeId[root.Id];
        var firstLayerMaxRadius = rootChildren.Max(child => sizeByNodeId[child.Id].Radius);
        var firstLayerRadius = rootSize.Radius + firstLayerMaxRadius + LevelGap;
        if (rootChildren.Count > 1)
        {
            var halfStepSin = Math.Sin(firstLayerClockStep / 2d);
            if (halfStepSin > PositionEpsilon)
            {
                var minimumChord = (firstLayerMaxRadius * 2d) + FirstLayerSeparationGap;
                firstLayerRadius = Math.Max(firstLayerRadius, minimumChord / (2d * halfStepSin));
            }
        }

        var baseRadiusByDepth = new Dictionary<int, double>
        {
            [1] = firstLayerRadius
        };
        var maxDepth = maxRadiusByDepth.Keys.Max();

        for (var depth = 2; depth <= maxDepth; depth++)
        {
            baseRadiusByDepth[depth] =
                baseRadiusByDepth[depth - 1] +
                maxRadiusByDepth[depth - 1] +
                maxRadiusByDepth[depth] +
                LevelGap;
        }

        return baseRadiusByDepth;
    }

    private static BranchPlacement PlaceBranch(
        ProjectStructureNode root,
        BranchSlot slot,
        IReadOnlyList<string> branchNodeIds,
        IReadOnlyDictionary<string, ProjectStructureNode> nodeById,
        IReadOnlyDictionary<string, NodeSize> sizeByNodeId,
        IReadOnlyDictionary<string, int> depthByNodeId,
        IReadOnlyDictionary<string, double> angleByNodeId,
        IReadOnlyDictionary<int, double> baseRadiusByDepth,
        IReadOnlyList<LayoutRect> occupiedBubbleRects)
    {
        BranchPlacement? fallbackPlacement = null;

        for (var attempt = 0; attempt < MaxPlacementAttempts; attempt++)
        {
            var shift = attempt * RadialPushStep;
            var positions = PlaceBranchNodes(
                root,
                branchNodeIds,
                nodeById,
                sizeByNodeId,
                depthByNodeId,
                angleByNodeId,
                baseRadiusByDepth,
                shift);
            var bubbleRect = InflateRect(ResolveBounds(positions.Select(position => position.Rect)), BranchBubblePadding);
            fallbackPlacement = new BranchPlacement(slot.RootChild.Id, slot.CenterAngle, shift, bubbleRect, positions);

            if (!occupiedBubbleRects.Any(occupied => Intersects(bubbleRect, occupied)))
            {
                return fallbackPlacement;
            }
        }

        return fallbackPlacement!;
    }

    private static IReadOnlyList<PlannedPosition> PlaceBranchNodes(
        ProjectStructureNode root,
        IReadOnlyList<string> branchNodeIds,
        IReadOnlyDictionary<string, ProjectStructureNode> nodeById,
        IReadOnlyDictionary<string, NodeSize> sizeByNodeId,
        IReadOnlyDictionary<string, int> depthByNodeId,
        IReadOnlyDictionary<string, double> angleByNodeId,
        IReadOnlyDictionary<int, double> baseRadiusByDepth,
        double branchShift)
    {
        var placements = new Dictionary<string, PlannedPosition>(StringComparer.Ordinal);
        var orderedNodes = branchNodeIds
            .Select(nodeId => nodeById[nodeId])
            .OrderBy(node => depthByNodeId[node.Id])
            .ThenBy(node => angleByNodeId[node.Id])
            .ThenBy(node => node.Id, StringComparer.Ordinal)
            .ToList();

        foreach (var node in orderedNodes)
        {
            var depth = depthByNodeId[node.Id];
            var nodeSize = sizeByNodeId[node.Id];
            var desiredRadius = baseRadiusByDepth[depth] + branchShift;
            if (!string.IsNullOrWhiteSpace(node.ParentId) && placements.TryGetValue(node.ParentId, out var parentPlacement))
            {
                var parentSize = sizeByNodeId[node.ParentId];
                desiredRadius = Math.Max(
                    desiredRadius,
                    parentPlacement.DistanceFromRoot + parentSize.Radius + nodeSize.Radius + ParentChildGap);
            }

            placements[node.Id] = FindAvailablePlacement(
                root,
                node.Id,
                angleByNodeId[node.Id],
                desiredRadius,
                nodeSize,
                placements.Values);
        }

        return placements.Values
            .OrderBy(position => position.DistanceFromRoot)
            .ThenBy(position => position.Angle)
            .ThenBy(position => position.NodeId, StringComparer.Ordinal)
            .ToList();
    }

    private static PlannedPosition FindAvailablePlacement(
        ProjectStructureNode root,
        string nodeId,
        double angle,
        double desiredRadius,
        NodeSize nodeSize,
        IEnumerable<PlannedPosition> placedNodes)
    {
        var occupiedRects = placedNodes.Select(node => node.Rect).ToList();
        var radius = desiredRadius;

        for (var attempt = 0; attempt < MaxPlacementAttempts; attempt++)
        {
            var x = root.X + (Math.Cos(angle) * radius);
            var y = root.Y + (Math.Sin(angle) * radius);
            var rect = BuildRect(x, y, nodeSize);
            if (!occupiedRects.Any(occupied => Intersects(rect, occupied)))
            {
                return new PlannedPosition(nodeId, x, y, angle, radius, rect);
            }

            radius += RadialPushStep;
        }

        var fallbackX = root.X + (Math.Cos(angle) * radius);
        var fallbackY = root.Y + (Math.Sin(angle) * radius);
        return new PlannedPosition(nodeId, fallbackX, fallbackY, angle, radius, BuildRect(fallbackX, fallbackY, nodeSize));
    }

    private static void AssignAnglesWithinSector(
        ProjectStructureNode parent,
        double sectorStartAngle,
        double sectorEndAngle,
        IReadOnlyDictionary<string, List<ProjectStructureNode>> childrenByParent,
        IDictionary<string, IReadOnlyList<ProjectStructureNode>> orderedChildrenCache,
        IReadOnlyDictionary<string, int> spanByNodeId,
        IDictionary<string, double> angleByNodeId)
    {
        var children = GetOrderedChildren(parent, childrenByParent, orderedChildrenCache);
        if (children.Count == 0)
        {
            return;
        }

        var intervalWidth = sectorEndAngle - sectorStartAngle;
        var totalLeafSpan = children.Sum(child => spanByNodeId[child.Id]);
        var cursor = sectorStartAngle;

        foreach (var child in children)
        {
            var childWidth = intervalWidth * (spanByNodeId[child.Id] / (double)totalLeafSpan);
            var childStart = cursor;
            var childEnd = childStart + childWidth;
            angleByNodeId[child.Id] = (childStart + childEnd) / 2d;
            AssignAnglesWithinSector(
                child,
                childStart,
                childEnd,
                childrenByParent,
                orderedChildrenCache,
                spanByNodeId,
                angleByNodeId);
            cursor = childEnd;
        }
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

    private static List<string> CollectBranchNodeIds(
        string branchRootNodeId,
        IReadOnlyDictionary<string, List<ProjectStructureNode>> childrenByParent)
    {
        var branchNodeIds = new List<string>();
        var queue = new Queue<string>();
        queue.Enqueue(branchRootNodeId);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            branchNodeIds.Add(current);

            if (!childrenByParent.TryGetValue(current, out var children))
            {
                continue;
            }

            foreach (var child in children)
            {
                queue.Enqueue(child.Id);
            }
        }

        return branchNodeIds;
    }

    private static void AssignDepths(
        ProjectStructureNode node,
        int depth,
        IReadOnlyDictionary<string, List<ProjectStructureNode>> childrenByParent,
        IDictionary<string, IReadOnlyList<ProjectStructureNode>> orderedChildrenCache,
        IDictionary<string, int> depthByNodeId)
    {
        depthByNodeId[node.Id] = depth;

        foreach (var child in GetOrderedChildren(node, childrenByParent, orderedChildrenCache))
        {
            AssignDepths(child, depth + 1, childrenByParent, orderedChildrenCache, depthByNodeId);
        }
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
            .OrderBy(child => ResolveClockfaceAngle(parent, child))
            .ThenBy(child => ResolveDistanceSquared(parent, child))
            .ThenBy(child => child.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(child => child.Id, StringComparer.Ordinal)
            .ToList();
        orderedChildrenCache[parent.Id] = orderedChildren;
        return orderedChildren;
    }

    private static double ResolveSectorSpan(int firstLayerCount, double clockStep)
    {
        if (firstLayerCount <= 1)
        {
            return SingleBranchSectorSpan;
        }

        return clockStep * SectorUsageRatio;
    }

    private static NodeSize ResolveNodeSize(ProjectStructureNode node)
        => node.VisualProfile.Shape switch
        {
            "circle" => new NodeSize(104d + SizePaddingX, 104d + SizePaddingY),
            "pill" => new NodeSize(196d + SizePaddingX, 64d + SizePaddingY),
            _ => new NodeSize(204d + SizePaddingX, 80d + SizePaddingY)
        };

    private static LayoutRect ResolveBounds(IEnumerable<LayoutRect> rects)
    {
        var list = rects.ToList();
        if (list.Count == 0)
        {
            return new LayoutRect(0d, 0d, 0d, 0d);
        }

        return new LayoutRect(
            list.Min(rect => rect.Left),
            list.Min(rect => rect.Top),
            list.Max(rect => rect.Right),
            list.Max(rect => rect.Bottom));
    }

    private static LayoutRect InflateRect(LayoutRect rect, double padding)
        => new(
            rect.Left - padding,
            rect.Top - padding,
            rect.Right + padding,
            rect.Bottom + padding);

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

    private static double ResolveClockfaceAngle(ProjectStructureNode origin, ProjectStructureNode node)
        => NormalizeAngle(ResolvePolarAngle(origin, node) - TopAngle);

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

    private sealed record BranchSlot(
        ProjectStructureNode RootChild,
        int OrderIndex,
        double CenterAngle,
        int Weight);

    private sealed record BranchPlacement(
        string RootChildId,
        double CenterAngle,
        double Shift,
        LayoutRect BubbleRect,
        IReadOnlyList<PlannedPosition> Positions);

    private sealed record PlannedPosition(
        string NodeId,
        double X,
        double Y,
        double Angle,
        double DistanceFromRoot,
        LayoutRect Rect);

    private readonly record struct NodeSize(double Width, double Height)
    {
        public double Radius => Math.Sqrt((Width * Width) + (Height * Height)) / 2d;
    }

    private readonly record struct LayoutRect(double Left, double Top, double Right, double Bottom);
}
