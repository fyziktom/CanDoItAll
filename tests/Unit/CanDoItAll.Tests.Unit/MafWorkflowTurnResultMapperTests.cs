using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using Microsoft.Agents.AI.Workflows;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class MafWorkflowTurnResultMapperTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-08-20T12:00:00Z");

    [Fact]
    public async Task MapTurnAsync_rejects_incomplete_request_checkpoint_boundary()
    {
        var fixture = CreateFixture();
        var nativeRequest = CreateNativeRequest(fixture.Definition);
        var turn = new MafWorkflowStreamTurn(
            fixture.Session.Id.Value,
            RunStatus.PendingRequests,
            [],
            nativeRequest,
            WaitingCheckpoint: null);

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Mapper.MapTurnAsync(
            fixture.Definition,
            fixture.RunId,
            origin: null,
            Now,
            turn,
            fixture.ProgressObserver,
            startRequest: null,
            CancellationToken.None));
    }

    [Fact]
    public async Task MapTurnAsync_maps_terminal_turn_with_started_input_and_terminal_checkpoint()
    {
        var fixture = CreateFixture();
        var request = CreateStartRequest(fixture.Definition);
        var turn = new MafWorkflowStreamTurn(
            fixture.Session.Id.Value,
            RunStatus.Ended,
            [],
            PendingRequest: null,
            WaitingCheckpoint: null);

        var result = await fixture.Mapper.MapTurnAsync(
            fixture.Definition,
            fixture.RunId,
            origin: null,
            Now,
            turn,
            fixture.ProgressObserver,
            request,
            CancellationToken.None);

        Assert.Equal(WorkflowRunState.Completed, result.Run.State);
        Assert.Equal(fixture.Session.Id.Value, result.Run.BackendRunId);
        Assert.NotNull(result.Run.TerminalAtUtc);
        Assert.Empty(result.ExternalRequests);
        Assert.Contains(result.Events, workflowEvent => workflowEvent.Kind == WorkflowEventKind.Started);
        Assert.Contains(result.Events, workflowEvent => workflowEvent.Kind == WorkflowEventKind.Completed);
        Assert.Equal(WorkflowCheckpointKind.Completed, Assert.Single(result.Checkpoints).Kind);
    }

    [Fact]
    public async Task MapTurnAsync_maps_waiting_turn_to_safe_request_and_trusted_checkpoint()
    {
        var fixture = CreateFixture();
        var stored = await fixture.Store.CreateAsync(
            new WorkflowBackendCheckpointCreateRequest(
                fixture.Session,
                Parent: null,
                WorkflowBackendCheckpointPayload.Create("{\"checkpoint\":true}")));
        var checkpoint = Assert.IsType<WorkflowBackendCheckpointPayloadRecord>(stored.Checkpoint);
        var nativeRequest = CreateNativeRequest(fixture.Definition);
        var turn = new MafWorkflowStreamTurn(
            fixture.Session.Id.Value,
            RunStatus.PendingRequests,
            [],
            nativeRequest,
            new CheckpointInfo(
                checkpoint.Index.Link.SessionId.Value,
                checkpoint.Index.Link.CheckpointId.Value));

        var result = await fixture.Mapper.MapTurnAsync(
            fixture.Definition,
            fixture.RunId,
            origin: null,
            Now,
            turn,
            fixture.ProgressObserver,
            startRequest: null,
            CancellationToken.None);

        var request = Assert.Single(result.ExternalRequests);
        var applicationCheckpoint = Assert.Single(result.Checkpoints);
        Assert.Equal(WorkflowRunState.WaitingForInput, result.Run.State);
        Assert.Equal(WorkflowExternalRequestState.Pending, request.State);
        Assert.Equal(new WorkflowNodeId("human"), request.NodeId);
        Assert.Contains("prompt", request.RequestJson, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("responseShape", request.RequestJson, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("context", request.RequestJson, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(checkpoint.Index.Link, request.Continuation?.Checkpoint);
        Assert.Equal(checkpoint.Payload.Sha256, request.Continuation?.CheckpointPayloadHash);
        Assert.Equal(WorkflowCheckpointTrustBoundary.TrustedRuntimeState, applicationCheckpoint.TrustBoundary);
        Assert.Equal(WorkflowResumeAvailability.Available, applicationCheckpoint.ResumeAvailability);
        Assert.Contains(result.Events, workflowEvent => workflowEvent.Kind == WorkflowEventKind.WaitingForInput);
    }

    private static MapperFixture CreateFixture()
    {
        var clock = new FixedTimeProvider(Now);
        var definition = CreateDefinition();
        var runId = WorkflowRunId.New();
        var store = new InMemoryWorkflowBackendCheckpointPayloadStore(clock);
        var requestMapper = new MafWorkflowExternalRequestMapper(clock);
        var mapper = new MafWorkflowTurnResultMapper(
            store,
            requestMapper,
            new MafWorkflowEventNormalizer(),
            new WorkflowCheckpointFactory(),
            new WorkflowPayloadPolicyService(),
            clock);
        var session = new WorkflowBackendCheckpointSession(
            new WorkflowBackendSessionId(runId.ToString()),
            runId,
            definition.Id,
            definition.VersionId,
            WorkflowRuntimeBackendKind.InProcess,
            MafWorkflowCheckpointProtocol.Format,
            MafWorkflowCheckpointProtocol.FormatVersion,
            MafWorkflowCheckpointProtocol.CompilerContractVersion,
            WorkflowTopologyFingerprint.Create("mapper-test-topology"));
        var progressObserver = mapper.CreateProgressObserver(
            runId,
            definition,
            WorkflowPreviewSimulationPlan.Empty,
            origin: null);
        return new MapperFixture(
            mapper,
            store,
            definition,
            runId,
            session,
            progressObserver);
    }

    private static ExternalRequest CreateNativeRequest(WorkflowDefinition definition)
    {
        var port = RequestPort.Create<MafWorkflowHumanInputRequest, MafWorkflowHumanInputResponse>(
            "mapper-test-human-port");
        var request = new MafWorkflowHumanInputRequest(
            definition.Id,
            definition.VersionId,
            new WorkflowNodeId("human"),
            WorkflowExternalRequestKind.HumanInput,
            "Provide reviewed input.",
            new WorkflowValueShape(
                WorkflowValueShapeKind.Json,
                "{\"type\":\"object\"}",
                "JSON object"),
            new WorkflowNodeInput("{\"source\":\"mapper-test\"}"));
        return ExternalRequest.Create(port, request, "mapper-test-request");
    }

    private static WorkflowRunStartRequest CreateStartRequest(WorkflowDefinition definition)
    {
        return new WorkflowRunStartRequest(
            definition.Id,
            definition.VersionId,
            "{\"input\":true}",
            WorkflowRuntimeBackendKind.InProcess,
            SourceProcessRunId: null,
            SourceProcessAssignmentId: null);
    }

    private static WorkflowDefinition CreateDefinition()
    {
        var node = new WorkflowNode(
            new WorkflowNodeId("human"),
            WorkflowNodeKind.HumanInput,
            "Human input",
            [],
            new WorkflowNodeSettings(
                ComponentId: null,
                AgentId: null,
                SubworkflowId: null,
                WorkflowExternalRequestKind.HumanInput,
                "Provide reviewed input.",
                WorkflowValueShape.Text,
                new WorkflowValueShape(
                    WorkflowValueShapeKind.Json,
                    "{\"type\":\"object\"}",
                    "JSON object")));
        return new WorkflowDefinition(
            WorkflowId.New(),
            WorkflowVersionId.New(),
            "Mapper test workflow",
            "Exercises native turn result mapping in isolation.",
            WorkflowLifecycleStatus.Draft,
            new WorkflowGraph(node.Id, [node], []),
            new WorkflowRuntimePolicy(
                WorkflowRuntimeBackendKind.InProcess,
                AllowInProcessPreviewRuns: true,
                RequireDurableProductionRuns: false,
                ExposeAzureFunctionsStatusEndpoint: false,
                ExposeAzureFunctionsMcpTool: false),
            Now,
            Now);
    }

    private sealed record MapperFixture(
        MafWorkflowTurnResultMapper Mapper,
        InMemoryWorkflowBackendCheckpointPayloadStore Store,
        WorkflowDefinition Definition,
        WorkflowRunId RunId,
        WorkflowBackendCheckpointSession Session,
        WorkflowBackendProgressEventObserver ProgressObserver);

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
