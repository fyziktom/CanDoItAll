using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Agents.AI.Workflows;

namespace CanDoItAll.AgentFramework.Maf;

public sealed record MafWorkflowBuildResult(
    Workflow? Workflow,
    WorkflowCompilationResult Compilation);

public sealed class MafWorkflowCompiler(IWorkflowDefinitionValidator validator)
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
            var bindings = definition.Graph.Nodes.ToDictionary(
                node => node.Id,
                node => CreateExecutorBinding(node));
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

    private static ExecutorBinding CreateExecutorBinding(WorkflowNode node)
    {
        ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(WorkflowNodeInput input)
        {
            return ValueTask.FromResult(new WorkflowNodeExecutionResult(
                node.Id,
                input.PayloadJson,
                node.Settings.ResultShape ?? WorkflowValueShape.Text));
        }

        return ((Func<WorkflowNodeInput, ValueTask<WorkflowNodeExecutionResult>>)ExecuteAsync)
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
