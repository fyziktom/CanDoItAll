using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit;

public sealed class WorkflowPreviewSimulationTests
{
    [Fact]
    public void RendererPreservesInputPayloadAndResolvesTemplateTokens()
    {
        var node = CreateExecutorNode("store");
        var definition = CreateDefinition([CreateNode("start", WorkflowNodeKind.Start), node, CreateNode("end", WorkflowNodeKind.End)]);
        var step = new WorkflowPreviewSimulationStep(
            node.Id,
            WorkflowExecutorIds.ProjectStructure,
            "Preview skip",
            """
            {
              "result": {
                "nodeId": "{{node.id}}",
                "sourceExecutorId": "{{source.executor.id}}",
                "projectId": "{{inputPath:$.projectId}}"
              },
              "inputPayload": "{{inputPayload}}",
              "generatedAtUtc": "{{utcNow}}"
            }
            """);

        var output = WorkflowPreviewSimulationRenderer.Render(
            step,
            definition,
            node,
            new WorkflowNodeInput("""{"projectId":"project-1","runContext":{"gmailProcessing":{"messageIds":["msg-1"]}}}"""),
            DateTimeOffset.Parse("2026-05-14T12:00:00Z"));

        using var document = JsonDocument.Parse(output);
        Assert.Equal("store", document.RootElement.GetProperty("result").GetProperty("nodeId").GetString());
        Assert.Equal(WorkflowExecutorIds.ProjectStructure.Value, document.RootElement.GetProperty("result").GetProperty("sourceExecutorId").GetString());
        Assert.Equal("project-1", document.RootElement.GetProperty("result").GetProperty("projectId").GetString());
        Assert.Equal("msg-1", document.RootElement.GetProperty("inputPayload").GetProperty("runContext").GetProperty("gmailProcessing").GetProperty("messageIds")[0].GetString());
        Assert.Equal("2026-05-14T12:00:00.0000000+00:00", document.RootElement.GetProperty("generatedAtUtc").GetString());
    }

    [Fact]
    public async Task MafBackendUsesPreviewSimulationPlanInsteadOfInvokingExecutor()
    {
        var executor = new ThrowingWorkflowExecutor();
        var catalog = new WorkflowExecutorCatalog([executor]);
        var compiler = new MafWorkflowCompiler(
            new WorkflowDefinitionValidator(catalog),
            new WorkflowExecutorInvoker(catalog, [executor]));
        var backend = new MafInProcessWorkflowExecutionBackend(compiler, Array.Empty<LlmCallComponent>());
        var toolNode = CreateExecutorNode("tool");
        var definition = CreateDefinition([CreateNode("start", WorkflowNodeKind.Start), toolNode, CreateNode("end", WorkflowNodeKind.End)]);
        var request = new WorkflowRunStartRequest(
            definition.Id,
            definition.VersionId,
            """{"value":"input"}""",
            WorkflowRuntimeBackendKind.InProcess,
            SourceProcessRunId: null,
            SourceProcessAssignmentId: null)
        {
            PreviewSimulationPlan = new WorkflowPreviewSimulationPlan(
            [
                new WorkflowPreviewSimulationStep(
                    toolNode.Id,
                    executor.Descriptor.Id,
                    "Skip external action",
                    """{"simulated":true,"inputPayload":"{{inputPayload}}"}""")
            ])
        };

        var progressObserver = new RecordingWorkflowNodeExecutionProgressObserver();
        using var progressScope = WorkflowNodeExecutionProgressScope.Push(progressObserver);

        var result = await backend.StartAsync(definition, request, WorkflowRunId.New());

        Assert.Equal(WorkflowRunState.Completed, result.Run.State);
        Assert.Equal(0, executor.InvocationCount);
        Assert.Contains(progressObserver.Records, record =>
            record.NodeId == toolNode.Id &&
            record.State == WorkflowNodeExecutionProgressState.Started);
        Assert.Contains(progressObserver.Records, record =>
            record.NodeId == toolNode.Id &&
            record.State == WorkflowNodeExecutionProgressState.Completed);
    }

    private static WorkflowDefinition CreateDefinition(IReadOnlyList<WorkflowNode> nodes)
    {
        var now = DateTimeOffset.UtcNow;
        return new WorkflowDefinition(
            WorkflowId.New(),
            WorkflowVersionId.New(),
            "Preview simulation proof",
            "Verifies preview simulation output substitution.",
            WorkflowLifecycleStatus.Draft,
            new WorkflowGraph(
                new WorkflowNodeId("start"),
                nodes,
                [
                    CreateEdge("start-tool", "start", "tool"),
                    CreateEdge("tool-end", "tool", "end")
                ]),
            new WorkflowRuntimePolicy(
                WorkflowRuntimeBackendKind.InProcess,
                AllowInProcessPreviewRuns: true,
                RequireDurableProductionRuns: false,
                ExposeAzureFunctionsStatusEndpoint: false,
                ExposeAzureFunctionsMcpTool: false),
            now,
            now);
    }

    private static WorkflowNode CreateExecutorNode(string id)
        => new(
            new WorkflowNodeId(id),
            WorkflowNodeKind.Executor,
            id,
            [],
            new WorkflowNodeSettings(
                ComponentId: null,
                AgentId: null,
                SubworkflowId: null,
                ExternalRequestKind: null,
                Instructions: string.Empty,
                InputShape: WorkflowValueShape.Text,
                ResultShape: new WorkflowValueShape(WorkflowValueShapeKind.Json, "{}", "JSON"))
            {
                ExecutorId = WorkflowExecutorIds.StorageFile,
                ExecutorSettingsJson = "{}"
            });

    private static WorkflowNode CreateNode(
        string id,
        WorkflowNodeKind kind)
        => new(
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
                InputShape: kind == WorkflowNodeKind.End
                    ? new WorkflowValueShape(WorkflowValueShapeKind.Json, "{}", "JSON")
                    : WorkflowValueShape.Text,
                ResultShape: WorkflowValueShape.Text));

    private static WorkflowEdge CreateEdge(
        string id,
        string source,
        string target)
        => new(
            new WorkflowEdgeId(id),
            new WorkflowNodeId(source),
            SourcePortId: null,
            new WorkflowNodeId(target),
            TargetPortId: null,
            WorkflowEdgeKind.Direct,
            ConditionExpression: string.Empty);

    private sealed class ThrowingWorkflowExecutor : IWorkflowExecutor
    {
        public WorkflowExecutorDescriptor Descriptor => BuiltInWorkflowExecutorDescriptors.StorageFile;

        public int InvocationCount { get; private set; }

        public ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
            WorkflowExecutorExecutionContext context,
            WorkflowNodeInput input,
            CancellationToken cancellationToken = default)
        {
            InvocationCount++;
            throw new InvalidOperationException("Executor should not run when preview simulation is selected.");
        }
    }

    private sealed class RecordingWorkflowNodeExecutionProgressObserver : IWorkflowNodeExecutionProgressObserver
    {
        private readonly List<WorkflowNodeExecutionProgress> records = [];

        public IReadOnlyList<WorkflowNodeExecutionProgress> Records => records;

        public ValueTask RecordAsync(
            WorkflowNodeExecutionProgress progress,
            CancellationToken cancellationToken = default)
        {
            records.Add(progress);
            return ValueTask.CompletedTask;
        }
    }
}
