using CanDoItAll.Processes.Projections;

namespace CanDoItAll.Processes.Application;

internal sealed record ProcessDefinitionCanvasLayoutResult(
    IReadOnlyList<ProcessDefinitionCanvasEditorNodeProjection> Nodes,
    IReadOnlyList<ProcessDefinitionCanvasEdgeProjection> Edges,
    IReadOnlySet<ProcessDefinitionCanvasNodeKey> MainPathNodeKeys);

internal static class ProcessDefinitionCanvasRecompositionEngine
{
    private const double FirstStepX = 240d;
    private const double FirstGroupTop = 180d;
    private const double MinimumStepColumnGap = 560d;
    private const double MinimumStructuralLaneGap = 400d;
    private const double StepGroupGap = 112d;
    private const double UnownedNodeGap = 80d;

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
        var laneByNodeKey = ResolveLanes(steps, topologicalOrder, incoming, rankByNodeKey, mainPathNodeKeys);
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
        var stepByNodeKey = steps.ToDictionary(step => step.NodeKey);
        var ownerStepKeys = ResolveOwnerStepKeys(nodes, edges, steps, nodeOrder);
        var localGroups = ComposeLocalStepGroups(nodes, steps, ownerStepKeys, nodeOrder);
        var groupBounds = localGroups.ToDictionary(
            group => group.Key,
            group => ResolveBounds(group.Value));
        var rankPositions = ResolveRankPositions(steps, rankByNodeKey, groupBounds);
        var lanePositions = ResolveLanePositions(steps, laneByNodeKey, groupBounds);
        var positionedByKey = new Dictionary<ProcessDefinitionCanvasNodeKey, ProcessDefinitionCanvasEditorNodeProjection>();

        foreach (var step in steps.OrderBy(step => nodeOrder[step.NodeKey]))
        {
            var offsetX = rankPositions[rankByNodeKey[step.NodeKey]];
            var offsetY = lanePositions[laneByNodeKey[step.NodeKey]];
            foreach (var localNode in localGroups[step.NodeKey])
            {
                positionedByKey[localNode.NodeKey] = localNode with
                {
                    X = localNode.X + offsetX,
                    Y = localNode.Y + offsetY
                };
            }
        }

        var unownedNodes = nodes
            .Where(node => !stepByNodeKey.ContainsKey(node.NodeKey) && !ownerStepKeys.ContainsKey(node.NodeKey))
            .OrderBy(node => nodeOrder[node.NodeKey])
            .ToArray();
        if (unownedNodes.Length > 0)
        {
            var occupied = positionedByKey.Values
                .Select(ProcessDefinitionCanvasPlacementPolicy.ResolveBounds)
                .ToArray();
            var rowHeight = unownedNodes.Max(node => node.Height);
            var rowY = occupied.Length == 0
                ? FirstGroupTop + (rowHeight / 2d)
                : occupied.Min(bounds => bounds.Top) - StepGroupGap - (rowHeight / 2d);
            var cursorX = occupied.Length == 0
                ? FirstStepX
                : occupied.Min(bounds => bounds.Left);
            foreach (var node in unownedNodes)
            {
                var x = cursorX + (node.Width / 2d);
                positionedByKey[node.NodeKey] = node with { X = x, Y = rowY };
                cursorX = x + (node.Width / 2d) + UnownedNodeGap;
            }
        }

        return nodes
            .Select(node => positionedByKey.GetValueOrDefault(node.NodeKey, node))
            .ToArray();
    }

    private static IReadOnlyDictionary<ProcessDefinitionCanvasNodeKey, ProcessDefinitionCanvasNodeKey> ResolveOwnerStepKeys(
        IReadOnlyList<ProcessDefinitionCanvasEditorNodeProjection> nodes,
        IReadOnlyList<ProcessDefinitionCanvasEdgeProjection> edges,
        IReadOnlyList<ProcessDefinitionCanvasEditorNodeProjection> steps,
        IReadOnlyDictionary<ProcessDefinitionCanvasNodeKey, int> nodeOrder)
    {
        var stepByNodeKey = steps.ToDictionary(step => step.NodeKey);
        var stepByStepKey = steps
            .Where(step => step.StepKey is not null)
            .GroupBy(step => step.StepKey!.Value)
            .ToDictionary(group => group.Key, group => group.First());
        var owners = new Dictionary<ProcessDefinitionCanvasNodeKey, ProcessDefinitionCanvasNodeKey>();
        foreach (var node in nodes.Where(node => node.Kind != ProcessDefinitionCanvasNodeKind.Step))
        {
            var directOwner = ResolveDirectOwnerStepKey(node, edges, stepByNodeKey, stepByStepKey);
            if (directOwner is not null)
            {
                owners[node.NodeKey] = directOwner.Value;
            }
        }

        foreach (var reference in nodes
            .Where(node =>
                !owners.ContainsKey(node.NodeKey) &&
                node.Kind is ProcessDefinitionCanvasNodeKind.Role or ProcessDefinitionCanvasNodeKind.Artifact)
            .OrderBy(node => nodeOrder[node.NodeKey]))
        {
            var matchingOwner = nodes
                .Where(candidate =>
                    candidate.NodeKey != reference.NodeKey &&
                    owners.ContainsKey(candidate.NodeKey) &&
                    HasSameSharedIdentity(reference, candidate))
                .OrderBy(candidate => nodeOrder[candidate.NodeKey])
                .Select(candidate => (ProcessDefinitionCanvasNodeKey?)owners[candidate.NodeKey])
                .FirstOrDefault();
            if (matchingOwner is not null)
            {
                owners[reference.NodeKey] = matchingOwner.Value;
            }
        }

        return owners;
    }

    private static ProcessDefinitionCanvasNodeKey? ResolveDirectOwnerStepKey(
        ProcessDefinitionCanvasEditorNodeProjection node,
        IReadOnlyList<ProcessDefinitionCanvasEdgeProjection> edges,
        IReadOnlyDictionary<ProcessDefinitionCanvasNodeKey, ProcessDefinitionCanvasEditorNodeProjection> stepByNodeKey,
        IReadOnlyDictionary<ProcessDefinitionStepKey, ProcessDefinitionCanvasEditorNodeProjection> stepByStepKey)
    {
        ProcessDefinitionCanvasNodeKey? ownerFromStepKey = null;
        if (node.StepKey is { } stepKey && stepByStepKey.TryGetValue(stepKey, out var step))
        {
            ownerFromStepKey = step.NodeKey;
        }

        if (node.Kind == ProcessDefinitionCanvasNodeKind.Role)
        {
            var targets = edges
                .Where(edge =>
                    edge.Kind == ProcessDefinitionCanvasEdgeKind.RoleBinding &&
                    edge.FromNodeKey == node.NodeKey &&
                    stepByNodeKey.ContainsKey(edge.ToNodeKey))
                .Select(edge => edge.ToNodeKey)
                .Distinct()
                .ToArray();
            if (targets.Length > 1)
            {
                throw new InvalidOperationException(
                    $"Role representation '{node.NodeKey.Value}' targets multiple process steps. Clone the representation so each consuming step owns one local role node.");
            }

            if (ownerFromStepKey is { } ownedStep && targets.Any(target => target != ownedStep))
            {
                throw new InvalidOperationException(
                    $"Role representation '{node.NodeKey.Value}' has a StepKey that disagrees with its role-binding target.");
            }

            return ownerFromStepKey ?? (targets.Length == 1 ? targets[0] : null);
        }

        if (ownerFromStepKey is not null)
        {
            return ownerFromStepKey;
        }

        var edgeKind = node.Kind switch
        {
            ProcessDefinitionCanvasNodeKind.BranchRouter => ProcessDefinitionCanvasEdgeKind.BranchRoute,
            ProcessDefinitionCanvasNodeKind.Artifact => ProcessDefinitionCanvasEdgeKind.ArtifactExpectation,
            ProcessDefinitionCanvasNodeKind.SubprocessBoundary => ProcessDefinitionCanvasEdgeKind.SubprocessBoundary,
            _ => (ProcessDefinitionCanvasEdgeKind?)null
        };
        return edgeKind is null
            ? null
            : edges.FirstOrDefault(edge =>
                edge.Kind == edgeKind &&
                edge.ToNodeKey == node.NodeKey &&
                stepByNodeKey.ContainsKey(edge.FromNodeKey))?.FromNodeKey;
    }

    private static IReadOnlyDictionary<ProcessDefinitionCanvasNodeKey, IReadOnlyList<ProcessDefinitionCanvasEditorNodeProjection>> ComposeLocalStepGroups(
        IReadOnlyList<ProcessDefinitionCanvasEditorNodeProjection> nodes,
        IReadOnlyList<ProcessDefinitionCanvasEditorNodeProjection> steps,
        IReadOnlyDictionary<ProcessDefinitionCanvasNodeKey, ProcessDefinitionCanvasNodeKey> ownerStepKeys,
        IReadOnlyDictionary<ProcessDefinitionCanvasNodeKey, int> nodeOrder)
    {
        var groups = new Dictionary<ProcessDefinitionCanvasNodeKey, IReadOnlyList<ProcessDefinitionCanvasEditorNodeProjection>>();
        foreach (var step in steps.OrderBy(step => nodeOrder[step.NodeKey]))
        {
            var localStep = step with { X = 0d, Y = 0d };
            var localNodes = new List<ProcessDefinitionCanvasEditorNodeProjection> { localStep };
            var occupied = new List<ProcessDefinitionCanvasBounds>
            {
                ProcessDefinitionCanvasPlacementPolicy.ResolveBounds(localStep)
            };
            var members = nodes
                .Where(node => ownerStepKeys.GetValueOrDefault(node.NodeKey) == step.NodeKey)
                .OrderBy(ResolveGroupMemberOrder)
                .ThenBy(node => nodeOrder[node.NodeKey]);
            foreach (var member in members)
            {
                var position = member.Kind switch
                {
                    ProcessDefinitionCanvasNodeKind.BranchRouter => ProcessDefinitionCanvasPlacementPolicy.PlaceBranchRouter(
                        occupied,
                        localStep,
                        member.Width,
                        member.Height),
                    ProcessDefinitionCanvasNodeKind.Role => ProcessDefinitionCanvasPlacementPolicy.PlaceInputAttachment(
                        occupied,
                        localStep,
                        member.Width,
                        member.Height),
                    _ => ProcessDefinitionCanvasPlacementPolicy.PlaceOutputAttachment(
                        occupied,
                        localStep,
                        member.Width,
                        member.Height)
                };
                var positioned = member with { X = position.X, Y = position.Y };
                localNodes.Add(positioned);
                occupied.Add(ProcessDefinitionCanvasPlacementPolicy.ResolveBounds(positioned));
            }

            groups[step.NodeKey] = localNodes;
        }

        return groups;
    }

    private static IReadOnlyDictionary<int, double> ResolveRankPositions(
        IReadOnlyList<ProcessDefinitionCanvasEditorNodeProjection> steps,
        IReadOnlyDictionary<ProcessDefinitionCanvasNodeKey, int> rankByNodeKey,
        IReadOnlyDictionary<ProcessDefinitionCanvasNodeKey, ProcessDefinitionCanvasBounds> groupBounds)
    {
        var extents = steps
            .GroupBy(step => rankByNodeKey[step.NodeKey])
            .ToDictionary(
                group => group.Key,
                group => new AxisExtents(
                    group.Max(step => -groupBounds[step.NodeKey].Left),
                    group.Max(step => groupBounds[step.NodeKey].Right)));
        return ResolveAxisPositions(extents, FirstStepX, MinimumStepColumnGap);
    }

    private static IReadOnlyDictionary<int, double> ResolveLanePositions(
        IReadOnlyList<ProcessDefinitionCanvasEditorNodeProjection> steps,
        IReadOnlyDictionary<ProcessDefinitionCanvasNodeKey, int> laneByNodeKey,
        IReadOnlyDictionary<ProcessDefinitionCanvasNodeKey, ProcessDefinitionCanvasBounds> groupBounds)
    {
        var extents = steps
            .GroupBy(step => laneByNodeKey[step.NodeKey])
            .ToDictionary(
                group => group.Key,
                group => new AxisExtents(
                    group.Max(step => -groupBounds[step.NodeKey].Top),
                    group.Max(step => groupBounds[step.NodeKey].Bottom)));
        var firstLane = extents.Keys.Min();
        return ResolveAxisPositions(
            extents,
            FirstGroupTop + extents[firstLane].Before,
            MinimumStructuralLaneGap);
    }

    private static IReadOnlyDictionary<int, double> ResolveAxisPositions(
        IReadOnlyDictionary<int, AxisExtents> extents,
        double firstPosition,
        double minimumCenterGap)
    {
        var orderedKeys = extents.Keys.OrderBy(key => key).ToArray();
        var positions = new Dictionary<int, double> { [orderedKeys[0]] = firstPosition };
        for (var index = 1; index < orderedKeys.Length; index++)
        {
            var previousKey = orderedKeys[index - 1];
            var key = orderedKeys[index];
            var requiredGap = extents[previousKey].After + StepGroupGap + extents[key].Before;
            positions[key] = positions[previousKey] + Math.Max(minimumCenterGap, requiredGap);
        }

        return positions;
    }

    private static ProcessDefinitionCanvasBounds ResolveBounds(
        IReadOnlyList<ProcessDefinitionCanvasEditorNodeProjection> nodes)
        => new(
            nodes.Min(node => node.X - (node.Width / 2d)),
            nodes.Min(node => node.Y - (node.Height / 2d)),
            nodes.Max(node => node.X + (node.Width / 2d)),
            nodes.Max(node => node.Y + (node.Height / 2d)));

    private static bool HasSameSharedIdentity(
        ProcessDefinitionCanvasEditorNodeProjection left,
        ProcessDefinitionCanvasEditorNodeProjection right)
        => left.Kind == right.Kind &&
           left.Kind switch
           {
               ProcessDefinitionCanvasNodeKind.Role => left.RoleKey is not null && left.RoleKey == right.RoleKey,
               ProcessDefinitionCanvasNodeKind.Artifact =>
                   !string.IsNullOrWhiteSpace(left.ArtifactKey) &&
                   string.Equals(left.ArtifactKey, right.ArtifactKey, StringComparison.OrdinalIgnoreCase),
               _ => false
           };

    private static int ResolveGroupMemberOrder(ProcessDefinitionCanvasEditorNodeProjection node)
        => node.Kind switch
        {
            ProcessDefinitionCanvasNodeKind.BranchRouter => 0,
            ProcessDefinitionCanvasNodeKind.Role => 1,
            ProcessDefinitionCanvasNodeKind.Artifact => 2,
            ProcessDefinitionCanvasNodeKind.SubprocessBoundary => 3,
            _ => 4
        };

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
        IReadOnlyList<ProcessDefinitionCanvasEditorNodeProjection> steps,
        IReadOnlyList<ProcessDefinitionCanvasNodeKey> topologicalOrder,
        IReadOnlyDictionary<ProcessDefinitionCanvasNodeKey, IReadOnlyList<StructuralArc>> incoming,
        IReadOnlyDictionary<ProcessDefinitionCanvasNodeKey, int> rankByNodeKey,
        IReadOnlySet<ProcessDefinitionCanvasNodeKey> mainPathNodeKeys)
    {
        var stepByKey = steps.ToDictionary(step => step.NodeKey);
        var mainPathY = steps
            .Where(step => mainPathNodeKeys.Contains(step.NodeKey))
            .Average(step => step.Y);
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
            var preferredSide = Math.Sign(stepByKey[nodeKey].Y - mainPathY);
            var lane = inheritedLanes
                .Concat(EnumerateSideLanes(topologicalOrder.Count, preferredSide))
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
            .ThenBy(candidate => Math.Abs(stepByKey[candidate.Arc.TargetNodeKey].Y - stepByKey[nodeKey].Y))
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

    private static IEnumerable<int> EnumerateSideLanes(int nodeCount, int preferredSide)
    {
        if (preferredSide != 0)
        {
            for (var lane = 1; lane <= Math.Max(1, nodeCount); lane++)
            {
                yield return lane * preferredSide;
            }

            for (var lane = 1; lane <= Math.Max(1, nodeCount); lane++)
            {
                yield return lane * -preferredSide;
            }

            yield break;
        }

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

    private sealed record AxisExtents(
        double Before,
        double After);
}
