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
        var roleNodes = new Dictionary<string, ProcessDefinitionCanvasEditorNodeProjection>(StringComparer.OrdinalIgnoreCase);

        foreach (var role in template.RoleAuthoringDefaults.Roles.Select((role, index) => new { Role = role, Index = index }))
        {
            var roleNode = CreateNode(
                new ProcessDefinitionCanvasNodeKey($"role:{role.Role.Key}"),
                ProcessDefinitionCanvasNodeKind.Role,
                role.Role.DisplayName,
                role.Role.PreferredExecutorKind,
                role.Role.Purpose,
                role.Role.CanvasX != 0 || role.Role.CanvasY != 0 ? role.Role.CanvasX : 160 + (role.Index * 230),
                role.Role.CanvasX != 0 || role.Role.CanvasY != 0 ? role.Role.CanvasY : 40,
                RoleWidth,
                RoleHeight,
                "success",
                StepKey: null,
                new ProcessDefinitionRoleKey(role.Role.Key),
                ArtifactKey: null,
                [role.Role.IsRequired ? "Required" : "Optional"]);
            nodes.Add(roleNode);
            roleNodes[role.Role.Key] = roleNode;
        }

        foreach (var step in template.CanvasAuthoringDefaults.Steps.Select((step, index) => new { Step = step, Index = index }))
        {
            var stepNode = CreateNode(
                new ProcessDefinitionCanvasNodeKey($"step:{step.Step.Key}"),
                ProcessDefinitionCanvasNodeKind.Step,
                step.Step.Title,
                string.IsNullOrWhiteSpace(step.Step.Subtitle) ? step.Step.StepKind : step.Step.Subtitle,
                string.IsNullOrWhiteSpace(step.Step.Notes) ? $"{step.Step.StepKind} step." : step.Step.Notes,
                160 + (step.Index * 280),
                220 + ResolveStepLane(step.Step, step.Index),
                StepWidth,
                StepHeight,
                ResolveStepTone(step.Step.StepKind),
                new ProcessDefinitionStepKey(step.Step.Key),
                RoleKey: null,
                ArtifactKey: null,
                [step.Step.StepKind]);
            nodes.Add(stepNode);
            stepNodes[step.Step.Key] = stepNode;
        }

        foreach (var step in template.CanvasAuthoringDefaults.Steps)
        {
            if (!stepNodes.TryGetValue(step.Key, out var stepNode))
            {
                continue;
            }

            if (ShouldCreateBranchRouter(step))
            {
                var branchNode = CreateNode(
                    new ProcessDefinitionCanvasNodeKey($"branch:{step.Key}"),
                    ProcessDefinitionCanvasNodeKind.BranchRouter,
                    $"{step.Title} routes",
                    "Typed branch router",
                    "Branch outcomes are typed route projections; display labels do not decide runtime routing.",
                    stepNode.X + 250,
                    stepNode.Y - 118,
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
                var artifactNode = CreateNode(
                    new ProcessDefinitionCanvasNodeKey($"artifact:{step.Key}:{artifact.Key}"),
                    ProcessDefinitionCanvasNodeKind.Artifact,
                    artifact.Title,
                    artifact.ArtifactKind,
                    $"Required artifact expectation for {step.Title}.",
                    stepNode.X,
                    stepNode.Y + 150,
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
                var subprocessNode = CreateNode(
                    new ProcessDefinitionCanvasNodeKey($"subprocess:{step.Key}"),
                    ProcessDefinitionCanvasNodeKind.SubprocessBoundary,
                    $"{step.Title} child run",
                    "Subprocess boundary",
                    string.IsNullOrWhiteSpace(step.SubprocessProcessKey)
                        ? "Subprocess boundary awaits a child process binding."
                        : $"Subprocess binding: {step.SubprocessProcessKey}.",
                    stepNode.X,
                    stepNode.Y + 236,
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

            foreach (var binding in template.RoleAuthoringDefaults.StepRoleBindings.Where(binding =>
                string.Equals(binding.StepKey, step.Key, StringComparison.OrdinalIgnoreCase)))
            {
                if (!roleNodes.TryGetValue(binding.RoleKey, out var roleNode))
                {
                    continue;
                }

                edges.Add(CreateEdge(
                    new ProcessDefinitionCanvasEdgeKey($"role-binding:{binding.RoleKey}:{step.Key}:{binding.ResponsibilityKind}"),
                    ProcessDefinitionCanvasEdgeKind.RoleBinding,
                    roleNode.NodeKey,
                    stepNode.NodeKey,
                    binding.ResponsibilityKind,
                    $"{binding.RoleDisplayName} is {binding.ResponsibilityKind} for {step.Title}.",
                    "success",
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
                edges.Add(CreateEdge(
                    new ProcessDefinitionCanvasEdgeKey($"dependency:{dependency.DependsOnStepKey}:{step.Key}:{dependency.DependsOnBranchOutcomeKey}"),
                    kind,
                    fromNode.NodeKey,
                    stepNode.NodeKey,
                    string.IsNullOrWhiteSpace(dependency.DependsOnBranchOutcomeKey) ? "next" : dependency.DependsOnBranchOutcomeKey,
                    $"Route from {fromNode.Title} to {stepNode.Title}.",
                    kind == ProcessDefinitionCanvasEdgeKind.BranchRoute ? "warning" : "info",
                    IsBackwardRoute: false));
            }
        }

        var recomposedNodes = RecomposeNodes(nodes, edges);
        var selection = recomposedNodes.FirstOrDefault(node => node.Kind == ProcessDefinitionCanvasNodeKind.Step) is { } firstStep
            ? CreateSelection(firstStep)
            : ProcessDefinitionCanvasSelectionProjection.None;
        return new ProcessDefinitionCanvasSnapshot(
            scope,
            new ProcessDefinitionCatalogItemKey(template.Key),
            new ProcessDefinitionCanvasVersionToken($"template:{template.Key}:canvas:{template.UpdatedAtUtc.UtcTicks}"),
            recomposedNodes,
            edges,
            template.CanvasAuthoringDefaults.ToolboxActions.Select(CreateToolboxAction).ToArray(),
            selection);
    }

    private static IReadOnlyList<ProcessDefinitionCanvasEditorNodeProjection> RecomposeNodes(
        IReadOnlyList<ProcessDefinitionCanvasEditorNodeProjection> nodes,
        IReadOnlyList<ProcessDefinitionCanvasEdgeProjection> edges)
    {
        var recomposed = new List<ProcessDefinitionCanvasEditorNodeProjection>(nodes.Count);
        var stepNodes = nodes
            .Where(node => node.Kind == ProcessDefinitionCanvasNodeKind.Step)
            .OrderBy(node => node.X)
            .ThenBy(node => node.NodeKey.Value, StringComparer.Ordinal)
            .ToArray();
        var roleNodes = nodes
            .Where(node => node.Kind == ProcessDefinitionCanvasNodeKind.Role)
            .OrderBy(node => node.X)
            .ThenBy(node => node.NodeKey.Value, StringComparer.Ordinal)
            .ToArray();

        var positions = new Dictionary<ProcessDefinitionCanvasNodeKey, (double X, double Y)>();
        for (var index = 0; index < roleNodes.Length; index++)
        {
            positions[roleNodes[index].NodeKey] = (160 + (index * 230), 40);
        }

        for (var index = 0; index < stepNodes.Length; index++)
        {
            positions[stepNodes[index].NodeKey] = (160 + (index * 280), 220 + ((index % 3) * 120));
        }

        foreach (var node in nodes)
        {
            if (positions.TryGetValue(node.NodeKey, out var position))
            {
                recomposed.Add(node with { X = position.X, Y = position.Y });
                continue;
            }

            var sourceEdge = edges.FirstOrDefault(edge => edge.ToNodeKey == node.NodeKey);
            if (sourceEdge is not null && positions.TryGetValue(sourceEdge.FromNodeKey, out var sourcePosition))
            {
                var offset = node.Kind switch
                {
                    ProcessDefinitionCanvasNodeKind.BranchRouter => (X: 250d, Y: -112d),
                    ProcessDefinitionCanvasNodeKind.Artifact => (X: 0d, Y: 148d),
                    ProcessDefinitionCanvasNodeKind.SubprocessBoundary => (X: 0d, Y: 232d),
                    _ => (X: 0d, Y: 0d)
                };
                var resolved = (sourcePosition.X + offset.X, sourcePosition.Y + offset.Y);
                positions[node.NodeKey] = resolved;
                recomposed.Add(node with { X = resolved.Item1, Y = resolved.Item2 });
                continue;
            }

            recomposed.Add(node);
        }

        return recomposed;
    }

    private static ProcessDefinitionCanvasViewportProjection CreateViewport(
        IReadOnlyList<ProcessDefinitionCanvasEditorNodeProjection> nodes)
    {
        if (nodes.Count == 0)
        {
            return new ProcessDefinitionCanvasViewportProjection(960, 480, "Empty definition canvas.");
        }

        var minX = nodes.Min(node => node.X);
        var minY = nodes.Min(node => node.Y);
        var maxX = nodes.Max(node => node.X + node.Width);
        var maxY = nodes.Max(node => node.Y + node.Height);
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
        IReadOnlyList<string> badges)
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
            CreatePorts(kind, width, height));

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
        => CreateNode(
            BuildUniqueNodeKey($"branch:{step.StepKey?.Value}", nodes),
            ProcessDefinitionCanvasNodeKind.BranchRouter,
            $"{step.Title} routes",
            "Typed branch router",
            "Branch outcomes route through typed targets, not display text.",
            step.X + 250,
            step.Y - 112,
            BranchWidth,
            BranchHeight,
            "warning",
            step.StepKey,
            RoleKey: null,
            ArtifactKey: null,
            ["Branch"]);
}
