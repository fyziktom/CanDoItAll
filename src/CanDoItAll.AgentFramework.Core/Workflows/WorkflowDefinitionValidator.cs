using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public sealed class WorkflowDefinitionValidator : IWorkflowDefinitionValidator
{
    public WorkflowValidationResult Validate(WorkflowDefinition definition, IReadOnlyList<LlmCallComponent> components)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(components);

        var issues = new List<WorkflowValidationIssue>();
        if (string.IsNullOrWhiteSpace(definition.Name))
        {
            issues.Add(new WorkflowValidationIssue(
                WorkflowValidationIssueCode.MissingName,
                "Workflow name is required."));
        }

        if (!Enum.IsDefined(definition.RuntimePolicy.PreferredBackend))
        {
            issues.Add(new WorkflowValidationIssue(
                WorkflowValidationIssueCode.UnsupportedRuntimeBackend,
                $"Workflow runtime backend '{definition.RuntimePolicy.PreferredBackend}' is not supported."));
        }

        var graph = definition.Graph;
        if (graph.Nodes.Count == 0)
        {
            issues.Add(new WorkflowValidationIssue(
                WorkflowValidationIssueCode.EmptyGraph,
                "Workflow graph must contain at least one node."));

            return new WorkflowValidationResult(issues);
        }

        var duplicateNodeIds = graph.Nodes
            .GroupBy(node => node.Id)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        foreach (var nodeId in duplicateNodeIds)
        {
            issues.Add(new WorkflowValidationIssue(
                WorkflowValidationIssueCode.DuplicateNodeId,
                $"Workflow node id '{nodeId}' is duplicated.",
                nodeId));
        }

        var nodeIds = graph.Nodes.Select(node => node.Id).ToHashSet();
        if (!nodeIds.Contains(graph.StartNodeId))
        {
            issues.Add(new WorkflowValidationIssue(
                WorkflowValidationIssueCode.MissingStartNode,
                $"Workflow start node '{graph.StartNodeId}' does not exist.",
                graph.StartNodeId));
        }

        if (!graph.Nodes.Any(node => node.Kind == WorkflowNodeKind.End))
        {
            issues.Add(new WorkflowValidationIssue(
                WorkflowValidationIssueCode.MissingEndNode,
                "Workflow graph must contain at least one end node."));
        }

        var duplicateEdgeIds = graph.Edges
            .GroupBy(edge => edge.Id)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        foreach (var edgeId in duplicateEdgeIds)
        {
            issues.Add(new WorkflowValidationIssue(
                WorkflowValidationIssueCode.DuplicateEdgeId,
                $"Workflow edge id '{edgeId}' is duplicated.",
                EdgeId: edgeId));
        }

        foreach (var edge in graph.Edges)
        {
            if (!nodeIds.Contains(edge.SourceNodeId) || !nodeIds.Contains(edge.TargetNodeId))
            {
                issues.Add(new WorkflowValidationIssue(
                    WorkflowValidationIssueCode.UnknownEdgeEndpoint,
                    $"Workflow edge '{edge.Id}' references an unknown source or target node.",
                    EdgeId: edge.Id));
            }
        }

        var componentIds = components.Select(component => component.Id).ToHashSet();
        var componentsById = components.ToDictionary(component => component.Id);
        foreach (var node in graph.Nodes.Where(node => node.Kind == WorkflowNodeKind.LlmCall))
        {
            if (node.Settings.ComponentId is null || !componentIds.Contains(node.Settings.ComponentId.Value))
            {
                issues.Add(new WorkflowValidationIssue(
                    WorkflowValidationIssueCode.InvalidComponentReference,
                    $"LLM workflow node '{node.Id}' must reference a prepared LLM Call Component.",
                    node.Id));
            }
        }

        AddDisconnectedNodeIssues(graph, issues);
        AddShapeIssues(graph, componentsById, issues);

        foreach (var component in components)
        {
            if (string.IsNullOrWhiteSpace(component.Model))
            {
                issues.Add(new WorkflowValidationIssue(
                    WorkflowValidationIssueCode.InvalidProviderModel,
                    $"LLM Call Component '{component.Id}' must specify a model."));
            }

            if (string.IsNullOrWhiteSpace(component.Instructions))
            {
                issues.Add(new WorkflowValidationIssue(
                    WorkflowValidationIssueCode.InvalidComponentReference,
                    $"LLM Call Component '{component.Id}' must specify instructions."));
            }

            if (component.Modality is WorkflowModality.Audio or WorkflowModality.Image)
            {
                issues.Add(new WorkflowValidationIssue(
                    WorkflowValidationIssueCode.UnsupportedModality,
                    $"LLM Call Component '{component.Id}' uses modality '{component.Modality}', which is not supported by the current workflow runtime."));
            }

            if (component.ModelSettings.Temperature is < 0 or > 2)
            {
                issues.Add(new WorkflowValidationIssue(
                    WorkflowValidationIssueCode.InvalidProviderModel,
                    $"LLM Call Component '{component.Id}' has an invalid temperature setting."));
            }

            if (component.ModelSettings.MaxOutputTokens is <= 0)
            {
                issues.Add(new WorkflowValidationIssue(
                    WorkflowValidationIssueCode.InvalidProviderModel,
                    $"LLM Call Component '{component.Id}' must use a positive max output token setting when a limit is specified."));
            }

            if (component.ModelSettings.RequireJsonOutput &&
                component.ResultShape.Kind != WorkflowValueShapeKind.Json &&
                string.IsNullOrWhiteSpace(component.ModelSettings.ResponseFormatJsonSchema))
            {
                issues.Add(new WorkflowValidationIssue(
                    WorkflowValidationIssueCode.ShapeMismatch,
                    $"LLM Call Component '{component.Id}' requires JSON output but does not define a JSON result shape or schema."));
            }
        }

        return new WorkflowValidationResult(issues);
    }

    private static void AddDisconnectedNodeIssues(
        WorkflowGraph graph,
        List<WorkflowValidationIssue> issues)
    {
        var reachable = new HashSet<WorkflowNodeId>();
        var edgesBySource = graph.Edges
            .GroupBy(edge => edge.SourceNodeId)
            .ToDictionary(group => group.Key, group => group.ToArray());
        var pending = new Queue<WorkflowNodeId>();
        pending.Enqueue(graph.StartNodeId);

        while (pending.TryDequeue(out var current))
        {
            if (!reachable.Add(current))
            {
                continue;
            }

            if (!edgesBySource.TryGetValue(current, out var outgoingEdges))
            {
                continue;
            }

            foreach (var edge in outgoingEdges)
            {
                pending.Enqueue(edge.TargetNodeId);
            }
        }

        foreach (var node in graph.Nodes.Where(node => !reachable.Contains(node.Id)))
        {
            issues.Add(new WorkflowValidationIssue(
                WorkflowValidationIssueCode.DisconnectedNode,
                $"Workflow node '{node.Id}' is not reachable from the start node.",
                node.Id));
        }
    }

    private static void AddShapeIssues(
        WorkflowGraph graph,
        IReadOnlyDictionary<WorkflowComponentId, LlmCallComponent> componentsById,
        List<WorkflowValidationIssue> issues)
    {
        var nodesById = graph.Nodes.ToDictionary(node => node.Id);
        foreach (var edge in graph.Edges)
        {
            if (!nodesById.TryGetValue(edge.SourceNodeId, out var source) ||
                !nodesById.TryGetValue(edge.TargetNodeId, out var target))
            {
                continue;
            }

            var sourceShape = GetOutputShape(source, componentsById);
            var targetShape = GetInputShape(target, componentsById);
            if (sourceShape is null || targetShape is null)
            {
                continue;
            }

            if (sourceShape.Kind != targetShape.Kind && targetShape.Kind != WorkflowValueShapeKind.Object)
            {
                issues.Add(new WorkflowValidationIssue(
                    WorkflowValidationIssueCode.ShapeMismatch,
                    $"Workflow edge '{edge.Id}' connects '{sourceShape.Kind}' output to '{targetShape.Kind}' input.",
                    EdgeId: edge.Id));
            }
        }
    }

    private static WorkflowValueShape? GetInputShape(
        WorkflowNode node,
        IReadOnlyDictionary<WorkflowComponentId, LlmCallComponent> componentsById)
    {
        if (node.Kind == WorkflowNodeKind.LlmCall &&
            node.Settings.ComponentId is { } componentId &&
            componentsById.TryGetValue(componentId, out var component))
        {
            return component.InputShape;
        }

        return node.Settings.InputShape;
    }

    private static WorkflowValueShape? GetOutputShape(
        WorkflowNode node,
        IReadOnlyDictionary<WorkflowComponentId, LlmCallComponent> componentsById)
    {
        if (node.Kind == WorkflowNodeKind.LlmCall &&
            node.Settings.ComponentId is { } componentId &&
            componentsById.TryGetValue(componentId, out var component))
        {
            return component.ResultShape;
        }

        return node.Settings.ResultShape;
    }
}

public sealed class WorkflowRuntimeBackendCatalog : IWorkflowRuntimeBackendCatalog
{
    private static readonly WorkflowRuntimeBackendDescriptor[] Backends =
    [
        new(
            WorkflowRuntimeBackendKind.InProcess,
            "MAF in-process workflow runtime",
            IsDurable: false,
            SupportsStreaming: true,
            SupportsExternalRequests: true,
            SupportsDashboardObservability: false,
            OperationalNotes: "Use for local development, tests, previews, and approved short non-durable runs only."),
        new(
            WorkflowRuntimeBackendKind.DurableTask,
            "MAF DurableTask workflow runtime",
            IsDurable: true,
            SupportsStreaming: true,
            SupportsExternalRequests: true,
            SupportsDashboardObservability: true,
            OperationalNotes: "Preferred for production, long-running, distributed, or restart-resilient workflows."),
        new(
            WorkflowRuntimeBackendKind.AzureFunctions,
            "MAF Azure Functions durable workflow hosting",
            IsDurable: true,
            SupportsStreaming: false,
            SupportsExternalRequests: true,
            SupportsDashboardObservability: true,
            OperationalNotes: "Evaluate for generated HTTP, status/respond, and MCP tool triggers behind product authorization.")
    ];

    public IReadOnlyList<WorkflowRuntimeBackendDescriptor> ListBackends() => Backends;

    public WorkflowRuntimeBackendDescriptor GetRequiredBackend(WorkflowRuntimeBackendKind backend)
    {
        foreach (var descriptor in Backends)
        {
            if (descriptor.Kind == backend)
            {
                return descriptor;
            }
        }

        throw new InvalidOperationException($"Workflow runtime backend '{backend}' is not registered.");
    }
}
