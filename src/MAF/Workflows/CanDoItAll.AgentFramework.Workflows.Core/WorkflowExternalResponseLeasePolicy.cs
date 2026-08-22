using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public enum WorkflowExternalResponseLeaseClaimDecision
{
    Allowed,
    ExpiredLeaseTakeover,
    ActiveLease,
    AttemptLimitReached,
    TerminalState,
    StateNotClaimable
}

public enum WorkflowExternalResponseLeaseValidationOutcome
{
    Valid,
    MissingLease,
    ConcurrencyVersionMismatch,
    OwnerMismatch,
    EpochMismatch,
    Expired,
    InvalidState
}

public sealed class WorkflowExternalResponseLeasePolicy
{
    private readonly TimeProvider timeProvider;
    private readonly TimeSpan leaseDuration;
    private readonly int maximumAttempts;

    public WorkflowExternalResponseLeasePolicy(
        TimeProvider timeProvider,
        TimeSpan leaseDuration,
        int maximumAttempts)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        if (leaseDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(leaseDuration));
        }

        if (maximumAttempts <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumAttempts));
        }

        this.timeProvider = timeProvider;
        this.leaseDuration = leaseDuration;
        this.maximumAttempts = maximumAttempts;
    }

    public WorkflowExternalResponseLeaseClaimDecision EvaluateClaim(
        WorkflowExternalResponseOperationRecord operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (WorkflowExternalResponseOperationTransitionRules.IsTerminal(operation.State))
        {
            return WorkflowExternalResponseLeaseClaimDecision.TerminalState;
        }

        if (operation.State is not (
                WorkflowExternalResponseOperationState.Accepted or
                WorkflowExternalResponseOperationState.FailedRetryable or
                WorkflowExternalResponseOperationState.Claimed or
                WorkflowExternalResponseOperationState.Resuming))
        {
            return WorkflowExternalResponseLeaseClaimDecision.StateNotClaimable;
        }

        if (operation.Attempt >= maximumAttempts)
        {
            return WorkflowExternalResponseLeaseClaimDecision.AttemptLimitReached;
        }

        if (operation.State is WorkflowExternalResponseOperationState.Claimed or
            WorkflowExternalResponseOperationState.Resuming)
        {
            return operation.Lease is { } lease && !lease.IsExpired(timeProvider.GetUtcNow())
                ? WorkflowExternalResponseLeaseClaimDecision.ActiveLease
                : WorkflowExternalResponseLeaseClaimDecision.ExpiredLeaseTakeover;
        }

        return operation.Lease is { } unexpectedLease && !unexpectedLease.IsExpired(timeProvider.GetUtcNow())
            ? WorkflowExternalResponseLeaseClaimDecision.ActiveLease
            : WorkflowExternalResponseLeaseClaimDecision.Allowed;
    }

    public WorkflowExternalResponseOperationClaim CreateClaim(
        WorkflowExternalResponseOperationRecord operation,
        WorkflowExternalResponseLeaseOwnerId ownerId)
    {
        var decision = EvaluateClaim(operation);
        if (decision is not (
                WorkflowExternalResponseLeaseClaimDecision.Allowed or
                WorkflowExternalResponseLeaseClaimDecision.ExpiredLeaseTakeover))
        {
            throw new InvalidOperationException(
                $"Workflow external response operation '{operation.Id}' cannot be claimed: {decision}.");
        }

        var now = timeProvider.GetUtcNow();
        var epoch = operation.Lease is null
            ? new WorkflowExternalResponseLeaseEpoch(1)
            : operation.Lease.Epoch.Next();
        var lease = new WorkflowExternalResponseLease(ownerId, epoch, now, now.Add(leaseDuration));
        return new WorkflowExternalResponseOperationClaim(
            operation.Id,
            lease,
            checked(operation.Attempt + 1),
            operation.ConcurrencyVersion.Next())
        {
            Recovery = decision == WorkflowExternalResponseLeaseClaimDecision.ExpiredLeaseTakeover
                ? WorkflowExternalResponseOperationRecoveryRules.CreateExpiredLeaseRecovery(operation.State)
                : null
        };
    }

    public WorkflowExternalResponseLeaseValidationOutcome ValidateLease(
        WorkflowExternalResponseOperationRecord operation,
        WorkflowExternalResponseOperationConcurrencyVersion expectedVersion,
        WorkflowExternalResponseLeaseOwnerId ownerId,
        WorkflowExternalResponseLeaseEpoch epoch)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (operation.State is not (
                WorkflowExternalResponseOperationState.Claimed or
                WorkflowExternalResponseOperationState.Resuming))
        {
            return WorkflowExternalResponseLeaseValidationOutcome.InvalidState;
        }

        if (operation.ConcurrencyVersion != expectedVersion)
        {
            return WorkflowExternalResponseLeaseValidationOutcome.ConcurrencyVersionMismatch;
        }

        if (operation.Lease is not { } lease)
        {
            return WorkflowExternalResponseLeaseValidationOutcome.MissingLease;
        }

        if (lease.OwnerId != ownerId)
        {
            return WorkflowExternalResponseLeaseValidationOutcome.OwnerMismatch;
        }

        if (lease.Epoch != epoch)
        {
            return WorkflowExternalResponseLeaseValidationOutcome.EpochMismatch;
        }

        return lease.IsExpired(timeProvider.GetUtcNow())
            ? WorkflowExternalResponseLeaseValidationOutcome.Expired
            : WorkflowExternalResponseLeaseValidationOutcome.Valid;
    }

    public WorkflowExternalResponseOperationClaim RenewClaim(
        WorkflowExternalResponseOperationRecord operation,
        WorkflowExternalResponseOperationConcurrencyVersion expectedVersion,
        WorkflowExternalResponseLeaseOwnerId ownerId,
        WorkflowExternalResponseLeaseEpoch epoch)
    {
        var validation = ValidateLease(operation, expectedVersion, ownerId, epoch);
        if (validation != WorkflowExternalResponseLeaseValidationOutcome.Valid)
        {
            throw new InvalidOperationException(
                $"Workflow external response operation '{operation.Id}' lease cannot be renewed: {validation}.");
        }

        var now = timeProvider.GetUtcNow();
        return new WorkflowExternalResponseOperationClaim(
            operation.Id,
            new WorkflowExternalResponseLease(ownerId, epoch, now, now.Add(leaseDuration)),
            operation.Attempt,
            operation.ConcurrencyVersion.Next());
    }
}
