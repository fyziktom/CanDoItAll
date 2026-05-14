using CanDoItAll.Components.CanvasLib;

namespace CanDoItAll.Modules.Processes;

public enum ProcessCanvasRecompositionMode
{
    ResolveCollisions = 0,
    AddSpaceAround = 1,
    Recompose = 2,
    MainPathSpine = 3,
    BranchFanOut = 4,
    FeedbackLanes = 5
}

public sealed record ProcessCanvasRecompositionResult(
    ProcessCanvasRecompositionMode Mode,
    int RepositionedNodeCount,
    IReadOnlyList<string> RepositionedNodeIds);

public sealed class ProcessCanvasRecompositionService(ProcessCanvasSurfaceFactory surfaceFactory)
{
    private const double StepColumnStartX = 260d;
    private const double StepColumnGap = 900d;
    private const double StepLaneGap = 380d;
    private const double RoleColumnGap = 430d;
    private const double RoleLocalOffsetX = 430d;
    private const double RoleLocalOffsetY = 300d;
    private const double BranchOffsetX = 260d;
    private const double BranchMinimumOffsetX = 190d;
    private const double BranchSameLaneOffsetY = 230d;
    private static readonly string[] FeedbackPathTokens =
    [
        "repair",
        "recheck",
        "re-check",
        "rerun",
        "re-run",
        "feedback",
        "finding",
        "exception",
        "escalat",
        "remediat",
        "rollback",
        "blocked",
        "rework",
        "reject",
        "declin",
        "fail",
        "halt",
        "no-go",
        "invalid",
        "incomplete"
    ];
    private static readonly string[] PrimaryPathOutcomeTokens =
    [
        "accept",
        "approv",
        "ready",
        "pass",
        "success",
        "continue",
        "proceed",
        "complete"
    ];

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
            ProcessCanvasRecompositionMode.Recompose or
            ProcessCanvasRecompositionMode.MainPathSpine or
            ProcessCanvasRecompositionMode.BranchFanOut or
            ProcessCanvasRecompositionMode.FeedbackLanes => ApplySmartRecomposition(editor, mode),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported process canvas recomposition mode.")
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

    private ProcessCanvasRecompositionResult ApplySmartRecomposition(
        ProcessDefinitionEditorModel editor,
        ProcessCanvasRecompositionMode mode)
    {
        var profile = RecompositionLayoutProfile.Resolve(mode);
        var baseline = BuildDefinitionNodeBoxMap(editor);
        var nodeMap = surfaceFactory.BuildDefinitionSurface(editor).Nodes
            .ToDictionary(node => node.Id, StringComparer.Ordinal);
        var stepBoxes = BuildStepLayout(editor, nodeMap, profile);
        stepBoxes = CanvasLayoutCollisionResolver.Resolve(
                stepBoxes.Values.ToList(),
                new CanvasLayoutCollisionOptions
                {
                    MinimumGapX = profile.StepMinimumGapX,
                    MinimumGapY = profile.StepMinimumGapY,
                    AxisPreference = CanvasLayoutAxisPreference.Vertical,
                    PreferredAxisBias = profile.StepResolverBias
                })
            .ToDictionary(node => node.NodeId, StringComparer.Ordinal);

        var roleBoxes = BuildRoleLayout(editor, nodeMap, stepBoxes, profile);
        var roleResolverInput = new List<CanvasLayoutNodeBox>(stepBoxes.Count + roleBoxes.Count);
        roleResolverInput.AddRange(stepBoxes.Values.Select(CloneAsPinned));
        roleResolverInput.AddRange(roleBoxes.Values);
        roleBoxes = CanvasLayoutCollisionResolver.Resolve(
                roleResolverInput,
                new CanvasLayoutCollisionOptions
                {
                    MinimumGapX = profile.RoleMinimumGapX,
                    MinimumGapY = profile.RoleMinimumGapY,
                    AxisPreference = CanvasLayoutAxisPreference.Vertical,
                    PreferredAxisBias = 2.2d
                })
            .Where(node => !node.IsPinned)
            .ToDictionary(node => node.NodeId, StringComparer.Ordinal);

        var branchBoxes = BuildBranchLayout(editor, nodeMap, stepBoxes, profile);
        var branchResolverInput = new List<CanvasLayoutNodeBox>(stepBoxes.Count + roleBoxes.Count + branchBoxes.Count);
        branchResolverInput.AddRange(stepBoxes.Values.Select(node => CloneAsPinned(node)));
        branchResolverInput.AddRange(roleBoxes.Values.Select(node => CloneAsPinned(node)));
        branchResolverInput.AddRange(branchBoxes.Values);
        branchBoxes = CanvasLayoutCollisionResolver.Resolve(
                branchResolverInput,
                new CanvasLayoutCollisionOptions
                {
                    MinimumGapX = profile.BranchMinimumGapX,
                    MinimumGapY = profile.BranchMinimumGapY,
                    AxisPreference = CanvasLayoutAxisPreference.Auto,
                    PreferredAxisBias = 1.4d
                })
            .Where(node => !node.IsPinned)
            .ToDictionary(node => node.NodeId, StringComparer.Ordinal);
        var resolved = stepBoxes.Values
            .Concat(roleBoxes.Values)
            .Concat(branchBoxes.Values)
            .ToDictionary(node => node.NodeId, StringComparer.Ordinal);

        return ApplyPositions(editor, mode, baseline, resolved);
    }

    private sealed record RecompositionLayoutProfile(
        ProcessCanvasRecompositionMode Mode,
        double StepColumnGap,
        double StepLaneGap,
        double RoleLocalOffsetY,
        double BranchSameLaneOffsetY,
        int BranchLaneStep,
        bool PushFeedbackBranchesBelowMain,
        bool PushAlternativeBranchesAboveMain,
        double StepMinimumGapX,
        double StepMinimumGapY,
        double StepResolverBias,
        double RoleMinimumGapX,
        double RoleMinimumGapY,
        double BranchMinimumGapX,
        double BranchMinimumGapY)
    {
        public static RecompositionLayoutProfile Resolve(ProcessCanvasRecompositionMode mode)
        {
            return mode switch
            {
                ProcessCanvasRecompositionMode.Recompose => new RecompositionLayoutProfile(
                    mode,
                    ProcessCanvasRecompositionService.StepColumnGap,
                    ProcessCanvasRecompositionService.StepLaneGap,
                    ProcessCanvasRecompositionService.RoleLocalOffsetY,
                    ProcessCanvasRecompositionService.BranchSameLaneOffsetY,
                    BranchLaneStep: 1,
                    PushFeedbackBranchesBelowMain: false,
                    PushAlternativeBranchesAboveMain: false,
                    StepMinimumGapX: 48d,
                    StepMinimumGapY: 44d,
                    StepResolverBias: 3.2d,
                    RoleMinimumGapX: 72d,
                    RoleMinimumGapY: 58d,
                    BranchMinimumGapX: 72d,
                    BranchMinimumGapY: 56d),
                ProcessCanvasRecompositionMode.MainPathSpine => new RecompositionLayoutProfile(
                    mode,
                    StepColumnGap: 980d,
                    StepLaneGap: 430d,
                    RoleLocalOffsetY: 330d,
                    BranchSameLaneOffsetY: 270d,
                    BranchLaneStep: 2,
                    PushFeedbackBranchesBelowMain: false,
                    PushAlternativeBranchesAboveMain: false,
                    StepMinimumGapX: 68d,
                    StepMinimumGapY: 56d,
                    StepResolverBias: 3.8d,
                    RoleMinimumGapX: 82d,
                    RoleMinimumGapY: 64d,
                    BranchMinimumGapX: 86d,
                    BranchMinimumGapY: 68d),
                ProcessCanvasRecompositionMode.BranchFanOut => new RecompositionLayoutProfile(
                    mode,
                    StepColumnGap: 1060d,
                    StepLaneGap: 500d,
                    RoleLocalOffsetY: 360d,
                    BranchSameLaneOffsetY: 330d,
                    BranchLaneStep: 2,
                    PushFeedbackBranchesBelowMain: false,
                    PushAlternativeBranchesAboveMain: false,
                    StepMinimumGapX: 82d,
                    StepMinimumGapY: 70d,
                    StepResolverBias: 4d,
                    RoleMinimumGapX: 96d,
                    RoleMinimumGapY: 72d,
                    BranchMinimumGapX: 96d,
                    BranchMinimumGapY: 76d),
                ProcessCanvasRecompositionMode.FeedbackLanes => new RecompositionLayoutProfile(
                    mode,
                    StepColumnGap: 1080d,
                    StepLaneGap: 520d,
                    RoleLocalOffsetY: 370d,
                    BranchSameLaneOffsetY: 350d,
                    BranchLaneStep: 2,
                    PushFeedbackBranchesBelowMain: true,
                    PushAlternativeBranchesAboveMain: true,
                    StepMinimumGapX: 90d,
                    StepMinimumGapY: 78d,
                    StepResolverBias: 4.2d,
                    RoleMinimumGapX: 104d,
                    RoleMinimumGapY: 78d,
                    BranchMinimumGapX: 104d,
                    BranchMinimumGapY: 84d),
                _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported process canvas recomposition layout profile.")
            };
        }
    }

    private Dictionary<string, CanvasLayoutNodeBox> BuildDefinitionNodeBoxMap(ProcessDefinitionEditorModel editor)
    {
        return surfaceFactory.BuildDefinitionSurface(editor).Nodes
            .Select(node => CanvasLayoutNodeBox.FromNode(node))
            .ToDictionary(node => node.NodeId, StringComparer.Ordinal);
    }

    private Dictionary<string, CanvasLayoutNodeBox> BuildStepLayout(
        ProcessDefinitionEditorModel editor,
        IReadOnlyDictionary<string, CanvasWorkbenchNode> nodeMap,
        RecompositionLayoutProfile profile)
    {
        var steps = editor.Steps.ToList();
        var stepIdMap = steps
            .Where(step => step.Id.HasValue)
            .ToDictionary(step => step.Id!.Value);
        var primaryOutcomeIdsByStepId = BuildPrimaryOutcomeIdsByStepId(steps);
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
                columnByStepId,
                primaryOutcomeIdsByStepId,
                branchLaneOffsetsByParentId,
                profile,
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
            box.X = StepColumnStartX + (column * profile.StepColumnGap);
            box.Y = lane * profile.StepLaneGap;
            stepBoxes[nodeId] = box;
        }

        return stepBoxes;
    }

    private Dictionary<string, CanvasLayoutNodeBox> BuildRoleLayout(
        ProcessDefinitionEditorModel editor,
        IReadOnlyDictionary<string, CanvasWorkbenchNode> nodeMap,
        IReadOnlyDictionary<string, CanvasLayoutNodeBox> stepBoxes,
        RecompositionLayoutProfile profile)
    {
        var roleBoxes = new Dictionary<string, CanvasLayoutNodeBox>(StringComparer.Ordinal);
        var roleNodes = nodeMap.Values
            .Where(node => string.Equals(node.Kind, ProcessCanvasCatalog.NodeKinds.DefinitionRole, StringComparison.Ordinal))
            .OrderBy(node => node.X)
            .ThenBy(node => node.Y)
            .ThenBy(node => node.Id, StringComparer.Ordinal)
            .ToList();
        var anchorsByRoleId = BuildRoleAnchors(editor, stepBoxes);
        var roleColumnX = stepBoxes.Count == 0
            ? -RoleColumnGap
            : stepBoxes.Values.Min(node => node.X) - RoleColumnGap;
        var roleInstancesByStepToken = roleNodes
            .Where(node => ProcessCanvasBranching.TryResolveDefinitionRoleInstanceTokens(node.Id, out _, out _))
            .GroupBy(
                node =>
                {
                    ProcessCanvasBranching.TryResolveDefinitionRoleInstanceTokens(node.Id, out _, out var stepToken);
                    return stepToken;
                },
                StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(node => node.Y).ThenBy(node => node.Id, StringComparer.Ordinal).ToList(),
                StringComparer.Ordinal);

        for (var index = 0; index < roleNodes.Count; index++)
        {
            var node = roleNodes[index];
            var box = CanvasLayoutNodeBox.FromNode(node);
            if (ProcessCanvasBranching.TryResolveDefinitionRoleInstanceTokens(node.Id, out _, out var stepToken) &&
                TryResolveDefinitionStepByToken(editor.Steps, stepToken, out var relatedStep) &&
                stepBoxes.TryGetValue(ProcessCanvasBranching.BuildDefinitionStepNodeId(relatedStep), out var relatedStepBox))
            {
                var siblingIndex = ResolveRoleInstanceSiblingIndex(roleInstancesByStepToken, stepToken, node.Id);
                var siblingCount = roleInstancesByStepToken.GetValueOrDefault(stepToken)?.Count ?? 1;
                box.X = relatedStepBox.X - RoleLocalOffsetX;
                box.Y = relatedStepBox.Y + ResolveRoleInstanceOffsetY(siblingIndex, siblingCount, profile);
            }
            else if (ProcessCanvasBranching.TryResolveDefinitionRoleToken(node.Id, out var roleToken) &&
                TryResolveDefinitionRoleByToken(editor.Roles, roleToken, out var role) &&
                role.Id.HasValue &&
                anchorsByRoleId.TryGetValue(role.Id.Value, out var anchors) &&
                anchors.Count > 0)
            {
                var anchorX = anchors.Average(anchor => anchor.X);
                var anchorY = anchors.Average(anchor => anchor.Y);
                var side = ResolveRoleSide(index, anchorY, profile);
                box.X = anchorX - RoleLocalOffsetX;
                box.Y = anchorY + (side * profile.RoleLocalOffsetY);
            }
            else
            {
                box.X = roleColumnX;
                box.Y = ResolveUnboundRoleY(index, roleNodes.Count, profile);
            }

            roleBoxes[node.Id] = box;
        }

        return roleBoxes;
    }

    private Dictionary<string, CanvasLayoutNodeBox> BuildBranchLayout(
        ProcessDefinitionEditorModel editor,
        IReadOnlyDictionary<string, CanvasWorkbenchNode> nodeMap,
        IReadOnlyDictionary<string, CanvasLayoutNodeBox> stepBoxes,
        RecompositionLayoutProfile profile)
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
                    sourceBox.X + Math.Max(BranchMinimumOffsetX, (dependentBoxes.Min(item => item.X) - sourceBox.X) / 2d));
            box.X = targetX;
            box.Y = ResolveBranchRouterY(sourceBox, dependentBoxes, profile);
            boxes[branchNodeId] = box;
        }

        return boxes;
    }

    private static Dictionary<Guid, List<RoleLayoutAnchor>> BuildRoleAnchors(
        ProcessDefinitionEditorModel editor,
        IReadOnlyDictionary<string, CanvasLayoutNodeBox> stepBoxes)
    {
        var anchorsByRoleId = new Dictionary<Guid, List<RoleLayoutAnchor>>();
        foreach (var step in editor.Steps)
        {
            var stepNodeId = ProcessCanvasBranching.BuildDefinitionStepNodeId(step);
            if (!stepBoxes.TryGetValue(stepNodeId, out var stepBox))
            {
                continue;
            }

            foreach (var roleId in step.RoleAssignments
                         .Where(assignment => assignment.RoleRequirementId.HasValue)
                         .Select(assignment => assignment.RoleRequirementId!.Value)
                         .Distinct())
            {
                AddRoleAnchor(anchorsByRoleId, roleId, stepBox);
            }

            if (step.DecisionRoleRequirementId.HasValue)
            {
                AddRoleAnchor(anchorsByRoleId, step.DecisionRoleRequirementId.Value, stepBox);
            }
        }

        return anchorsByRoleId;
    }

    private static void AddRoleAnchor(
        IDictionary<Guid, List<RoleLayoutAnchor>> anchorsByRoleId,
        Guid roleId,
        CanvasLayoutNodeBox stepBox)
    {
        if (!anchorsByRoleId.TryGetValue(roleId, out var anchors))
        {
            anchors = [];
            anchorsByRoleId[roleId] = anchors;
        }

        anchors.Add(new RoleLayoutAnchor(stepBox.X, stepBox.Y));
    }

    private static double ResolveRoleSide(
        int index,
        double anchorY,
        RecompositionLayoutProfile profile)
    {
        if (anchorY < -(profile.StepLaneGap / 2d))
        {
            return -1d;
        }

        if (anchorY > profile.StepLaneGap / 2d)
        {
            return 1d;
        }

        return index % 2 == 0
            ? -1d
            : 1d;
    }

    private static double ResolveUnboundRoleY(
        int index,
        int roleCount,
        RecompositionLayoutProfile profile)
    {
        return (index - ((roleCount - 1) / 2d)) * (profile.StepLaneGap * 0.72d);
    }

    private static int ResolveRoleInstanceSiblingIndex(
        IReadOnlyDictionary<string, List<CanvasWorkbenchNode>> roleInstancesByStepToken,
        string stepToken,
        string nodeId)
    {
        if (!roleInstancesByStepToken.TryGetValue(stepToken, out var siblings))
        {
            return 0;
        }

        var index = siblings.FindIndex(node => string.Equals(node.Id, nodeId, StringComparison.Ordinal));
        return index < 0 ? 0 : index;
    }

    private static double ResolveRoleInstanceOffsetY(
        int siblingIndex,
        int siblingCount,
        RecompositionLayoutProfile profile)
    {
        if (siblingCount <= 1)
        {
            return 0d;
        }

        var row = (siblingIndex / 2) + 1;
        var direction = siblingIndex % 2 == 0
            ? -1d
            : 1d;
        return direction * row * profile.RoleLocalOffsetY;
    }

    private static double ResolveBranchRouterY(
        CanvasLayoutNodeBox sourceBox,
        IReadOnlyList<CanvasLayoutNodeBox> dependentBoxes,
        RecompositionLayoutProfile profile)
    {
        if (dependentBoxes.Count == 0)
        {
            return sourceBox.Y;
        }

        var minDependentY = dependentBoxes.Min(item => item.Y);
        if (minDependentY < sourceBox.Y - 1d)
        {
            return minDependentY - profile.BranchSameLaneOffsetY;
        }

        var maxDependentY = dependentBoxes.Max(item => item.Y);
        return maxDependentY > sourceBox.Y + 1d
            ? maxDependentY + profile.BranchSameLaneOffsetY
            : sourceBox.Y + profile.BranchSameLaneOffsetY;
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

    private static bool TryResolveDefinitionStepByToken(
        IReadOnlyList<ProcessStepEditorModel> steps,
        string token,
        out ProcessStepEditorModel step)
    {
        step = default!;
        if (Guid.TryParse(token, out var stepId))
        {
            var matchedById = steps.FirstOrDefault(candidate => candidate.Id == stepId);
            if (matchedById is not null)
            {
                step = matchedById;
                return true;
            }
        }

        var matched = steps.FirstOrDefault(candidate => MatchesNodeToken(token, candidate.Id, candidate.Key, candidate.Title));
        if (matched is null)
        {
            return false;
        }

        step = matched;
        return true;
    }

    private static bool TryResolveDefinitionRoleByToken(
        IReadOnlyList<ProcessRoleEditorModel> roles,
        string token,
        out ProcessRoleEditorModel role)
    {
        role = default!;
        if (Guid.TryParse(token, out var roleId))
        {
            var matchedById = roles.FirstOrDefault(candidate => candidate.Id == roleId);
            if (matchedById is not null)
            {
                role = matchedById;
                return true;
            }
        }

        var matched = roles.FirstOrDefault(candidate => MatchesNodeToken(token, candidate.Id, candidate.Key, candidate.DisplayName));
        if (matched is null)
        {
            return false;
        }

        role = matched;
        return true;
    }

    private static bool MatchesNodeToken(
        string token,
        Guid? id,
        string key,
        string title)
    {
        if (id.HasValue && string.Equals(token, id.Value.ToString("D"), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return string.Equals(token, NormalizeNodeToken(key), StringComparison.OrdinalIgnoreCase) ||
            string.Equals(token, NormalizeNodeToken(title), StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeNodeToken(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToLowerInvariant().Replace(' ', '-');
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

    private static Dictionary<Guid, Guid?> BuildPrimaryOutcomeIdsByStepId(IReadOnlyList<ProcessStepEditorModel> steps)
    {
        return steps
            .Where(step => step.Id.HasValue)
            .ToDictionary(step => step.Id!.Value, ResolvePrimaryOutcomeId);
    }

    private static Guid? ResolvePrimaryOutcomeId(ProcessStepEditorModel step)
    {
        return step.BranchOutcomes.FirstOrDefault(IsPrimaryPathOutcome)?.Id ??
            ProcessCanvasBranching.GetDefaultOutcomeId(step);
    }

    private static bool IsPrimaryPathOutcome(ProcessStepBranchOutcomeEditorModel outcome)
    {
        if (ProcessCanvasBranching.IsDefaultOutcome(outcome))
        {
            return true;
        }

        if (IsFeedbackPathOutcome(outcome.Key, outcome.Title, outcome.Description))
        {
            return false;
        }

        return PrimaryPathOutcomeTokens.Any(token =>
            ContainsOrdinalIgnoreCase(outcome.Key, token) ||
            ContainsOrdinalIgnoreCase(outcome.Title, token) ||
            ContainsOrdinalIgnoreCase(outcome.Description, token));
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
        IReadOnlyDictionary<Guid, int> columnByStepId,
        IReadOnlyDictionary<Guid, Guid?> primaryOutcomeIdsByStepId,
        IDictionary<Guid, Dictionary<string, int>> branchLaneOffsetsByParentId,
        RecompositionLayoutProfile profile,
        ref int rootLaneCursor)
    {
        if (dependencies.Count == 0)
        {
            return ResolveRootLane(rootLaneCursor++);
        }

        var dependency = ResolveLayoutParentDependency(
            step,
            dependencies,
            dependentsByParentId,
            laneByStepId,
            columnByStepId,
            primaryOutcomeIdsByStepId);
        if (dependency is null ||
            !dependency.DependsOnStepId.HasValue ||
            !laneByStepId.TryGetValue(dependency.DependsOnStepId.Value, out var parentLane))
        {
            return ResolveRootLane(rootLaneCursor++);
        }

        if (dependency.DependsOnBranchOutcomeId.HasValue &&
            !IsPrimaryRouteDependency(dependency, primaryOutcomeIdsByStepId))
        {
            var laneKey = dependency.DependsOnBranchOutcomeId.Value.ToString("D");
            return parentLane + ResolveBranchLaneOffset(
                branchLaneOffsetsByParentId,
                dependency.DependsOnStepId.Value,
                laneKey,
                step,
                profile);
        }

        if (dependentsByParentId.TryGetValue(dependency.DependsOnStepId.Value, out var dependents) && dependents.Count > 1 && step.Id.HasValue)
        {
            var primaryChildId = ResolvePrimaryChildId(
                dependents,
                primaryOutcomeIdsByStepId,
                dependency.DependsOnStepId.Value);
            if (primaryChildId != step.Id.Value)
            {
                return parentLane + ResolveBranchLaneOffset(
                    branchLaneOffsetsByParentId,
                    dependency.DependsOnStepId.Value,
                    step.Id.Value.ToString("D"),
                    step,
                    profile);
            }
        }

        return parentLane;
    }

    private static ProcessStepDependencyEditorModel? ResolveLayoutParentDependency(
        ProcessStepEditorModel step,
        IReadOnlyList<ProcessStepDependencyEditorModel> dependencies,
        IReadOnlyDictionary<Guid, List<(Guid ChildId, Guid? BranchOutcomeId)>> dependentsByParentId,
        IReadOnlyDictionary<Guid, int> laneByStepId,
        IReadOnlyDictionary<Guid, int> columnByStepId,
        IReadOnlyDictionary<Guid, Guid?> primaryOutcomeIdsByStepId)
    {
        var candidates = dependencies
            .Where(dependency => dependency.DependsOnStepId.HasValue &&
                laneByStepId.ContainsKey(dependency.DependsOnStepId.Value))
            .ToList();
        if (candidates.Count == 0)
        {
            return null;
        }

        return candidates
            .OrderByDescending(dependency => IsPrimaryContinuationDependency(
                step,
                dependency,
                dependentsByParentId,
                primaryOutcomeIdsByStepId))
            .ThenByDescending(dependency => IsPrimaryRouteDependency(dependency, primaryOutcomeIdsByStepId))
            .ThenByDescending(dependency => columnByStepId.GetValueOrDefault(dependency.DependsOnStepId!.Value, 0))
            .First();
    }

    private static bool IsPrimaryContinuationDependency(
        ProcessStepEditorModel step,
        ProcessStepDependencyEditorModel dependency,
        IReadOnlyDictionary<Guid, List<(Guid ChildId, Guid? BranchOutcomeId)>> dependentsByParentId,
        IReadOnlyDictionary<Guid, Guid?> primaryOutcomeIdsByStepId)
    {
        if (!step.Id.HasValue ||
            !dependency.DependsOnStepId.HasValue ||
            !IsPrimaryRouteDependency(dependency, primaryOutcomeIdsByStepId))
        {
            return false;
        }

        if (!dependentsByParentId.TryGetValue(dependency.DependsOnStepId.Value, out var dependents) ||
            dependents.Count <= 1)
        {
            return true;
        }

        var primaryChildId = ResolvePrimaryChildId(
            dependents,
            primaryOutcomeIdsByStepId,
            dependency.DependsOnStepId.Value);
        return primaryChildId == step.Id.Value;
    }

    private static bool IsPrimaryRouteDependency(
        ProcessStepDependencyEditorModel dependency,
        IReadOnlyDictionary<Guid, Guid?> primaryOutcomeIdsByStepId)
    {
        if (!dependency.DependsOnBranchOutcomeId.HasValue)
        {
            return true;
        }

        return dependency.DependsOnStepId.HasValue &&
            primaryOutcomeIdsByStepId.TryGetValue(dependency.DependsOnStepId.Value, out var primaryOutcomeId) &&
            primaryOutcomeId == dependency.DependsOnBranchOutcomeId.Value;
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
        string laneKey,
        ProcessStepEditorModel step,
        RecompositionLayoutProfile profile)
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

        var offset = ResolveNextBranchLaneOffset(offsets.Count, step, profile);
        offsets[laneKey] = offset;
        return offset;
    }

    private static int ResolveNextBranchLaneOffset(
        int branchIndex,
        ProcessStepEditorModel step,
        RecompositionLayoutProfile profile)
    {
        var magnitude = ((branchIndex / 2) + 1) * profile.BranchLaneStep;
        if (profile.PushFeedbackBranchesBelowMain && IsFeedbackStep(step))
        {
            return (branchIndex + 1) * profile.BranchLaneStep;
        }

        if (profile.PushAlternativeBranchesAboveMain)
        {
            return -((branchIndex + 1) * profile.BranchLaneStep);
        }

        return branchIndex % 2 == 0
            ? -magnitude
            : magnitude;
    }

    private static bool IsFeedbackStep(ProcessStepEditorModel step)
    {
        return IsFeedbackPathOutcome(
            step.Key,
            step.Title,
            step.Subtitle,
            step.Notes,
            step.ExceptionPolicySummary);
    }

    private static bool IsFeedbackPathOutcome(params string[] values)
    {
        return FeedbackPathTokens.Any(token =>
            values.Any(value => ContainsOrdinalIgnoreCase(value, token)));
    }

    private static bool ContainsOrdinalIgnoreCase(string value, string token)
    {
        return !string.IsNullOrWhiteSpace(value) &&
            value.Contains(token, StringComparison.OrdinalIgnoreCase);
    }

    private static Guid ResolvePrimaryChildId(
        IReadOnlyList<(Guid ChildId, Guid? BranchOutcomeId)> dependents,
        IReadOnlyDictionary<Guid, Guid?> primaryOutcomeIdsByStepId,
        Guid parentStepId)
    {
        var primaryOutcomeId = primaryOutcomeIdsByStepId.GetValueOrDefault(parentStepId);
        var primary = dependents.FirstOrDefault(item =>
            !item.BranchOutcomeId.HasValue ||
            item.BranchOutcomeId == primaryOutcomeId);

        return primary.ChildId == Guid.Empty
            ? dependents[0].ChildId
            : primary.ChildId;
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

    private readonly record struct RoleLayoutAnchor(double X, double Y);
}
