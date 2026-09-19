namespace CanDoItAll.AgentFramework.Models;

/// <summary>
/// Stored state of an attempt to answer an external request, as a JSON integer: 0 Accepted, 1 Claimed, 2 Resuming
/// (processing has not finished), 3 WaitingAgain (the run waits for another request), 4 Completed, 5 Denied,
/// 6 FailedRetryable (resend the submission with the same key to continue), 7 FailedTerminal, 8 Cancelled.
/// </summary>
public enum WorkflowExternalResponseOperationState
{
    Accepted,
    Claimed,
    Resuming,
    WaitingAgain,
    Completed,
    Denied,
    FailedRetryable,
    FailedTerminal,
    Cancelled
}

/// <summary>
/// Detailed result code of an attempt to answer an external request, as a JSON integer: 0 None (no final result yet),
/// 1 WaitingAgain, 2 Completed, 3 Denied, 4 Cancelled, 5 BackendUnavailable, 6 ResumeFailed, 7 CheckpointMissing,
/// 8 CheckpointCorrupt, 9 CheckpointIncompatible, 10 TopologyMismatch, 11 WorkflowVersionMismatch,
/// 12 RequestMismatch, 13 ResponseRejected, 14 AttemptLimitReached.
/// </summary>
public enum WorkflowExternalResponseOperationOutcomeCode
{
    None,
    WaitingAgain,
    Completed,
    Denied,
    Cancelled,
    BackendUnavailable,
    ResumeFailed,
    CheckpointMissing,
    CheckpointCorrupt,
    CheckpointIncompatible,
    TopologyMismatch,
    WorkflowVersionMismatch,
    RequestMismatch,
    ResponseRejected,
    AttemptLimitReached
}

public sealed record WorkflowExternalResponseLease(
    WorkflowExternalResponseLeaseOwnerId OwnerId,
    WorkflowExternalResponseLeaseEpoch Epoch,
    DateTimeOffset AcquiredAtUtc,
    DateTimeOffset ExpiresAtUtc)
{
    public bool IsExpired(DateTimeOffset nowUtc) => ExpiresAtUtc <= nowUtc;
}

public sealed record WorkflowExternalResponseOperationFinalResult(
    WorkflowExternalResponseOperationState State,
    WorkflowExternalResponseOperationOutcomeCode OutcomeCode,
    string SafeMessage,
    WorkflowRunState ResultRunState)
{
    public WorkflowCheckpointId? ResultCheckpointId { get; init; }

    public WorkflowExternalRequestId? NextExternalRequestId { get; init; }
}

public sealed record WorkflowExternalResponseOperationRecord(
    WorkflowExternalResponseOperationId Id,
    WorkflowExternalRequestId RequestId,
    WorkflowRunId RunId,
    WorkflowExternalRequestVersion ExpectedRequestVersion,
    WorkflowExternalResponseIdempotencyKeyHash IdempotencyKeyHash,
    WorkflowExternalResponsePayloadHash ResponsePayloadHash,
    WorkflowExternalResponseActorScopeFingerprint ActorScopeFingerprint,
    WorkflowExternalResponsePayload ResponsePayload,
    WorkflowLaunchActor Actor,
    WorkflowLaunchCorrelationId CorrelationId,
    WorkflowExternalResponseOperationState State,
    int Attempt,
    WorkflowExternalResponseOperationConcurrencyVersion ConcurrencyVersion,
    DateTimeOffset AcceptedAtUtc)
{
    public WorkflowExternalResponseLease? Lease { get; init; }

    public DateTimeOffset? StartedAtUtc { get; init; }

    public DateTimeOffset? CompletedAtUtc { get; init; }

    public WorkflowExternalResponseOperationOutcomeCode OutcomeCode { get; init; }

    public string SafeMessage { get; init; } = string.Empty;

    public WorkflowExternalResponseOperationFinalResult? FinalResult { get; init; }
}

public sealed record WorkflowExternalResponseOperationClaim(
    WorkflowExternalResponseOperationId OperationId,
    WorkflowExternalResponseLease Lease,
    int Attempt,
    WorkflowExternalResponseOperationConcurrencyVersion ConcurrencyVersion)
{
    public WorkflowExternalResponseExpiredLeaseRecovery? Recovery { get; init; }
}

public sealed record WorkflowExternalResponseExpiredLeaseRecovery(
    WorkflowExternalResponseOperationState PriorState,
    IReadOnlyList<WorkflowExternalResponseOperationState> TransitionPath);

public sealed record WorkflowExternalResponseOperationReplay(
    WorkflowExternalResponseOperationId OperationId,
    WorkflowExternalResponseOperationState State,
    WorkflowExternalResponseOperationFinalResult? FinalResult,
    DateTimeOffset ReplayedAtUtc);

public sealed record WorkflowExternalResponseFingerprint(
    WorkflowExternalResponseIdempotencyKeyHash IdempotencyKeyHash,
    WorkflowExternalResponsePayloadHash PayloadHash,
    WorkflowExternalResponseActorScopeFingerprint ActorScopeFingerprint,
    WorkflowExternalResponsePayload CanonicalPayload);
