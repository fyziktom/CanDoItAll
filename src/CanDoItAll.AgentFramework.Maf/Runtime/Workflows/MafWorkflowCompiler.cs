using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Agents.AI.Workflows;

namespace CanDoItAll.AgentFramework.Maf;

public sealed record MafWorkflowBuildResult(
    Workflow? Workflow,
    WorkflowCompilationResult Compilation);

public sealed class MafWorkflowCompiler(
    IWorkflowDefinitionValidator validator,
    IWorkflowExecutorInvoker? executorInvoker = null,
    IWorkflowLlmComponentInvoker? llmComponentInvoker = null)
{
    public MafWorkflowBuildResult Compile(
        WorkflowDefinition definition,
        IReadOnlyList<LlmCallComponent> components)
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
            var bindings = definition.Graph.Nodes.ToDictionary(
                node => node.Id,
                node => CreateExecutorBinding(definition, node, componentsById));
            var start = bindings[definition.Graph.StartNodeId];
            var builder = new WorkflowBuilder(start)
                .WithName(definition.Name)
                .WithDescription(definition.Description);

            foreach (var edge in definition.Graph.Edges)
            {
                builder.AddEdge(
                    bindings[edge.SourceNodeId],
                    bindings[edge.TargetNodeId],
                    string.IsNullOrWhiteSpace(edge.ConditionExpression) ? null : edge.ConditionExpression,
                    idempotent: true);
            }

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

    private ExecutorBinding CreateExecutorBinding(
        WorkflowDefinition definition,
        WorkflowNode node,
        IReadOnlyDictionary<WorkflowComponentId, LlmCallComponent> componentsById)
    {
        async ValueTask<WorkflowNodeInput> ExecuteAsync(
            WorkflowNodeInput input,
            IWorkflowContext context,
            CancellationToken cancellationToken)
        {
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

                var result = await llmComponentInvoker.ExecuteAsync(definition, node, component, input, cancellationToken);
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

                var result = await executorInvoker.ExecuteAsync(definition, node, input, cancellationToken);
                return new WorkflowNodeInput(result.PayloadJson);
            }

            return input;
        }

        return ((Func<WorkflowNodeInput, IWorkflowContext, CancellationToken, ValueTask<WorkflowNodeInput>>)ExecuteAsync)
            .BindAsExecutor(node.Id.Value, threadsafe: true);
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
