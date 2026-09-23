using System.Reflection;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Providers;
using CanDoItAll.AgentFramework.Workflows.Abstractions;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class MafWorkflowExecutorFailureDiagnosticsTests
{
    private const string RootCauseMessage = "No Office365 messages were found with category 'CanDoItAllSummaryTest'.";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Executor_failure_surfaces_root_cause_in_summary_events_and_diagnostic_payload()
    {
        var executor = new ThrowingWorkflowExecutor();
        var manager = WorkflowRuntimeManager.CreateInMemory(
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

        Assert.Equal(WorkflowRunState.Failed, run.State);
        Assert.Contains(RootCauseMessage, run.Summary, StringComparison.Ordinal);
        Assert.DoesNotContain("Error invoking handler", run.Summary, StringComparison.Ordinal);

        var executorFailed = events.First(workflowEvent =>
            workflowEvent.Kind == WorkflowEventKind.ExecutorFailed &&
            workflowEvent.NodeId == new WorkflowNodeId("work-a"));
        Assert.Contains(RootCauseMessage, executorFailed.Message, StringComparison.Ordinal);

        var error = events.Last(workflowEvent => workflowEvent.Kind == WorkflowEventKind.Error);
        Assert.Contains(RootCauseMessage, error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Error invoking handler", error.Message, StringComparison.Ordinal);

        var payload = JsonSerializer.Deserialize<WorkflowEventPayloadEnvelope>(error.PayloadJson, JsonOptions)!;
        var diagnostic = JsonSerializer.Deserialize<WorkflowFailureDiagnosticEnvelope>(payload.InlineJson, JsonOptions)!;
        Assert.Equal(WorkflowFailureKind.Executor, diagnostic.Kind);
        Assert.Equal(new WorkflowNodeId("work-a"), diagnostic.NodeId);
        Assert.Equal(new WorkflowExecutorId("test.throw"), diagnostic.ExecutorId);
        Assert.Contains(RootCauseMessage, diagnostic.Message, StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(diagnostic.RedactedTechnicalDetail));

        var sourceProviderId = Guid.Parse(
            "f75f6257-cd0d-4a71-9a30-c30557c8ece2");
        var sourceFailure = new MafProviderTransportException(
            sourceProviderId,
            "shared-routing-model",
            new ProviderFailureBoundaryException(
                sourceProviderId,
                ProviderFailureOperation.RuntimeRequest));
        var sourceDiagnostic = WorkflowExecutorFailureDiagnosticMapper
            .FromExecutionFailure(
                definition,
                definition.Graph.Nodes.Single(node =>
                    node.Id == new WorkflowNodeId("work-a")),
                executor.Descriptor,
                sourceFailure,
                WorkflowExecutorExecutionPolicy.Default);
        var serializedSourceDiagnostic = JsonSerializer.Serialize(
            sourceDiagnostic,
            JsonOptions);

        Assert.Contains(
            ProviderFailureDisclosurePolicy.SanitizedRuntimeFailureMessage,
            sourceDiagnostic.RedactedTechnicalDetail,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            sourceProviderId.ToString("D"),
            serializedSourceDiagnostic,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "10.23.45.67:43123",
            serializedSourceDiagnostic,
            StringComparison.Ordinal);
    }

    [Fact]
    public void CreateDetailedMessage_unwraps_reflection_wrappers_and_joins_distinct_messages()
    {
        var exception = new TargetInvocationException(
            "Error invoking handler for CanDoItAll.AgentFramework.Models.WorkflowNodeInput",
            new InvalidOperationException(
                "Workflow executor 'test.throw' failed on node 'work-a' after 2 attempt(s).",
                new InvalidOperationException(RootCauseMessage)));

        var message = MafWorkflowFailureDetails.CreateDetailedMessage(exception);

        Assert.Equal(
            $"Workflow executor 'test.throw' failed on node 'work-a' after 2 attempt(s). {RootCauseMessage}",
            message);
    }

    [Fact]
    public void CreateDetailedMessage_deduplicates_contained_inner_messages()
    {
        var exception = new InvalidOperationException(
            $"Executor failed: {RootCauseMessage}",
            new InvalidOperationException(RootCauseMessage));

        var message = MafWorkflowFailureDetails.CreateDetailedMessage(exception);

        Assert.Equal($"Executor failed: {RootCauseMessage}", message);
    }

    [Fact]
    public void ResolveRootException_skips_reflection_wrappers()
    {
        var invocation = new InvalidOperationException("Executor failed.");
        var exception = new TargetInvocationException(
            "Error invoking handler for CanDoItAll.AgentFramework.Models.WorkflowNodeInput",
            invocation);

        Assert.Same(invocation, MafWorkflowFailureDetails.ResolveRootException(exception));
    }

    private static WorkflowDefinition CreateDefinition()
    {
        return new WorkflowDefinition(
            WorkflowId.New(),
            WorkflowVersionId.New(),
            "Executor failure diagnostics workflow",
            "Executor failure diagnostics workflow.",
            WorkflowLifecycleStatus.Draft,
            new WorkflowGraph(
                new WorkflowNodeId("start"),
                [
                    CreateNode("start", WorkflowNodeKind.Start),
                    CreateExecutorNode("work-a"),
                    CreateNode("end", WorkflowNodeKind.End)
                ],
                [
                    CreateEdge("start-to-work-a", "start", "work-a"),
                    CreateEdge("work-a-to-end", "work-a", "end")
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
                ExecutorId = new WorkflowExecutorId("test.throw"),
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

    private sealed class ThrowingWorkflowExecutor : IWorkflowExecutor
    {
        public WorkflowExecutorDescriptor Descriptor { get; } = new(
            new WorkflowExecutorId("test.throw"),
            "Throw",
            "Throws a root-cause exception.",
            WorkflowExecutorCategoryKind.Utility,
            "sync",
            "test.throw",
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
            => throw new InvalidOperationException(RootCauseMessage);
    }
}
