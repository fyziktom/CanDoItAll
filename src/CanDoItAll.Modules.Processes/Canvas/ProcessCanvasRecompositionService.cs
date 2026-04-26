using CanDoItAll.Components.CanvasLib;

namespace CanDoItAll.Modules.Processes;

public enum ProcessCanvasRecompositionMode
{
    ResolveCollisions = 0,
    AddSpaceAround = 1,
    Recompose = 2
}

public sealed record ProcessCanvasRecompositionResult(
    ProcessCanvasRecompositionMode Mode,
    int RepositionedNodeCount,
    IReadOnlyList<string> RepositionedNodeIds);

public sealed class ProcessCanvasRecompositionService(ProcessCanvasSurfaceFactory surfaceFactory)
{
    private const double StepColumnStartX = 220d;
    private const double StepColumnGap = 390d;
    private const double StepLaneGap = 290d;
    private const double RoleColumnGap = 430d;
    private const double BranchOffsetX = 220d;

    public ProcessCanvasRecompositionResult Apply(
        ProcessDefinitionEditorModel editor,
        ProcessCanvasRecompositionMode mode)
    {
        ArgumentNullException.ThrowIfNull(editor);

        ProcessCanvasBranching.NormalizeDefinitionEditor(editor);

        return mode switch
        {
            ProcessCanvasRecompositionMode.ResolveCollisions => ApplyCollisionRelief(editor),
            ProcessCanvasRecompositionMode.AddSpaceAround => ApplySpacing(editor),
            _ => ApplySmartRecomposition(editor)
        };
    }

    private ProcessCanvasRecompositionResult ApplyCollisionRelief(ProcessDefinitionEditorModel editor)
    {
        var baseline = BuildDefinitionNodeBoxMap(editor);
        var resolved = CanvasLayoutCollisionResolver.Resolve(
                baseline.Values.ToList(),
                new CanvasLayoutCollisionOptions
                {
                    MinimumGapX = 52d,
                    MinimumGapY = 44d,
                    AxisPreference = CanvasLayoutAxisPreference.Vertical,
                    PreferredAxisBias = 2.8d
                })
            .ToDictionary(node => node.NodeId, StringComparer.Ordinal);

        return ApplyPositions(editor, ProcessCanvasRecompositionMode.ResolveCollisions, baseline, resolved);
    }

    private ProcessCanvasRecompositionResult ApplySpacing(ProcessDefinitionEditorModel editor)
    {
        var baseline = BuildDefinitionNodeBoxMap(editor);
        var expanded = CanvasLayoutSpacingExpander.Expand(
            baseline.Values.ToList(),
            new CanvasLayoutExpansionOptions
            {
                HorizontalFactor = 1.18d,
                VerticalFactor = 1.14d,
                MinimumOffset = 24d
            });
        var resolved = CanvasLayoutCollisionResolver.Resolve(
                expanded,
                new CanvasLayoutCollisionOptions
                {
                    MinimumGapX = 64d,
                    MinimumGapY = 52d,
                    AxisPreference = CanvasLayoutAxisPreference.Vertical,
                    PreferredAxisBias = 2.6d
                })
            .ToDictionary(node => node.NodeId, StringComparer.Ordinal);

        return ApplyPositions(editor, ProcessCanvasRecompositionMode.AddSpaceAround, baseline, resolved);
    }

    private ProcessCanvasRecompositionResult ApplySmartRecomposition(ProcessDefinitionEditorModel editor)
    {
        var baseline = BuildDefinitionNodeBoxMap(editor);
        var nodeMap = surfaceFactory.BuildDefinitionSurface(editor).Nodes
            .ToDictionary(node => node.Id, StringComparer.Ordinal);
        var stepBoxes = BuildStepLayout(editor, nodeMap);
        stepBoxes = CanvasLayoutCollisionResolver.Resolve(
                stepBoxes.Values.ToList(),
                new CanvasLayoutCollisionOptions
                {
                    MinimumGapX = 48d,
                    MinimumGapY = 44d,
                    AxisPreference = CanvasLayoutAxisPreference.Vertical,
                    PreferredAxisBias = 3.2d
                })
            .ToDictionary(node => node.NodeId, StringComparer.Ordinal);

        var roleBoxes = BuildRoleLayout(editor, nodeMap, stepBoxes);
        roleBoxes = CanvasLayoutCollisionResolver.Resolve(
                roleBoxes.Values.ToList(),
                new CanvasLayoutCollisionOptions
                {
                    MinimumGapX = 32d,
                    MinimumGapY = 36d,
                    AxisPreference = CanvasLayoutAxisPreference.Vertical,
                    PreferredAxisBias = 8d
                })
            .ToDictionary(node => node.NodeId, StringComparer.Ordinal);

        var roleColumnX = stepBoxes.Count == 0
            ? -RoleColumnGap
            : stepBoxes.Values.Min(node => node.X) - RoleColumnGap;
        foreach (var roleBox in roleBoxes.Values)
        {
            roleBox.X = roleColumnX;
        }

        var branchBoxes = BuildBranchLayout(editor, nodeMap, stepBoxes);
        var branchResolverInput = new List<CanvasLayoutNodeBox>(stepBoxes.Count + roleBoxes.Count + branchBoxes.Count);
        branchResolverInput.AddRange(stepBoxes.Values.Select(node => CloneAsPinned(node)));
        branchResolverInput.AddRange(roleBoxes.Values.Select(node => CloneAsPinned(node)));
        branchResolverInput.AddRange(branchBoxes.Values);
        var resolvedBranchState = CanvasLayoutCollisionResolver.Resolve(
            branchResolverInput,
            new CanvasLayoutCollisionOptions
            {
                MinimumGapX = 44d,
                MinimumGapY = 34d,
                AxisPreference = CanvasLayoutAxisPreference.Auto,
                PreferredAxisBias = 1.3d
            });
        var resolved = CanvasLayoutCollisionResolver.Resolve(
                stepBoxes.Values
                    .Concat(roleBoxes.Values)
                    .Concat(resolvedBranchState.Where(node => !node.IsPinned))
                    .ToList(),
                new CanvasLayoutCollisionOptions
                {
                    MinimumGapX = 40d,
                    MinimumGapY = 36d,
                    AxisPreference = CanvasLayoutAxisPreference.Vertical,
                    PreferredAxisBias = 4.4d
                })
            .ToDictionary(node => node.NodeId, StringComparer.Ordinal);

        return ApplyPositions(editor, ProcessCanvasRecompositionMode.Recompose, baseline, resolved);
    }

    private Dictionary<string, CanvasLayoutNodeBox> BuildDefinitionNodeBoxMap(ProcessDefinitionEditorModel editor)
    {
        return surfaceFactory.BuildDefinitionSurface(editor).Nodes
            .Select(node => CanvasLayoutNodeBox.FromNode(node))
            .ToDictionary(node => node.NodeId, StringComparer.Ordinal);
    }

    private Dictionary<string, CanvasLayoutNodeBox> BuildStepLayout(
        ProcessDefinitionEditorModel editor,
        IReadOnlyDictionary<string, CanvasWorkbenchNode> nodeMap)
    {
        var steps = editor.Steps.ToList();
        var stepIdMap = steps
            .Where(step => step.Id.HasValue)
            .ToDictionary(step => step.Id!.Value);
        var dependentsByParentId = BuildDependentsByParentId(steps, stepIdMap);
        var topologicalOrder = BuildTopologicalOrder(steps, stepIdMap);
        var rootLaneCursor = 0;
        var occupiedLanesByColumn = new Dictionary<int, HashSet<int>>();
        var laneByStepId = new Dictionary<Guid, int>();
        var columnByStepId = new Dictionary<Guid, int>();
        var branchLaneOffsetsByParentId = new Dictionary<Guid, Dictionary<string, int>>();
        var stepBoxes = new Dictionary<string, CanvasLayoutNodeBox>(StringComparer.Ordinal);

        foreach (var step in topologicalOrder)
        {
            var stepId = step.Id;
            var dependencyEntries = ProcessCanvasBranching.GetOrderedDependencies(step)
                .Where(dependency => dependency.DependsOnStepId.HasValue && stepIdMap.ContainsKey(dependency.DependsOnStepId.Value))
                .ToList();
            var column = dependencyEntries.Count == 0
                ? 0
                : dependencyEntries.Max(dependency => columnByStepId.GetValueOrDefault(dependency.DependsOnStepId!.Value, 0) + 1);
            var preferredLane = ResolvePreferredLane(
                step,
                dependencyEntries,
                dependentsByParentId,
                laneByStepId,
                branchLaneOffsetsByParentId,
                ref rootLaneCursor);
            var lane = ClaimLane(column, preferredLane, occupiedLanesByColumn);
            if (stepId.HasValue)
            {
                laneByStepId[stepId.Value] = lane;
                columnByStepId[stepId.Value] = column;
            }

            var nodeId = ProcessCanvasBranching.BuildDefinitionStepNodeId(step);
            var node = nodeMap[nodeId];
            var box = CanvasLayoutNodeBox.FromNode(node);
            box.X = StepColumnStartX + (column * StepColumnGap);
            box.Y = lane * StepLaneGap;
            stepBoxes[nodeId] = box;
        }

        return stepBoxes;
    }

    private Dictionary<string, CanvasLayoutNodeBox> BuildRoleLayout(
        ProcessDefinitionEditorModel editor,
        IReadOnlyDictionary<string, CanvasWorkbenchNode> nodeMap,
        IReadOnlyDictionary<string, CanvasLayoutNodeBox> stepBoxes)
    {
        var roleBoxes = new Dictionary<string, CanvasLayoutNodeBox>(StringComparer.Ordinal);
        var stepMidpointsByRoleId = editor.Steps
            .SelectMany(step =>
            {
                var stepNodeId = ProcessCanvasBranching.BuildDefinitionStepNodeId(step);
                if (!stepBoxes.TryGetValue(stepNodeId, out var stepBox))
                {
                    return [];
                }

                var bindings = step.RoleAssignments
                    .Where(assignment => assignment.RoleRequirementId.HasValue)
                    .Select(assignment => assignment.RoleRequirementId!.Value)
                    .Distinct()
                    .Select(roleId => new KeyValuePair<Guid, double>(roleId, stepBox.Y))
                    .ToList();
                if (step.DecisionRoleRequirementId.HasValue)
                {
                    bindings.Add(new KeyValuePair<Guid, double>(step.DecisionRoleRequirementId.Value, stepBox.Y));
                }

                return bindings;
            })
            .GroupBy(item => item.Key)
            .ToDictionary(group => group.Key, group => group.Select(item => item.Value).ToList());
        var roleColumnX = stepBoxes.Count == 0
            ? -RoleColumnGap
            : stepBoxes.Values.Min(node => node.X) - RoleColumnGap;

        for (var index = 0; index < editor.Roles.Count; index++)
        {
            var role = editor.Roles[index];
            var nodeId = ProcessCanvasBranching.BuildDefinitionRoleNodeId(role);
            var node = nodeMap[nodeId];
            var box = CanvasLayoutNodeBox.FromNode(node);
            box.X = roleColumnX;
            if (role.Id.HasValue && stepMidpointsByRoleId.TryGetValue(role.Id.Value, out var stepYValues) && stepYValues.Count > 0)
            {
                box.Y = stepYValues.Average();
            }
            else
            {
                box.Y = (index - ((editor.Roles.Count - 1) / 2d)) * (StepLaneGap * 0.8d);
            }

            roleBoxes[nodeId] = box;
        }

        return roleBoxes;
    }

    private Dictionary<string, CanvasLayoutNodeBox> BuildBranchLayout(
        ProcessDefinitionEditorModel editor,
        IReadOnlyDictionary<string, CanvasWorkbenchNode> nodeMap,
        IReadOnlyDictionary<string, CanvasLayoutNodeBox> stepBoxes)
    {
        var boxes = new Dictionary<string, CanvasLayoutNodeBox>(StringComparer.Ordinal);
        foreach (var step in editor.Steps.Where(ProcessCanvasBranching.ShouldRenderBranchRouter))
        {
            var branchNodeId = ProcessCanvasBranching.BuildDefinitionBranchNodeId(step);
            if (!nodeMap.TryGetValue(branchNodeId, out var node))
            {
                continue;
            }

            var stepNodeId = ProcessCanvasBranching.BuildDefinitionStepNodeId(step);
            if (!stepBoxes.TryGetValue(stepNodeId, out var sourceBox))
            {
                continue;
            }

            var dependentBoxes = ResolveOrderedDependents(step, editor.Steps)
                .Select(linkedStep => stepBoxes.GetValueOrDefault(ProcessCanvasBranching.BuildDefinitionStepNodeId(linkedStep)))
                .Where(box => box is not null)
                .Select(box => box!)
                .ToList();
            var box = CanvasLayoutNodeBox.FromNode(node);
            var targetX = dependentBoxes.Count == 0
                ? sourceBox.X + BranchOffsetX
                : Math.Min(
                    sourceBox.X + BranchOffsetX,
                    sourceBox.X + Math.Max(140d, (dependentBoxes.Min(item => item.X) - sourceBox.X) / 2d));
            box.X = targetX;
            box.Y = dependentBoxes.Count == 0
                ? sourceBox.Y
                : dependentBoxes.Average(item => item.Y);
            boxes[branchNodeId] = box;
        }

        return boxes;
    }

    private static IReadOnlyList<ProcessStepEditorModel> ResolveOrderedDependents(
        ProcessStepEditorModel sourceStep,
        IReadOnlyList<ProcessStepEditorModel> steps)
    {
        if (!sourceStep.Id.HasValue)
        {
            return [];
        }

        return steps
            .Where(candidate => ProcessCanvasBranching.GetOrderedDependencies(candidate)
                .Any(dependency => dependency.DependsOnStepId == sourceStep.Id.Value))
            .ToList();
    }

    private static Dictionary<Guid, List<(Guid ChildId, Guid? BranchOutcomeId)>> BuildDependentsByParentId(
        IReadOnlyList<ProcessStepEditorModel> steps,
        IReadOnlyDictionary<Guid, ProcessStepEditorModel> stepIdMap)
    {
        var dependents = new Dictionary<Guid, List<(Guid ChildId, Guid? BranchOutcomeId)>>();
        foreach (var step in steps)
        {
            if (!step.Id.HasValue)
            {
                continue;
            }

            foreach (var dependency in ProcessCanvasBranching.GetOrderedDependencies(step))
            {
                if (!dependency.DependsOnStepId.HasValue || !stepIdMap.ContainsKey(dependency.DependsOnStepId.Value))
                {
                    continue;
                }

                if (!dependents.TryGetValue(dependency.DependsOnStepId.Value, out var list))
                {
                    list = [];
                    dependents[dependency.DependsOnStepId.Value] = list;
                }

                list.Add((step.Id.Value, dependency.DependsOnBranchOutcomeId));
            }
        }

        return dependents;
    }

    private static List<ProcessStepEditorModel> BuildTopologicalOrder(
        IReadOnlyList<ProcessStepEditorModel> steps,
        IReadOnlyDictionary<Guid, ProcessStepEditorModel> stepIdMap)
    {
        var originalIndexByStepId = steps
            .Select((step, index) => new { step, index })
            .Where(item => item.step.Id.HasValue)
            .ToDictionary(item => item.step.Id!.Value, item => item.index);
        var indegreeByStepId = originalIndexByStepId.Keys.ToDictionary(stepId => stepId, _ => 0);
        var dependentsByParentId = BuildDependentsByParentId(steps, stepIdMap);

        foreach (var step in steps)
        {
            if (!step.Id.HasValue)
            {
                continue;
            }

            indegreeByStepId[step.Id.Value] = ProcessCanvasBranching.GetOrderedDependencies(step)
                .Count(dependency => dependency.DependsOnStepId.HasValue && stepIdMap.ContainsKey(dependency.DependsOnStepId.Value));
        }

        var queue = new Queue<Guid>(indegreeByStepId
            .Where(pair => pair.Value == 0)
            .OrderBy(pair => originalIndexByStepId[pair.Key])
            .Select(pair => pair.Key));
        var orderedIds = new List<Guid>(indegreeByStepId.Count);
        var enqueued = new HashSet<Guid>(queue);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            orderedIds.Add(current);
            if (!dependentsByParentId.TryGetValue(current, out var dependents))
            {
                continue;
            }

            foreach (var (childId, _) in dependents.OrderBy(item => originalIndexByStepId.GetValueOrDefault(item.ChildId, int.MaxValue)))
            {
                indegreeByStepId[childId]--;
                if (indegreeByStepId[childId] == 0 && enqueued.Add(childId))
                {
                    queue.Enqueue(childId);
                }
            }
        }

        if (orderedIds.Count != indegreeByStepId.Count)
        {
            var unresolvedStepTitles = indegreeByStepId.Keys
                .Where(stepId => !orderedIds.Contains(stepId))
                .OrderBy(stepId => originalIndexByStepId[stepId])
                .Select(stepId => string.IsNullOrWhiteSpace(stepIdMap[stepId].Title)
                    ? stepIdMap[stepId].Key
                    : stepIdMap[stepId].Title)
                .ToList();
            throw new InvalidOperationException(
                $"Process canvas recomposition requires an acyclic dependency graph. Remove the dependency cycle involving: {string.Join(" -> ", unresolvedStepTitles)}.");
        }

        return orderedIds
            .Select(stepId => stepIdMap[stepId])
            .Concat(steps.Where(step => !step.Id.HasValue))
            .ToList();
    }

    private static int ResolvePreferredLane(
        ProcessStepEditorModel step,
        IReadOnlyList<ProcessStepDependencyEditorModel> dependencies,
        IReadOnlyDictionary<Guid, List<(Guid ChildId, Guid? BranchOutcomeId)>> dependentsByParentId,
        IReadOnlyDictionary<Guid, int> laneByStepId,
        IDictionary<Guid, Dictionary<string, int>> branchLaneOffsetsByParentId,
        ref int rootLaneCursor)
    {
        if (dependencies.Count == 0)
        {
            return ResolveRootLane(rootLaneCursor++);
        }

        var dependency = dependencies
            .FirstOrDefault(item => item.DependsOnStepId.HasValue && laneByStepId.ContainsKey(item.DependsOnStepId.Value))
            ?? dependencies[0];
        if (!dependency.DependsOnStepId.HasValue || !laneByStepId.TryGetValue(dependency.DependsOnStepId.Value, out var parentLane))
        {
            return ResolveRootLane(rootLaneCursor++);
        }

        if (dependency.DependsOnBranchOutcomeId.HasValue)
        {
            var laneKey = dependency.DependsOnBranchOutcomeId.Value.ToString("D");
            return parentLane + ResolveBranchLaneOffset(branchLaneOffsetsByParentId, dependency.DependsOnStepId.Value, laneKey);
        }

        if (dependentsByParentId.TryGetValue(dependency.DependsOnStepId.Value, out var dependents) && dependents.Count > 1 && step.Id.HasValue)
        {
            var primaryChildId = ResolvePrimaryChildId(dependents);
            if (primaryChildId != step.Id.Value)
            {
                return parentLane + ResolveBranchLaneOffset(
                    branchLaneOffsetsByParentId,
                    dependency.DependsOnStepId.Value,
                    step.Id.Value.ToString("D"));
            }
        }

        return parentLane;
    }

    private static int ResolveRootLane(int index)
    {
        if (index == 0)
        {
            return 0;
        }

        var magnitude = ((index + 1) / 2);
        return index % 2 == 0
            ? magnitude
            : -magnitude;
    }

    private static int ClaimLane(
        int column,
        int preferredLane,
        IDictionary<int, HashSet<int>> occupiedLanesByColumn)
    {
        if (!occupiedLanesByColumn.TryGetValue(column, out var occupied))
        {
            occupied = [];
            occupiedLanesByColumn[column] = occupied;
        }

        foreach (var candidate in EnumerateLaneCandidates(preferredLane))
        {
            if (!occupied.Add(candidate))
            {
                continue;
            }

            return candidate;
        }

        occupied.Add(preferredLane);
        return preferredLane;
    }

    private static IEnumerable<int> EnumerateLaneCandidates(int preferredLane)
    {
        yield return preferredLane;
        for (var offset = 1; offset < 64; offset++)
        {
            yield return preferredLane - offset;
            yield return preferredLane + offset;
        }
    }

    private static int ResolveBranchLaneOffset(
        IDictionary<Guid, Dictionary<string, int>> branchLaneOffsetsByParentId,
        Guid parentStepId,
        string laneKey)
    {
        if (!branchLaneOffsetsByParentId.TryGetValue(parentStepId, out var offsets))
        {
            offsets = new Dictionary<string, int>(StringComparer.Ordinal);
            branchLaneOffsetsByParentId[parentStepId] = offsets;
        }

        if (offsets.TryGetValue(laneKey, out var existingOffset))
        {
            return existingOffset;
        }

        var offset = offsets.Count switch
        {
            0 => -1,
            1 => 1,
            _ => offsets.Count % 2 == 0
                ? -((offsets.Count / 2) + 1)
                : ((offsets.Count / 2) + 1)
        };
        offsets[laneKey] = offset;
        return offset;
    }

    private static Guid ResolvePrimaryChildId(IReadOnlyList<(Guid ChildId, Guid? BranchOutcomeId)> dependents)
    {
        return dependents
            .OrderBy(item => item.BranchOutcomeId.HasValue)
            .ThenBy(item => item.ChildId)
            .Select(item => item.ChildId)
            .First();
    }

    private static CanvasLayoutNodeBox CloneAsPinned(CanvasLayoutNodeBox node)
    {
        var clone = node.Clone();
        return new CanvasLayoutNodeBox
        {
            NodeId = clone.NodeId,
            X = clone.X,
            Y = clone.Y,
            Width = clone.Width,
            Height = clone.Height,
            IsPinned = true
        };
    }

    private static ProcessCanvasRecompositionResult ApplyPositions(
        ProcessDefinitionEditorModel editor,
        ProcessCanvasRecompositionMode mode,
        IReadOnlyDictionary<string, CanvasLayoutNodeBox> baseline,
        IReadOnlyDictionary<string, CanvasLayoutNodeBox> updated)
    {
        var changedNodeIds = new List<string>();
        foreach (var step in editor.Steps)
        {
            var stepNodeId = ProcessCanvasBranching.BuildDefinitionStepNodeId(step);
            if (TryApplyPosition(stepNodeId, baseline, updated, step.CanvasX, step.CanvasY, out var stepX, out var stepY))
            {
                step.CanvasX = stepX;
                step.CanvasY = stepY;
                changedNodeIds.Add(stepNodeId);
            }

            if (!ProcessCanvasBranching.ShouldRenderBranchRouter(step))
            {
                continue;
            }

            var branchNodeId = ProcessCanvasBranching.BuildDefinitionBranchNodeId(step);
            if (TryApplyPosition(branchNodeId, baseline, updated, step.BranchCanvasX, step.BranchCanvasY, out var branchX, out var branchY))
            {
                step.BranchCanvasX = branchX;
                step.BranchCanvasY = branchY;
                changedNodeIds.Add(branchNodeId);
            }
        }

        foreach (var role in editor.Roles)
        {
            var roleNodeId = ProcessCanvasBranching.BuildDefinitionRoleNodeId(role);
            if (TryApplyPosition(roleNodeId, baseline, updated, role.CanvasX, role.CanvasY, out var roleX, out var roleY))
            {
                role.CanvasX = roleX;
                role.CanvasY = roleY;
                changedNodeIds.Add(roleNodeId);
            }
        }

        return new ProcessCanvasRecompositionResult(mode, changedNodeIds.Count, changedNodeIds);
    }

    private static bool TryApplyPosition(
        string nodeId,
        IReadOnlyDictionary<string, CanvasLayoutNodeBox> baseline,
        IReadOnlyDictionary<string, CanvasLayoutNodeBox> updated,
        double currentX,
        double currentY,
        out double nextX,
        out double nextY)
    {
        nextX = currentX;
        nextY = currentY;
        if (!updated.TryGetValue(nodeId, out var updatedBox))
        {
            return false;
        }

        var baselineBox = baseline.GetValueOrDefault(nodeId);
        var sourceX = baselineBox?.X ?? currentX;
        var sourceY = baselineBox?.Y ?? currentY;
        var roundedX = Math.Round(updatedBox.X, 2, MidpointRounding.AwayFromZero);
        var roundedY = Math.Round(updatedBox.Y, 2, MidpointRounding.AwayFromZero);
        if (Math.Abs(sourceX - roundedX) < 0.5d && Math.Abs(sourceY - roundedY) < 0.5d)
        {
            return false;
        }

        nextX = roundedX;
        nextY = roundedY;
        return true;
    }
}
