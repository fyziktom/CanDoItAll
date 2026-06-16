using System.Globalization;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Templates;

namespace CanDoItAll.Processes.Application;

public sealed partial class ProcessDefinitionCanvasEditorProjectionService
{
    private ProcessDefinitionCanvasCommandResult ExecuteAddStep(
        ProcessDefinitionCanvasStateKey stateKey,
        ProcessDefinitionCanvasSnapshot baseline,
        ProcessDefinitionCanvasCommand command,
        DateTimeOffset observedAtUtc)
    {
        var action = ResolveToolboxAction(baseline, command.ToolboxActionKey);
        if (action is null)
        {
            return CreateRejectedResult(baseline, command.CommandKind, observedAtUtc, "Canvas step was not added because the toolbox action is unavailable.");
        }

        var anchor = ResolveSelectedNode(baseline, command.SelectedNodeKey) ?? FindLastStepNode(baseline);
        var stepIndex = baseline.Nodes.Count(node => node.Kind == ProcessDefinitionCanvasNodeKind.Step) + 1;
        var stepKey = BuildUniqueNodeKey($"step:{Slugify(action.Label)}", baseline.Nodes);
        var x = anchor is null ? 160 + (stepIndex * 260) : anchor.X + 280;
        var y = anchor?.Y ?? 220;
        var step = CreateNode(
            stepKey,
            ProcessDefinitionCanvasNodeKind.Step,
            action.Label,
            action.Kind == ProcessDefinitionCanvasToolboxActionKind.BranchRouter ? "Decision step" : "Authoring step",
            action.Summary,
            x,
            y,
            StepWidth,
            StepHeight,
            action.Kind == ProcessDefinitionCanvasToolboxActionKind.BranchRouter ? "warning" : "info",
            new ProcessDefinitionStepKey(stepKey.Value.Replace("step:", string.Empty, StringComparison.Ordinal)),
            RoleKey: null,
            ArtifactKey: null,
            action.Kind == ProcessDefinitionCanvasToolboxActionKind.BranchRouter ? ["Decision"] : ["Step"]);

        var nodes = new List<ProcessDefinitionCanvasEditorNodeProjection>(baseline.Nodes.Count + 2);
        nodes.AddRange(baseline.Nodes);
        nodes.Add(step);
        var edges = new List<ProcessDefinitionCanvasEdgeProjection>(baseline.Edges.Count + 2);
        edges.AddRange(baseline.Edges);
        if (anchor is not null)
        {
            edges.Add(CreateEdge(
                BuildUniqueEdgeKey($"dependency:{anchor.NodeKey.Value}:{step.NodeKey.Value}", edges),
                ProcessDefinitionCanvasEdgeKind.Dependency,
                anchor.NodeKey,
                step.NodeKey,
                "next",
                $"Dependency from {anchor.Title} to {step.Title}.",
                "info",
                IsBackwardRoute: false));
        }

        if (action.Kind == ProcessDefinitionCanvasToolboxActionKind.BranchRouter)
        {
            var branch = CreateBranchNodeForStep(step, nodes);
            nodes.Add(branch);
            edges.Add(CreateEdge(
                BuildUniqueEdgeKey($"branch-route:{step.NodeKey.Value}:{branch.NodeKey.Value}", edges),
                ProcessDefinitionCanvasEdgeKind.BranchRoute,
                step.NodeKey,
                branch.NodeKey,
                "routes",
                $"Routes from {step.Title} through a typed branch router.",
                "warning",
                IsBackwardRoute: false));
        }

        return StoreAccepted(
            stateKey,
            baseline,
            command.CommandKind,
            observedAtUtc,
            nodes,
            edges,
            CreateSelection(step),
            $"Canvas step '{step.Title}' added.");
    }

    private ProcessDefinitionCanvasCommandResult ExecuteAddBranchRouter(
        ProcessDefinitionCanvasStateKey stateKey,
        ProcessDefinitionCanvasSnapshot baseline,
        ProcessDefinitionCanvasCommand command,
        DateTimeOffset observedAtUtc)
    {
        var step = ResolveSelectedNode(baseline, command.SelectedNodeKey) ?? FindLastStepNode(baseline);
        if (step is null || step.Kind != ProcessDefinitionCanvasNodeKind.Step)
        {
            return CreateRejectedResult(baseline, command.CommandKind, observedAtUtc, "Select a step before adding a branch router.");
        }

        if (baseline.Nodes.Any(node =>
            node.Kind == ProcessDefinitionCanvasNodeKind.BranchRouter &&
            node.StepKey == step.StepKey))
        {
            return CreateRejectedResult(baseline, command.CommandKind, observedAtUtc, $"Step '{step.Title}' already has a branch router.");
        }

        var nodes = new List<ProcessDefinitionCanvasEditorNodeProjection>(baseline.Nodes.Count + 1);
        nodes.AddRange(baseline.Nodes);
        var branch = CreateBranchNodeForStep(step, nodes);
        nodes.Add(branch);
        var edges = new List<ProcessDefinitionCanvasEdgeProjection>(baseline.Edges.Count + 1);
        edges.AddRange(baseline.Edges);
        edges.Add(CreateEdge(
            BuildUniqueEdgeKey($"branch-route:{step.NodeKey.Value}:{branch.NodeKey.Value}", edges),
            ProcessDefinitionCanvasEdgeKind.BranchRoute,
            step.NodeKey,
            branch.NodeKey,
            "routes",
            $"Routes from {step.Title} through a typed branch router.",
            "warning",
            IsBackwardRoute: false));

        return StoreAccepted(
            stateKey,
            baseline,
            command.CommandKind,
            observedAtUtc,
            nodes,
            edges,
            CreateSelection(branch),
            $"Branch router added for '{step.Title}'.");
    }

    private ProcessDefinitionCanvasCommandResult ExecuteAddRoleBinding(
        ProcessDefinitionCanvasStateKey stateKey,
        ProcessDefinitionCanvasSnapshot baseline,
        ProcessDefinitionCanvasCommand command,
        DateTimeOffset observedAtUtc)
    {
        var step = ResolveSelectedNode(baseline, command.SelectedNodeKey) ?? FindLastStepNode(baseline);
        var role = baseline.Nodes.FirstOrDefault(node => node.Kind == ProcessDefinitionCanvasNodeKind.Role);
        if (step is null || step.Kind != ProcessDefinitionCanvasNodeKind.Step)
        {
            return CreateRejectedResult(baseline, command.CommandKind, observedAtUtc, "Select a step before adding a role binding.");
        }

        if (role is null)
        {
            return CreateRejectedResult(baseline, command.CommandKind, observedAtUtc, "No role node is available to bind to the selected step.");
        }

        if (baseline.Edges.Any(edge =>
            edge.Kind == ProcessDefinitionCanvasEdgeKind.RoleBinding &&
            edge.FromNodeKey == role.NodeKey &&
            edge.ToNodeKey == step.NodeKey))
        {
            return CreateRejectedResult(baseline, command.CommandKind, observedAtUtc, $"Role '{role.Title}' is already bound to '{step.Title}'.");
        }

        var edges = new List<ProcessDefinitionCanvasEdgeProjection>(baseline.Edges.Count + 1);
        edges.AddRange(baseline.Edges);
        edges.Add(CreateEdge(
            BuildUniqueEdgeKey($"role-binding:{role.NodeKey.Value}:{step.NodeKey.Value}", edges),
            ProcessDefinitionCanvasEdgeKind.RoleBinding,
            role.NodeKey,
            step.NodeKey,
            "Responsible",
            $"Role {role.Title} is responsible for {step.Title}.",
            "success",
            IsBackwardRoute: false));

        return StoreAccepted(
            stateKey,
            baseline,
            command.CommandKind,
            observedAtUtc,
            baseline.Nodes,
            edges,
            CreateSelection(edges[^1]),
            $"Role binding added between '{role.Title}' and '{step.Title}'.");
    }

    private ProcessDefinitionCanvasCommandResult ExecuteAddArtifactExpectation(
        ProcessDefinitionCanvasStateKey stateKey,
        ProcessDefinitionCanvasSnapshot baseline,
        ProcessDefinitionCanvasCommand command,
        DateTimeOffset observedAtUtc)
    {
        var step = ResolveSelectedNode(baseline, command.SelectedNodeKey) ?? FindLastStepNode(baseline);
        if (step is null || step.Kind != ProcessDefinitionCanvasNodeKind.Step)
        {
            return CreateRejectedResult(baseline, command.CommandKind, observedAtUtc, "Select a step before adding an artifact expectation.");
        }

        var artifactIndex = baseline.Nodes.Count(node => node.Kind == ProcessDefinitionCanvasNodeKind.Artifact) + 1;
        var artifactKey = BuildUniqueNodeKey($"artifact:{step.StepKey?.Value}:artifact-{artifactIndex}", baseline.Nodes);
        var artifact = CreateNode(
            artifactKey,
            ProcessDefinitionCanvasNodeKind.Artifact,
            $"Artifact {artifactIndex.ToString(CultureInfo.InvariantCulture)}",
            "Required evidence",
            $"Artifact expectation attached to {step.Title}.",
            step.X,
            step.Y + 150,
            ArtifactWidth,
            ArtifactHeight,
            "accent",
            step.StepKey,
            RoleKey: null,
            ArtifactKey: artifactKey.Value,
            ["Artifact", "Required"]);
        var nodes = new List<ProcessDefinitionCanvasEditorNodeProjection>(baseline.Nodes.Count + 1);
        nodes.AddRange(baseline.Nodes);
        nodes.Add(artifact);
        var edges = new List<ProcessDefinitionCanvasEdgeProjection>(baseline.Edges.Count + 1);
        edges.AddRange(baseline.Edges);
        edges.Add(CreateEdge(
            BuildUniqueEdgeKey($"artifact:{step.NodeKey.Value}:{artifact.NodeKey.Value}", edges),
            ProcessDefinitionCanvasEdgeKind.ArtifactExpectation,
            step.NodeKey,
            artifact.NodeKey,
            "evidence",
            $"Artifact expectation for {step.Title}.",
            "accent",
            IsBackwardRoute: false));

        return StoreAccepted(
            stateKey,
            baseline,
            command.CommandKind,
            observedAtUtc,
            nodes,
            edges,
            CreateSelection(artifact),
            $"Artifact expectation added for '{step.Title}'.");
    }

    private ProcessDefinitionCanvasCommandResult ExecuteAddSubprocessBoundary(
        ProcessDefinitionCanvasStateKey stateKey,
        ProcessDefinitionCanvasSnapshot baseline,
        ProcessDefinitionCanvasCommand command,
        DateTimeOffset observedAtUtc)
    {
        var step = ResolveSelectedNode(baseline, command.SelectedNodeKey) ?? FindLastStepNode(baseline);
        if (step is null || step.Kind != ProcessDefinitionCanvasNodeKind.Step)
        {
            return CreateRejectedResult(baseline, command.CommandKind, observedAtUtc, "Select a step before adding a subprocess boundary.");
        }

        if (baseline.Nodes.Any(node =>
            node.Kind == ProcessDefinitionCanvasNodeKind.SubprocessBoundary &&
            node.StepKey == step.StepKey))
        {
            return CreateRejectedResult(baseline, command.CommandKind, observedAtUtc, $"Step '{step.Title}' already has a subprocess boundary.");
        }

        var subprocess = CreateNode(
            BuildUniqueNodeKey($"subprocess:{step.StepKey?.Value}", baseline.Nodes),
            ProcessDefinitionCanvasNodeKind.SubprocessBoundary,
            $"{step.Title} subprocess",
            "Subprocess boundary",
            "Observed child process boundary attached to the selected step.",
            step.X,
            step.Y + 170,
            SubprocessWidth,
            SubprocessHeight,
            "info",
            step.StepKey,
            RoleKey: null,
            ArtifactKey: null,
            ["Subprocess"]);
        var nodes = new List<ProcessDefinitionCanvasEditorNodeProjection>(baseline.Nodes.Count + 1);
        nodes.AddRange(baseline.Nodes);
        nodes.Add(subprocess);
        var edges = new List<ProcessDefinitionCanvasEdgeProjection>(baseline.Edges.Count + 1);
        edges.AddRange(baseline.Edges);
        edges.Add(CreateEdge(
            BuildUniqueEdgeKey($"subprocess:{step.NodeKey.Value}:{subprocess.NodeKey.Value}", edges),
            ProcessDefinitionCanvasEdgeKind.SubprocessBoundary,
            step.NodeKey,
            subprocess.NodeKey,
            "child run",
            $"Subprocess boundary attached to {step.Title}.",
            "info",
            IsBackwardRoute: false));

        return StoreAccepted(
            stateKey,
            baseline,
            command.CommandKind,
            observedAtUtc,
            nodes,
            edges,
            CreateSelection(subprocess),
            $"Subprocess boundary added for '{step.Title}'.");
    }

    private ProcessDefinitionCanvasCommandResult ExecuteCloneArtifactReference(
        ProcessDefinitionCanvasStateKey stateKey,
        ProcessDefinitionCanvasSnapshot baseline,
        ProcessDefinitionCanvasCommand command,
        DateTimeOffset observedAtUtc)
    {
        var artifact = ResolveSelectedNode(baseline, command.SelectedNodeKey);
        if (artifact is null ||
            artifact.Kind != ProcessDefinitionCanvasNodeKind.Artifact ||
            string.IsNullOrWhiteSpace(artifact.ArtifactKey))
        {
            return CreateRejectedResult(baseline, command.CommandKind, observedAtUtc, "Select an artifact reference before cloning it.");
        }

        var referenceIndex = baseline.Nodes.Count(node =>
            node.Kind == ProcessDefinitionCanvasNodeKind.Artifact &&
            string.Equals(node.ArtifactKey, artifact.ArtifactKey, StringComparison.OrdinalIgnoreCase)) + 1;
        var clone = CreateNode(
            BuildUniqueNodeKey($"artifact-ref:{Slugify(artifact.ArtifactKey)}", baseline.Nodes),
            ProcessDefinitionCanvasNodeKind.Artifact,
            artifact.Title,
            "Artifact reference",
            $"Reference clone for the shared artifact key '{artifact.ArtifactKey}'. Place it near another step without duplicating the artifact.",
            artifact.X + 230,
            artifact.Y + 96 + (((referenceIndex - 2) % 4) * 28),
            artifact.Width,
            artifact.Height,
            artifact.Tone,
            StepKey: null,
            RoleKey: null,
            artifact.ArtifactKey,
            BuildArtifactReferenceBadges(artifact.Badges));
        var nodes = new List<ProcessDefinitionCanvasEditorNodeProjection>(baseline.Nodes.Count + 1);
        nodes.AddRange(baseline.Nodes);
        nodes.Add(clone);

        return StoreAccepted(
            stateKey,
            baseline,
            command.CommandKind,
            observedAtUtc,
            nodes,
            baseline.Edges,
            CreateSelection(clone),
            $"Artifact reference cloned for '{artifact.Title}' using shared key '{artifact.ArtifactKey}'.");
    }

    private ProcessDefinitionCanvasCommandResult ExecuteRecompose(
        ProcessDefinitionCanvasStateKey stateKey,
        ProcessDefinitionCanvasSnapshot baseline,
        ProcessDefinitionCanvasCommand command,
        DateTimeOffset observedAtUtc)
    {
        var nodes = RecomposeNodes(baseline.Nodes, baseline.Edges);
        var selectedNode = baseline.Selection.NodeKey is { } nodeKey
            ? nodes.FirstOrDefault(node => node.NodeKey == nodeKey)
            : null;
        var selection = selectedNode is null
            ? baseline.Selection
            : CreateSelection(selectedNode);

        return StoreAccepted(
            stateKey,
            baseline,
            command.CommandKind,
            observedAtUtc,
            nodes,
            baseline.Edges,
            selection,
            $"Canvas recomposed with {command.RecompositionMode} layout constraints.");
    }

    private ProcessDefinitionCanvasCommandResult StoreAccepted(
        ProcessDefinitionCanvasStateKey stateKey,
        ProcessDefinitionCanvasSnapshot baseline,
        ProcessDefinitionCanvasCommandKind commandKind,
        DateTimeOffset observedAtUtc,
        IReadOnlyList<ProcessDefinitionCanvasEditorNodeProjection> nodes,
        IReadOnlyList<ProcessDefinitionCanvasEdgeProjection> edges,
        ProcessDefinitionCanvasSelectionProjection selection,
        string summary)
    {
        var stored = baseline with
        {
            VersionToken = CreateVersionToken(commandKind),
            Nodes = nodes,
            Edges = edges,
            Selection = selection
        };
        snapshots[stateKey] = stored;
        var receipt = new ProcessDefinitionCanvasCommandReceipt(
            Guid.NewGuid(),
            commandKind,
            ProcessDefinitionCanvasCommandStatus.Accepted,
            stored.VersionToken,
            observedAtUtc,
            summary);
        return new ProcessDefinitionCanvasCommandResult(receipt, CreateProjection(stored, receipt));
    }

    private ProcessDefinitionCanvasCommandResult CreateRejectedResult(
        ProcessDefinitionCanvasSnapshot snapshot,
        ProcessDefinitionCanvasCommandKind commandKind,
        DateTimeOffset observedAtUtc,
        string summary)
    {
        var receipt = new ProcessDefinitionCanvasCommandReceipt(
            Guid.NewGuid(),
            commandKind,
            ProcessDefinitionCanvasCommandStatus.Rejected,
            snapshot.VersionToken,
            observedAtUtc,
            summary);
        return new ProcessDefinitionCanvasCommandResult(receipt, CreateProjection(snapshot, receipt));
    }

    private static IReadOnlyList<string> BuildArtifactReferenceBadges(IReadOnlyList<string> badges)
    {
        var result = badges
            .Where(badge => !string.Equals(badge, "Reference", StringComparison.OrdinalIgnoreCase))
            .Take(3)
            .ToList();
        result.Add("Reference");
        return result;
    }
}
