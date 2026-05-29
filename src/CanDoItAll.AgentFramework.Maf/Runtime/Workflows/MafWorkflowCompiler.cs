using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Agents.AI.Workflows;

namespace CanDoItAll.AgentFramework.Maf;

public sealed record MafWorkflowBuildResult(
    Workflow? Workflow,
    WorkflowCompilationResult Compilation);

public interface IWorkflowMafCompiler
{
    MafWorkflowBuildResult Compile(
        WorkflowDefinition definition,
        IReadOnlyList<LlmCallComponent> components);

    MafWorkflowBuildResult Compile(
        WorkflowDefinition definition,
        IReadOnlyList<LlmCallComponent> components,
        WorkflowPreviewSimulationPlan? previewSimulationPlan);
}

public sealed class MafWorkflowCompiler(
    IWorkflowDefinitionValidator validator,
    IWorkflowExecutorInvoker? executorInvoker = null,
    IWorkflowLlmComponentInvoker? llmComponentInvoker = null,
    IWorkflowRoutingCompiler? routingCompiler = null) : IWorkflowMafCompiler
{
    public MafWorkflowBuildResult Compile(
        WorkflowDefinition definition,
        IReadOnlyList<LlmCallComponent> components)
        => Compile(definition, components, WorkflowPreviewSimulationPlan.Empty);

    public MafWorkflowBuildResult Compile(
        WorkflowDefinition definition,
        IReadOnlyList<LlmCallComponent> components,
        WorkflowPreviewSimulationPlan? previewSimulationPlan)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(components);

        var validation = validator.Validate(definition, components);
        if (!validation.Succeeded)
        {
            return new MafWorkflowBuildResult(
                null,
                WorkflowCompilationResult.Failed(validation, "Workflow definition failed validation."));
        }

        try
        {
            var componentsById = components.ToDictionary(component => component.Id);
            var simulationSteps = (previewSimulationPlan ?? WorkflowPreviewSimulationPlan.Empty).Steps
                .ToDictionary(step => step.NodeId);
            var bindings = definition.Graph.Nodes.ToDictionary(
                node => node.Id,
                node => CreateExecutorBinding(definition, node, componentsById, simulationSteps));
            var start = bindings[definition.Graph.StartNodeId];
            var builder = new WorkflowBuilder(start)
                .WithName(definition.Name)
                .WithDescription(definition.Description);
            var resolvedRoutingCompiler = routingCompiler ?? new BuiltInJsonWorkflowRoutingCompiler();

            AddWorkflowEdges(builder, definition, bindings, resolvedRoutingCompiler);

            var endBindings = definition.Graph.Nodes
                .Where(node => node.Kind == WorkflowNodeKind.End)
                .Select(node => bindings[node.Id])
                .ToArray();
            if (endBindings.Length > 0)
            {
                builder.WithOutputFrom(endBindings);
            }

            var workflow = builder.Build();
            return new MafWorkflowBuildResult(
                workflow,
                new WorkflowCompilationResult(
                    Succeeded: true,
                    RuntimeDefinitionKey: workflow.Name ?? definition.Id.ToString(),
                    Validation: validation,
                    ErrorMessage: string.Empty));
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            return new MafWorkflowBuildResult(
                null,
                WorkflowCompilationResult.Failed(validation, ex.Message));
        }
    }

    private static void AddWorkflowEdges(
        WorkflowBuilder builder,
        WorkflowDefinition definition,
        IReadOnlyDictionary<WorkflowNodeId, ExecutorBinding> bindings,
        IWorkflowRoutingCompiler routingCompiler)
    {
        foreach (var group in definition.Graph.Edges.GroupBy(edge => edge.SourceNodeId))
        {
            var source = bindings[group.Key];
            var edges = group.ToArray();
            var groupedEdgeIds = new HashSet<WorkflowEdgeId>();

            var fanOutEdges = edges
                .Where(edge => edge.Kind == WorkflowEdgeKind.FanOut || edge.Routing.Kind == WorkflowRouteKind.FanOutSelector)
                .ToArray();
            if (fanOutEdges.Length > 0)
            {
                foreach (var edge in fanOutEdges)
                {
                    groupedEdgeIds.Add(edge.Id);
                }

                var compiled = routingCompiler.CompileFanOut(definition, group.Key, fanOutEdges);
                var targets = compiled.OrderedTargetNodeIds
                    .Select(targetNodeId => bindings[targetNodeId])
                    .ToArray();
                var label = ResolveGroupLabel(fanOutEdges);
                if (fanOutEdges.Any(edge => edge.Routing.Kind == WorkflowRouteKind.FanOutSelector))
                {
                    builder.AddFanOutEdge<WorkflowNodeInput>(
                        source,
                        targets,
                        compiled.TargetSelector,
                        label);
                }
                else
                {
                    if (label is null)
                    {
                        builder.AddFanOutEdge(source, targets);
                    }
                    else
                    {
                        builder.AddFanOutEdge(source, targets, label);
                    }
                }
            }

            var switchEdges = edges
                .Where(edge => edge.Routing.Kind is WorkflowRouteKind.SwitchCase or WorkflowRouteKind.SwitchDefault)
                .ToArray();
            if (switchEdges.Length > 0)
            {
                foreach (var edge in switchEdges)
                {
                    groupedEdgeIds.Add(edge.Id);
                }

                builder.AddSwitch(source, switchBuilder =>
                {
                    foreach (var edge in switchEdges.Where(edge => edge.Routing.Kind == WorkflowRouteKind.SwitchCase))
                    {
                        var compiled = routingCompiler.CompilePredicate(definition, edge);
                        switchBuilder.AddCase<WorkflowNodeInput>(
                            compiled.Predicate,
                            [bindings[edge.TargetNodeId]]);
                    }

                    var defaultEdge = switchEdges.SingleOrDefault(edge => edge.Routing.Kind == WorkflowRouteKind.SwitchDefault);
                    if (defaultEdge is not null)
                    {
                        switchBuilder.WithDefault([bindings[defaultEdge.TargetNodeId]]);
                    }
                });
            }

            foreach (var edge in edges.Where(edge => !groupedEdgeIds.Contains(edge.Id)))
            {
                if (edge.Routing.Kind == WorkflowRouteKind.Predicate)
                {
                    var compiled = routingCompiler.CompilePredicate(definition, edge);
                    builder.AddEdge<WorkflowNodeInput>(
                        source,
                        bindings[edge.TargetNodeId],
                        compiled.Predicate,
                        compiled.Label,
                        idempotent: true);
                    continue;
                }

                builder.AddEdge(
                    source,
                    bindings[edge.TargetNodeId],
                    ResolveEdgeLabel(edge),
                    idempotent: true);
            }
        }
    }

    private static string? ResolveGroupLabel(IReadOnlyList<WorkflowEdge> edges)
    {
        var label = edges
            .Select(WorkflowRoutingValidation.GetRouteLabel)
            .FirstOrDefault(label => !string.IsNullOrWhiteSpace(label));

        return string.IsNullOrWhiteSpace(label) ? null : label;
    }

    private static string? ResolveEdgeLabel(WorkflowEdge edge)
    {
        var label = WorkflowRoutingValidation.GetRouteLabel(edge);
        return string.IsNullOrWhiteSpace(label) ? null : label;
    }

    private ExecutorBinding CreateExecutorBinding(
        WorkflowDefinition definition,
        WorkflowNode node,
        IReadOnlyDictionary<WorkflowComponentId, LlmCallComponent> componentsById,
        IReadOnlyDictionary<WorkflowNodeId, WorkflowPreviewSimulationStep> simulationSteps)
    {
        async ValueTask<WorkflowNodeInput> ExecuteAsync(
            WorkflowNodeInput input,
            IWorkflowContext context,
            CancellationToken cancellationToken)
        {
            var progressObserver = WorkflowNodeExecutionProgressScope.Current;
            await RecordProgressAsync(
                progressObserver,
                definition,
                node,
                WorkflowNodeExecutionProgressState.Started,
                cancellationToken);

            try
            {
                var output = await ExecuteNodeCoreAsync(input, cancellationToken);
                await RecordProgressAsync(
                    progressObserver,
                    definition,
                    node,
                    WorkflowNodeExecutionProgressState.Completed,
                    cancellationToken,
                    payloadJson: output.PayloadJson);
                return output;
            }
            catch (WorkflowExternalRequestPendingException)
            {
                throw;
            }
            catch (Exception exception)
            {
                await RecordProgressAsync(
                    progressObserver,
                    definition,
                    node,
                    WorkflowNodeExecutionProgressState.Failed,
                    CancellationToken.None,
                    errorMessage: exception.Message);
                throw;
            }

            async ValueTask<WorkflowNodeInput> ExecuteNodeCoreAsync(
                WorkflowNodeInput nodeInput,
                CancellationToken nodeCancellationToken)
            {
                if (simulationSteps.TryGetValue(node.Id, out var simulationStep))
                {
                    if (node.Settings.ExecutorId != simulationStep.SourceExecutorId)
                    {
                        var actualExecutorId = node.Settings.ExecutorId?.Value ?? "<none>";
                        var requestedExecutorId = simulationStep.SourceExecutorId?.Value ?? "<none>";
                        throw new InvalidOperationException(
                            $"Preview simulation for workflow node '{node.Id}' targets executor '{requestedExecutorId}', but the node uses executor '{actualExecutorId}'.");
                    }

                    return new WorkflowNodeInput(WorkflowPreviewSimulationRenderer.Render(
                        simulationStep,
                        definition,
                        node,
                        nodeInput));
                }

                if (node.Kind == WorkflowNodeKind.LlmCall)
                {
                    if (llmComponentInvoker is null)
                    {
                        throw new InvalidOperationException($"LLM workflow node '{node.Id}' requires a registered LLM component invoker.");
                    }

                    if (node.Settings.ComponentId is not { } componentId ||
                        !componentsById.TryGetValue(componentId, out var component))
                    {
                        throw new InvalidOperationException($"LLM workflow node '{node.Id}' references component '{node.Settings.ComponentId}', but it was not supplied to the compiler.");
                    }

                    var result = await llmComponentInvoker.ExecuteAsync(definition, node, component, nodeInput, nodeCancellationToken);
                    if (result.NodeId != node.Id)
                    {
                        throw new InvalidOperationException($"LLM workflow node '{node.Id}' returned result for node '{result.NodeId}'.");
                    }

                    return new WorkflowNodeInput(result.PayloadJson);
                }

                if (node.Kind == WorkflowNodeKind.Executor || node.Settings.ExecutorId is not null)
                {
                    if (executorInvoker is null)
                    {
                        throw new InvalidOperationException($"Workflow executor node '{node.Id}' requires a registered executor invoker.");
                    }

                    var result = await executorInvoker.ExecuteAsync(definition, node, nodeInput, nodeCancellationToken);
                    return new WorkflowNodeInput(result.PayloadJson);
                }

                if (node.Kind == WorkflowNodeKind.HumanInput)
                {
                    var runId = WorkflowExecutorExecutionAuditScope.CurrentRunId
                        ?? throw new InvalidOperationException($"Human input workflow node '{node.Id}' requires an active workflow run id.");
                    var now = DateTimeOffset.UtcNow;
                    var requestRecord = new WorkflowExternalRequestRecord(
                        WorkflowExternalRequestId.New(),
                        runId,
                        node.Settings.ExternalRequestKind ?? WorkflowExternalRequestKind.HumanInput,
                        node.Id,
                        EventName: node.Id.Value,
                        RequestJson: nodeInput.PayloadJson,
                        ResponseJson: string.Empty,
                        CreatedAtUtc: now,
                        RespondedAtUtc: null);
                    WorkflowExternalRequestCaptureScope.Record(requestRecord);

                    throw new WorkflowExternalRequestPendingException(
                        requestRecord,
                        $"Workflow is waiting for input at node '{node.Id}'.");
                }

                return nodeInput;
            }
        }

        return ((Func<WorkflowNodeInput, IWorkflowContext, CancellationToken, ValueTask<WorkflowNodeInput>>)ExecuteAsync)
            .BindAsExecutor(node.Id.Value, threadsafe: true);
    }

    private static ValueTask RecordProgressAsync(
        IWorkflowNodeExecutionProgressObserver? observer,
        WorkflowDefinition definition,
        WorkflowNode node,
        WorkflowNodeExecutionProgressState state,
        CancellationToken cancellationToken,
        string payloadJson = "",
        string errorMessage = "")
    {
        return observer is null
            ? ValueTask.CompletedTask
            : observer.RecordAsync(
                new WorkflowNodeExecutionProgress(
                    definition.Id,
                    definition.VersionId,
                    WorkflowExecutorExecutionAuditScope.CurrentRunId,
                    node.Id,
                    state,
                    DateTimeOffset.UtcNow)
                {
                    ExecutorId = node.Settings.ExecutorId,
                    PayloadJson = payloadJson,
                    ErrorMessage = errorMessage
                },
                cancellationToken);
    }
}

internal static class MafWorkflowStatusMapper
{
    public static WorkflowRunState MapRunStatus(RunStatus status)
    {
        return status switch
        {
            RunStatus.NotStarted => WorkflowRunState.NotStarted,
            RunStatus.Idle => WorkflowRunState.Idle,
            RunStatus.PendingRequests => WorkflowRunState.WaitingForInput,
            RunStatus.Ended => WorkflowRunState.Completed,
            RunStatus.Running => WorkflowRunState.Running,
            _ => WorkflowRunState.Failed
        };
    }

    public static WorkflowEventKind MapEventKind(WorkflowEvent workflowEvent)
    {
        ArgumentNullException.ThrowIfNull(workflowEvent);

        return workflowEvent switch
        {
            WorkflowStartedEvent => WorkflowEventKind.Started,
            WorkflowOutputEvent => WorkflowEventKind.Output,
            WorkflowWarningEvent => WorkflowEventKind.Warning,
            WorkflowErrorEvent => WorkflowEventKind.Error,
            RequestInfoEvent => WorkflowEventKind.WaitingForInput,
            SuperStepEvent => WorkflowEventKind.SuperStep,
            ExecutorEvent executorEvent => MapExecutorEvent(executorEvent),
            _ => WorkflowEventKind.Unknown
        };
    }

    private static WorkflowEventKind MapExecutorEvent(ExecutorEvent executorEvent)
    {
        var eventName = executorEvent.GetType().Name;
        if (eventName.Contains("Failed", StringComparison.Ordinal))
        {
            return WorkflowEventKind.ExecutorFailed;
        }

        if (eventName.Contains("Completed", StringComparison.Ordinal))
        {
            return WorkflowEventKind.ExecutorCompleted;
        }

        return WorkflowEventKind.ExecutorInvoked;
    }
}
