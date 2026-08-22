using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;

namespace CanDoItAll.AgentFramework.Core;

internal sealed class WorkflowExternalResponseResultMapper
{
    public WorkflowExternalResponseContinuationResult Replay(
        WorkflowExternalResponseOperationRecord operation)
        => CreateResult(
            WorkflowExternalResponseContinuationOutcome.Replayed,
            operation,
            string.IsNullOrWhiteSpace(operation.SafeMessage)
                ? "The workflow external response operation has already reached a stable outcome."
                : operation.SafeMessage);

    public WorkflowExternalResponseContinuationResult ClaimFailure(
        WorkflowExternalResponseOperationClaimResult claim)
    {
        if (claim is
            {
                Outcome: WorkflowExternalResponseOperationClaimOutcome.AttemptLimitReached,
                Operation.State: WorkflowExternalResponseOperationState.FailedTerminal
            })
        {
            return CreateResult(
                WorkflowExternalResponseContinuationOutcome.FailedTerminal,
                claim.Operation,
                string.IsNullOrWhiteSpace(claim.Operation.SafeMessage)
                    ? "Workflow response recovery reached its retry-attempt limit."
                    : claim.Operation.SafeMessage);
        }

        return CreateResult(
            claim.Outcome == WorkflowExternalResponseOperationClaimOutcome.NotFound
                ? WorkflowExternalResponseContinuationOutcome.NotFound
                : WorkflowExternalResponseContinuationOutcome.ClaimConflict,
            claim.Operation,
            $"Workflow response operation claim did not succeed: {claim.Outcome}.");
    }

    public WorkflowExternalResponseContinuationResult MutationFailure(
        WorkflowExternalResponseOperationMutationResult mutation,
        WorkflowExternalResponseOperationRecord fallback)
        => CreateResult(
            mutation.Outcome == WorkflowExternalResponseOperationMutationOutcome.NotFound
                ? WorkflowExternalResponseContinuationOutcome.NotFound
                : WorkflowExternalResponseContinuationOutcome.ClaimConflict,
            mutation.Operation ?? fallback,
            $"Workflow response operation mutation did not succeed: {mutation.Outcome}.");

    public WorkflowExternalResponseContinuationResult CreateResult(
        WorkflowExternalResponseContinuationOutcome outcome,
        WorkflowExternalResponseOperationRecord? operation,
        string safeMessage)
        => new(
            outcome,
            operation,
            Run: null,
            NextRequest: null,
            safeMessage);

    public WorkflowExternalResponseOperationOutcomeCode MapBoundaryFailure(
        WorkflowResumeBoundaryLoadOutcome outcome)
        => outcome switch
        {
            WorkflowResumeBoundaryLoadOutcome.CheckpointMissing =>
                WorkflowExternalResponseOperationOutcomeCode.CheckpointMissing,
            WorkflowResumeBoundaryLoadOutcome.CheckpointCorrupt =>
                WorkflowExternalResponseOperationOutcomeCode.CheckpointCorrupt,
            WorkflowResumeBoundaryLoadOutcome.CheckpointIncompatible =>
                WorkflowExternalResponseOperationOutcomeCode.CheckpointIncompatible,
            WorkflowResumeBoundaryLoadOutcome.TopologyMismatch =>
                WorkflowExternalResponseOperationOutcomeCode.TopologyMismatch,
            WorkflowResumeBoundaryLoadOutcome.WorkflowVersionMismatch =>
                WorkflowExternalResponseOperationOutcomeCode.WorkflowVersionMismatch,
            WorkflowResumeBoundaryLoadOutcome.LegacyNonResumable =>
                WorkflowExternalResponseOperationOutcomeCode.CheckpointIncompatible,
            WorkflowResumeBoundaryLoadOutcome.LinkageMismatch =>
                WorkflowExternalResponseOperationOutcomeCode.RequestMismatch,
            WorkflowResumeBoundaryLoadOutcome.RequestNotFound =>
                WorkflowExternalResponseOperationOutcomeCode.RequestMismatch,
            _ => WorkflowExternalResponseOperationOutcomeCode.ResponseRejected
        };

    public WorkflowExternalResponseOperationFinalResult CreateFinalResult(
        WorkflowExternalResponseAction action,
        WorkflowBackendStartResult backendResult)
    {
        var state = backendResult.Run.State switch
        {
            WorkflowRunState.WaitingForInput => WorkflowExternalResponseOperationState.WaitingAgain,
            WorkflowRunState.Completed when action == WorkflowExternalResponseAction.Deny =>
                WorkflowExternalResponseOperationState.Denied,
            WorkflowRunState.Completed => WorkflowExternalResponseOperationState.Completed,
            WorkflowRunState.Cancelled => WorkflowExternalResponseOperationState.Cancelled,
            WorkflowRunState.Failed => WorkflowExternalResponseOperationState.FailedTerminal,
            _ => WorkflowExternalResponseOperationState.FailedRetryable
        };
        var outcome = state switch
        {
            WorkflowExternalResponseOperationState.WaitingAgain => WorkflowExternalResponseOperationOutcomeCode.WaitingAgain,
            WorkflowExternalResponseOperationState.Completed => WorkflowExternalResponseOperationOutcomeCode.Completed,
            WorkflowExternalResponseOperationState.Denied => WorkflowExternalResponseOperationOutcomeCode.Denied,
            WorkflowExternalResponseOperationState.Cancelled => WorkflowExternalResponseOperationOutcomeCode.Cancelled,
            WorkflowExternalResponseOperationState.FailedTerminal => WorkflowExternalResponseOperationOutcomeCode.ResumeFailed,
            _ => WorkflowExternalResponseOperationOutcomeCode.ResumeFailed
        };
        var pendingRequests = backendResult.ExternalRequests
            .Where(candidate => candidate.EffectiveState == WorkflowExternalRequestState.Pending)
            .ToArray();
        WorkflowExternalRequestRecord? nextRequest = null;
        WorkflowCheckpointRecord? checkpoint;
        if (backendResult.Run.State == WorkflowRunState.WaitingForInput)
        {
            if (pendingRequests.Length != 1)
            {
                throw new WorkflowBackendResumeException(
                    WorkflowBackendResumeFailureKind.ResponseMismatch,
                    "A waiting workflow response must produce exactly one pending external request.");
            }

            nextRequest = pendingRequests[0];
            checkpoint = FindAvailableCheckpoint(backendResult, nextRequest);
        }
        else
        {
            if (pendingRequests.Length != 0)
            {
                throw new WorkflowBackendResumeException(
                    WorkflowBackendResumeFailureKind.ResponseMismatch,
                    "A non-waiting workflow response cannot produce a pending external request.");
            }

            checkpoint = backendResult.Checkpoints
                .OrderByDescending(candidate => candidate.CreatedAtUtc)
                .ThenByDescending(candidate => candidate.Id.Value)
                .FirstOrDefault();
        }

        return new WorkflowExternalResponseOperationFinalResult(
            state,
            outcome,
            CreateSafeCompletionMessage(state),
            backendResult.Run.State)
        {
            ResultCheckpointId = checkpoint?.Id,
            NextExternalRequestId = nextRequest?.Id
        };
    }

    public WorkflowExternalResponseOperationOutcomeCode MapBackendResumeFailure(
        WorkflowBackendResumeFailureKind kind)
        => kind switch
        {
            WorkflowBackendResumeFailureKind.ExactWorkflowVersionMissing or
            WorkflowBackendResumeFailureKind.ExactWorkflowVersionMismatch =>
                WorkflowExternalResponseOperationOutcomeCode.WorkflowVersionMismatch,
            WorkflowBackendResumeFailureKind.TopologyMismatch =>
                WorkflowExternalResponseOperationOutcomeCode.TopologyMismatch,
            WorkflowBackendResumeFailureKind.CheckpointMissing =>
                WorkflowExternalResponseOperationOutcomeCode.CheckpointMissing,
            WorkflowBackendResumeFailureKind.CheckpointCorrupt =>
                WorkflowExternalResponseOperationOutcomeCode.CheckpointCorrupt,
            WorkflowBackendResumeFailureKind.RequestMismatch or
            WorkflowBackendResumeFailureKind.PortMismatch =>
                WorkflowExternalResponseOperationOutcomeCode.RequestMismatch,
            WorkflowBackendResumeFailureKind.ResponseMismatch =>
                WorkflowExternalResponseOperationOutcomeCode.ResponseRejected,
            WorkflowBackendResumeFailureKind.CompilationFailed or
            WorkflowBackendResumeFailureKind.CompilerContractMismatch or
            WorkflowBackendResumeFailureKind.CheckpointIncompatible =>
                WorkflowExternalResponseOperationOutcomeCode.CheckpointIncompatible,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };

    public WorkflowBackendStartResult CreateCancelledResult(
        WorkflowRunSnapshot run,
        DateTimeOffset cancelledAtUtc)
    {
        var cancelled = run with
        {
            State = WorkflowRunState.Cancelled,
            Summary = "Workflow response recovery was cancelled.",
            UpdatedAtUtc = cancelledAtUtc,
            TerminalAtUtc = cancelledAtUtc
        };
        return new WorkflowBackendStartResult(
            cancelled,
            [
                new WorkflowEventRecord(
                    Guid.NewGuid(),
                    run.RunId,
                    WorkflowEventKind.Cancelled,
                    NodeId: null,
                    "Workflow response recovery was cancelled.",
                    "{}",
                    cancelledAtUtc)
            ],
            ExternalRequests: [],
            Artifacts: []);
    }

    public WorkflowExternalResponseContinuationOutcome MapContinuationOutcome(
        WorkflowExternalResponseOperationState state)
        => state switch
        {
            WorkflowExternalResponseOperationState.WaitingAgain => WorkflowExternalResponseContinuationOutcome.WaitingAgain,
            WorkflowExternalResponseOperationState.Completed => WorkflowExternalResponseContinuationOutcome.Completed,
            WorkflowExternalResponseOperationState.Denied => WorkflowExternalResponseContinuationOutcome.Denied,
            WorkflowExternalResponseOperationState.FailedRetryable => WorkflowExternalResponseContinuationOutcome.FailedRetryable,
            WorkflowExternalResponseOperationState.FailedTerminal => WorkflowExternalResponseContinuationOutcome.FailedTerminal,
            WorkflowExternalResponseOperationState.Cancelled => WorkflowExternalResponseContinuationOutcome.Cancelled,
            _ => WorkflowExternalResponseContinuationOutcome.ClaimConflict
        };

    private static WorkflowCheckpointRecord FindAvailableCheckpoint(
        WorkflowBackendStartResult backendResult,
        WorkflowExternalRequestRecord nextRequest)
    {
        var continuation = nextRequest.Continuation;
        if (continuation is null)
        {
            throw new WorkflowBackendResumeException(
                WorkflowBackendResumeFailureKind.CheckpointMissing,
                "The next external request has no resumable checkpoint linkage.");
        }

        var checkpoints = backendResult.Checkpoints
            .Where(candidate =>
                candidate.RunId == backendResult.Run.RunId &&
                candidate.WorkflowId == backendResult.Run.WorkflowId &&
                candidate.VersionId == backendResult.Run.VersionId &&
                candidate.Backend == backendResult.Run.Backend &&
                candidate.ExternalRequestId == nextRequest.Id &&
                candidate.ResumeAvailability == WorkflowResumeAvailability.Available &&
                candidate.TrustBoundary == WorkflowCheckpointTrustBoundary.TrustedRuntimeState &&
                string.Equals(
                    candidate.BackendCheckpointId,
                    continuation.Checkpoint.CheckpointId.Value,
                    StringComparison.Ordinal) &&
                string.Equals(
                    candidate.PayloadHash,
                    continuation.CheckpointPayloadHash.Value,
                    StringComparison.Ordinal))
            .ToArray();
        if (checkpoints.Length != 1)
        {
            throw new WorkflowBackendResumeException(
                WorkflowBackendResumeFailureKind.CheckpointMissing,
                "The next external request does not have exactly one matching available checkpoint.");
        }

        return checkpoints[0];
    }

    private static string CreateSafeCompletionMessage(WorkflowExternalResponseOperationState state)
        => state switch
        {
            WorkflowExternalResponseOperationState.WaitingAgain => "The response was accepted and the workflow is waiting for another external request.",
            WorkflowExternalResponseOperationState.Completed => "The response was accepted and the workflow completed.",
            WorkflowExternalResponseOperationState.Denied => "The approval was denied and the governed executor was not invoked.",
            WorkflowExternalResponseOperationState.Cancelled => "Cancellation won while the response was being resumed.",
            WorkflowExternalResponseOperationState.FailedTerminal => "The workflow response failed with a terminal recovery outcome.",
            _ => "The workflow response did not reach a terminal boundary and remains recoverable."
        };
}
