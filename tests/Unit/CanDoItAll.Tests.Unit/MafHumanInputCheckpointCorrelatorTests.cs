using CanDoItAll.AgentFramework.Maf;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class MafHumanInputCheckpointCorrelatorTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Completion_correlates_one_request_and_one_usable_checkpoint_in_either_event_order(
        bool requestFirst)
    {
        var correlator = new MafHumanInputCheckpointCorrelator();
        var request = Request("session-a", "request-a", "input-a");
        var checkpoint = Checkpoint("session-a", "checkpoint-a", hasPendingRequest: true);

        if (requestFirst)
        {
            correlator.ObserveRequest(request);
            correlator.ObserveCheckpoint(checkpoint);
        }
        else
        {
            correlator.ObserveCheckpoint(checkpoint);
            correlator.ObserveRequest(request);
        }

        var result = correlator.CompleteBoundary(MafWorkflowStreamCompletionKind.Completed);

        Assert.Equal(MafHumanInputCheckpointCorrelationStatus.Correlated, result.Status);
        Assert.Equal(MafWorkflowStreamCompletionKind.Completed, result.CompletionKind);
        Assert.Equal(new MafHumanInputCheckpointBoundary(request, checkpoint), result.Boundary);
        Assert.Null(result.FailureKind);
    }

    [Fact]
    public void Completion_emits_a_correlated_boundary_exactly_once()
    {
        var correlator = CreateCompleteCorrelation();

        var first = correlator.CompleteBoundary(MafWorkflowStreamCompletionKind.Completed);
        var second = correlator.CompleteBoundary(MafWorkflowStreamCompletionKind.Completed);

        Assert.Equal(MafHumanInputCheckpointCorrelationStatus.Correlated, first.Status);
        Assert.Equal(MafHumanInputCheckpointCorrelationStatus.Rejected, second.Status);
        Assert.Equal(
            MafHumanInputCheckpointCorrelationFailureKind.BoundaryAlreadyFinalized,
            second.FailureKind);
        Assert.Null(second.Boundary);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Boundary_with_only_a_checkpoint_reports_the_missing_request_and_stream_outcome(
        bool faulted)
    {
        var correlator = new MafHumanInputCheckpointCorrelator();
        correlator.ObserveCheckpoint(Checkpoint("session-a", "checkpoint-a", hasPendingRequest: true));
        var completionKind = faulted
            ? MafWorkflowStreamCompletionKind.Faulted
            : MafWorkflowStreamCompletionKind.Completed;

        var result = correlator.CompleteBoundary(completionKind);

        AssertRejected(
            result,
            completionKind,
            MafHumanInputCheckpointCorrelationFailureKind.MissingRequest);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Boundary_with_only_a_request_reports_the_missing_checkpoint_and_stream_outcome(
        bool faulted)
    {
        var correlator = new MafHumanInputCheckpointCorrelator();
        correlator.ObserveRequest(Request("session-a", "request-a", "input-a"));
        var completionKind = faulted
            ? MafWorkflowStreamCompletionKind.Faulted
            : MafWorkflowStreamCompletionKind.Completed;

        var result = correlator.CompleteBoundary(completionKind);

        AssertRejected(
            result,
            completionKind,
            MafHumanInputCheckpointCorrelationFailureKind.MissingCheckpoint);
    }

    [Fact]
    public void Completion_rejects_a_request_and_checkpoint_from_different_sessions()
    {
        var correlator = new MafHumanInputCheckpointCorrelator();
        correlator.ObserveRequest(Request("session-a", "request-a", "input-a"));
        correlator.ObserveCheckpoint(Checkpoint("session-b", "checkpoint-a", hasPendingRequest: true));

        var result = correlator.CompleteBoundary(MafWorkflowStreamCompletionKind.Completed);

        AssertRejected(
            result,
            MafWorkflowStreamCompletionKind.Completed,
            MafHumanInputCheckpointCorrelationFailureKind.SessionMismatch);
    }

    [Fact]
    public void Completion_rejects_an_exact_duplicate_request()
    {
        var correlator = new MafHumanInputCheckpointCorrelator();
        var request = Request("session-a", "request-a", "input-a");
        correlator.ObserveRequest(request);
        correlator.ObserveRequest(request);
        correlator.ObserveCheckpoint(Checkpoint("session-a", "checkpoint-a", hasPendingRequest: true));

        var result = correlator.CompleteBoundary(MafWorkflowStreamCompletionKind.Completed);

        AssertRejected(
            result,
            MafWorkflowStreamCompletionKind.Completed,
            MafHumanInputCheckpointCorrelationFailureKind.DuplicateRequest);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Completion_rejects_conflicting_versions_of_the_same_native_request_regardless_of_order(
        bool originalFirst)
    {
        var correlator = new MafHumanInputCheckpointCorrelator();
        var original = Request("session-a", "request-a", "input-a");
        var conflicting = Request("session-a", "request-a", "input-b");
        correlator.ObserveRequest(originalFirst ? original : conflicting);
        correlator.ObserveRequest(originalFirst ? conflicting : original);
        correlator.ObserveCheckpoint(Checkpoint("session-a", "checkpoint-a", hasPendingRequest: true));

        var result = correlator.CompleteBoundary(MafWorkflowStreamCompletionKind.Completed);

        AssertRejected(
            result,
            MafWorkflowStreamCompletionKind.Completed,
            MafHumanInputCheckpointCorrelationFailureKind.ConflictingRequest);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Completion_fails_closed_for_multiple_pending_requests_regardless_of_order(bool firstRequestFirst)
    {
        var correlator = new MafHumanInputCheckpointCorrelator();
        var first = Request("session-a", "request-a", "input-a");
        var second = Request("session-a", "request-b", "input-b");
        correlator.ObserveRequest(firstRequestFirst ? first : second);
        correlator.ObserveRequest(firstRequestFirst ? second : first);
        correlator.ObserveCheckpoint(Checkpoint("session-a", "checkpoint-a", hasPendingRequest: true));

        var result = correlator.CompleteBoundary(MafWorkflowStreamCompletionKind.Completed);

        AssertRejected(
            result,
            MafWorkflowStreamCompletionKind.Completed,
            MafHumanInputCheckpointCorrelationFailureKind.MultiplePendingRequests);
    }

    [Fact]
    public void Completion_rejects_an_exact_duplicate_checkpoint()
    {
        var correlator = new MafHumanInputCheckpointCorrelator();
        var checkpoint = Checkpoint("session-a", "checkpoint-a", hasPendingRequest: true);
        correlator.ObserveRequest(Request("session-a", "request-a", "input-a"));
        correlator.ObserveCheckpoint(checkpoint);
        correlator.ObserveCheckpoint(checkpoint);

        var result = correlator.CompleteBoundary(MafWorkflowStreamCompletionKind.Completed);

        AssertRejected(
            result,
            MafWorkflowStreamCompletionKind.Completed,
            MafHumanInputCheckpointCorrelationFailureKind.DuplicateCheckpoint);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Completion_rejects_conflicting_checkpoints_regardless_of_order(bool firstCheckpointFirst)
    {
        var correlator = new MafHumanInputCheckpointCorrelator();
        var first = Checkpoint("session-a", "checkpoint-a", hasPendingRequest: true);
        var second = Checkpoint("session-a", "checkpoint-b", hasPendingRequest: true);
        correlator.ObserveRequest(Request("session-a", "request-a", "input-a"));
        correlator.ObserveCheckpoint(firstCheckpointFirst ? first : second);
        correlator.ObserveCheckpoint(firstCheckpointFirst ? second : first);

        var result = correlator.CompleteBoundary(MafWorkflowStreamCompletionKind.Completed);

        AssertRejected(
            result,
            MafWorkflowStreamCompletionKind.Completed,
            MafHumanInputCheckpointCorrelationFailureKind.ConflictingCheckpoint);
    }

    [Fact]
    public void Completion_rejects_a_checkpoint_without_a_pending_request()
    {
        var correlator = new MafHumanInputCheckpointCorrelator();
        correlator.ObserveRequest(Request("session-a", "request-a", "input-a"));
        correlator.ObserveCheckpoint(Checkpoint("session-a", "checkpoint-a", hasPendingRequest: false));

        var result = correlator.CompleteBoundary(MafWorkflowStreamCompletionKind.Completed);

        AssertRejected(
            result,
            MafWorkflowStreamCompletionKind.Completed,
            MafHumanInputCheckpointCorrelationFailureKind.CheckpointNotUsable);
    }

    [Fact]
    public void Faulted_stream_does_not_emit_an_otherwise_complete_boundary()
    {
        var correlator = CreateCompleteCorrelation();

        var result = correlator.CompleteBoundary(MafWorkflowStreamCompletionKind.Faulted);

        AssertRejected(
            result,
            MafWorkflowStreamCompletionKind.Faulted,
            MafHumanInputCheckpointCorrelationFailureKind.StreamFaulted);
    }

    [Fact]
    public void Reset_after_finalization_supports_consecutive_turns_without_leaking_prior_facts()
    {
        var correlator = CreateCompleteCorrelation();
        var first = correlator.CompleteBoundary(MafWorkflowStreamCompletionKind.Completed);

        correlator.Reset();
        var secondRequest = Request("session-b", "request-b", "input-b");
        var secondCheckpoint = Checkpoint("session-b", "checkpoint-b", hasPendingRequest: true);
        correlator.ObserveCheckpoint(secondCheckpoint);
        correlator.ObserveRequest(secondRequest);
        var second = correlator.CompleteBoundary(MafWorkflowStreamCompletionKind.Completed);

        Assert.Equal(MafHumanInputCheckpointCorrelationStatus.Correlated, first.Status);
        Assert.Equal(MafHumanInputCheckpointCorrelationStatus.Correlated, second.Status);
        Assert.Equal(new MafHumanInputCheckpointBoundary(secondRequest, secondCheckpoint), second.Boundary);
    }

    [Fact]
    public void Reset_while_a_correlation_is_pending_throws_a_typed_failure()
    {
        var correlator = new MafHumanInputCheckpointCorrelator();
        correlator.ObserveRequest(Request("session-a", "request-a", "input-a"));

        var exception = Assert.Throws<MafHumanInputCheckpointCorrelationException>(correlator.Reset);

        Assert.Equal(
            MafHumanInputCheckpointCorrelationFailureKind.ResetWhileCorrelationPending,
            exception.FailureKind);
    }

    [Fact]
    public void Completion_without_observations_reports_no_pending_boundary()
    {
        var result = new MafHumanInputCheckpointCorrelator()
            .CompleteBoundary(MafWorkflowStreamCompletionKind.Completed);

        Assert.Equal(MafHumanInputCheckpointCorrelationStatus.NoPendingBoundary, result.Status);
        Assert.Null(result.Boundary);
        Assert.Null(result.FailureKind);
    }

    [Fact]
    public void Fault_without_observations_reports_a_typed_stream_failure()
    {
        var result = new MafHumanInputCheckpointCorrelator()
            .CompleteBoundary(MafWorkflowStreamCompletionKind.Faulted);

        AssertRejected(
            result,
            MafWorkflowStreamCompletionKind.Faulted,
            MafHumanInputCheckpointCorrelationFailureKind.StreamFaulted);
    }

    private static MafHumanInputCheckpointCorrelator CreateCompleteCorrelation()
    {
        var correlator = new MafHumanInputCheckpointCorrelator();
        correlator.ObserveRequest(Request("session-a", "request-a", "input-a"));
        correlator.ObserveCheckpoint(Checkpoint("session-a", "checkpoint-a", hasPendingRequest: true));
        return correlator;
    }

    private static MafHumanInputRequestFact Request(string sessionId, string requestId, string portId)
    {
        return new(
            new MafWorkflowSessionId(sessionId),
            new MafNativeRequestId(requestId),
            new MafRequestPortId(portId));
    }

    private static MafWorkflowCheckpointFact Checkpoint(
        string sessionId,
        string checkpointId,
        bool hasPendingRequest)
    {
        return new(
            new MafWorkflowSessionId(sessionId),
            new MafCheckpointId(checkpointId),
            hasPendingRequest);
    }

    private static void AssertRejected(
        MafHumanInputCheckpointCorrelationResult result,
        MafWorkflowStreamCompletionKind completionKind,
        MafHumanInputCheckpointCorrelationFailureKind failureKind)
    {
        Assert.Equal(MafHumanInputCheckpointCorrelationStatus.Rejected, result.Status);
        Assert.Equal(completionKind, result.CompletionKind);
        Assert.Equal(failureKind, result.FailureKind);
        Assert.Null(result.Boundary);
    }
}
