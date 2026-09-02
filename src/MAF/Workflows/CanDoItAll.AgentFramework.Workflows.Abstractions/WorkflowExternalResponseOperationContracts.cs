using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Workflows.Abstractions;

public sealed record WorkflowExternalResponseOperationCreateRequest(
    WorkflowExternalResponseOperationId OperationId,
    WorkflowExternalRequestId RequestId,
    WorkflowRunId RunId,
    WorkflowExternalRequestVersion ExpectedRequestVersion,
    WorkflowExternalResponseFingerprint Fingerprint,
    WorkflowLaunchActor Actor,
    WorkflowLaunchCorrelationId CorrelationId,
    DateTimeOffset AcceptedAtUtc);

public enum WorkflowExternalResponseOperationCreateOutcome
{
    Created,
    Replayed,
    IdempotencyConflict,
    ActiveOperationConflict,
    RequestNotFound,
    RunNotFound,
    RequestNotPending,
    RunNotWaiting,
    RequestVersionMismatch,
    LegacyNonResumable
}

public sealed record WorkflowExternalResponseOperationCreateResult(
    WorkflowExternalResponseOperationCreateOutcome Outcome,
    WorkflowExternalResponseOperationRecord? Operation,
    WorkflowExternalResponseOperationReplay? Replay = null)
{
    public bool Succeeded => Outcome is WorkflowExternalResponseOperationCreateOutcome.Created or
        WorkflowExternalResponseOperationCreateOutcome.Replayed;
}

public sealed record WorkflowExternalResponseOperationClaimRequest(
    WorkflowExternalResponseOperationId OperationId,
    WorkflowExternalResponseOperationConcurrencyVersion ExpectedVersion,
    WorkflowExternalResponseLeaseOwnerId LeaseOwnerId,
    DateTimeOffset ClaimedAtUtc,
    DateTimeOffset LeaseExpiresAtUtc,
    int MaximumAttempts);

public enum WorkflowExternalResponseOperationClaimOutcome
{
    Claimed,
    NotFound,
    ConcurrencyConflict,
    ActiveLease,
    AttemptLimitReached,
    InvalidState
}

public sealed record WorkflowExternalResponseOperationClaimResult(
    WorkflowExternalResponseOperationClaimOutcome Outcome,
    WorkflowExternalResponseOperationRecord? Operation,
    WorkflowExternalResponseOperationClaim? Claim)
{
    public bool Succeeded => Outcome == WorkflowExternalResponseOperationClaimOutcome.Claimed;
}

public sealed record WorkflowExternalResponseOperationLeaseRenewalRequest(
    WorkflowExternalResponseOperationId OperationId,
    WorkflowExternalResponseOperationConcurrencyVersion ExpectedVersion,
    WorkflowExternalResponseLeaseOwnerId LeaseOwnerId,
    WorkflowExternalResponseLeaseEpoch LeaseEpoch,
    DateTimeOffset RenewedAtUtc,
    DateTimeOffset LeaseExpiresAtUtc);

public sealed record WorkflowExternalResponseOperationMarkResumingRequest(
    WorkflowExternalResponseOperationId OperationId,
    WorkflowExternalResponseOperationConcurrencyVersion ExpectedVersion,
    WorkflowExternalResponseLeaseOwnerId LeaseOwnerId,
    WorkflowExternalResponseLeaseEpoch LeaseEpoch,
    DateTimeOffset StartedAtUtc);

public sealed record WorkflowExternalResponseOperationCompletionRequest(
    WorkflowExternalResponseOperationId OperationId,
    WorkflowExternalResponseOperationConcurrencyVersion ExpectedVersion,
    WorkflowExternalResponseLeaseOwnerId LeaseOwnerId,
    WorkflowExternalResponseLeaseEpoch LeaseEpoch,
    WorkflowExternalResponseOperationFinalResult FinalResult,
    DateTimeOffset CompletedAtUtc);

public sealed record WorkflowExternalResponseOperationFailureRequest(
    WorkflowExternalResponseOperationId OperationId,
    WorkflowExternalResponseOperationConcurrencyVersion ExpectedVersion,
    WorkflowExternalResponseLeaseOwnerId LeaseOwnerId,
    WorkflowExternalResponseLeaseEpoch LeaseEpoch,
    WorkflowExternalResponseOperationState FailureState,
    WorkflowExternalResponseOperationOutcomeCode OutcomeCode,
    string SafeMessage,
    DateTimeOffset FailedAtUtc);

public sealed record WorkflowExternalResponseOperationLeaseReleaseRequest(
    WorkflowExternalResponseOperationId OperationId,
    WorkflowExternalResponseOperationConcurrencyVersion ExpectedVersion,
    WorkflowExternalResponseLeaseOwnerId LeaseOwnerId,
    WorkflowExternalResponseLeaseEpoch LeaseEpoch,
    DateTimeOffset ReleasedAtUtc);

public enum WorkflowExternalResponseOperationMutationOutcome
{
    Updated,
    NotFound,
    ConcurrencyConflict,
    LeaseConflict,
    LeaseExpired,
    InvalidTransition
}

public sealed record WorkflowExternalResponseOperationMutationResult(
    WorkflowExternalResponseOperationMutationOutcome Outcome,
    WorkflowExternalResponseOperationRecord? Operation)
{
    public bool Succeeded => Outcome == WorkflowExternalResponseOperationMutationOutcome.Updated;
}

public interface IWorkflowExternalResponseOperationStore
{
    Task<WorkflowExternalResponseOperationCreateResult> CreateOrReplayAsync(
        WorkflowExternalResponseOperationCreateRequest request,
        CancellationToken cancellationToken = default);

    Task<WorkflowExternalResponseOperationRecord?> GetAsync(
        WorkflowExternalResponseOperationId operationId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkflowExternalResponseOperationRecord>> ListRecoverableAsync(
        DateTimeOffset asOfUtc,
        int maximumCount,
        CancellationToken cancellationToken = default);

    Task<WorkflowExternalResponseOperationClaimResult> TryClaimAsync(
        WorkflowExternalResponseOperationClaimRequest request,
        CancellationToken cancellationToken = default);

    Task<WorkflowExternalResponseOperationMutationResult> TryRenewLeaseAsync(
        WorkflowExternalResponseOperationLeaseRenewalRequest request,
        CancellationToken cancellationToken = default);

    Task<WorkflowExternalResponseOperationMutationResult> TryMarkResumingAsync(
        WorkflowExternalResponseOperationMarkResumingRequest request,
        CancellationToken cancellationToken = default);

    Task<WorkflowExternalResponseOperationMutationResult> TryCompleteAsync(
        WorkflowExternalResponseOperationCompletionRequest request,
        CancellationToken cancellationToken = default);

    Task<WorkflowExternalResponseOperationMutationResult> TryFailAsync(
        WorkflowExternalResponseOperationFailureRequest request,
        CancellationToken cancellationToken = default);

    Task<WorkflowExternalResponseOperationMutationResult> TryReleaseLeaseAsync(
        WorkflowExternalResponseOperationLeaseReleaseRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record WorkflowExternalResponseContinuationRequest(
    WorkflowExternalResponseOperationId OperationId,
    WorkflowExternalResponseLeaseOwnerId LeaseOwnerId);

public enum WorkflowExternalResponseContinuationOutcome
{
    WaitingAgain,
    Completed,
    Denied,
    FailedRetryable,
    FailedTerminal,
    Cancelled,
    Replayed,
    ClaimConflict,
    NotFound
}

public sealed record WorkflowExternalResponseContinuationResult(
    WorkflowExternalResponseContinuationOutcome Outcome,
    WorkflowExternalResponseOperationRecord? Operation,
    WorkflowRunSnapshot? Run,
    WorkflowExternalRequestRecord? NextRequest,
    string SafeMessage);

public interface IWorkflowExternalResponseContinuation
{
    Task<WorkflowExternalResponseContinuationResult> ContinueAsync(
        WorkflowExternalResponseContinuationRequest request,
        CancellationToken cancellationToken = default);
}
