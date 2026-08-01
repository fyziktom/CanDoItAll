using CanDoItAll.Processes.Projections;

namespace CanDoItAll.Processes.Application;

internal static class ProcessDefinitionCanvasPlacementPolicy
{
    private const double CollisionPadding = 28d;
    private const double HorizontalGap = 104d;
    private const double AttachmentGap = 64d;
    private const double StructuralLaneGap = 320d;
    private const int MaximumOutwardRing = 2;
    private const int MaximumLane = 5;

    public static (double X, double Y) PlaceStep(
        IReadOnlyList<ProcessDefinitionCanvasEditorNodeProjection> nodes,
        IReadOnlyList<ProcessDefinitionCanvasEdgeProjection> edges,
        ProcessDefinitionCanvasEditorNodeProjection? anchor,
        double width,
        double height)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(edges);

        var occupied = nodes.Select(ResolveBounds).ToList();
        if (anchor is null)
        {
            return occupied.Count == 0
                ? (240d, 360d)
                : (occupied.Max(bounds => bounds.Right) + CollisionPadding + (width / 2d), 360d);
        }

        var structuralNodeKeys = nodes
            .Where(node => node.Kind is ProcessDefinitionCanvasNodeKind.Step or ProcessDefinitionCanvasNodeKind.BranchRouter)
            .Select(node => node.NodeKey)
            .ToHashSet();
        var hasForwardContinuation = edges.Any(edge =>
            !edge.IsBackwardRoute &&
            edge.FromNodeKey == anchor.NodeKey &&
            structuralNodeKeys.Contains(edge.ToNodeKey));
        var horizontalDistance = ((anchor.Width + width) / 2d) + HorizontalGap;
        var laneOffsets = hasForwardContinuation
            ? EnumerateSideLanes().Concat([0])
            : new[] { 0 }.Concat(EnumerateSideLanes());
        var candidates = EnumerateStructuralCandidates(anchor, horizontalDistance, laneOffsets, width);

        return ResolveFirstAvailable(
            occupied,
            width,
            height,
            candidates,
            () => (
                occupied.Max(bounds => bounds.Right) + CollisionPadding + (width / 2d),
                anchor.Y + (hasForwardContinuation ? StructuralLaneGap : 0d)));
    }

    public static (double X, double Y) PlaceBranchRouter(
        IReadOnlyList<ProcessDefinitionCanvasEditorNodeProjection> nodes,
        ProcessDefinitionCanvasEditorNodeProjection anchor,
        double width,
        double height)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(anchor);

        return PlaceBranchRouter(nodes.Select(ResolveBounds).ToList(), anchor, width, height);
    }

    internal static (double X, double Y) PlaceBranchRouter(
        IReadOnlyList<ProcessDefinitionCanvasBounds> occupied,
        ProcessDefinitionCanvasEditorNodeProjection anchor,
        double width,
        double height)
    {
        var horizontalDistance = ((anchor.Width + width) / 2d) + AttachmentGap;
        var verticalDistance = ((anchor.Height + height) / 2d) + AttachmentGap;
        (double X, double Y)[] candidates =
        [
            (anchor.X + horizontalDistance, anchor.Y - verticalDistance),
            (anchor.X + horizontalDistance, anchor.Y + verticalDistance),
            (anchor.X, anchor.Y - verticalDistance),
            (anchor.X, anchor.Y + verticalDistance),
            (anchor.X + horizontalDistance + width + CollisionPadding, anchor.Y - verticalDistance),
            (anchor.X + horizontalDistance + width + CollisionPadding, anchor.Y + verticalDistance)
        ];

        return ResolveFirstAvailable(
            occupied,
            width,
            height,
            candidates,
            () => ResolveRightFallback(occupied, anchor.Y, width));
    }

    public static (double X, double Y) PlaceAttachment(
        IReadOnlyList<ProcessDefinitionCanvasEditorNodeProjection> nodes,
        ProcessDefinitionCanvasEditorNodeProjection anchor,
        double width,
        double height)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(anchor);

        return PlaceAttachment(nodes.Select(ResolveBounds).ToList(), anchor, width, height);
    }

    internal static (double X, double Y) PlaceAttachment(
        IReadOnlyList<ProcessDefinitionCanvasBounds> occupied,
        ProcessDefinitionCanvasEditorNodeProjection anchor,
        double width,
        double height)
    {
        var verticalDistance = ((anchor.Height + height) / 2d) + AttachmentGap;
        var lateralDistance = ((anchor.Width + width) / 2d) + AttachmentGap;
        (double X, double Y)[] candidates =
        [
            (anchor.X, anchor.Y + verticalDistance),
            (anchor.X + lateralDistance, anchor.Y + verticalDistance),
            (anchor.X - lateralDistance, anchor.Y + verticalDistance),
            (anchor.X, anchor.Y - verticalDistance),
            (anchor.X + lateralDistance, anchor.Y - verticalDistance),
            (anchor.X - lateralDistance, anchor.Y - verticalDistance)
        ];

        return ResolveFirstAvailable(
            occupied,
            width,
            height,
            candidates,
            () => (
                anchor.X,
                occupied.Max(bounds => bounds.Bottom) + CollisionPadding + (height / 2d)));
    }

    internal static (double X, double Y) PlaceInputAttachment(
        IReadOnlyList<ProcessDefinitionCanvasBounds> occupied,
        ProcessDefinitionCanvasEditorNodeProjection anchor,
        double width,
        double height)
        => PlaceDirectionalAttachment(occupied, anchor, width, height, placeAbove: true);

    internal static (double X, double Y) PlaceOutputAttachment(
        IReadOnlyList<ProcessDefinitionCanvasBounds> occupied,
        ProcessDefinitionCanvasEditorNodeProjection anchor,
        double width,
        double height)
        => PlaceDirectionalAttachment(occupied, anchor, width, height, placeAbove: false);

    public static (double X, double Y) PlaceReference(
        IReadOnlyList<ProcessDefinitionCanvasEditorNodeProjection> nodes,
        ProcessDefinitionCanvasEditorNodeProjection anchor,
        double width,
        double height)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(anchor);

        var occupied = nodes.Select(ResolveBounds).ToList();
        var horizontalDistance = ((anchor.Width + width) / 2d) + AttachmentGap;
        var verticalDistance = ((anchor.Height + height) / 2d) + AttachmentGap;
        (double X, double Y)[] candidates =
        [
            (anchor.X + horizontalDistance, anchor.Y),
            (anchor.X + horizontalDistance, anchor.Y + verticalDistance),
            (anchor.X + horizontalDistance, anchor.Y - verticalDistance),
            (anchor.X, anchor.Y + verticalDistance),
            (anchor.X, anchor.Y - verticalDistance)
        ];

        return ResolveFirstAvailable(
            occupied,
            width,
            height,
            candidates,
            () => ResolveRightFallback(occupied, anchor.Y, width));
    }

    internal static ProcessDefinitionCanvasBounds ResolveBounds(
        ProcessDefinitionCanvasEditorNodeProjection node)
        => ProcessDefinitionCanvasBounds.FromCenter(node.X, node.Y, node.Width, node.Height);

    internal static bool Intersects(
        ProcessDefinitionCanvasBounds left,
        ProcessDefinitionCanvasBounds right)
        => left.Left < right.Right &&
           left.Right > right.Left &&
           left.Top < right.Bottom &&
           left.Bottom > right.Top;

    private static IEnumerable<(double X, double Y)> EnumerateStructuralCandidates(
        ProcessDefinitionCanvasEditorNodeProjection anchor,
        double horizontalDistance,
        IEnumerable<int> laneOffsets,
        double width)
    {
        var materializedLanes = laneOffsets.ToArray();
        for (var ring = 0; ring <= MaximumOutwardRing; ring++)
        {
            var x = anchor.X + horizontalDistance + (ring * (width + HorizontalGap));
            foreach (var lane in materializedLanes)
            {
                yield return (x, anchor.Y + (lane * StructuralLaneGap));
            }
        }
    }

    private static IEnumerable<int> EnumerateSideLanes()
    {
        for (var lane = 1; lane <= MaximumLane; lane++)
        {
            yield return lane;
            yield return -lane;
        }
    }

    private static (double X, double Y) PlaceDirectionalAttachment(
        IReadOnlyList<ProcessDefinitionCanvasBounds> occupied,
        ProcessDefinitionCanvasEditorNodeProjection anchor,
        double width,
        double height,
        bool placeAbove)
    {
        var verticalDistance = ((anchor.Height + height) / 2d) + AttachmentGap;
        var lateralDistance = ((anchor.Width + width) / 2d) + AttachmentGap;
        var y = anchor.Y + (placeAbove ? -verticalDistance : verticalDistance);
        var candidates = EnumerateHorizontalSlots(anchor.X, y, lateralDistance, occupied.Count + 2);

        return ResolveFirstAvailable(
            occupied,
            width,
            height,
            candidates,
            () => (
                anchor.X,
                placeAbove
                    ? occupied.Min(bounds => bounds.Top) - CollisionPadding - (height / 2d)
                    : occupied.Max(bounds => bounds.Bottom) + CollisionPadding + (height / 2d)));
    }

    private static IEnumerable<(double X, double Y)> EnumerateHorizontalSlots(
        double anchorX,
        double y,
        double lateralDistance,
        int slotCount)
    {
        yield return (anchorX, y);
        for (var slot = 1; slot <= slotCount; slot++)
        {
            yield return (anchorX + (slot * lateralDistance), y);
            yield return (anchorX - (slot * lateralDistance), y);
        }
    }

    private static (double X, double Y) ResolveFirstAvailable(
        IReadOnlyList<ProcessDefinitionCanvasBounds> occupied,
        double width,
        double height,
        IEnumerable<(double X, double Y)> candidates,
        Func<(double X, double Y)> fallback)
    {
        foreach (var candidate in candidates)
        {
            var candidateBounds = ProcessDefinitionCanvasBounds
                .FromCenter(candidate.X, candidate.Y, width, height)
                .Inflate(CollisionPadding);
            if (!occupied.Any(bounds => Intersects(candidateBounds, bounds)))
            {
                return candidate;
            }
        }

        return fallback();
    }

    private static (double X, double Y) ResolveRightFallback(
        IReadOnlyList<ProcessDefinitionCanvasBounds> occupied,
        double y,
        double width)
        => occupied.Count == 0
            ? (240d, y)
            : (occupied.Max(bounds => bounds.Right) + CollisionPadding + (width / 2d), y);
}

internal readonly record struct ProcessDefinitionCanvasBounds(
    double Left,
    double Top,
    double Right,
    double Bottom)
{
    public static ProcessDefinitionCanvasBounds FromCenter(
        double x,
        double y,
        double width,
        double height)
        => new(
            x - (width / 2d),
            y - (height / 2d),
            x + (width / 2d),
            y + (height / 2d));

    public ProcessDefinitionCanvasBounds Inflate(double padding)
        => new(Left - padding, Top - padding, Right + padding, Bottom + padding);
}
