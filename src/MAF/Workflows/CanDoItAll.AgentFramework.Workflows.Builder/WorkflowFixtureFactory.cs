using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;

namespace CanDoItAll.AgentFramework.Workflows.Builder;

public static class WorkflowFixtureFactory
{
    public static WorkflowDefinition CreateLinearLlmWorkflow(
        WorkflowComponentId componentId,
        string name = "Linear LLM workflow")
    {
        return WorkflowDefinitionBuilder
            .Create(name)
            .WithDescription("Deterministic workflow fixture with start, LLM, and end nodes.")
            .AddNode(WorkflowNodeBuilder.Start("start"))
            .AddNode(WorkflowNodeBuilder.Llm("llm", componentId))
            .AddNode(WorkflowNodeBuilder.End("end"))
            .AddEdge(WorkflowEdgeBuilder.Direct("start-to-llm", "start", "llm"))
            .AddEdge(WorkflowEdgeBuilder.Direct("llm-to-end", "llm", "end"))
            .Build();
    }

    public static WorkflowDefinition CreateExecutorWorkflow(
        WorkflowExecutorId executorId,
        string settingsJson = "{}",
        string name = "Executor workflow")
    {
        return WorkflowDefinitionBuilder
            .Create(name)
            .WithDescription("Deterministic workflow fixture with a single executor node.")
            .AddNode(WorkflowNodeBuilder.Start("start"))
            .AddNode(WorkflowNodeBuilder.Executor("execute", executorId, settingsJson))
            .AddNode(WorkflowNodeBuilder.End("end"))
            .AddEdge(WorkflowEdgeBuilder.Direct("start-to-execute", "start", "execute"))
            .AddEdge(WorkflowEdgeBuilder.Direct("execute-to-end", "execute", "end"))
            .Build();
    }

    public static WorkflowDefinition CreateBranchingExecutorWorkflow(
        WorkflowExecutorId firstExecutorId,
        WorkflowExecutorId fallbackExecutorId,
        string name = "Branching executor workflow")
    {
        return WorkflowDefinitionBuilder
            .Create(name)
            .WithDescription("Deterministic workflow fixture with predicate and default branches.")
            .AddNode(WorkflowNodeBuilder.Start("start"))
            .AddNode(WorkflowNodeBuilder
                .For("triage", WorkflowNodeKind.StrictLogic)
                .AddPort(WorkflowPortBuilder.Input("input").Build())
                .AddPort(WorkflowPortBuilder.Output("matched").Build())
                .AddPort(WorkflowPortBuilder.Output("default").Optional().Build())
                .Build())
            .AddNode(WorkflowNodeBuilder.Executor("matched-executor", firstExecutorId))
            .AddNode(WorkflowNodeBuilder.Executor("fallback-executor", fallbackExecutorId))
            .AddNode(WorkflowNodeBuilder.End("end"))
            .AddEdge(WorkflowEdgeBuilder.Direct("start-to-triage", "start", "triage"))
            .AddEdge(WorkflowEdgeBuilder.Predicate(
                "triage-to-matched",
                "triage",
                "matched-executor",
                "$.route",
                WorkflowRouteOperator.Equals,
                "\"matched\"",
                WorkflowRouteValueKind.String,
                "Matched route"))
            .AddEdge(WorkflowEdgeBuilder.SwitchDefault(
                "triage-to-fallback",
                "triage",
                "fallback-executor",
                "Fallback route"))
            .AddEdge(WorkflowEdgeBuilder.Direct("matched-to-end", "matched-executor", "end"))
            .AddEdge(WorkflowEdgeBuilder.Direct("fallback-to-end", "fallback-executor", "end"))
            .Build();
    }

    public static WorkflowDefinition CreateInvalidMissingStartWorkflow()
    {
        return WorkflowDefinitionBuilder
            .Create("Invalid workflow")
            .AddNode(WorkflowNodeBuilder.End("end"))
            .BuildUnchecked();
    }

    public static WorkflowFailureDiagnosticEnvelope CreateExecutorFailureDiagnostic(
        WorkflowNodeId nodeId,
        WorkflowExecutorId executorId,
        string correlationId = "workflow-fixture-correlation")
        => new(
            WorkflowFailureKind.Executor,
            WorkflowFailureRetryability.RetryableAfterRepair,
            "Executor settings are invalid.",
            $"Fix the executor settings JSON for node '{nodeId.Value}'.",
            "Executor settings parse failure: [REDACTED]",
            correlationId,
            WorkflowId.New(),
            workflowVersionId: null,
            WorkflowRunId.New(),
            nodeId,
            executorId,
            WorkflowFailureSourceContext.ForExecutor(executorId),
            DateTimeOffset.UtcNow);
}
