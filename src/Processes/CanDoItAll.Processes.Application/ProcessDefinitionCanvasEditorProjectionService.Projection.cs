using System.Globalization;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Templates;

namespace CanDoItAll.Processes.Application;

public sealed partial class ProcessDefinitionCanvasEditorProjectionService
{
    private ProcessDefinitionCanvasEditorProjection CreateProjection(
        ProcessDefinitionCanvasSnapshot snapshot,
        ProcessDefinitionCanvasCommandReceipt? lastReceipt)
        => new(
            snapshot.DefinitionKey,
            snapshot.VersionToken,
            CreateViewport(snapshot.Nodes),
            snapshot.Nodes,
            snapshot.Edges,
            snapshot.ToolboxActions,
            snapshot.Selection,
            CreateCommands(snapshot),
            lastReceipt);

    private ProcessDefinitionCanvasSnapshot CreateTemplateSnapshot(
        ProcessWorkspaceShellScope scope,
        ProcessTemplateDefinitionSummary template)
    {
        var nodes = new List<ProcessDefinitionCanvasEditorNodeProjection>();
        var edges = new List<ProcessDefinitionCanvasEdgeProjection>();
        var stepNodes = new Dictionary<string, ProcessDefinitionCanvasEditorNodeProjection>(StringComparer.OrdinalIgnoreCase);

        foreach (var step in template.CanvasAuthoringDefaults.Steps.Select((step, index) => new { Step = step, Index = index }))
        {
            var hasSavedPosition = step.Step.CanvasX != 0 || step.Step.CanvasY != 0;
            var stepNode = CreateNode(
                new ProcessDefinitionCanvasNodeKey($"step:{step.Step.Key}"),
                ProcessDefinitionCanvasNodeKind.Step,
                step.Step.Title,
                string.IsNullOrWhiteSpace(step.Step.Subtitle) ? step.Step.StepKind : step.Step.Subtitle,
                string.IsNullOrWhiteSpace(step.Step.Notes) ? $"{step.Step.StepKind} step." : step.Step.Notes,
                hasSavedPosition ? step.Step.CanvasX : 240 + (step.Index * 480),
                hasSavedPosition ? step.Step.CanvasY : 420 + ResolveStepLane(step.Step, step.Index),
                StepWidth,
                StepHeight,
                ResolveStepTone(step.Step.StepKind),
                new ProcessDefinitionStepKey(step.Step.Key),
                RoleKey: null,
                ArtifactKey: null,
                [step.Step.StepKind],
                ResolveStepKind(step.Step.StepKind));
            nodes.Add(stepNode);
            stepNodes[step.Step.Key] = stepNode;
        }

        foreach (var roleItem in template.RoleAuthoringDefaults.Roles.Select((role, index) => new { Role = role, Index = index }))
        {
            var role = roleItem.Role;
            var bindingGroups = template.RoleAuthoringDefaults.StepRoleBindings
                .Where(binding =>
                    string.Equals(binding.RoleKey, role.Key, StringComparison.OrdinalIgnoreCase) &&
                    stepNodes.ContainsKey(binding.StepKey))
                .GroupBy(binding => binding.StepKey, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (bindingGroups.Length == 0)
            {
                nodes.Add(CreateRoleRepresentation(
                    new ProcessDefinitionCanvasNodeKey($"role:{role.Key}"),
                    role,
                    stepKey: null,
                    role.CanvasX != 0 || role.CanvasY != 0 ? role.CanvasX : 160 + (roleItem.Index * 230),
                    role.CanvasX != 0 || role.CanvasY != 0 ? role.CanvasY : 40,
                    isReference: false));
                continue;
            }

            for (var representationIndex = 0; representationIndex < bindingGroups.Length; representationIndex++)
            {
                var bindingGroup = bindingGroups[representationIndex];
                var stepNode = stepNodes[bindingGroup.Key];
                var hasAuthoredPosition = representationIndex == 0 && (role.CanvasX != 0 || role.CanvasY != 0);
                var position = hasAuthoredPosition
                    ? (X: role.CanvasX, Y: role.CanvasY)
                    : ProcessDefinitionCanvasPlacementPolicy.PlaceInputAttachment(
                        nodes.Select(ProcessDefinitionCanvasPlacementPolicy.ResolveBounds).ToList(),
                        stepNode,
                        RoleWidth,
                        RoleHeight);
                var nodeKey = representationIndex == 0
                    ? new ProcessDefinitionCanvasNodeKey($"role:{role.Key}")
                    : BuildUniqueNodeKey($"role-ref:{Slugify(role.Key)}:{Slugify(bindingGroup.Key)}", nodes);
                var roleNode = CreateRoleRepresentation(
                    nodeKey,
                    role,
                    stepNode.StepKey,
                    position.X,
                    position.Y,
                    isReference: representationIndex > 0);
                nodes.Add(roleNode);

                foreach (var binding in bindingGroup)
                {
                    edges.Add(CreateEdge(
                        BuildUniqueEdgeKey($"role-binding:{binding.RoleKey}:{binding.StepKey}:{binding.ResponsibilityKind}", edges),
                        ProcessDefinitionCanvasEdgeKind.RoleBinding,
                        roleNode.NodeKey,
                        stepNode.NodeKey,
                        binding.ResponsibilityKind,
                        $"{binding.RoleDisplayName} is {binding.ResponsibilityKind} for {stepNode.Title}.",
                        "success",
                        IsBackwardRoute: false));
                }
            }
        }

        foreach (var step in template.CanvasAuthoringDefaults.Steps)
        {
            if (!stepNodes.TryGetValue(step.Key, out var stepNode))
            {
                continue;
            }

            if (ShouldCreateBranchRouter(step))
            {
                var hasSavedBranchPosition = step.BranchCanvasX != 0 || step.BranchCanvasY != 0;
                var branchPosition = hasSavedBranchPosition
                    ? (X: step.BranchCanvasX, Y: step.BranchCanvasY)
                    : ProcessDefinitionCanvasPlacementPolicy.PlaceBranchRouter(nodes, stepNode, BranchWidth, BranchHeight);
                var branchNode = CreateNode(
                    new ProcessDefinitionCanvasNodeKey($"branch:{step.Key}"),
                    ProcessDefinitionCanvasNodeKind.BranchRouter,
                    $"{step.Title} routes",
                    "Typed branch router",
                    "Branch outcomes are typed route projections; display labels do not decide runtime routing.",
                    branchPosition.X,
                    branchPosition.Y,
                    BranchWidth,
                    BranchHeight,
                    "warning",
                    stepNode.StepKey,
                    RoleKey: null,
                    ArtifactKey: null,
                    [$"{Math.Max(1, step.BranchOutcomes.Count).ToString(CultureInfo.InvariantCulture)} outcome(s)"]);
                nodes.Add(branchNode);
                edges.Add(CreateEdge(
                    new ProcessDefinitionCanvasEdgeKey($"branch-route:{step.Key}:router"),
                    ProcessDefinitionCanvasEdgeKind.BranchRoute,
                    stepNode.NodeKey,
                    branchNode.NodeKey,
                    "routes",
                    $"Typed branch router for {step.Title}.",
                    "warning",
                    IsBackwardRoute: false));
            }

            foreach (var artifact in step.ArtifactExpectations.Where(artifact => artifact.IsRequired).Take(3))
            {
                var artifactPosition = ProcessDefinitionCanvasPlacementPolicy.PlaceAttachment(
                    nodes,
                    stepNode,
                    ArtifactWidth,
                    ArtifactHeight);
                var artifactNode = CreateNode(
                    new ProcessDefinitionCanvasNodeKey($"artifact:{step.Key}:{artifact.Key}"),
                    ProcessDefinitionCanvasNodeKind.Artifact,
                    artifact.Title,
                    artifact.ArtifactKind,
                    $"Required artifact expectation for {step.Title}.",
                    artifactPosition.X,
                    artifactPosition.Y,
                    ArtifactWidth,
                    ArtifactHeight,
                    "accent",
                    stepNode.StepKey,
                    RoleKey: null,
                    artifact.Key,
                    ["Artifact", "Required"]);
                nodes.Add(artifactNode);
                edges.Add(CreateEdge(
                    new ProcessDefinitionCanvasEdgeKey($"artifact:{step.Key}:{artifact.Key}"),
                    ProcessDefinitionCanvasEdgeKind.ArtifactExpectation,
                    stepNode.NodeKey,
                    artifactNode.NodeKey,
                    "evidence",
                    $"Required artifact expectation for {step.Title}.",
                    "accent",
                    IsBackwardRoute: false));
            }

            if (IsSubprocessStep(step))
            {
                var subprocessPosition = ProcessDefinitionCanvasPlacementPolicy.PlaceAttachment(
                    nodes,
                    stepNode,
                    SubprocessWidth,
                    SubprocessHeight);
                var subprocessNode = CreateNode(
                    new ProcessDefinitionCanvasNodeKey($"subprocess:{step.Key}"),
                    ProcessDefinitionCanvasNodeKind.SubprocessBoundary,
                    $"{step.Title} child run",
                    "Subprocess boundary",
                    string.IsNullOrWhiteSpace(step.SubprocessProcessKey)
                        ? "Subprocess boundary awaits a child process binding."
                        : $"Subprocess binding: {step.SubprocessProcessKey}.",
                    subprocessPosition.X,
                    subprocessPosition.Y,
                    SubprocessWidth,
                    SubprocessHeight,
                    "info",
                    stepNode.StepKey,
                    RoleKey: null,
                    ArtifactKey: null,
                    ["Subprocess"]);
                nodes.Add(subprocessNode);
                edges.Add(CreateEdge(
                    new ProcessDefinitionCanvasEdgeKey($"subprocess:{step.Key}"),
                    ProcessDefinitionCanvasEdgeKind.SubprocessBoundary,
                    stepNode.NodeKey,
                    subprocessNode.NodeKey,
                    "child run",
                    $"Subprocess boundary for {step.Title}.",
                    "info",
                    IsBackwardRoute: false));
            }

        }

        foreach (var step in template.CanvasAuthoringDefaults.Steps)
        {
            if (!stepNodes.TryGetValue(step.Key, out var stepNode))
            {
                continue;
            }

            foreach (var dependency in step.Dependencies)
            {
                if (!stepNodes.TryGetValue(dependency.DependsOnStepKey, out var sourceNode))
                {
                    continue;
                }

                var fromNode = string.IsNullOrWhiteSpace(dependency.DependsOnBranchOutcomeKey)
                    ? sourceNode
                    : nodes.FirstOrDefault(node => node.NodeKey.Value == $"branch:{dependency.DependsOnStepKey}") ?? sourceNode;
                var kind = string.IsNullOrWhiteSpace(dependency.DependsOnBranchOutcomeKey)
                    ? ProcessDefinitionCanvasEdgeKind.Dependency
                    : ProcessDefinitionCanvasEdgeKind.BranchRoute;
                var sourceStep = template.CanvasAuthoringDefaults.Steps.FirstOrDefault(candidate =>
                    string.Equals(candidate.Key, dependency.DependsOnStepKey, StringComparison.OrdinalIgnoreCase));
                var branchOutcome = sourceStep?.BranchOutcomes.FirstOrDefault(outcome =>
                    string.Equals(outcome.Key, dependency.DependsOnBranchOutcomeKey, StringComparison.OrdinalIgnoreCase));
                edges.Add(CreateEdge(
                    new ProcessDefinitionCanvasEdgeKey($"dependency:{dependency.DependsOnStepKey}:{step.Key}:{dependency.DependsOnBranchOutcomeKey}"),
                    kind,
                    fromNode.NodeKey,
                    stepNode.NodeKey,
                    string.IsNullOrWhiteSpace(dependency.DependsOnBranchOutcomeKey) ? "next" : dependency.DependsOnBranchOutcomeKey,
                    $"Route from {fromNode.Title} to {stepNode.Title}.",
                    kind == ProcessDefinitionCanvasEdgeKind.BranchRoute ? "warning" : "info",
                    IsBackwardRoute: branchOutcome is not null && IsBackwardBranchOutcome(branchOutcome)));
            }
        }

        var orderedSteps = template.CanvasAuthoringDefaults.Steps;
        for (var sourceIndex = 0; sourceIndex < orderedSteps.Count; sourceIndex++)
        {
            var sourceStep = orderedSteps[sourceIndex];
            if (!stepNodes.TryGetValue(sourceStep.Key, out var sourceNode))
            {
                continue;
            }

            var routeSource = nodes.FirstOrDefault(node => node.NodeKey.Value == $"branch:{sourceStep.Key}") ?? sourceNode;
            foreach (var outcome in sourceStep.BranchOutcomes)
            {
                var targetStepKey = ResolveBranchTargetStepKey(orderedSteps, sourceIndex, outcome);
                if (targetStepKey is null || !stepNodes.TryGetValue(targetStepKey, out var targetNode))
                {
                    continue;
                }

                edges.RemoveAll(edge =>
                    edge.Kind == ProcessDefinitionCanvasEdgeKind.BranchRoute &&
                    edge.FromNodeKey == routeSource.NodeKey &&
                    edge.ToNodeKey != targetNode.NodeKey &&
                    string.Equals(edge.Label, outcome.Key, StringComparison.OrdinalIgnoreCase));
                if (edges.Any(edge =>
                    edge.Kind == ProcessDefinitionCanvasEdgeKind.BranchRoute &&
                    edge.FromNodeKey == routeSource.NodeKey &&
                    edge.ToNodeKey == targetNode.NodeKey &&
                    string.Equals(edge.Label, outcome.Key, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                edges.Add(CreateEdge(
                    new ProcessDefinitionCanvasEdgeKey($"branch-target:{sourceStep.Key}:{targetStepKey}:{outcome.Key}"),
                    ProcessDefinitionCanvasEdgeKind.BranchRoute,
                    routeSource.NodeKey,
                    targetNode.NodeKey,
                    outcome.Key,
                    $"Typed branch route from {sourceStep.Title} to {targetNode.Title}.",
                    "warning",
                    IsBackwardBranchOutcome(outcome)));
            }
        }

        var selection = nodes.FirstOrDefault(node => node.Kind == ProcessDefinitionCanvasNodeKind.Step) is { } firstStep
            ? CreateSelection(firstStep)
            : ProcessDefinitionCanvasSelectionProjection.None;
        return new ProcessDefinitionCanvasSnapshot(
            scope,
            new ProcessDefinitionCatalogItemKey(template.Key),
            new ProcessDefinitionCanvasVersionToken($"template:{template.Key}:canvas:{template.UpdatedAtUtc.UtcTicks}"),
            nodes,
            edges,
            template.CanvasAuthoringDefaults.ToolboxActions.Select(CreateToolboxAction).ToArray(),
            selection);
    }

    private static ProcessDefinitionCanvasViewportProjection CreateViewport(
        IReadOnlyList<ProcessDefinitionCanvasEditorNodeProjection> nodes)
    {
        if (nodes.Count == 0)
        {
            return new ProcessDefinitionCanvasViewportProjection(960, 480, "Empty definition canvas.");
        }

        var minX = nodes.Min(node => node.X - (node.Width / 2d));
        var minY = nodes.Min(node => node.Y - (node.Height / 2d));
        var maxX = nodes.Max(node => node.X + (node.Width / 2d));
        var maxY = nodes.Max(node => node.Y + (node.Height / 2d));
        return new ProcessDefinitionCanvasViewportProjection(
            Math.Max(960, maxX - minX + (Margin * 2)),
            Math.Max(520, maxY - minY + (Margin * 2)),
            $"Canvas bounds cover {nodes.Count.ToString(CultureInfo.InvariantCulture)} node(s) with deterministic spacing.");
    }

    private static ProcessDefinitionCanvasEditorNodeProjection CreateNode(
        ProcessDefinitionCanvasNodeKey nodeKey,
        ProcessDefinitionCanvasNodeKind kind,
        string title,
        string subtitle,
        string summary,
        double x,
        double y,
        double width,
        double height,
        string tone,
        ProcessDefinitionStepKey? StepKey,
        ProcessDefinitionRoleKey? RoleKey,
        string? ArtifactKey,
        IReadOnlyList<string> badges,
        ProcessDefinitionStepKind? stepKind = null)
        => new(
            nodeKey,
            kind,
            title,
            subtitle,
            summary,
            x,
            y,
            width,
            height,
            tone,
            StepKey,
            RoleKey,
            ArtifactKey,
            badges,
            CreatePorts(kind, width, height),
            stepKind);

    private static ProcessDefinitionCanvasEditorNodeProjection CreateRoleRepresentation(
        ProcessDefinitionCanvasNodeKey nodeKey,
        ProcessTemplateDefinitionRoleSummary role,
        ProcessDefinitionStepKey? stepKey,
        double x,
        double y,
        bool isReference)
        => CreateNode(
            nodeKey,
            ProcessDefinitionCanvasNodeKind.Role,
            role.DisplayName,
            role.PreferredExecutorKind,
            isReference
                ? $"Canvas representation of the shared role definition '{role.Key}'."
                : role.Purpose,
            x,
            y,
            RoleWidth,
            RoleHeight,
            "success",
            stepKey,
            new ProcessDefinitionRoleKey(role.Key),
            ArtifactKey: null,
            isReference
                ? BuildReferenceBadges([role.IsRequired ? "Required" : "Optional"])
                : [role.IsRequired ? "Required" : "Optional"]);

    private static IReadOnlyList<ProcessDefinitionCanvasPortProjection> CreatePorts(
        ProcessDefinitionCanvasNodeKind kind,
        double width,
        double height)
        => kind switch
        {
            ProcessDefinitionCanvasNodeKind.Step =>
            [
                new("in", ProcessDefinitionCanvasPortKind.StructuralInput, "Input", 0, height / 2),
                new("out", ProcessDefinitionCanvasPortKind.StructuralOutput, "Output", width, height / 2),
                new("role", ProcessDefinitionCanvasPortKind.RoleBinding, "Role", width / 2, 0),
                new("artifact", ProcessDefinitionCanvasPortKind.ArtifactExpectation, "Artifact", width / 2, height)
            ],
            ProcessDefinitionCanvasNodeKind.BranchRouter =>
            [
                new("in", ProcessDefinitionCanvasPortKind.StructuralInput, "Decision input", 0, height / 2),
                new("out", ProcessDefinitionCanvasPortKind.BranchOutcome, "Outcome", width, height / 2)
            ],
            ProcessDefinitionCanvasNodeKind.Role =>
            [
                new("role-out", ProcessDefinitionCanvasPortKind.RoleBinding, "Responsibility", width, height / 2)
            ],
            ProcessDefinitionCanvasNodeKind.Artifact =>
            [
                new("artifact-in", ProcessDefinitionCanvasPortKind.ArtifactExpectation, "Expectation", 0, height / 2)
            ],
            ProcessDefinitionCanvasNodeKind.SubprocessBoundary =>
            [
                new("subprocess-in", ProcessDefinitionCanvasPortKind.SubprocessBoundary, "Child process", 0, height / 2)
            ],
            _ => []
        };

    private static ProcessDefinitionCanvasEdgeProjection CreateEdge(
        ProcessDefinitionCanvasEdgeKey edgeKey,
        ProcessDefinitionCanvasEdgeKind kind,
        ProcessDefinitionCanvasNodeKey fromNodeKey,
        ProcessDefinitionCanvasNodeKey toNodeKey,
        string label,
        string summary,
        string tone,
        bool IsBackwardRoute)
        => new(edgeKey, kind, fromNodeKey, toNodeKey, label, summary, tone, IsBackwardRoute);

    private static ProcessDefinitionCanvasEditorNodeProjection CreateBranchNodeForStep(
        ProcessDefinitionCanvasEditorNodeProjection step,
        IReadOnlyList<ProcessDefinitionCanvasEditorNodeProjection> nodes)
    {
        var position = ProcessDefinitionCanvasPlacementPolicy.PlaceBranchRouter(
            nodes,
            step,
            BranchWidth,
            BranchHeight);
        return CreateNode(
            BuildUniqueNodeKey($"branch:{step.StepKey?.Value}", nodes),
            ProcessDefinitionCanvasNodeKind.BranchRouter,
            $"{step.Title} routes",
            "Typed branch router",
            "Branch outcomes route through typed targets, not display text.",
            position.X,
            position.Y,
            BranchWidth,
            BranchHeight,
            "warning",
            step.StepKey,
            RoleKey: null,
            ArtifactKey: null,
            ["Branch"]);
    }

    private static bool IsBackwardBranchOutcome(
        ProcessTemplateDefinitionCanvasBranchOutcomeSummary outcome)
        => outcome.IsBackwardRoute ||
           string.Equals(
               outcome.RouteTargetKind,
               nameof(ProcessDefinitionRouteTargetKind.PreviousStep),
               StringComparison.OrdinalIgnoreCase);

    private static string? ResolveBranchTargetStepKey(
        IReadOnlyList<ProcessTemplateDefinitionCanvasStepSummary> steps,
        int sourceIndex,
        ProcessTemplateDefinitionCanvasBranchOutcomeSummary outcome)
    {
        if (!string.IsNullOrWhiteSpace(outcome.RouteTargetStepKey))
        {
            return outcome.RouteTargetStepKey;
        }

        if (!Enum.TryParse<ProcessDefinitionRouteTargetKind>(outcome.RouteTargetKind, ignoreCase: true, out var targetKind))
        {
            return null;
        }

        return targetKind switch
        {
            ProcessDefinitionRouteTargetKind.PreviousStep when sourceIndex > 0 => steps[sourceIndex - 1].Key,
            ProcessDefinitionRouteTargetKind.NextStep when sourceIndex + 1 < steps.Count => steps[sourceIndex + 1].Key,
            _ => null
        };
    }
}
