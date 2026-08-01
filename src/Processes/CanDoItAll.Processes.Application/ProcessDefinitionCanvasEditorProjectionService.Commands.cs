using System.Globalization;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Templates;

namespace CanDoItAll.Processes.Application;

public sealed partial class ProcessDefinitionCanvasEditorProjectionService
{
    private ProcessDefinitionCanvasCommandResult ExecuteMoveNodes(
        ProcessDefinitionCanvasStateKey stateKey,
        ProcessDefinitionCanvasSnapshot baseline,
        ProcessDefinitionCanvasCommand command,
        DateTimeOffset observedAtUtc)
    {
        if (command.NodePositions is not { Count: > 0 } positions)
        {
            return CreateRejectedResult(baseline, command.CommandKind, observedAtUtc, "Canvas nodes were not moved because no positions were supplied.");
        }

        if (positions.Any(position =>
            !double.IsFinite(position.X) ||
            !double.IsFinite(position.Y)))
        {
            return CreateRejectedResult(baseline, command.CommandKind, observedAtUtc, "Canvas nodes were not moved because a coordinate is not finite.");
        }

        var positionsByNodeKey = positions
            .GroupBy(position => position.NodeKey)
            .ToDictionary(group => group.Key, group => group.Last());
        var missingNodeKeys = positionsByNodeKey.Keys
            .Where(nodeKey => baseline.Nodes.All(node => node.NodeKey != nodeKey))
            .Select(nodeKey => nodeKey.Value)
            .ToArray();
        if (missingNodeKeys.Length > 0)
        {
            return CreateRejectedResult(
                baseline,
                command.CommandKind,
                observedAtUtc,
                $"Canvas nodes were not moved because these node keys are unavailable: {string.Join(", ", missingNodeKeys)}.");
        }

        var nodes = baseline.Nodes
            .Select(node => positionsByNodeKey.TryGetValue(node.NodeKey, out var position)
                ? node with { X = position.X, Y = position.Y }
                : node)
            .ToArray();
        var selectedNodeKey = command.SelectedNodeKey ?? baseline.Selection.NodeKey;
        var selectedNode = selectedNodeKey is { } key
            ? nodes.FirstOrDefault(node => node.NodeKey == key)
            : null;
        return StoreAccepted(
            stateKey,
            baseline,
            command.CommandKind,
            observedAtUtc,
            nodes,
            baseline.Edges,
            selectedNode is null ? baseline.Selection : CreateSelection(selectedNode),
            $"Saved {positionsByNodeKey.Count.ToString(CultureInfo.InvariantCulture)} canvas node position(s) without recomposition.");
    }

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

        var selectedNode = ResolveSelectedNode(baseline, command.SelectedNodeKey);
        var hasExistingSteps = baseline.Nodes.Any(node => node.Kind == ProcessDefinitionCanvasNodeKind.Step);
        if (selectedNode is not { Kind: ProcessDefinitionCanvasNodeKind.Step or ProcessDefinitionCanvasNodeKind.BranchRouter } &&
            hasExistingSteps)
        {
            return CreateRejectedResult(
                baseline,
                command.CommandKind,
                observedAtUtc,
                "Select the structural parent step or branch router before adding a process step.");
        }

        var anchor = selectedNode;
        if (anchor?.StepKind == ProcessDefinitionStepKind.End)
        {
            return CreateRejectedResult(
                baseline,
                command.CommandKind,
                observedAtUtc,
                $"Canvas step '{anchor.Title}' is an End step and cannot have a forward child.");
        }

        if (action.StepKind == ProcessDefinitionStepKind.Start && hasExistingSteps)
        {
            return CreateRejectedResult(
                baseline,
                command.CommandKind,
                observedAtUtc,
                "A Start step can only be added to a process canvas that has no structural steps.");
        }

        var stepKey = BuildUniqueNodeKey($"step:{Slugify(action.Label)}", baseline.Nodes);
        var position = ProcessDefinitionCanvasPlacementPolicy.PlaceStep(
            baseline.Nodes,
            baseline.Edges,
            anchor,
            StepWidth,
            StepHeight);
        var step = CreateNode(
            stepKey,
            ProcessDefinitionCanvasNodeKind.Step,
            action.Label,
            action.Kind == ProcessDefinitionCanvasToolboxActionKind.BranchRouter ? "Decision step" : "Authoring step",
            action.Summary,
            position.X,
            position.Y,
            StepWidth,
            StepHeight,
            action.Kind == ProcessDefinitionCanvasToolboxActionKind.BranchRouter ? "warning" : "info",
            new ProcessDefinitionStepKey(stepKey.Value.Replace("step:", string.Empty, StringComparison.Ordinal)),
            RoleKey: null,
            ArtifactKey: null,
            action.Kind == ProcessDefinitionCanvasToolboxActionKind.BranchRouter ? ["Decision"] : ["Step"],
            action.StepKind == ProcessDefinitionStepKind.Unspecified
                ? ProcessDefinitionStepKind.Work
                : action.StepKind);

        var nodes = new List<ProcessDefinitionCanvasEditorNodeProjection>(baseline.Nodes.Count + 2);
        nodes.AddRange(baseline.Nodes);
        nodes.Add(step);
        var edges = new List<ProcessDefinitionCanvasEdgeProjection>(baseline.Edges.Count + 2);
        edges.AddRange(baseline.Edges);
        if (anchor is not null)
        {
            var edgeKind = anchor.Kind == ProcessDefinitionCanvasNodeKind.BranchRouter
                ? ProcessDefinitionCanvasEdgeKind.BranchRoute
                : ProcessDefinitionCanvasEdgeKind.Dependency;
            edges.Add(CreateEdge(
                BuildUniqueEdgeKey($"dependency:{anchor.NodeKey.Value}:{step.NodeKey.Value}", edges),
                edgeKind,
                anchor.NodeKey,
                step.NodeKey,
                "next",
                $"Dependency from {anchor.Title} to {step.Title}.",
                edgeKind == ProcessDefinitionCanvasEdgeKind.BranchRoute ? "warning" : "info",
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
        var role = baseline.Nodes.FirstOrDefault(node =>
            node.Kind == ProcessDefinitionCanvasNodeKind.Role &&
            node.RoleKey is not null);
        if (step is null || step.Kind != ProcessDefinitionCanvasNodeKind.Step)
        {
            return CreateRejectedResult(baseline, command.CommandKind, observedAtUtc, "Select a step before adding a role binding.");
        }

        if (role is null)
        {
            return CreateRejectedResult(baseline, command.CommandKind, observedAtUtc, "No role node is available to bind to the selected step.");
        }

        var roleKey = role.RoleKey!.Value;
        var roleNodesByKey = baseline.Nodes
            .Where(node => node.Kind == ProcessDefinitionCanvasNodeKind.Role)
            .ToDictionary(node => node.NodeKey);
        if (baseline.Edges.Any(edge =>
            edge.Kind == ProcessDefinitionCanvasEdgeKind.RoleBinding &&
            edge.ToNodeKey == step.NodeKey &&
            roleNodesByKey.TryGetValue(edge.FromNodeKey, out var source) &&
            source.RoleKey == roleKey))
        {
            return CreateRejectedResult(baseline, command.CommandKind, observedAtUtc, $"Role '{role.Title}' is already bound to '{step.Title}'.");
        }

        var boundRoleNodeKeys = baseline.Edges
            .Where(edge => edge.Kind == ProcessDefinitionCanvasEdgeKind.RoleBinding)
            .Select(edge => edge.FromNodeKey)
            .ToHashSet();
        var reusableRole = baseline.Nodes.FirstOrDefault(node =>
            node.Kind == ProcessDefinitionCanvasNodeKind.Role &&
            node.RoleKey == roleKey &&
            !boundRoleNodeKeys.Contains(node.NodeKey));
        var occupied = baseline.Nodes
            .Where(node => node.NodeKey != reusableRole?.NodeKey)
            .Select(ProcessDefinitionCanvasPlacementPolicy.ResolveBounds)
            .ToList();
        var rolePosition = ProcessDefinitionCanvasPlacementPolicy.PlaceInputAttachment(
            occupied,
            step,
            role.Width,
            role.Height);
        var roleRepresentation = reusableRole is null
            ? CreateNode(
                BuildUniqueNodeKey($"role-ref:{Slugify(roleKey.Value)}:{Slugify(step.StepKey?.Value ?? step.NodeKey.Value)}", baseline.Nodes),
                ProcessDefinitionCanvasNodeKind.Role,
                role.Title,
                "Role reference",
                $"Role representation for {step.Title}; the shared role definition remains '{roleKey.Value}'.",
                rolePosition.X,
                rolePosition.Y,
                role.Width,
                role.Height,
                role.Tone,
                step.StepKey,
                roleKey,
                ArtifactKey: null,
                BuildReferenceBadges(role.Badges))
            : reusableRole with
            {
                StepKey = step.StepKey,
                X = rolePosition.X,
                Y = rolePosition.Y
            };
        IReadOnlyList<ProcessDefinitionCanvasEditorNodeProjection> nodes = reusableRole is null
            ? [.. baseline.Nodes, roleRepresentation]
            : baseline.Nodes
                .Select(node => node.NodeKey == reusableRole.NodeKey ? roleRepresentation : node)
                .ToArray();
        var edges = new List<ProcessDefinitionCanvasEdgeProjection>(baseline.Edges.Count + 1);
        edges.AddRange(baseline.Edges);
        edges.Add(CreateEdge(
            BuildUniqueEdgeKey($"role-binding:{roleRepresentation.NodeKey.Value}:{step.NodeKey.Value}", edges),
            ProcessDefinitionCanvasEdgeKind.RoleBinding,
            roleRepresentation.NodeKey,
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
            nodes,
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
        var artifactPosition = ProcessDefinitionCanvasPlacementPolicy.PlaceAttachment(
            baseline.Nodes,
            step,
            ArtifactWidth,
            ArtifactHeight);
        var artifact = CreateNode(
            artifactKey,
            ProcessDefinitionCanvasNodeKind.Artifact,
            $"Artifact {artifactIndex.ToString(CultureInfo.InvariantCulture)}",
            "Required evidence",
            $"Artifact expectation attached to {step.Title}.",
            artifactPosition.X,
            artifactPosition.Y,
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

        var subprocessPosition = ProcessDefinitionCanvasPlacementPolicy.PlaceAttachment(
            baseline.Nodes,
            step,
            SubprocessWidth,
            SubprocessHeight);
        var subprocess = CreateNode(
            BuildUniqueNodeKey($"subprocess:{step.StepKey?.Value}", baseline.Nodes),
            ProcessDefinitionCanvasNodeKind.SubprocessBoundary,
            $"{step.Title} subprocess",
            "Subprocess boundary",
            "Observed child process boundary attached to the selected step.",
            subprocessPosition.X,
            subprocessPosition.Y,
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

        var clonePosition = ProcessDefinitionCanvasPlacementPolicy.PlaceReference(
            baseline.Nodes,
            artifact,
            artifact.Width,
            artifact.Height);
        var clone = CreateNode(
            BuildUniqueNodeKey($"artifact-ref:{Slugify(artifact.ArtifactKey)}", baseline.Nodes),
            ProcessDefinitionCanvasNodeKind.Artifact,
            artifact.Title,
            "Artifact reference",
            $"Reference clone for the shared artifact key '{artifact.ArtifactKey}'. Place it near another step without duplicating the artifact.",
            clonePosition.X,
            clonePosition.Y,
            artifact.Width,
            artifact.Height,
            artifact.Tone,
            StepKey: null,
            RoleKey: null,
            artifact.ArtifactKey,
            BuildReferenceBadges(artifact.Badges));
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

    private ProcessDefinitionCanvasCommandResult ExecuteCloneRoleReference(
        ProcessDefinitionCanvasStateKey stateKey,
        ProcessDefinitionCanvasSnapshot baseline,
        ProcessDefinitionCanvasCommand command,
        DateTimeOffset observedAtUtc)
    {
        var role = ResolveSelectedNode(baseline, command.SelectedNodeKey);
        if (role is null ||
            role.Kind != ProcessDefinitionCanvasNodeKind.Role ||
            role.RoleKey is null)
        {
            return CreateRejectedResult(baseline, command.CommandKind, observedAtUtc, "Select a role representation before cloning it.");
        }

        var roleKey = role.RoleKey.Value;
        var clonePosition = ProcessDefinitionCanvasPlacementPolicy.PlaceReference(
            baseline.Nodes,
            role,
            role.Width,
            role.Height);
        var clone = CreateNode(
            BuildUniqueNodeKey($"role-ref:{Slugify(roleKey.Value)}", baseline.Nodes),
            ProcessDefinitionCanvasNodeKind.Role,
            role.Title,
            "Role reference",
            $"Reference clone for the shared role key '{roleKey.Value}'. Place it near another consuming step without duplicating the role definition.",
            clonePosition.X,
            clonePosition.Y,
            role.Width,
            role.Height,
            role.Tone,
            StepKey: null,
            roleKey,
            ArtifactKey: null,
            BuildReferenceBadges(role.Badges));
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
            $"Role representation cloned for '{role.Title}' using shared key '{roleKey.Value}'.");
    }

    private ProcessDefinitionCanvasCommandResult ExecuteRecompose(
        ProcessDefinitionCanvasStateKey stateKey,
        ProcessDefinitionCanvasSnapshot baseline,
        ProcessDefinitionCanvasCommand command,
        DateTimeOffset observedAtUtc)
    {
        ProcessDefinitionCanvasLayoutResult layout;
        try
        {
            layout = command.RecompositionMode == ProcessDefinitionCanvasRecompositionMode.PreserveProjection
                ? new ProcessDefinitionCanvasLayoutResult(
                    baseline.Nodes,
                    baseline.Edges,
                    new HashSet<ProcessDefinitionCanvasNodeKey>())
                : ProcessDefinitionCanvasRecompositionEngine.Recompose(baseline.Nodes, baseline.Edges);
        }
        catch (InvalidOperationException exception)
        {
            return CreateRejectedResult(
                baseline,
                command.CommandKind,
                observedAtUtc,
                $"Canvas recomposition was rejected: {exception.Message}");
        }

        var nodes = layout.Nodes;
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
            layout.Edges,
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

    private static IReadOnlyList<string> BuildReferenceBadges(IReadOnlyList<string> badges)
    {
        var result = badges
            .Where(badge => !string.Equals(badge, "Reference", StringComparison.OrdinalIgnoreCase))
            .Take(3)
            .ToList();
        result.Add("Reference");
        return result;
    }
}
