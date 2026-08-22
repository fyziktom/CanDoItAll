using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class WorkflowExternalResponseResultMapperTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-08-20T12:00:00Z");
    private readonly WorkflowExternalResponseResultMapper mapper = new();

    [Theory]
    [InlineData(WorkflowResumeBoundaryLoadOutcome.CheckpointMissing, WorkflowExternalResponseOperationOutcomeCode.CheckpointMissing)]
    [InlineData(WorkflowResumeBoundaryLoadOutcome.CheckpointCorrupt, WorkflowExternalResponseOperationOutcomeCode.CheckpointCorrupt)]
    [InlineData(WorkflowResumeBoundaryLoadOutcome.CheckpointIncompatible, WorkflowExternalResponseOperationOutcomeCode.CheckpointIncompatible)]
    [InlineData(WorkflowResumeBoundaryLoadOutcome.TopologyMismatch, WorkflowExternalResponseOperationOutcomeCode.TopologyMismatch)]
    [InlineData(WorkflowResumeBoundaryLoadOutcome.WorkflowVersionMismatch, WorkflowExternalResponseOperationOutcomeCode.WorkflowVersionMismatch)]
    [InlineData(WorkflowResumeBoundaryLoadOutcome.LegacyNonResumable, WorkflowExternalResponseOperationOutcomeCode.CheckpointIncompatible)]
    [InlineData(WorkflowResumeBoundaryLoadOutcome.LinkageMismatch, WorkflowExternalResponseOperationOutcomeCode.RequestMismatch)]
    [InlineData(WorkflowResumeBoundaryLoadOutcome.RequestNotFound, WorkflowExternalResponseOperationOutcomeCode.RequestMismatch)]
    public void BoundaryFailure_MapsToTypedOperationOutcome(
        WorkflowResumeBoundaryLoadOutcome boundaryOutcome,
        WorkflowExternalResponseOperationOutcomeCode expectedOutcome)
        => Assert.Equal(expectedOutcome, mapper.MapBoundaryFailure(boundaryOutcome));

    [Theory]
    [InlineData(
        WorkflowRunState.Completed,
        WorkflowExternalResponseAction.SubmitInput,
        WorkflowExternalResponseOperationState.Completed,
        WorkflowExternalResponseOperationOutcomeCode.Completed,
        "The response was accepted and the workflow completed.")]
    [InlineData(
        WorkflowRunState.Completed,
        WorkflowExternalResponseAction.Deny,
        WorkflowExternalResponseOperationState.Denied,
        WorkflowExternalResponseOperationOutcomeCode.Denied,
        "The approval was denied and the governed executor was not invoked.")]
    [InlineData(
        WorkflowRunState.Cancelled,
        WorkflowExternalResponseAction.SubmitInput,
        WorkflowExternalResponseOperationState.Cancelled,
        WorkflowExternalResponseOperationOutcomeCode.Cancelled,
        "Cancellation won while the response was being resumed.")]
    [InlineData(
        WorkflowRunState.Failed,
        WorkflowExternalResponseAction.SubmitInput,
        WorkflowExternalResponseOperationState.FailedTerminal,
        WorkflowExternalResponseOperationOutcomeCode.ResumeFailed,
        "The workflow response failed with a terminal recovery outcome.")]
    public void FinalResult_MapsTerminalDeniedAndCancelledStatesWithSafeMessages(
        WorkflowRunState runState,
        WorkflowExternalResponseAction action,
        WorkflowExternalResponseOperationState expectedState,
        WorkflowExternalResponseOperationOutcomeCode expectedOutcome,
        string expectedSafeMessage)
    {
        var run = CreateRun(runState);

        var result = mapper.CreateFinalResult(
            action,
            CreateBackendResult(run));

        Assert.Equal(expectedState, result.State);
        Assert.Equal(expectedOutcome, result.OutcomeCode);
        Assert.Equal(runState, result.ResultRunState);
        Assert.Equal(expectedSafeMessage, result.SafeMessage);
        Assert.DoesNotContain("Sensitive backend summary", result.SafeMessage, StringComparison.Ordinal);
        Assert.Null(result.NextExternalRequestId);
    }

    [Fact]
    public void FinalResult_WaitingBoundaryLinksExactlyOnePendingRequestAndCheckpoint()
    {
        var run = CreateRun(WorkflowRunState.WaitingForInput);
        var nextRequest = CreateRequest(run, WorkflowExternalRequestKind.HumanInput, "checkpoint-next");
        var checkpoint = CreateCheckpoint(run, nextRequest);

        var result = mapper.CreateFinalResult(
            WorkflowExternalResponseAction.SubmitInput,
            CreateBackendResult(run, [nextRequest], [checkpoint]));

        Assert.Equal(WorkflowExternalResponseOperationState.WaitingAgain, result.State);
        Assert.Equal(WorkflowExternalResponseOperationOutcomeCode.WaitingAgain, result.OutcomeCode);
        Assert.Equal(nextRequest.Id, result.NextExternalRequestId);
        Assert.Equal(checkpoint.Id, result.ResultCheckpointId);
        Assert.Equal(
            "The response was accepted and the workflow is waiting for another external request.",
            result.SafeMessage);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void FinalResult_MissingOrAmbiguousCheckpoint_FailsClosed(int checkpointCount)
    {
        var run = CreateRun(WorkflowRunState.WaitingForInput);
        var nextRequest = CreateRequest(run, WorkflowExternalRequestKind.HumanInput, "checkpoint-next");
        var checkpoints = Enumerable.Range(0, checkpointCount)
            .Select(_ => CreateCheckpoint(run, nextRequest))
            .ToArray();

        var exception = Assert.Throws<WorkflowBackendResumeException>(() =>
            mapper.CreateFinalResult(
                WorkflowExternalResponseAction.SubmitInput,
                CreateBackendResult(run, [nextRequest], checkpoints)));

        Assert.Equal(WorkflowBackendResumeFailureKind.CheckpointMissing, exception.Kind);
        Assert.Equal(
            "The next external request does not have exactly one matching available checkpoint.",
            exception.SafeMessage);
    }

    [Fact]
    public void FinalResult_ApproveAction_CompletesWithoutReparsingResponsePayload()
    {
        var run = CreateRun(WorkflowRunState.Completed);

        var result = mapper.CreateFinalResult(
            WorkflowExternalResponseAction.Approve,
            CreateBackendResult(run));

        Assert.Equal(WorkflowExternalResponseOperationState.Completed, result.State);
        Assert.Equal(WorkflowExternalResponseOperationOutcomeCode.Completed, result.OutcomeCode);
    }

    [Theory]
    [InlineData(WorkflowExternalResponseOperationState.WaitingAgain, WorkflowExternalResponseContinuationOutcome.WaitingAgain)]
    [InlineData(WorkflowExternalResponseOperationState.Completed, WorkflowExternalResponseContinuationOutcome.Completed)]
    [InlineData(WorkflowExternalResponseOperationState.Denied, WorkflowExternalResponseContinuationOutcome.Denied)]
    [InlineData(WorkflowExternalResponseOperationState.FailedTerminal, WorkflowExternalResponseContinuationOutcome.FailedTerminal)]
    [InlineData(WorkflowExternalResponseOperationState.Cancelled, WorkflowExternalResponseContinuationOutcome.Cancelled)]
    public void ContinuationOutcome_MapsStableOperationState(
        WorkflowExternalResponseOperationState state,
        WorkflowExternalResponseContinuationOutcome expectedOutcome)
        => Assert.Equal(expectedOutcome, mapper.MapContinuationOutcome(state));

    private static WorkflowRunSnapshot CreateRun(WorkflowRunState state)
        => new(
            WorkflowRunId.New(),
            WorkflowId.New(),
            WorkflowVersionId.New(),
            state,
            WorkflowRuntimeBackendKind.InProcess,
            "native-session",
            "Sensitive backend summary that must not become a safe message.",
            Now,
            Now)
        {
            TerminalAtUtc = state is WorkflowRunState.Completed or WorkflowRunState.Failed or WorkflowRunState.Cancelled
                ? Now
                : null
        };

    private static WorkflowExternalRequestRecord CreateRequest(
        WorkflowRunSnapshot run,
        WorkflowExternalRequestKind kind,
        string checkpointId = "checkpoint-current")
    {
        var requestId = WorkflowExternalRequestId.New();
        return new WorkflowExternalRequestRecord(
            requestId,
            run.RunId,
            kind,
            new WorkflowNodeId("human"),
            "human-input",
            "{}",
            string.Empty,
            Now,
            RespondedAtUtc: null)
        {
            Version = WorkflowExternalRequestVersion.Initial,
            State = WorkflowExternalRequestState.Pending,
            ResponseContract = new WorkflowExternalResponseContract(
                kind,
                $"test.{kind}",
                1,
                "{\"type\":\"object\"}",
                4_096),
            Continuation = new WorkflowExternalRequestContinuation(
                new WorkflowBackendExternalRequestLink(
                    requestId,
                    new WorkflowBackendRequestId($"request-{requestId.Value:N}"),
                    new WorkflowBackendRequestPortId("human-input")),
                new WorkflowBackendCheckpointLink(
                    new WorkflowBackendSessionId(run.BackendRunId),
                    new WorkflowBackendCheckpointId(checkpointId)),
                new WorkflowCompilerContractVersion(1),
                WorkflowTopologyFingerprint.Create("result-mapper-test"),
                WorkflowBackendCheckpointPayloadHash.Compute("{}"))
        };
    }

    private static WorkflowCheckpointRecord CreateCheckpoint(
        WorkflowRunSnapshot run,
        WorkflowExternalRequestRecord request)
    {
        var continuation = request.Continuation
            ?? throw new InvalidOperationException("The test request must contain native checkpoint linkage.");
        return new(
            WorkflowCheckpointId.New(),
            run.RunId,
            run.WorkflowId,
            run.VersionId,
            run.Backend,
            WorkflowCheckpointKind.WaitingForInput,
            WorkflowCheckpointTrustBoundary.TrustedRuntimeState,
            WorkflowResumeAvailability.Available,
            request.NodeId,
            request.Id,
            continuation.Checkpoint.CheckpointId.Value,
            "protected-payload-reference",
            continuation.CheckpointPayloadHash.Value,
            "Waiting for response.",
            string.Empty,
            Now,
            ResumedAtUtc: null);
    }

    private static WorkflowBackendStartResult CreateBackendResult(
        WorkflowRunSnapshot run,
        IReadOnlyList<WorkflowExternalRequestRecord>? requests = null,
        IReadOnlyList<WorkflowCheckpointRecord>? checkpoints = null)
        => new(
            run,
            Events: [],
            ExternalRequests: requests ?? [],
            Artifacts: [])
        {
            Checkpoints = checkpoints ?? []
        };
}
