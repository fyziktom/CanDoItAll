using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SharedKernel.Configuration;

namespace CanDoItAll.AgentFramework.Core;

public sealed class WorkflowDefinitionValidator : IWorkflowDefinitionValidator
{
    private static readonly IConfigurationSchemaValidator SettingsSchemaValidator = new ConfigurationSchemaValidator();

    private readonly IWorkflowExecutorCatalog? executorCatalog;
    private readonly WorkflowDefinitionValidationOptions options;

    public WorkflowDefinitionValidator()
        : this(executorCatalog: null, WorkflowDefinitionValidationOptions.Default)
    {
    }

    public WorkflowDefinitionValidator(IWorkflowExecutorCatalog executorCatalog)
        : this(executorCatalog, WorkflowDefinitionValidationOptions.Default)
    {
    }

    public WorkflowDefinitionValidator(
        IWorkflowExecutorCatalog? executorCatalog,
        WorkflowDefinitionValidationOptions options)
    {
        this.executorCatalog = executorCatalog;
        this.options = options;
    }

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

        AddNodeKindIssues(definition, issues);
        AddRoutingIssues(graph, issues);
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

        AddExecutorIssues(graph, issues);
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

    private void AddExecutorIssues(
        WorkflowGraph graph,
        List<WorkflowValidationIssue> issues)
    {
        foreach (var node in graph.Nodes.Where(node => node.Kind == WorkflowNodeKind.Executor || node.Settings.ExecutorId is not null))
        {
            if (node.Settings.ExecutorId is not { } executorId)
            {
                issues.Add(new WorkflowValidationIssue(
                    WorkflowValidationIssueCode.InvalidExecutorReference,
                    $"Workflow executor node '{node.Id}' must specify an executor id.",
                    node.Id));
                continue;
            }

            WorkflowExecutorDescriptor? descriptor = null;
            if (executorCatalog is not null)
            {
                if (!executorCatalog.TryGetExecutor(executorId, out descriptor))
                {
                    issues.Add(new WorkflowValidationIssue(
                        WorkflowValidationIssueCode.InvalidExecutorReference,
                        $"Workflow executor '{executorId}' is not registered.",
                        node.Id));
                }
                else if (options.RequireRunnableExecutors && !descriptor.CanExecute)
                {
                    issues.Add(new WorkflowValidationIssue(
                        WorkflowValidationIssueCode.InvalidExecutorReference,
                        $"Workflow executor '{executorId}' is not runnable: {descriptor.Availability.Message}",
                        node.Id));
                }
            }

            var policy = node.Settings.ExecutionPolicy ?? WorkflowExecutorExecutionPolicy.Default;
            if (!WorkflowExecutorPolicyLimits.IsValid(policy))
            {
                issues.Add(new WorkflowValidationIssue(
                    WorkflowValidationIssueCode.InvalidExecutionPolicy,
                    $"Workflow executor node '{node.Id}' has an invalid timeout or retry policy.",
                    node.Id));
            }
            else if (descriptor is not null &&
                     !WorkflowExecutorSideEffectPolicy.IsRetryPolicySafe(descriptor, policy))
            {
                issues.Add(new WorkflowValidationIssue(
                    WorkflowValidationIssueCode.InvalidExecutionPolicy,
                    WorkflowExecutorSideEffectPolicy.CreateUnsafeRetryPolicyMessage(descriptor, policy, node.Id),
                    node.Id));
            }

            var settingsJson = string.IsNullOrWhiteSpace(node.Settings.ExecutorSettingsJson)
                ? descriptor?.DefaultSettingsJson ?? string.Empty
                : node.Settings.ExecutorSettingsJson;
            if (string.IsNullOrWhiteSpace(settingsJson))
            {
                continue;
            }

            JsonDocument settingsDocument;
            try
            {
                settingsDocument = JsonDocument.Parse(settingsJson);
            }
            catch (JsonException exception)
            {
                issues.Add(new WorkflowValidationIssue(
                    WorkflowValidationIssueCode.InvalidExecutorSettings,
                    $"Workflow executor node '{node.Id}' has invalid settings JSON: {exception.Message}",
                    node.Id));
                continue;
            }

            using (settingsDocument)
            {
                AddExecutorSettingsSchemaIssues(node, descriptor, settingsDocument.RootElement, issues);
            }
        }
    }

    private static void AddNodeKindIssues(
        WorkflowDefinition definition,
        List<WorkflowValidationIssue> issues)
    {
        foreach (var node in definition.Graph.Nodes)
        {
            if (!Enum.IsDefined(node.Kind))
            {
                issues.Add(new WorkflowValidationIssue(
                    WorkflowValidationIssueCode.UnsupportedNodeKind,
                    $"Workflow node '{node.Id}' uses unsupported node kind '{node.Kind}'.",
                    node.Id));
                continue;
            }

            if (definition.Status != WorkflowLifecycleStatus.Active ||
                node.Settings.ExecutorId is not null)
            {
                continue;
            }

            if (node.Kind is WorkflowNodeKind.Artifact or WorkflowNodeKind.AgentStep or WorkflowNodeKind.Subworkflow)
            {
                issues.Add(new WorkflowValidationIssue(
                    WorkflowValidationIssueCode.UnsupportedNodeKind,
                    $"Active workflow node '{node.Id}' uses '{node.Kind}', which is not executable in this runtime. Use an executor-backed node or keep the workflow in draft.",
                    node.Id));
            }
        }
    }

    private static void AddExecutorSettingsSchemaIssues(
        WorkflowNode node,
        WorkflowExecutorDescriptor? descriptor,
        JsonElement settingsRoot,
        List<WorkflowValidationIssue> issues)
    {
        if (descriptor?.ConfigurationSchema.Fields.Count is not > 0)
        {
            return;
        }

        if (settingsRoot.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new WorkflowValidationIssue(
                WorkflowValidationIssueCode.InvalidExecutorSettings,
                $"Workflow executor node '{node.Id}' settings must be a JSON object.",
                node.Id));
            return;
        }

        var state = new ConfigurationState(ReadConfigurationValues(settingsRoot));
        var validation = SettingsSchemaValidator.Validate(descriptor.ConfigurationSchema, state);
        foreach (var issue in validation.Issues)
        {
            issues.Add(new WorkflowValidationIssue(
                WorkflowValidationIssueCode.InvalidExecutorSettings,
                $"Workflow executor node '{node.Id}' has invalid setting '{issue.FieldKey}': {issue.Message}",
                node.Id));
        }
    }

    private static IReadOnlyDictionary<string, string> ReadConfigurationValues(JsonElement settingsRoot)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in settingsRoot.EnumerateObject())
        {
            values[property.Name] = property.Value.ValueKind == JsonValueKind.String
                ? property.Value.GetString() ?? string.Empty
                : property.Value.GetRawText();
        }

        return values;
    }

    private static void AddRoutingIssues(
        WorkflowGraph graph,
        List<WorkflowValidationIssue> issues)
    {
        foreach (var edge in graph.Edges)
        {
            AddEdgeRoutingIssues(edge, issues);
        }

        foreach (var group in graph.Edges.GroupBy(edge => edge.SourceNodeId))
        {
            AddGroupedRoutingIssues(group.Key, group.ToArray(), issues);
        }
    }

    private static void AddEdgeRoutingIssues(
        WorkflowEdge edge,
        List<WorkflowValidationIssue> issues)
    {
        var routing = edge.Routing;
        if (routing is null)
        {
            AddRouteIssue(edge, issues, "Routing metadata cannot be null.");
            return;
        }

        if (!Enum.IsDefined(routing.Kind))
        {
            AddRouteIssue(edge, issues, $"Route kind '{routing.Kind}' is not supported.");
        }

        if (!Enum.IsDefined(routing.Operator))
        {
            AddRouteIssue(edge, issues, $"Route operator '{routing.Operator}' is not supported.");
        }

        if (!Enum.IsDefined(routing.ExpectedValueKind))
        {
            AddRouteIssue(edge, issues, $"Route value kind '{routing.ExpectedValueKind}' is not supported.");
        }

        if (string.IsNullOrWhiteSpace(routing.RoutingLanguage))
        {
            AddRouteIssue(edge, issues, "Routing language is required.");
        }
        else if (string.Equals(routing.RoutingLanguage, WorkflowRoutingLanguages.ArtlV1, StringComparison.Ordinal))
        {
            AddRouteIssue(edge, issues, "Routing language 'artl-v1' is reserved for a later ARTL compiler and is not supported by this runtime.");
        }
        else if (!WorkflowRoutingValidation.IsBuiltInRoute(routing) &&
                 !(routing.Kind == WorkflowRouteKind.Always &&
                   string.Equals(routing.RoutingLanguage, WorkflowRoutingLanguages.LegacyConditionExpression, StringComparison.Ordinal)))
        {
            AddRouteIssue(edge, issues, $"Routing language '{routing.RoutingLanguage}' is not supported.");
        }

        switch (routing.Kind)
        {
            case WorkflowRouteKind.Always:
                AddAlwaysRouteIssues(edge, issues);
                return;
            case WorkflowRouteKind.SwitchDefault:
                AddSwitchDefaultIssues(edge, issues);
                return;
            case WorkflowRouteKind.Predicate:
            case WorkflowRouteKind.SwitchCase:
            case WorkflowRouteKind.FanOutSelector:
                AddPredicateRouteIssues(edge, issues);
                return;
            default:
                return;
        }
    }

    private static void AddAlwaysRouteIssues(
        WorkflowEdge edge,
        List<WorkflowValidationIssue> issues)
    {
        var routing = edge.Routing;
        if (!string.IsNullOrWhiteSpace(routing.JsonPath) ||
            !string.IsNullOrWhiteSpace(routing.ExpectedValueJson) ||
            routing.FanOutTargetIndex.HasValue)
        {
            AddRouteIssue(edge, issues, "Direct routes cannot carry predicate fields or fan-out target indices.");
        }
    }

    private static void AddSwitchDefaultIssues(
        WorkflowEdge edge,
        List<WorkflowValidationIssue> issues)
    {
        var routing = edge.Routing;
        if (!string.IsNullOrWhiteSpace(routing.JsonPath) ||
            !string.IsNullOrWhiteSpace(routing.ExpectedValueJson) ||
            routing.FanOutTargetIndex.HasValue)
        {
            AddRouteIssue(edge, issues, "Switch default routes cannot carry predicate fields or fan-out target indices.");
        }
    }

    private static void AddPredicateRouteIssues(
        WorkflowEdge edge,
        List<WorkflowValidationIssue> issues)
    {
        var routing = edge.Routing;
        if (!WorkflowRoutingValidation.TryParseJsonPath(routing.JsonPath, out _, out var pathError))
        {
            AddRouteIssue(edge, issues, $"Route JSON path is invalid: {pathError}.");
        }

        if (!WorkflowRoutingValidation.TryValidateExpectedValue(routing, out var expectedValueError))
        {
            AddRouteIssue(edge, issues, $"Route expected value is invalid: {expectedValueError}.");
        }

        if ((routing.Operator is
                WorkflowRouteOperator.GreaterThan or
                WorkflowRouteOperator.GreaterThanOrEqual or
                WorkflowRouteOperator.LessThan or
                WorkflowRouteOperator.LessThanOrEqual) &&
            routing.ExpectedValueKind != WorkflowRouteValueKind.Number)
        {
            AddRouteIssue(edge, issues, "Numeric comparison routes must use a numeric expected value.");
        }

        if ((routing.Operator is WorkflowRouteOperator.StartsWith or WorkflowRouteOperator.EndsWith) &&
            routing.ExpectedValueKind != WorkflowRouteValueKind.String)
        {
            AddRouteIssue(edge, issues, "String prefix and suffix routes must use a string expected value.");
        }

        if (routing.Kind == WorkflowRouteKind.FanOutSelector)
        {
            if (routing.FanOutTargetIndex is < 0)
            {
                AddRouteIssue(edge, issues, "Fan-out target index cannot be negative.");
            }
        }
        else if (routing.FanOutTargetIndex.HasValue)
        {
            AddRouteIssue(edge, issues, "Only fan-out selector routes can specify a fan-out target index.");
        }
    }

    private static void AddGroupedRoutingIssues(
        WorkflowNodeId sourceNodeId,
        IReadOnlyList<WorkflowEdge> outgoingEdges,
        List<WorkflowValidationIssue> issues)
    {
        var switchEdges = outgoingEdges
            .Where(edge => edge.Routing.Kind is WorkflowRouteKind.SwitchCase or WorkflowRouteKind.SwitchDefault)
            .ToArray();
        if (switchEdges.Length > 0)
        {
            var nonSwitchEdges = outgoingEdges
                .Where(edge => edge.Routing.Kind is not (WorkflowRouteKind.SwitchCase or WorkflowRouteKind.SwitchDefault))
                .ToArray();
            foreach (var edge in nonSwitchEdges)
            {
                AddRouteIssue(
                    edge,
                    issues,
                    $"Source node '{sourceNodeId}' cannot mix switch routes with non-switch outgoing routes.");
            }

            var defaultEdges = switchEdges
                .Where(edge => edge.Routing.Kind == WorkflowRouteKind.SwitchDefault)
                .ToArray();
            foreach (var edge in defaultEdges.Skip(1))
            {
                AddRouteIssue(edge, issues, $"Source node '{sourceNodeId}' has more than one switch default route.");
            }
        }

        var fanOutEdges = outgoingEdges
            .Where(edge => edge.Kind == WorkflowEdgeKind.FanOut || edge.Routing.Kind == WorkflowRouteKind.FanOutSelector)
            .ToArray();
        var duplicateFanOutIndices = fanOutEdges
            .Where(edge => edge.Routing.FanOutTargetIndex.HasValue)
            .GroupBy(edge => edge.Routing.FanOutTargetIndex!.Value)
            .Where(group => group.Count() > 1)
            .SelectMany(group => group.Skip(1))
            .ToArray();
        foreach (var edge in duplicateFanOutIndices)
        {
            AddRouteIssue(edge, issues, $"Source node '{sourceNodeId}' has a duplicate fan-out target index '{edge.Routing.FanOutTargetIndex}'.");
        }

        foreach (var edge in fanOutEdges.Where(edge => edge.Routing.FanOutTargetIndex >= fanOutEdges.Length))
        {
            AddRouteIssue(
                edge,
                issues,
                $"Fan-out target index '{edge.Routing.FanOutTargetIndex}' is outside the target range 0-{fanOutEdges.Length - 1}.");
        }
    }

    private static void AddRouteIssue(
        WorkflowEdge edge,
        List<WorkflowValidationIssue> issues,
        string message)
    {
        issues.Add(new WorkflowValidationIssue(
            WorkflowValidationIssueCode.InvalidRouteDefinition,
            $"Workflow edge '{edge.Id}' route is invalid: {message}",
            EdgeId: edge.Id));
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

public sealed record WorkflowDefinitionValidationOptions(bool RequireRunnableExecutors)
{
    public static WorkflowDefinitionValidationOptions Default { get; } = new(RequireRunnableExecutors: true);

    public static WorkflowDefinitionValidationOptions RegisteredExecutorsOnly { get; } = new(RequireRunnableExecutors: false);
}

public sealed class WorkflowRuntimeBackendCatalog : IWorkflowRuntimeBackendCatalog
{
    private static readonly WorkflowRuntimeBackendDescriptor[] BackendDefinitions =
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

    private readonly IReadOnlyList<WorkflowRuntimeBackendDescriptor> backends;

    public WorkflowRuntimeBackendCatalog()
        : this([WorkflowRuntimeBackendKind.InProcess])
    {
    }

    public WorkflowRuntimeBackendCatalog(IEnumerable<WorkflowRuntimeBackendKind> registeredBackends)
    {
        ArgumentNullException.ThrowIfNull(registeredBackends);

        var registeredBackendSet = registeredBackends.ToHashSet();
        backends = BackendDefinitions
            .Select(descriptor => registeredBackendSet.Contains(descriptor.Kind)
                ? MarkRegistered(descriptor)
                : MarkPlanned(descriptor))
            .ToArray();
    }

    public IReadOnlyList<WorkflowRuntimeBackendDescriptor> ListBackends() => backends;

    public WorkflowRuntimeBackendDescriptor GetRequiredBackend(WorkflowRuntimeBackendKind backend)
    {
        foreach (var descriptor in backends)
        {
            if (descriptor.Kind == backend)
            {
                return descriptor;
            }
        }

        throw new InvalidOperationException($"Workflow runtime backend '{backend}' is not recognized by this host.");
    }

    private static WorkflowRuntimeBackendDescriptor MarkRegistered(WorkflowRuntimeBackendDescriptor descriptor)
        => descriptor with
        {
            Availability = WorkflowRuntimeBackendAvailabilityKind.Registered,
            IsRegistered = true,
            IsRunnable = true,
            AvailabilityReason = "Runtime backend is registered and runnable in this host."
        };

    private static WorkflowRuntimeBackendDescriptor MarkPlanned(WorkflowRuntimeBackendDescriptor descriptor)
        => descriptor with
        {
            Availability = WorkflowRuntimeBackendAvailabilityKind.Planned,
            IsRegistered = false,
            IsRunnable = false,
            AvailabilityReason = $"Runtime backend '{descriptor.Kind}' is planned but not registered in this host."
        };
}

public static class WorkflowRuntimePolicyValidator
{
    public static IReadOnlyList<WorkflowValidationIssue> ValidateRegisteredBackendAvailability(WorkflowRuntimePolicy policy)
        => ValidateRegisteredBackendAvailability(policy, new WorkflowRuntimeBackendCatalog());

    public static IReadOnlyList<WorkflowValidationIssue> ValidateRegisteredBackendAvailability(
        WorkflowRuntimePolicy policy,
        IWorkflowRuntimeBackendCatalog backendCatalog)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(backendCatalog);

        var issues = new List<WorkflowValidationIssue>();
        if (!Enum.IsDefined(policy.PreferredBackend))
        {
            return issues;
        }

        var preferredBackend = backendCatalog.GetRequiredBackend(policy.PreferredBackend);
        if (!preferredBackend.IsRunnable)
        {
            issues.Add(new WorkflowValidationIssue(
                WorkflowValidationIssueCode.UnsupportedRuntimeBackend,
                $"Workflow runtime backend '{preferredBackend.Kind}' is not registered in this host. {preferredBackend.AvailabilityReason}"));
        }

        if ((policy.ExposeAzureFunctionsStatusEndpoint || policy.ExposeAzureFunctionsMcpTool) &&
            !backendCatalog.GetRequiredBackend(WorkflowRuntimeBackendKind.AzureFunctions).IsRunnable)
        {
            issues.Add(new WorkflowValidationIssue(
                WorkflowValidationIssueCode.InvalidWorkflowSettings,
                "Azure Functions workflow endpoints require a registered AzureFunctions backend."));
        }

        return issues;
    }
}
