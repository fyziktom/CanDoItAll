using CanDoItAll.Processes.Projections;

namespace CanDoItAll.Processes.Application;

internal sealed record ProcessDefinitionCanvasLayoutResult(
    IReadOnlyList<ProcessDefinitionCanvasEditorNodeProjection> Nodes,
    IReadOnlyList<ProcessDefinitionCanvasEdgeProjection> Edges,
    IReadOnlySet<ProcessDefinitionCanvasNodeKey> MainPathNodeKeys);

internal static class ProcessDefinitionCanvasRecompositionEngine
{
    private const double FirstStepX = 240d;
    private const double StepColumnGap = 480d;
    private const double FirstStructuralLaneY = 420d;
    private const double StructuralLaneGap = 320d;
    private const double RoleRowY = 130d;
    private const double RoleGap = 80d;

    public static ProcessDefinitionCanvasLayoutResult Recompose(
        IReadOnlyList<ProcessDefinitionCanvasEditorNodeProjection> nodes,
        IReadOnlyList<ProcessDefinitionCanvasEdgeProjection> edges)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(edges);

        ValidateNodeKeys(nodes);
        var steps = nodes
            .Where(node => node.Kind == ProcessDefinitionCanvasNodeKind.Step)
            .ToArray();
        if (steps.Length == 0)
        {
            return new ProcessDefinitionCanvasLayoutResult(nodes, edges, new HashSet<ProcessDefinitionCanvasNodeKey>());
        }

        var nodeOrder = nodes
            .Select((node, index) => new { node.NodeKey, Index = index })
            .ToDictionary(item => item.NodeKey, item => item.Index);
        var stepByKey = steps.ToDictionary(step => step.NodeKey);
        var structuralArcs = BuildStructuralArcs(nodes, edges, stepByKey, nodeOrder);
        var forwardArcs = structuralArcs
            .Where(arc => !arc.IsBackwardRoute)
            .GroupBy(arc => (arc.SourceNodeKey, arc.TargetNodeKey))
            .Select(group => group
                .OrderBy(arc => arc.Kind == ProcessDefinitionCanvasEdgeKind.Dependency ? 0 : 1)
                .ThenBy(arc => arc.EdgeKey.Value, StringComparer.Ordinal)
                .First())
            .ToArray();
        var topologicalOrder = BuildTopologicalOrder(steps, forwardArcs, nodeOrder);
        var outgoing = BuildArcLookup(forwardArcs, arc => arc.SourceNodeKey, nodeOrder, arc => arc.TargetNodeKey);
        var incoming = BuildArcLookup(forwardArcs, arc => arc.TargetNodeKey, nodeOrder, arc => arc.SourceNodeKey);
        var mainPath = ResolveMainPath(steps, topologicalOrder, outgoing, incoming, nodeOrder);
        var mainPathNodeKeys = mainPath.NodeKeys.ToHashSet();
        var rankByNodeKey = ResolveRanks(topologicalOrder, incoming);
        var laneByNodeKey = ResolveLanes(topologicalOrder, incoming, rankByNodeKey, mainPathNodeKeys);
        var positionedNodes = PositionNodes(
            nodes,
            edges,
            steps,
            nodeOrder,
            rankByNodeKey,
            laneByNodeKey);

        return new ProcessDefinitionCanvasLayoutResult(positionedNodes, edges, mainPathNodeKeys);
    }

    private static IReadOnlyList<ProcessDefinitionCanvasEditorNodeProjection> PositionNodes(
        IReadOnlyList<ProcessDefinitionCanvasEditorNodeProjection> nodes,
        IReadOnlyList<ProcessDefinitionCanvasEdgeProjection> edges,
        IReadOnlyList<ProcessDefinitionCanvasEditorNodeProjection> steps,
        IReadOnlyDictionary<ProcessDefinitionCanvasNodeKey, int> nodeOrder,
        IReadOnlyDictionary<ProcessDefinitionCanvasNodeKey, int> rankByNodeKey,
        IReadOnlyDictionary<ProcessDefinitionCanvasNodeKey, int> laneByNodeKey)
    {
        var minimumLane = laneByNodeKey.Values.Min();
        var mainLaneY = FirstStructuralLaneY - (minimumLane * StructuralLaneGap);
        var positionedByKey = new Dictionary<ProcessDefinitionCanvasNodeKey, ProcessDefinitionCanvasEditorNodeProjection>();
        var occupied = new List<ProcessDefinitionCanvasBounds>();

        foreach (var step in steps.OrderBy(step => nodeOrder[step.NodeKey]))
        {
            var positioned = step with
            {
                X = FirstStepX + (rankByNodeKey[step.NodeKey] * StepColumnGap),
                Y = mainLaneY + (laneByNodeKey[step.NodeKey] * StructuralLaneGap)
            };
            positionedByKey[positioned.NodeKey] = positioned;
            occupied.Add(ProcessDefinitionCanvasPlacementPolicy.ResolveBounds(positioned));
        }

        var roles = nodes
            .Where(node => node.Kind == ProcessDefinitionCanvasNodeKind.Role)
            .OrderBy(node => nodeOrder[node.NodeKey])
            .ToArray();
        for (var index = 0; index < roles.Length; index++)
        {
            var role = roles[index] with
            {
                X = FirstStepX + (index * (roles[index].Width + RoleGap)),
                Y = RoleRowY
            };
            positionedByKey[role.NodeKey] = role;
            occupied.Add(ProcessDefinitionCanvasPlacementPolicy.ResolveBounds(role));
        }

        var routers = nodes
            .Where(node => node.Kind == ProcessDefinitionCanvasNodeKind.BranchRouter)
            .OrderBy(node => nodeOrder[node.NodeKey])
            .ToArray();
        foreach (var router in routers)
        {
            var anchor = ResolveRouterAnchor(router, positionedByKey, nodes, edges)
                ?? throw new InvalidOperationException(
                    $"Branch router '{router.NodeKey.Value}' must be associated with an owning process step before recomposition.");

            var position = ProcessDefinitionCanvasPlacementPolicy.PlaceBranchRouter(
                occupied,
                anchor,
                router.Width,
                router.Height);
            var positioned = router with { X = position.X, Y = position.Y };
            positionedByKey[positioned.NodeKey] = positioned;
            occupied.Add(ProcessDefinitionCanvasPlacementPolicy.ResolveBounds(positioned));
        }

        var satellites = nodes
            .Where(node => node.Kind is ProcessDefinitionCanvasNodeKind.Artifact or ProcessDefinitionCanvasNodeKind.SubprocessBoundary)
            .OrderBy(node => nodeOrder[node.NodeKey])
            .ToArray();
        foreach (var satellite in satellites)
        {
            var anchor = ResolveSatelliteAnchor(satellite, positionedByKey, nodes, edges)
                ?? throw new InvalidOperationException(
                    $"Canvas node '{satellite.NodeKey.Value}' must be associated with a structural process step before recomposition.");

            var position = satellite.StepKey is null
                ? ProcessDefinitionCanvasPlacementPolicy.PlaceReference(
                    positionedByKey.Values.ToArray(),
                    anchor,
                    satellite.Width,
                    satellite.Height)
                : ProcessDefinitionCanvasPlacementPolicy.PlaceAttachment(
                    occupied,
                    anchor,
                    satellite.Width,
                    satellite.Height);
            var positioned = satellite with { X = position.X, Y = position.Y };
            positionedByKey[positioned.NodeKey] = positioned;
            occupied.Add(ProcessDefinitionCanvasPlacementPolicy.ResolveBounds(positioned));
        }

        return nodes
            .Select(node => positionedByKey.GetValueOrDefault(node.NodeKey, node))
            .ToArray();
    }

    private static ProcessDefinitionCanvasEditorNodeProjection? ResolveRouterAnchor(
        ProcessDefinitionCanvasEditorNodeProjection router,
        IReadOnlyDictionary<ProcessDefinitionCanvasNodeKey, ProcessDefinitionCanvasEditorNodeProjection> positionedByKey,
        IReadOnlyList<ProcessDefinitionCanvasEditorNodeProjection> nodes,
        IReadOnlyList<ProcessDefinitionCanvasEdgeProjection> edges)
    {
        if (router.StepKey is { } stepKey)
        {
            var matchingStep = positionedByKey.Values.FirstOrDefault(node =>
                node.Kind == ProcessDefinitionCanvasNodeKind.Step &&
                node.StepKey == stepKey);
            if (matchingStep is not null)
            {
                return matchingStep;
            }
        }

        var sourceNodeKey = edges.FirstOrDefault(edge =>
            edge.ToNodeKey == router.NodeKey &&
            edge.Kind == ProcessDefinitionCanvasEdgeKind.BranchRoute)?.FromNodeKey;
        return sourceNodeKey is { } key && positionedByKey.TryGetValue(key, out var source)
            ? source
            : null;
    }

    private static ProcessDefinitionCanvasEditorNodeProjection? ResolveSatelliteAnchor(
        ProcessDefinitionCanvasEditorNodeProjection satellite,
        IReadOnlyDictionary<ProcessDefinitionCanvasNodeKey, ProcessDefinitionCanvasEditorNodeProjection> positionedByKey,
        IReadOnlyList<ProcessDefinitionCanvasEditorNodeProjection> nodes,
        IReadOnlyList<ProcessDefinitionCanvasEdgeProjection> edges)
    {
        var sourceNodeKey = edges.FirstOrDefault(edge => edge.ToNodeKey == satellite.NodeKey)?.FromNodeKey;
        if (sourceNodeKey is { } key && positionedByKey.TryGetValue(key, out var source))
        {
            return source;
        }

        if (satellite.StepKey is { } stepKey)
        {
            var step = positionedByKey.Values.FirstOrDefault(node =>
                node.Kind == ProcessDefinitionCanvasNodeKind.Step &&
                node.StepKey == stepKey);
            if (step is not null)
            {
                return step;
            }
        }

        if (!string.IsNullOrWhiteSpace(satellite.ArtifactKey))
        {
            var original = nodes.FirstOrDefault(node =>
                node.NodeKey != satellite.NodeKey &&
                node.Kind == ProcessDefinitionCanvasNodeKind.Artifact &&
                node.StepKey is not null &&
                string.Equals(node.ArtifactKey, satellite.ArtifactKey, StringComparison.OrdinalIgnoreCase));
            if (original is not null && positionedByKey.TryGetValue(original.NodeKey, out var positionedOriginal))
            {
                return positionedOriginal;
            }
        }

        return positionedByKey.Values
            .Where(node => node.Kind == ProcessDefinitionCanvasNodeKind.Step)
            .OrderBy(node => node.X)
            .FirstOrDefault();
    }

    private static IReadOnlyDictionary<ProcessDefinitionCanvasNodeKey, int> ResolveRanks(
        IReadOnlyList<ProcessDefinitionCanvasNodeKey> topologicalOrder,
        IReadOnlyDictionary<ProcessDefinitionCanvasNodeKey, IReadOnlyList<StructuralArc>> incoming)
    {
        var ranks = topologicalOrder.ToDictionary(nodeKey => nodeKey, _ => 0);
        foreach (var nodeKey in topologicalOrder)
        {
            if (!incoming.TryGetValue(nodeKey, out var parents) || parents.Count == 0)
            {
                continue;
            }

            ranks[nodeKey] = parents.Max(parent => ranks[parent.SourceNodeKey] + 1);
        }

        return ranks;
    }

    private static IReadOnlyDictionary<ProcessDefinitionCanvasNodeKey, int> ResolveLanes(
        IReadOnlyList<ProcessDefinitionCanvasNodeKey> topologicalOrder,
        IReadOnlyDictionary<ProcessDefinitionCanvasNodeKey, IReadOnlyList<StructuralArc>> incoming,
        IReadOnlyDictionary<ProcessDefinitionCanvasNodeKey, int> rankByNodeKey,
        IReadOnlySet<ProcessDefinitionCanvasNodeKey> mainPathNodeKeys)
    {
        var lanes = new Dictionary<ProcessDefinitionCanvasNodeKey, int>();
        var occupiedRanksByLane = new Dictionary<int, HashSet<int>>
        {
            [0] = []
        };
        foreach (var nodeKey in mainPathNodeKeys)
        {
            lanes[nodeKey] = 0;
            occupiedRanksByLane[0].Add(rankByNodeKey[nodeKey]);
        }

        foreach (var nodeKey in topologicalOrder)
        {
            if (mainPathNodeKeys.Contains(nodeKey))
            {
                continue;
            }

            var inheritedLanes = incoming.GetValueOrDefault(nodeKey, [])
                .Select(arc => lanes.GetValueOrDefault(arc.SourceNodeKey))
                .Where(lane => lane != 0)
                .GroupBy(lane => lane)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => Math.Abs(group.Key))
                .ThenBy(group => group.Key)
                .Select(group => group.Key);
            var rank = rankByNodeKey[nodeKey];
            var lane = inheritedLanes
                .Concat(EnumerateSideLanes(topologicalOrder.Count))
                .Distinct()
                .First(candidate => !occupiedRanksByLane.GetValueOrDefault(candidate, []).Contains(rank));
            lanes[nodeKey] = lane;
            if (!occupiedRanksByLane.TryGetValue(lane, out var occupiedRanks))
            {
                occupiedRanks = [];
                occupiedRanksByLane[lane] = occupiedRanks;
            }

            occupiedRanks.Add(rank);
        }

        return lanes;
    }

    private static MainPath ResolveMainPath(
        IReadOnlyList<ProcessDefinitionCanvasEditorNodeProjection> steps,
        IReadOnlyList<ProcessDefinitionCanvasNodeKey> topologicalOrder,
        IReadOnlyDictionary<ProcessDefinitionCanvasNodeKey, IReadOnlyList<StructuralArc>> outgoing,
        IReadOnlyDictionary<ProcessDefinitionCanvasNodeKey, IReadOnlyList<StructuralArc>> incoming,
        IReadOnlyDictionary<ProcessDefinitionCanvasNodeKey, int> nodeOrder)
    {
        var stepByKey = steps.ToDictionary(step => step.NodeKey);
        var typedStarts = steps
            .Where(step => step.StepKind == ProcessDefinitionStepKind.Start)
            .Select(step => step.NodeKey)
            .ToArray();
        var startCandidates = typedStarts.Length > 0
            ? typedStarts
            : topologicalOrder
                .Where(nodeKey => !incoming.TryGetValue(nodeKey, out var arcs) || arcs.Count == 0)
                .ToArray();
        if (startCandidates.Length == 0)
        {
            throw new InvalidOperationException("Process canvas recomposition requires at least one forward graph root or typed Start step.");
        }

        var memo = new Dictionary<ProcessDefinitionCanvasNodeKey, MainPath>();
        var candidates = startCandidates
            .Select(start => ResolveBestPath(start, stepByKey, outgoing, nodeOrder, memo))
            .OrderByDescending(path => path.TerminalPriority)
            .ThenBy(path => nodeOrder[path.NodeKeys[0]])
            .ThenByDescending(path => path.NodeKeys.Count)
            .ToArray();
        return candidates[0];
    }

    private static MainPath ResolveBestPath(
        ProcessDefinitionCanvasNodeKey nodeKey,
        IReadOnlyDictionary<ProcessDefinitionCanvasNodeKey, ProcessDefinitionCanvasEditorNodeProjection> stepByKey,
        IReadOnlyDictionary<ProcessDefinitionCanvasNodeKey, IReadOnlyList<StructuralArc>> outgoing,
        IReadOnlyDictionary<ProcessDefinitionCanvasNodeKey, int> nodeOrder,
        IDictionary<ProcessDefinitionCanvasNodeKey, MainPath> memo)
    {
        if (memo.TryGetValue(nodeKey, out var cached))
        {
            return cached;
        }

        if (stepByKey[nodeKey].StepKind == ProcessDefinitionStepKind.End)
        {
            var end = new MainPath([nodeKey], ResolveTerminalPriority(stepByKey[nodeKey].StepKind));
            memo[nodeKey] = end;
            return end;
        }

        if (!outgoing.TryGetValue(nodeKey, out var arcs) || arcs.Count == 0)
        {
            var terminal = new MainPath([nodeKey], ResolveTerminalPriority(stepByKey[nodeKey].StepKind));
            memo[nodeKey] = terminal;
            return terminal;
        }

        var candidates = arcs
            .Select(arc => new
            {
                Arc = arc,
                Suffix = ResolveBestPath(arc.TargetNodeKey, stepByKey, outgoing, nodeOrder, memo)
            })
            .OrderByDescending(candidate => candidate.Suffix.TerminalPriority)
            .ThenBy(candidate => nodeOrder[candidate.Arc.TargetNodeKey])
            .ThenByDescending(candidate => candidate.Suffix.NodeKeys.Count)
            .First();
        var resolved = new MainPath(
            [nodeKey, .. candidates.Suffix.NodeKeys],
            candidates.Suffix.TerminalPriority);
        memo[nodeKey] = resolved;
        return resolved;
    }

    private static int ResolveTerminalPriority(ProcessDefinitionStepKind? stepKind)
        => stepKind switch
        {
            ProcessDefinitionStepKind.End or
            ProcessDefinitionStepKind.Delivery or
            ProcessDefinitionStepKind.Approval => 3,
            ProcessDefinitionStepKind.Review => 2,
            ProcessDefinitionStepKind.Work or
            ProcessDefinitionStepKind.Subprocess => 1,
            _ => 0
        };

    private static IReadOnlyList<ProcessDefinitionCanvasNodeKey> BuildTopologicalOrder(
        IReadOnlyList<ProcessDefinitionCanvasEditorNodeProjection> steps,
        IReadOnlyList<StructuralArc> forwardArcs,
        IReadOnlyDictionary<ProcessDefinitionCanvasNodeKey, int> nodeOrder)
    {
        var indegree = steps.ToDictionary(step => step.NodeKey, _ => 0);
        var outgoing = forwardArcs
            .GroupBy(arc => arc.SourceNodeKey)
            .ToDictionary(group => group.Key, group => group.ToArray());
        foreach (var arc in forwardArcs)
        {
            indegree[arc.TargetNodeKey]++;
        }

        var ready = new PriorityQueue<ProcessDefinitionCanvasNodeKey, int>();
        foreach (var step in steps.Where(step => indegree[step.NodeKey] == 0))
        {
            ready.Enqueue(step.NodeKey, nodeOrder[step.NodeKey]);
        }

        var result = new List<ProcessDefinitionCanvasNodeKey>(steps.Count);
        while (ready.TryDequeue(out var nodeKey, out _))
        {
            result.Add(nodeKey);
            if (!outgoing.TryGetValue(nodeKey, out var children))
            {
                continue;
            }

            foreach (var child in children.OrderBy(arc => nodeOrder[arc.TargetNodeKey]))
            {
                indegree[child.TargetNodeKey]--;
                if (indegree[child.TargetNodeKey] == 0)
                {
                    ready.Enqueue(child.TargetNodeKey, nodeOrder[child.TargetNodeKey]);
                }
            }
        }

        if (result.Count != steps.Count)
        {
            var cyclicNodeKeys = indegree
                .Where(item => item.Value > 0)
                .OrderBy(item => nodeOrder[item.Key])
                .Select(item => item.Key.Value);
            throw new InvalidOperationException(
                $"Process canvas forward routes must be acyclic. Mark bounded loop routes as backward before recomposition. Cycle members: {string.Join(", ", cyclicNodeKeys)}.");
        }

        return result;
    }

    private static IReadOnlyList<StructuralArc> BuildStructuralArcs(
        IReadOnlyList<ProcessDefinitionCanvasEditorNodeProjection> nodes,
        IReadOnlyList<ProcessDefinitionCanvasEdgeProjection> edges,
        IReadOnlyDictionary<ProcessDefinitionCanvasNodeKey, ProcessDefinitionCanvasEditorNodeProjection> stepByKey,
        IReadOnlyDictionary<ProcessDefinitionCanvasNodeKey, int> nodeOrder)
    {
        var routerOwners = nodes
            .Where(node => node.Kind == ProcessDefinitionCanvasNodeKind.BranchRouter)
            .Select(router => new
            {
                router.NodeKey,
                Owner = ResolveRouterOwnerKey(router, stepByKey, edges)
            })
            .Where(item => item.Owner is not null)
            .ToDictionary(item => item.NodeKey, item => item.Owner!.Value);
        var arcs = new List<StructuralArc>();
        foreach (var edge in edges.Where(edge =>
            edge.Kind is ProcessDefinitionCanvasEdgeKind.Dependency or ProcessDefinitionCanvasEdgeKind.BranchRoute))
        {
            if (!stepByKey.ContainsKey(edge.ToNodeKey))
            {
                continue;
            }

            var sourceNodeKey = stepByKey.ContainsKey(edge.FromNodeKey)
                ? edge.FromNodeKey
                : routerOwners.GetValueOrDefault(edge.FromNodeKey);
            if (sourceNodeKey == default || !stepByKey.ContainsKey(sourceNodeKey))
            {
                continue;
            }

            arcs.Add(new StructuralArc(
                edge.EdgeKey,
                sourceNodeKey,
                edge.ToNodeKey,
                edge.Kind,
                edge.IsBackwardRoute));
        }

        return arcs
            .OrderBy(arc => nodeOrder[arc.SourceNodeKey])
            .ThenBy(arc => nodeOrder[arc.TargetNodeKey])
            .ThenBy(arc => arc.EdgeKey.Value, StringComparer.Ordinal)
            .ToArray();
    }

    private static ProcessDefinitionCanvasNodeKey? ResolveRouterOwnerKey(
        ProcessDefinitionCanvasEditorNodeProjection router,
        IReadOnlyDictionary<ProcessDefinitionCanvasNodeKey, ProcessDefinitionCanvasEditorNodeProjection> stepByKey,
        IReadOnlyList<ProcessDefinitionCanvasEdgeProjection> edges)
    {
        if (router.StepKey is { } stepKey)
        {
            var matchingStep = stepByKey.Values.FirstOrDefault(step => step.StepKey == stepKey);
            if (matchingStep is not null)
            {
                return matchingStep.NodeKey;
            }
        }

        var incoming = edges.FirstOrDefault(edge =>
            edge.ToNodeKey == router.NodeKey &&
            stepByKey.ContainsKey(edge.FromNodeKey));
        return incoming?.FromNodeKey;
    }

    private static IReadOnlyDictionary<ProcessDefinitionCanvasNodeKey, IReadOnlyList<StructuralArc>> BuildArcLookup(
        IReadOnlyList<StructuralArc> arcs,
        Func<StructuralArc, ProcessDefinitionCanvasNodeKey> keySelector,
        IReadOnlyDictionary<ProcessDefinitionCanvasNodeKey, int> nodeOrder,
        Func<StructuralArc, ProcessDefinitionCanvasNodeKey> orderSelector)
        => arcs
            .GroupBy(keySelector)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<StructuralArc>)group
                    .OrderBy(arc => nodeOrder[orderSelector(arc)])
                    .ThenBy(arc => arc.EdgeKey.Value, StringComparer.Ordinal)
                    .ToArray());

    private static IEnumerable<int> EnumerateSideLanes(int nodeCount)
    {
        for (var lane = 1; lane <= Math.Max(1, nodeCount); lane++)
        {
            yield return -lane;
            yield return lane;
        }
    }

    private static void ValidateNodeKeys(IReadOnlyList<ProcessDefinitionCanvasEditorNodeProjection> nodes)
    {
        var duplicate = nodes
            .GroupBy(node => node.NodeKey)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidOperationException($"Process canvas contains duplicate node key '{duplicate.Key.Value}'.");
        }
    }

    private sealed record StructuralArc(
        ProcessDefinitionCanvasEdgeKey EdgeKey,
        ProcessDefinitionCanvasNodeKey SourceNodeKey,
        ProcessDefinitionCanvasNodeKey TargetNodeKey,
        ProcessDefinitionCanvasEdgeKind Kind,
        bool IsBackwardRoute);

    private sealed record MainPath(
        IReadOnlyList<ProcessDefinitionCanvasNodeKey> NodeKeys,
        int TerminalPriority);
}
