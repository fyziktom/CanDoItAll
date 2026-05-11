using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Agents.AI.Workflows;

namespace CanDoItAll.Tests.Unit;

public sealed class WorkflowFoundationTests
{
    [Fact]
    public void WorkflowIdRejectsEmptyValue()
    {
        Assert.Throws<ArgumentException>(() => new WorkflowId(Guid.Empty));
    }

    [Fact]
    public void ValidatorRequiresReferencedLlmComponent()
    {
        var definition = CreateDefinition([
            CreateNode("start", WorkflowNodeKind.Start),
            CreateNode("llm", WorkflowNodeKind.LlmCall),
            CreateNode("end", WorkflowNodeKind.End)
        ], [
            CreateEdge("start-to-llm", "start", "llm"),
            CreateEdge("llm-to-end", "llm", "end")
        ]);

        var result = new WorkflowDefinitionValidator().Validate(definition, []);

        Assert.Contains(result.Issues, issue => issue.Code == WorkflowValidationIssueCode.InvalidComponentReference);
    }

    [Fact]
    public void ValidatorAcceptsBasicLlmWorkflow()
    {
        var component = CreateComponent();
        var definition = CreateDefinition([
            CreateNode("start", WorkflowNodeKind.Start),
            CreateNode("llm", WorkflowNodeKind.LlmCall, component.Id),
            CreateNode("end", WorkflowNodeKind.End)
        ], [
            CreateEdge("start-to-llm", "start", "llm"),
            CreateEdge("llm-to-end", "llm", "end")
        ]);

        var result = new WorkflowDefinitionValidator().Validate(definition, [component]);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void MafCompilerBuildsWorkflowWithoutLeakingMafTypesThroughCoreContracts()
    {
        var component = CreateComponent();
        var definition = CreateDefinition([
            CreateNode("start", WorkflowNodeKind.Start),
            CreateNode("llm", WorkflowNodeKind.LlmCall, component.Id),
            CreateNode("end", WorkflowNodeKind.End)
        ], [
            CreateEdge("start-to-llm", "start", "llm"),
            CreateEdge("llm-to-end", "llm", "end")
        ]);

        var compiler = new MafWorkflowCompiler(new WorkflowDefinitionValidator());
        var result = compiler.Compile(definition, [component]);

        Assert.True(result.Compilation.Succeeded);
        Assert.NotNull(result.Workflow);
        Assert.Equal("Sample workflow", result.Workflow.Name);
    }

    [Fact]
    public void MafStatusMapperMapsPendingRequestsToWaitingForInput()
    {
        var state = MafWorkflowStatusMapper.MapRunStatus(RunStatus.PendingRequests);

        Assert.Equal(WorkflowRunState.WaitingForInput, state);
    }

    [Fact]
    public void RuntimeBackendCatalogPrefersDurableTaskForDurableProductionRuns()
    {
        var catalog = new WorkflowRuntimeBackendCatalog();

        var durableTask = catalog.GetRequiredBackend(WorkflowRuntimeBackendKind.DurableTask);

        Assert.True(durableTask.IsDurable);
        Assert.True(durableTask.SupportsDashboardObservability);
    }

    [Fact]
    public async Task RuntimeManagerCompletesInProcessWorkflow()
    {
        var component = CreateComponent();
        var definition = CreateDefinition([
            CreateNode("start", WorkflowNodeKind.Start),
            CreateNode("llm", WorkflowNodeKind.LlmCall, component.Id),
            CreateNode("end", WorkflowNodeKind.End)
        ], [
            CreateEdge("start-to-llm", "start", "llm"),
            CreateEdge("llm-to-end", "llm", "end")
        ]) with
        {
            RuntimePolicy = new WorkflowRuntimePolicy(
                WorkflowRuntimeBackendKind.InProcess,
                AllowInProcessPreviewRuns: true,
                RequireDurableProductionRuns: false,
                ExposeAzureFunctionsStatusEndpoint: false,
                ExposeAzureFunctionsMcpTool: false)
        };
        var store = new InMemoryWorkflowRunStore();
        var manager = new WorkflowRuntimeManager(
            [
                new MafInProcessWorkflowExecutionBackend(
                    new MafWorkflowCompiler(
                        new WorkflowDefinitionValidator(),
                        llmComponentInvoker: new PassthroughLlmComponentInvoker()),
                    [component])
            ],
            store);

        var run = await manager.StartAsync(
            definition,
            new WorkflowRunStartRequest(
                definition.Id,
                definition.VersionId,
                "{\"prompt\":\"hello\"}",
                WorkflowRuntimeBackendKind.InProcess,
                SourceProcessRunId: null,
                SourceProcessAssignmentId: null));

        Assert.Equal(WorkflowRunState.Completed, run.State);
        Assert.NotEmpty(await manager.ListEventsAsync(run.RunId));
    }

    [Fact]
    public async Task RuntimeManagerCreatesAndRespondsToHumanInputRequest()
    {
        var definition = CreateDefinition([
            CreateNode("start", WorkflowNodeKind.Start),
            CreateNode("human", WorkflowNodeKind.HumanInput),
            CreateNode("end", WorkflowNodeKind.End)
        ], [
            CreateEdge("start-to-human", "start", "human"),
            CreateEdge("human-to-end", "human", "end")
        ]);
        var store = new InMemoryWorkflowRunStore();
        var manager = new WorkflowRuntimeManager([], store);

        var run = await manager.StartAsync(
            definition,
            new WorkflowRunStartRequest(
                definition.Id,
                definition.VersionId,
                "{\"question\":\"approve?\"}",
                WorkflowRuntimeBackendKind.DurableTask,
                SourceProcessRunId: null,
                SourceProcessAssignmentId: null));
        var pending = await store.ListPendingExternalRequestsAsync(run.RunId);
        var completed = await manager.RespondToExternalRequestAsync(pending[0].Id, "{\"approved\":true}");

        Assert.Equal(WorkflowRunState.WaitingForInput, run.State);
        Assert.Single(pending);
        Assert.Equal(WorkflowRunState.Completed, completed.State);
    }

    [Fact]
    public async Task RuntimeManagerRejectsUnregisteredBackendInsteadOfFallingBack()
    {
        var definition = CreateDefinition([
            CreateNode("start", WorkflowNodeKind.Start),
            CreateNode("end", WorkflowNodeKind.End)
        ], [
            CreateEdge("start-to-end", "start", "end")
        ]);
        var manager = new WorkflowRuntimeManager([], new InMemoryWorkflowRunStore());

        await Assert.ThrowsAsync<InvalidOperationException>(() => manager.StartAsync(
            definition,
            new WorkflowRunStartRequest(
                definition.Id,
                definition.VersionId,
                "{}",
                WorkflowRuntimeBackendKind.DurableTask,
                SourceProcessRunId: null,
                SourceProcessAssignmentId: null)));
    }

    [Fact]
    public async Task RuntimeManagerCreatesDistinctParallelRuns()
    {
        var component = CreateComponent();
        var definition = CreateDefinition([
            CreateNode("start", WorkflowNodeKind.Start),
            CreateNode("llm", WorkflowNodeKind.LlmCall, component.Id),
            CreateNode("end", WorkflowNodeKind.End)
        ], [
            CreateEdge("start-to-llm", "start", "llm"),
            CreateEdge("llm-to-end", "llm", "end")
        ]) with
        {
            RuntimePolicy = new WorkflowRuntimePolicy(
                WorkflowRuntimeBackendKind.InProcess,
                AllowInProcessPreviewRuns: true,
                RequireDurableProductionRuns: false,
                ExposeAzureFunctionsStatusEndpoint: false,
                ExposeAzureFunctionsMcpTool: false)
        };
        var manager = new WorkflowRuntimeManager(
            [
                new MafInProcessWorkflowExecutionBackend(
                    new MafWorkflowCompiler(
                        new WorkflowDefinitionValidator(),
                        llmComponentInvoker: new PassthroughLlmComponentInvoker()),
                    [component])
            ],
            new InMemoryWorkflowRunStore());
        var request = new WorkflowRunStartRequest(
            definition.Id,
            definition.VersionId,
            "{}",
            WorkflowRuntimeBackendKind.InProcess,
            SourceProcessRunId: null,
            SourceProcessAssignmentId: null);

        var runs = await Task.WhenAll(
            manager.StartAsync(definition, request),
            manager.StartAsync(definition, request));

        Assert.NotEqual(runs[0].RunId, runs[1].RunId);
        Assert.All(runs, run => Assert.Equal(WorkflowRunState.Completed, run.State));
    }

    private static WorkflowDefinition CreateDefinition(
        IReadOnlyList<WorkflowNode> nodes,
        IReadOnlyList<WorkflowEdge> edges)
    {
        return new WorkflowDefinition(
            WorkflowId.New(),
            WorkflowVersionId.New(),
            "Sample workflow",
            "Sample workflow for tests.",
            WorkflowLifecycleStatus.Draft,
            new WorkflowGraph(new WorkflowNodeId("start"), nodes, edges),
            new WorkflowRuntimePolicy(
                WorkflowRuntimeBackendKind.DurableTask,
                AllowInProcessPreviewRuns: true,
                RequireDurableProductionRuns: true,
                ExposeAzureFunctionsStatusEndpoint: false,
                ExposeAzureFunctionsMcpTool: false),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
    }

    private static WorkflowNode CreateNode(
        string id,
        WorkflowNodeKind kind,
        WorkflowComponentId? componentId = null)
    {
        return new WorkflowNode(
            new WorkflowNodeId(id),
            kind,
            id,
            [],
            new WorkflowNodeSettings(
                componentId,
                AgentId: null,
                SubworkflowId: null,
                ExternalRequestKind: kind == WorkflowNodeKind.HumanInput ? WorkflowExternalRequestKind.HumanInput : null,
                Instructions: string.Empty,
                InputShape: WorkflowValueShape.Text,
                ResultShape: WorkflowValueShape.Text));
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

    private static LlmCallComponent CreateComponent()
    {
        return new LlmCallComponent(
            WorkflowComponentId.New(),
            "Summarize",
            ProviderProfileId: null,
            "gpt-5.4",
            WorkflowModality.Text,
            new WorkflowModelSettings(
                Temperature: 0.2,
                MaxOutputTokens: 800,
                RequireJsonOutput: false,
                ResponseFormatJsonSchema: string.Empty),
            "Summarize the input.",
            WorkflowValueShape.Text,
            WorkflowValueShape.Text,
            AgentPermissionsPolicy.Default,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
    }

    private sealed class PassthroughLlmComponentInvoker : IWorkflowLlmComponentInvoker
    {
        public ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
            WorkflowDefinition definition,
            WorkflowNode node,
            LlmCallComponent component,
            WorkflowNodeInput input,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(new WorkflowNodeExecutionResult(
                node.Id,
                input.PayloadJson,
                component.ResultShape));
        }
    }
}
