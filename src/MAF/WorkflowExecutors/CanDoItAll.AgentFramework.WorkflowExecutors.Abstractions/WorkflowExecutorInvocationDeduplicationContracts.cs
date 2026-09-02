using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public sealed record WorkflowExecutorInvocationClaimRequest(
    WorkflowExecutorInvocationIdentity Identity,
    WorkflowExecutorInvocationLeaseOwnerId LeaseOwnerId,
    DateTimeOffset ClaimedAtUtc,
    DateTimeOffset LeaseExpiresAtUtc,
    int MaximumAttempts);

public enum WorkflowExecutorInvocationClaimOutcome
{
    Claimed,
    ReplayedCompleted,
    ActiveLease,
    InputMismatch,
    AttemptLimitReached,
    FailedTerminal,
    ConcurrencyConflict
}

public sealed record WorkflowExecutorInvocationClaimResult(
    WorkflowExecutorInvocationClaimOutcome Outcome,
    WorkflowExecutorInvocationRecord? Record,
    WorkflowExecutorInvocationClaim? Claim)
{
    public bool Succeeded => Outcome is WorkflowExecutorInvocationClaimOutcome.Claimed or
        WorkflowExecutorInvocationClaimOutcome.ReplayedCompleted;
}

public sealed record WorkflowExecutorInvocationLeaseRenewalRequest(
    WorkflowExecutorInvocationKey Key,
    WorkflowExecutorInvocationConcurrencyVersion ExpectedVersion,
    WorkflowExecutorInvocationLeaseOwnerId LeaseOwnerId,
    WorkflowExecutorInvocationLeaseEpoch LeaseEpoch,
    DateTimeOffset RenewedAtUtc,
    DateTimeOffset LeaseExpiresAtUtc);

public sealed record WorkflowExecutorInvocationCompletionRequest(
    WorkflowExecutorInvocationKey Key,
    WorkflowExecutorInputHash ExpectedInputHash,
    WorkflowExecutorInvocationConcurrencyVersion ExpectedVersion,
    WorkflowExecutorInvocationLeaseOwnerId LeaseOwnerId,
    WorkflowExecutorInvocationLeaseEpoch LeaseEpoch,
    WorkflowExecutorInvocationStoredResult StoredResult);

public sealed record WorkflowExecutorInvocationFailureRequest(
    WorkflowExecutorInvocationKey Key,
    WorkflowExecutorInputHash ExpectedInputHash,
    WorkflowExecutorInvocationConcurrencyVersion ExpectedVersion,
    WorkflowExecutorInvocationLeaseOwnerId LeaseOwnerId,
    WorkflowExecutorInvocationLeaseEpoch LeaseEpoch,
    WorkflowExecutorInvocationState FailureState,
    WorkflowExecutorInvocationFailureCode FailureCode,
    string SafeMessage,
    DateTimeOffset FailedAtUtc);

public enum WorkflowExecutorInvocationMutationOutcome
{
    Updated,
    NotFound,
    ConcurrencyConflict,
    LeaseConflict,
    LeaseExpired,
    InputMismatch,
    InvalidState
}

public sealed record WorkflowExecutorInvocationMutationResult(
    WorkflowExecutorInvocationMutationOutcome Outcome,
    WorkflowExecutorInvocationRecord? Record)
{
    public bool Succeeded => Outcome == WorkflowExecutorInvocationMutationOutcome.Updated;
}

public interface IWorkflowExecutorInvocationDeduplicationStore
{
    Task<WorkflowExecutorInvocationClaimResult> TryClaimAsync(
        WorkflowExecutorInvocationClaimRequest request,
        CancellationToken cancellationToken = default);

    Task<WorkflowExecutorInvocationRecord?> GetAsync(
        WorkflowExecutorInvocationKey key,
        CancellationToken cancellationToken = default);

    Task<WorkflowExecutorInvocationMutationResult> TryRenewLeaseAsync(
        WorkflowExecutorInvocationLeaseRenewalRequest request,
        CancellationToken cancellationToken = default);

    Task<WorkflowExecutorInvocationMutationResult> TryCompleteAsync(
        WorkflowExecutorInvocationCompletionRequest request,
        CancellationToken cancellationToken = default);

    Task<WorkflowExecutorInvocationMutationResult> TryFailAsync(
        WorkflowExecutorInvocationFailureRequest request,
        CancellationToken cancellationToken = default);
}
