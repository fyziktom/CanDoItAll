namespace CanDoItAll.AgentFramework.Models;

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
