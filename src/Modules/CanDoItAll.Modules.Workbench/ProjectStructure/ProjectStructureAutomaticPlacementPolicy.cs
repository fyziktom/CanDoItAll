using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workbench;

internal enum ProjectStructurePlacementDirection
{
    Right,
    Down,
    Left,
    Up
}

internal sealed record ProjectStructureAutomaticPlacementRequest(
    string ParentNodeKey,
    ProjectObjectType ObjectType,
    string Title,
    string Subtitle,
    string Notes,
    (double X, double Y)? PreferredPosition = null,
    ProjectStructurePlacementDirection? RequiredDirection = null);

internal sealed class ProjectStructureAutomaticPlacementSession
{
    private readonly List<ProjectObjectRecord> _nodes;
    private readonly List<ProjectStructureNodeBounds> _occupiedBounds;

    public ProjectStructureAutomaticPlacementSession(IReadOnlyList<ProjectObjectRecord> nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        _nodes = [.. nodes];
        _occupiedBounds = nodes
            .Select(ProjectStructureAutomaticPlacementPolicy.ResolveBounds)
            .ToList();
    }

    public (double X, double Y) Resolve(ProjectStructureAutomaticPlacementRequest request)
        => ProjectStructureAutomaticPlacementPolicy.Resolve(_nodes, _occupiedBounds, request);

    public ProjectStructurePlacementDirection ResolveIncomingDirection(string nodeKey)
        => ProjectStructureAutomaticPlacementPolicy.ResolveIncomingDirection(_nodes, nodeKey);

    public void Add(ProjectObjectRecord node)
    {
        ArgumentNullException.ThrowIfNull(node);
        _nodes.Add(node);
        _occupiedBounds.Add(ProjectStructureAutomaticPlacementPolicy.ResolveBounds(node));
    }
}

internal static class ProjectStructureAutomaticPlacementPolicy
{
    private const double StandardChildGap = 72d;
    private const double CandidateGap = 32d;
    private const double CollisionPadding = 20d;
    private const double AlternativeDirectionPenalty = 5d;
    private const double OutwardRingPenalty = 2d;
    private const int MaximumLateralLane = 8;
    private const int MaximumOutwardRing = 2;

    private static readonly ProjectStructurePlacementDirection[] AllDirections =
    [
        ProjectStructurePlacementDirection.Right,
        ProjectStructurePlacementDirection.Down,
        ProjectStructurePlacementDirection.Left,
        ProjectStructurePlacementDirection.Up
    ];

    public static (double X, double Y) Resolve(
        IReadOnlyList<ProjectObjectRecord> nodes,
        ProjectStructureAutomaticPlacementRequest request)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        var occupiedBounds = nodes
            .Select(ResolveBounds)
            .ToList();
        return Resolve(nodes, occupiedBounds, request);
    }

    internal static (double X, double Y) Resolve(
        IReadOnlyList<ProjectObjectRecord> nodes,
        IReadOnlyList<ProjectStructureNodeBounds> occupiedBounds,
        ProjectStructureAutomaticPlacementRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ParentNodeKey);

        var parent = nodes.FirstOrDefault(node =>
            string.Equals(node.NodeKey, request.ParentNodeKey, StringComparison.Ordinal));
        if (parent is null)
        {
            throw new InvalidOperationException(
                $"Automatic placement requires parent node '{request.ParentNodeKey}' to be present in the assembled project structure.");
        }

        var newNodeSize = ProjectStructureNodeGeometry.Estimate(
            request.ObjectType,
            request.Title,
            request.Subtitle,
            request.Notes);
        var preferredDirection = request.RequiredDirection ?? ResolvePreferredDirection(nodes, parent, request.PreferredPosition);
        var directions = request.RequiredDirection.HasValue
            ? [request.RequiredDirection.Value]
            : OrderDirections(preferredDirection);

        PlacementCandidate? best = null;
        foreach (var direction in directions)
        {
            var directionPenalty = direction == preferredDirection ? 0d : AlternativeDirectionPenalty;
            foreach (var candidate in EnumerateCandidates(parent, newNodeSize, direction))
            {
                var candidateBounds = ProjectStructureNodeBounds
                    .FromCenter(candidate.X, candidate.Y, newNodeSize)
                    .Inflate(CollisionPadding, CollisionPadding);
                if (occupiedBounds.Any(candidateBounds.Intersects))
                {
                    continue;
                }

                var score = directionPenalty +
                            Math.Abs(candidate.LateralLane) +
                            (candidate.OutwardRing * OutwardRingPenalty);
                if (best is null || score < best.Score)
                {
                    best = candidate with { Score = score };
                }
            }
        }

        return best is not null
            ? (best.X, best.Y)
            : ResolveOuterPlacement(occupiedBounds, parent, newNodeSize, preferredDirection);
    }

    internal static ProjectStructureNodeBounds ResolveBounds(ProjectObjectRecord node)
        => ProjectStructureNodeBounds.FromCenter(
            node.PositionX,
            node.PositionY,
            ProjectStructureNodeGeometry.Estimate(node));

    public static ProjectStructurePlacementDirection ResolveIncomingDirection(
        IReadOnlyList<ProjectObjectRecord> nodes,
        string nodeKey)
    {
        var node = nodes.FirstOrDefault(item => string.Equals(item.NodeKey, nodeKey, StringComparison.Ordinal));
        if (node is null || string.IsNullOrWhiteSpace(node.ParentNodeKey))
        {
            return ProjectStructurePlacementDirection.Right;
        }

        var parent = nodes.FirstOrDefault(item =>
            string.Equals(item.NodeKey, node.ParentNodeKey, StringComparison.Ordinal));
        return parent is null
            ? ProjectStructurePlacementDirection.Right
            : ResolveDirection(node.PositionX - parent.PositionX, node.PositionY - parent.PositionY);
    }

    private static ProjectStructurePlacementDirection ResolvePreferredDirection(
        IReadOnlyList<ProjectObjectRecord> nodes,
        ProjectObjectRecord parent,
        (double X, double Y)? preferredPosition)
    {
        var childDirections = nodes
            .Where(node => string.Equals(node.ParentNodeKey, parent.NodeKey, StringComparison.Ordinal))
            .Select(node => ResolveDirection(node.PositionX - parent.PositionX, node.PositionY - parent.PositionY))
            .GroupBy(direction => direction)
            .Select(group => new { Direction = group.Key, Count = group.Count() })
            .OrderByDescending(group => group.Count)
            .ThenBy(group => Array.IndexOf(AllDirections, group.Direction))
            .ToList();
        if (childDirections.Count > 0)
        {
            return childDirections[0].Direction;
        }

        if (preferredPosition is { } preferred &&
            (Math.Abs(preferred.X - parent.PositionX) > ProjectStructureNodeGeometry.PositionEpsilon ||
             Math.Abs(preferred.Y - parent.PositionY) > ProjectStructureNodeGeometry.PositionEpsilon))
        {
            return ResolveDirection(preferred.X - parent.PositionX, preferred.Y - parent.PositionY);
        }

        return ResolveIncomingDirection(nodes, parent.NodeKey);
    }

    private static IReadOnlyList<ProjectStructurePlacementDirection> OrderDirections(
        ProjectStructurePlacementDirection preferredDirection)
        => [preferredDirection, .. AllDirections.Where(direction => direction != preferredDirection)];

    private static IEnumerable<PlacementCandidate> EnumerateCandidates(
        ProjectObjectRecord parent,
        ProjectStructureNodeSize newNodeSize,
        ProjectStructurePlacementDirection direction)
    {
        var parentSize = ProjectStructureNodeGeometry.Estimate(parent);
        var horizontal = direction is ProjectStructurePlacementDirection.Right or ProjectStructurePlacementDirection.Left;
        var directionSign = direction is ProjectStructurePlacementDirection.Right or ProjectStructurePlacementDirection.Down ? 1d : -1d;
        var primaryDistance = horizontal
            ? ((parentSize.Width + newNodeSize.Width) / 2d) + StandardChildGap
            : ((parentSize.Height + newNodeSize.Height) / 2d) + StandardChildGap;
        var lateralStep = horizontal
            ? newNodeSize.Height + CandidateGap
            : newNodeSize.Width + CandidateGap;
        var outwardStep = horizontal
            ? newNodeSize.Width + StandardChildGap
            : newNodeSize.Height + StandardChildGap;

        for (var outwardRing = 0; outwardRing <= MaximumOutwardRing; outwardRing++)
        {
            foreach (var lateralLane in EnumerateCenteredLanes())
            {
                var primaryOffset = directionSign * (primaryDistance + (outwardRing * outwardStep));
                var lateralOffset = lateralLane * lateralStep;
                yield return horizontal
                    ? new PlacementCandidate(
                        parent.PositionX + primaryOffset,
                        parent.PositionY + lateralOffset,
                        lateralLane,
                        outwardRing,
                        0d)
                    : new PlacementCandidate(
                        parent.PositionX + lateralOffset,
                        parent.PositionY + primaryOffset,
                        lateralLane,
                        outwardRing,
                        0d);
            }
        }
    }

    private static IEnumerable<int> EnumerateCenteredLanes()
    {
        yield return 0;
        for (var lane = 1; lane <= MaximumLateralLane; lane++)
        {
            yield return lane;
            yield return -lane;
        }
    }

    private static (double X, double Y) ResolveOuterPlacement(
        IReadOnlyList<ProjectStructureNodeBounds> occupiedBounds,
        ProjectObjectRecord parent,
        ProjectStructureNodeSize newNodeSize,
        ProjectStructurePlacementDirection direction)
    {
        return direction switch
        {
            ProjectStructurePlacementDirection.Right => (
                occupiedBounds.Max(bounds => bounds.Right) + CollisionPadding + (newNodeSize.Width / 2d),
                parent.PositionY),
            ProjectStructurePlacementDirection.Down => (
                parent.PositionX,
                occupiedBounds.Max(bounds => bounds.Bottom) + CollisionPadding + (newNodeSize.Height / 2d)),
            ProjectStructurePlacementDirection.Left => (
                occupiedBounds.Min(bounds => bounds.Left) - CollisionPadding - (newNodeSize.Width / 2d),
                parent.PositionY),
            ProjectStructurePlacementDirection.Up => (
                parent.PositionX,
                occupiedBounds.Min(bounds => bounds.Top) - CollisionPadding - (newNodeSize.Height / 2d)),
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null)
        };
    }

    private static ProjectStructurePlacementDirection ResolveDirection(double deltaX, double deltaY)
    {
        if (Math.Abs(deltaX) >= Math.Abs(deltaY))
        {
            return deltaX < 0
                ? ProjectStructurePlacementDirection.Left
                : ProjectStructurePlacementDirection.Right;
        }

        return deltaY < 0
            ? ProjectStructurePlacementDirection.Up
            : ProjectStructurePlacementDirection.Down;
    }

    private sealed record PlacementCandidate(
        double X,
        double Y,
        int LateralLane,
        int OutwardRing,
        double Score);
}
