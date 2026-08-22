using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;

namespace CanDoItAll.AgentFramework.Core;

internal sealed class WorkflowExternalResponseServiceResultMapper
{
    public WorkflowExternalResponseServiceResult AuthorizationFailure(
        WorkflowExternalRequestAuthorizationDecision authorization,
        WorkflowRunSnapshot run,
        WorkflowExternalRequestRecord request)
    {
        var outcome = authorization.Outcome switch
        {
            WorkflowExternalRequestAuthorizationOutcome.Unauthenticated =>
                WorkflowExternalResponseServiceOutcome.Unauthenticated,
            WorkflowExternalRequestAuthorizationOutcome.AuthorizationContextUnavailable =>
                WorkflowExternalResponseServiceOutcome.AuthorizationContextUnavailable,
            _ => WorkflowExternalResponseServiceOutcome.Forbidden
        };
        return Failure(outcome, authorization.SafeMessage, run, request);
    }

    public WorkflowExternalResponseServiceResult ValidationFailure(
        WorkflowExternalResponseValidationResult validation,
        WorkflowRunSnapshot run,
        WorkflowExternalRequestRecord request)
    {
        var outcome = validation.Outcome switch
        {
            WorkflowExternalResponseValidationOutcome.RequestVersionMismatch =>
                WorkflowExternalResponseServiceOutcome.RequestVersionMismatch,
            WorkflowExternalResponseValidationOutcome.BoundaryMismatch =>
                WorkflowExternalResponseServiceOutcome.RequestMismatch,
            _ => WorkflowExternalResponseServiceOutcome.InvalidResponse
        };
        return Failure(outcome, validation.SafeMessage, run, request);
    }

    public WorkflowExternalResponseServiceResult CreationFailure(
        WorkflowExternalResponseOperationCreateResult creation,
        WorkflowRunSnapshot run,
        WorkflowExternalRequestRecord request)
    {
        var outcome = creation.Outcome switch
        {
            WorkflowExternalResponseOperationCreateOutcome.IdempotencyConflict =>
                WorkflowExternalResponseServiceOutcome.IdempotencyConflict,
            WorkflowExternalResponseOperationCreateOutcome.ActiveOperationConflict =>
                WorkflowExternalResponseServiceOutcome.ActiveOperationConflict,
            WorkflowExternalResponseOperationCreateOutcome.RequestNotFound =>
                WorkflowExternalResponseServiceOutcome.RequestNotFound,
            WorkflowExternalResponseOperationCreateOutcome.RunNotFound =>
                WorkflowExternalResponseServiceOutcome.RunNotFound,
            WorkflowExternalResponseOperationCreateOutcome.RequestVersionMismatch =>
                WorkflowExternalResponseServiceOutcome.RequestVersionMismatch,
            WorkflowExternalResponseOperationCreateOutcome.RunNotWaiting =>
                WorkflowExternalResponseServiceOutcome.RunNotWaiting,
            WorkflowExternalResponseOperationCreateOutcome.LegacyNonResumable =>
                WorkflowExternalResponseServiceOutcome.LegacyNonResumable,
            WorkflowExternalResponseOperationCreateOutcome.RequestNotPending
                when request.EffectiveState == WorkflowExternalRequestState.Cancelled =>
                    WorkflowExternalResponseServiceOutcome.Cancelled,
            WorkflowExternalResponseOperationCreateOutcome.RequestNotPending
                when request.EffectiveState == WorkflowExternalRequestState.Superseded =>
                    WorkflowExternalResponseServiceOutcome.Superseded,
            WorkflowExternalResponseOperationCreateOutcome.RequestNotPending =>
                WorkflowExternalResponseServiceOutcome.RequestNotPending,
            _ => WorkflowExternalResponseServiceOutcome.TerminalFailure
        };
        return Failure(
            outcome,
            $"The workflow response operation was not accepted: {creation.Outcome}.",
            run,
            request,
            creation.Operation);
    }

    public WorkflowExternalResponseServiceResult ContinuationFailure(
        WorkflowExternalResponseContinuationResult result,
        WorkflowRunSnapshot fallbackRun,
        WorkflowExternalRequestRecord request)
    {
        var outcome = result.Outcome switch
        {
            WorkflowExternalResponseContinuationOutcome.NotFound =>
                WorkflowExternalResponseServiceOutcome.OperationNotFound,
            WorkflowExternalResponseContinuationOutcome.ClaimConflict =>
                WorkflowExternalResponseServiceOutcome.ActiveOperationConflict,
            WorkflowExternalResponseContinuationOutcome.Cancelled =>
                WorkflowExternalResponseServiceOutcome.Cancelled,
            WorkflowExternalResponseContinuationOutcome.FailedRetryable =>
                WorkflowExternalResponseServiceOutcome.RetryableFailure,
            WorkflowExternalResponseContinuationOutcome.FailedTerminal =>
                WorkflowExternalResponseServiceOutcome.TerminalFailure,
            _ => WorkflowExternalResponseServiceOutcome.TerminalFailure
        };
        return Failure(
            outcome,
            result.SafeMessage,
            result.Run ?? fallbackRun,
            request,
            result.Operation);
    }

    public WorkflowExternalResponseServiceResult MapOperation(
        WorkflowExternalResponseOperationRecord operation,
        WorkflowRunSnapshot run,
        WorkflowExternalRequestRecord request,
        WorkflowExternalRequestRecord? nextRequest,
        bool replayed,
        string? safeMessage)
    {
        var outcome = MapOperationOutcome(operation);
        return new WorkflowExternalResponseServiceResult(
            outcome,
            operation,
            run,
            request,
            nextRequest,
            replayed,
            string.IsNullOrWhiteSpace(safeMessage)
                ? CreateSafeStatusMessage(outcome)
                : safeMessage);
    }

    public WorkflowExternalResponseServiceResult Failure(
        WorkflowExternalResponseServiceOutcome outcome,
        string safeMessage,
        WorkflowRunSnapshot? run = null,
        WorkflowExternalRequestRecord? request = null,
        WorkflowExternalResponseOperationRecord? operation = null)
        => new(
            outcome,
            operation,
            run,
            request,
            NextRequest: null,
            Replayed: false,
            safeMessage);

    private static WorkflowExternalResponseServiceOutcome MapOperationOutcome(
        WorkflowExternalResponseOperationRecord operation)
        => operation.State switch
        {
            WorkflowExternalResponseOperationState.Accepted or
            WorkflowExternalResponseOperationState.Claimed or
            WorkflowExternalResponseOperationState.Resuming =>
                WorkflowExternalResponseServiceOutcome.Resuming,
            WorkflowExternalResponseOperationState.WaitingAgain =>
                WorkflowExternalResponseServiceOutcome.WaitingAgain,
            WorkflowExternalResponseOperationState.Completed =>
                WorkflowExternalResponseServiceOutcome.Completed,
            WorkflowExternalResponseOperationState.Denied =>
                WorkflowExternalResponseServiceOutcome.Denied,
            WorkflowExternalResponseOperationState.Cancelled =>
                WorkflowExternalResponseServiceOutcome.Cancelled,
            WorkflowExternalResponseOperationState.FailedRetryable
                when operation.OutcomeCode == WorkflowExternalResponseOperationOutcomeCode.BackendUnavailable =>
                    WorkflowExternalResponseServiceOutcome.BackendUnavailable,
            WorkflowExternalResponseOperationState.FailedRetryable =>
                WorkflowExternalResponseServiceOutcome.RetryableFailure,
            WorkflowExternalResponseOperationState.FailedTerminal =>
                MapTerminalOutcome(operation.OutcomeCode),
            _ => WorkflowExternalResponseServiceOutcome.TerminalFailure
        };

    private static WorkflowExternalResponseServiceOutcome MapTerminalOutcome(
        WorkflowExternalResponseOperationOutcomeCode outcome)
        => outcome switch
        {
            WorkflowExternalResponseOperationOutcomeCode.CheckpointMissing =>
                WorkflowExternalResponseServiceOutcome.CheckpointMissing,
            WorkflowExternalResponseOperationOutcomeCode.CheckpointCorrupt =>
                WorkflowExternalResponseServiceOutcome.CheckpointCorrupt,
            WorkflowExternalResponseOperationOutcomeCode.CheckpointIncompatible =>
                WorkflowExternalResponseServiceOutcome.CheckpointIncompatible,
            WorkflowExternalResponseOperationOutcomeCode.TopologyMismatch =>
                WorkflowExternalResponseServiceOutcome.TopologyMismatch,
            WorkflowExternalResponseOperationOutcomeCode.WorkflowVersionMismatch =>
                WorkflowExternalResponseServiceOutcome.WorkflowVersionMismatch,
            WorkflowExternalResponseOperationOutcomeCode.RequestMismatch or
            WorkflowExternalResponseOperationOutcomeCode.ResponseRejected =>
                WorkflowExternalResponseServiceOutcome.RequestMismatch,
            WorkflowExternalResponseOperationOutcomeCode.BackendUnavailable =>
                WorkflowExternalResponseServiceOutcome.BackendUnavailable,
            WorkflowExternalResponseOperationOutcomeCode.Cancelled =>
                WorkflowExternalResponseServiceOutcome.Cancelled,
            _ => WorkflowExternalResponseServiceOutcome.TerminalFailure
        };

    private static string CreateSafeStatusMessage(WorkflowExternalResponseServiceOutcome outcome)
        => outcome switch
        {
            WorkflowExternalResponseServiceOutcome.Completed =>
                "The response was accepted and the workflow completed.",
            WorkflowExternalResponseServiceOutcome.WaitingAgain =>
                "The response was accepted and the workflow is waiting for another external request.",
            WorkflowExternalResponseServiceOutcome.Denied =>
                "The approval was denied and the governed executor was not invoked.",
            WorkflowExternalResponseServiceOutcome.Resuming =>
                "The response operation is being resumed.",
            WorkflowExternalResponseServiceOutcome.Cancelled =>
                "The workflow response operation was cancelled.",
            _ => "The workflow response operation reached a stable outcome."
        };
}
