using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using Microsoft.Agents.AI.Workflows;

namespace CanDoItAll.AgentFramework.Maf;

public sealed record MafWorkflowBuildResult(
    Workflow? Workflow,
    WorkflowCompilationResult Compilation)
{
    public WorkflowTopologyFingerprint? TopologyFingerprint { get; init; }

    public WorkflowCompilerContractVersion? CompilerContractVersion { get; init; }

    public bool HasNativeExternalRequests { get; init; }
}

public interface IWorkflowMafCompiler
{
    MafWorkflowBuildResult Compile(
        WorkflowDefinition definition,
        IReadOnlyList<LlmCallComponent> components);

    MafWorkflowBuildResult Compile(
        WorkflowDefinition definition,
        IReadOnlyList<LlmCallComponent> components,
        WorkflowPreviewSimulationPlan? previewSimulationPlan);

    MafWorkflowBuildResult Compile(
        WorkflowDefinition definition,
        IReadOnlyList<LlmCallComponent> components,
        WorkflowPreviewSimulationPlan? previewSimulationPlan,
        WorkflowExecutorInvocationContext invocationContext);
}

public sealed class MafWorkflowCompiler(
    IWorkflowDefinitionValidator validator,
    IWorkflowExecutorInvoker? executorInvoker = null,
    IWorkflowLlmComponentInvoker? llmComponentInvoker = null,
    IWorkflowRoutingCompiler? routingCompiler = null,
    TimeProvider? timeProvider = null,
    IWorkflowExecutorCatalog? executorCatalog = null) : IWorkflowMafCompiler
{
    public MafWorkflowBuildResult Compile(
        WorkflowDefinition definition,
        IReadOnlyList<LlmCallComponent> components)
        => Compile(definition, components, WorkflowPreviewSimulationPlan.Empty);

    public MafWorkflowBuildResult Compile(
        WorkflowDefinition definition,
        IReadOnlyList<LlmCallComponent> components,
        WorkflowPreviewSimulationPlan? previewSimulationPlan)
        => Compile(
            definition,
            components,
            previewSimulationPlan,
            WorkflowExecutorInvocationContext.Empty);

    public MafWorkflowBuildResult Compile(
        WorkflowDefinition definition,
        IReadOnlyList<LlmCallComponent> components,
        WorkflowPreviewSimulationPlan? previewSimulationPlan,
        WorkflowExecutorInvocationContext invocationContext)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(components);
        ArgumentNullException.ThrowIfNull(invocationContext);

        var validation = validator.Validate(definition, components);
        if (!validation.Succeeded)
        {
            return new MafWorkflowBuildResult(
                null,
                WorkflowCompilationResult.Failed(validation, "Workflow definition failed validation."));
        }

        try
        {
            var bindingCompiler = new MafWorkflowHitlBindingCompiler(
                executorInvoker,
                llmComponentInvoker,
                executorCatalog,
                timeProvider);
            var bindings = bindingCompiler.Compile(
                definition,
                components,
                previewSimulationPlan,
                invocationContext);
            var builder = new WorkflowBuilder(bindings[definition.Graph.StartNodeId].Entry)
                .WithName(definition.Name)
                .WithDescription(definition.Description);
            var resolvedRoutingCompiler = routingCompiler ?? new BuiltInJsonWorkflowRoutingCompiler();

            AddInternalEdges(builder, bindings.Values);
            AddWorkflowEdges(builder, definition, bindings, resolvedRoutingCompiler);

            var endBindings = definition.Graph.Nodes
                .Where(node => node.Kind == WorkflowNodeKind.End)
                .Select(node => bindings[node.Id].Exit)
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
                    ErrorMessage: string.Empty))
            {
                TopologyFingerprint = MafWorkflowTopologyFingerprintFactory.Create(definition, bindings),
                CompilerContractVersion = MafWorkflowTopologyFingerprintFactory.CompilerContractVersion,
                HasNativeExternalRequests = bindings.Values.Any(binding => binding.HasNativeExternalRequest)
            };
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            return new MafWorkflowBuildResult(
                null,
                WorkflowCompilationResult.Failed(validation, ex.Message));
        }
    }

    private static void AddInternalEdges(
        WorkflowBuilder builder,
        IEnumerable<MafCompiledNodeBinding> bindings)
    {
        foreach (var edge in bindings.SelectMany(binding => binding.InternalEdges))
        {
            builder.AddEdge(edge.Source, edge.Target, idempotent: true);
        }
    }

    private static void AddWorkflowEdges(
        WorkflowBuilder builder,
        WorkflowDefinition definition,
        IReadOnlyDictionary<WorkflowNodeId, MafCompiledNodeBinding> bindings,
        IWorkflowRoutingCompiler routingCompiler)
    {
        foreach (var group in definition.Graph.Edges.GroupBy(edge => edge.SourceNodeId))
        {
            var source = bindings[group.Key].Exit;
            var edges = group.ToArray();
            var groupedEdgeIds = new HashSet<WorkflowEdgeId>();

            AddFanOutEdges(builder, definition, bindings, routingCompiler, source, edges, groupedEdgeIds);
            AddSwitchEdges(builder, definition, bindings, routingCompiler, source, edges, groupedEdgeIds);
            AddRemainingEdges(builder, definition, bindings, routingCompiler, source, edges, groupedEdgeIds);
        }
    }

    private static void AddFanOutEdges(
        WorkflowBuilder builder,
        WorkflowDefinition definition,
        IReadOnlyDictionary<WorkflowNodeId, MafCompiledNodeBinding> bindings,
        IWorkflowRoutingCompiler routingCompiler,
        ExecutorBinding source,
        IReadOnlyList<WorkflowEdge> edges,
        ISet<WorkflowEdgeId> groupedEdgeIds)
    {
        var fanOutEdges = edges
            .Where(edge => edge.Kind == WorkflowEdgeKind.FanOut || edge.Routing.Kind == WorkflowRouteKind.FanOutSelector)
            .ToArray();
        if (fanOutEdges.Length == 0)
        {
            return;
        }

        foreach (var edge in fanOutEdges)
        {
            groupedEdgeIds.Add(edge.Id);
        }

        var compiled = routingCompiler.CompileFanOut(definition, fanOutEdges[0].SourceNodeId, fanOutEdges);
        var targets = compiled.OrderedTargetNodeIds
            .Select(targetNodeId => bindings[targetNodeId].Entry)
            .ToArray();
        var label = ResolveGroupLabel(fanOutEdges);
        if (fanOutEdges.Any(edge => edge.Routing.Kind == WorkflowRouteKind.FanOutSelector))
        {
            builder.AddFanOutEdge<WorkflowNodeInput>(source, targets, compiled.TargetSelector, label);
            return;
        }

        if (label is null)
        {
            builder.AddFanOutEdge(source, targets);
            return;
        }

        builder.AddFanOutEdge(source, targets, label);
    }

    private static void AddSwitchEdges(
        WorkflowBuilder builder,
        WorkflowDefinition definition,
        IReadOnlyDictionary<WorkflowNodeId, MafCompiledNodeBinding> bindings,
        IWorkflowRoutingCompiler routingCompiler,
        ExecutorBinding source,
        IReadOnlyList<WorkflowEdge> edges,
        ISet<WorkflowEdgeId> groupedEdgeIds)
    {
        var switchEdges = edges
            .Where(edge => edge.Routing.Kind is WorkflowRouteKind.SwitchCase or WorkflowRouteKind.SwitchDefault)
            .ToArray();
        if (switchEdges.Length == 0)
        {
            return;
        }

        foreach (var edge in switchEdges)
        {
            groupedEdgeIds.Add(edge.Id);
        }

        builder.AddSwitch(source, switchBuilder =>
        {
            foreach (var edge in switchEdges.Where(edge => edge.Routing.Kind == WorkflowRouteKind.SwitchCase))
            {
                var compiled = routingCompiler.CompilePredicate(definition, edge);
                switchBuilder.AddCase<WorkflowNodeInput>(compiled.Predicate, [bindings[edge.TargetNodeId].Entry]);
            }

            var defaultEdge = switchEdges.SingleOrDefault(edge => edge.Routing.Kind == WorkflowRouteKind.SwitchDefault);
            if (defaultEdge is not null)
            {
                switchBuilder.WithDefault([bindings[defaultEdge.TargetNodeId].Entry]);
            }
        });
    }

    private static void AddRemainingEdges(
        WorkflowBuilder builder,
        WorkflowDefinition definition,
        IReadOnlyDictionary<WorkflowNodeId, MafCompiledNodeBinding> bindings,
        IWorkflowRoutingCompiler routingCompiler,
        ExecutorBinding source,
        IEnumerable<WorkflowEdge> edges,
        IReadOnlySet<WorkflowEdgeId> groupedEdgeIds)
    {
        foreach (var edge in edges.Where(edge => !groupedEdgeIds.Contains(edge.Id)))
        {
            if (edge.Routing.Kind == WorkflowRouteKind.Predicate)
            {
                var compiled = routingCompiler.CompilePredicate(definition, edge);
                builder.AddEdge<WorkflowNodeInput>(
                    source,
                    bindings[edge.TargetNodeId].Entry,
                    compiled.Predicate,
                    compiled.Label,
                    idempotent: true);
                continue;
            }

            builder.AddEdge(
                source,
                bindings[edge.TargetNodeId].Entry,
                ResolveEdgeLabel(edge),
                idempotent: true);
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
