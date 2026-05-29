using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Agents.AI.Workflows;

namespace CanDoItAll.Tests.Unit;

public sealed class MafWorkflowEventNormalizerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Normalizer_maps_output_event_to_node_and_executor_metadata()
    {
        var definition = CreateDefinition();
        var normalizer = new MafWorkflowEventNormalizer();
        var bindings = MafWorkflowEventBindingIndex.FromDefinition(definition);
        var runId = WorkflowRunId.New();

        var record = normalizer.Normalize(
            runId,
            new WorkflowOutputEvent(new WorkflowNodeInput("{\"result\":\"ok\"}"), "work-a"),
            bindings,
            DateTimeOffset.UtcNow);
        var payload = JsonSerializer.Deserialize<WorkflowEventPayloadEnvelope>(record.PayloadJson, JsonOptions)!;

        Assert.Equal(WorkflowEventKind.Output, record.Kind);
        Assert.Equal(new WorkflowNodeId("work-a"), record.NodeId);
        Assert.Equal(new WorkflowNodeId("work-a"), payload.NodeId);
        Assert.Equal(new WorkflowExecutorId("test.echo"), payload.ExecutorId);
        Assert.Equal("{\"result\":\"ok\"}", payload.InlineJson);
        Assert.Contains("Workflow output", record.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Runtime_events_include_executor_identity_and_redacted_output_payload()
    {
        var executor = new EchoWorkflowExecutor();
        var manager = new WorkflowRuntimeManager(
            [
                new MafInProcessWorkflowExecutionBackend(
                    new MafWorkflowCompiler(
                        new WorkflowDefinitionValidator(),
                        new WorkflowExecutorInvoker(new WorkflowExecutorCatalog([executor]), [executor])),
                    [])
            ],
            new InMemoryWorkflowRunStore());
        var definition = CreateDefinition();

        var run = await manager.StartAsync(
            definition,
            new WorkflowRunStartRequest(
                definition.Id,
                definition.VersionId,
                "{\"prompt\":\"hello\"}",
                WorkflowRuntimeBackendKind.InProcess,
                SourceProcessRunId: null,
                SourceProcessAssignmentId: null));
        var events = await manager.ListEventsAsync(run.RunId);

        Assert.Equal(WorkflowRunState.Completed, run.State);
        var completed = events
            .Where(workflowEvent =>
                workflowEvent.Kind == WorkflowEventKind.ExecutorCompleted &&
                workflowEvent.NodeId == new WorkflowNodeId("work-a"))
            .Select(workflowEvent => JsonSerializer.Deserialize<WorkflowEventPayloadEnvelope>(workflowEvent.PayloadJson, JsonOptions))
            .Single(payload => payload is not null)!;

        Assert.Equal(new WorkflowExecutorId("test.echo"), completed.ExecutorId);
        Assert.Equal(new WorkflowNodeId("work-a"), completed.NodeId);
        Assert.Contains("\"node\":\"work-a\"", completed.InlineJson, StringComparison.Ordinal);
        Assert.DoesNotContain("raw-token-value", completed.InlineJson, StringComparison.Ordinal);
        Assert.Contains("[REDACTED]", completed.InlineJson, StringComparison.Ordinal);
    }

    private static WorkflowDefinition CreateDefinition()
    {
        return new WorkflowDefinition(
            WorkflowId.New(),
            WorkflowVersionId.New(),
            "Event identity workflow",
            "Event identity workflow.",
            WorkflowLifecycleStatus.Draft,
            new WorkflowGraph(
                new WorkflowNodeId("start"),
                [
                    CreateNode("start", WorkflowNodeKind.Start),
                    CreateExecutorNode("work-a"),
                    CreateExecutorNode("work-b"),
                    CreateNode("end", WorkflowNodeKind.End)
                ],
                [
                    CreateEdge("start-to-work-a", "start", "work-a"),
                    CreateEdge("work-a-to-work-b", "work-a", "work-b"),
                    CreateEdge("work-b-to-end", "work-b", "end")
                ]),
            new WorkflowRuntimePolicy(
                WorkflowRuntimeBackendKind.InProcess,
                AllowInProcessPreviewRuns: true,
                RequireDurableProductionRuns: false,
                ExposeAzureFunctionsStatusEndpoint: false,
                ExposeAzureFunctionsMcpTool: false),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
    }

    private static WorkflowNode CreateNode(string id, WorkflowNodeKind kind)
    {
        return new WorkflowNode(
            new WorkflowNodeId(id),
            kind,
            id,
            [],
            new WorkflowNodeSettings(
                ComponentId: null,
                AgentId: null,
                SubworkflowId: null,
                ExternalRequestKind: null,
                Instructions: string.Empty,
                InputShape: WorkflowValueShape.Text,
                ResultShape: WorkflowValueShape.Text));
    }

    private static WorkflowNode CreateExecutorNode(string id)
    {
        return CreateNode(id, WorkflowNodeKind.Executor) with
        {
            Settings = CreateNode(id, WorkflowNodeKind.Executor).Settings with
            {
                ExecutorId = new WorkflowExecutorId("test.echo"),
                ExecutorSettingsJson = "{}",
                ExecutionPolicy = WorkflowExecutorExecutionPolicy.Default
            }
        };
    }

    private static WorkflowEdge CreateEdge(string id, string source, string target)
    {
        return new WorkflowEdge(
            new WorkflowEdgeId(id),
            new WorkflowNodeId(source),
            SourcePortId: null,
            new WorkflowNodeId(target),
            TargetPortId: null,
            WorkflowEdgeKind.Direct,
            ConditionExpression: string.Empty);
    }

    private sealed class EchoWorkflowExecutor : IWorkflowExecutor
    {
        public WorkflowExecutorDescriptor Descriptor { get; } = new(
            new WorkflowExecutorId("test.echo"),
            "Echo",
            "Echoes node metadata.",
            WorkflowExecutorCategoryKind.Utility,
            "sync",
            "test.echo",
            WorkflowValueShape.Text,
            new WorkflowValueShape(WorkflowValueShapeKind.Json, "{}", "JSON"),
            "{}",
            "{}",
            WorkflowExecutorExecutionPolicy.Default,
            IsImplemented: true);

        public ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
            WorkflowExecutorExecutionContext context,
            WorkflowNodeInput input,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(new WorkflowNodeExecutionResult(
                context.Node.Id,
                $$"""{"node":"{{context.Node.Id.Value}}","token":"raw-token-value"}""",
                context.Descriptor.ResultShape));
        }
    }
}
