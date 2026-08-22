using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class WorkflowExternalResponseLeasePolicyTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-08-20T12:00:00Z");

    [Fact]
    public void EvaluateClaim_ActiveLease_ReturnsConflict()
    {
        var policy = CreatePolicy();
        var operation = CreateOperation(
            WorkflowExternalResponseOperationState.Claimed,
            attempt: 1,
            CreateLease(Now.AddMinutes(1)));

        var decision = policy.EvaluateClaim(operation);

        Assert.Equal(WorkflowExternalResponseLeaseClaimDecision.ActiveLease, decision);
    }

    [Theory]
    [InlineData(WorkflowExternalResponseOperationState.Claimed)]
    [InlineData(WorkflowExternalResponseOperationState.Resuming)]
    public void CreateClaim_ExpiredActiveLease_ReturnsAuditableTakeover(
        WorkflowExternalResponseOperationState state)
    {
        var policy = CreatePolicy();
        var operation = CreateOperation(state, attempt: 1, CreateLease(Now));

        var decision = policy.EvaluateClaim(operation);
        var claim = policy.CreateClaim(operation, new WorkflowExternalResponseLeaseOwnerId("replacement-host"));

        Assert.Equal(WorkflowExternalResponseLeaseClaimDecision.ExpiredLeaseTakeover, decision);
        Assert.Equal(2, claim.Attempt);
        Assert.Equal(2, claim.Lease.Epoch.Value);
        Assert.Equal("replacement-host", claim.Lease.OwnerId.Value);
        Assert.Equal(operation.ConcurrencyVersion.Next(), claim.ConcurrencyVersion);
        Assert.NotNull(claim.Recovery);
        Assert.Equal(state, claim.Recovery.PriorState);
    }

    [Fact]
    public void EvaluateClaim_MaximumAttemptsReached_RejectsClaim()
    {
        var policy = CreatePolicy(maximumAttempts: 3);
        var operation = CreateOperation(
            WorkflowExternalResponseOperationState.FailedRetryable,
            attempt: 3,
            lease: null);

        var decision = policy.EvaluateClaim(operation);

        Assert.Equal(WorkflowExternalResponseLeaseClaimDecision.AttemptLimitReached, decision);
        Assert.Throws<InvalidOperationException>(
            () => policy.CreateClaim(operation, new WorkflowExternalResponseLeaseOwnerId("host")));
    }

    [Fact]
    public void CreateClaim_AcceptedOperation_AssignsBoundedLeaseAndFirstEpoch()
    {
        var policy = CreatePolicy();
        var operation = CreateOperation(
            WorkflowExternalResponseOperationState.Accepted,
            attempt: 0,
            lease: null);

        var claim = policy.CreateClaim(operation, new WorkflowExternalResponseLeaseOwnerId("host"));

        Assert.Equal(1, claim.Attempt);
        Assert.Equal(1, claim.Lease.Epoch.Value);
        Assert.Equal(Now, claim.Lease.AcquiredAtUtc);
        Assert.Equal(Now.AddMinutes(2), claim.Lease.ExpiresAtUtc);
        Assert.Null(claim.Recovery);
    }

    [Theory]
    [InlineData("version", WorkflowExternalResponseLeaseValidationOutcome.ConcurrencyVersionMismatch)]
    [InlineData("owner", WorkflowExternalResponseLeaseValidationOutcome.OwnerMismatch)]
    [InlineData("epoch", WorkflowExternalResponseLeaseValidationOutcome.EpochMismatch)]
    public void ValidateLease_MismatchedGuard_FailsClosed(
        string mismatch,
        WorkflowExternalResponseLeaseValidationOutcome expected)
    {
        var policy = CreatePolicy();
        var operation = CreateOperation(
            WorkflowExternalResponseOperationState.Resuming,
            attempt: 1,
            CreateLease(Now.AddMinutes(1)));
        var version = mismatch == "version"
            ? operation.ConcurrencyVersion.Next()
            : operation.ConcurrencyVersion;
        var owner = mismatch == "owner"
            ? new WorkflowExternalResponseLeaseOwnerId("other-host")
            : operation.Lease!.OwnerId;
        var epoch = mismatch == "epoch"
            ? operation.Lease!.Epoch.Next()
            : operation.Lease!.Epoch;

        var outcome = policy.ValidateLease(operation, version, owner, epoch);

        Assert.Equal(expected, outcome);
    }

    [Fact]
    public void ValidateLease_ExpiryBoundary_IsExpired()
    {
        var policy = CreatePolicy();
        var operation = CreateOperation(
            WorkflowExternalResponseOperationState.Resuming,
            attempt: 1,
            CreateLease(Now));

        var outcome = policy.ValidateLease(
            operation,
            operation.ConcurrencyVersion,
            operation.Lease!.OwnerId,
            operation.Lease.Epoch);

        Assert.Equal(WorkflowExternalResponseLeaseValidationOutcome.Expired, outcome);
    }

    [Fact]
    public void RenewClaim_ValidLease_PreservesEpochAndAdvancesVersion()
    {
        var policy = CreatePolicy();
        var operation = CreateOperation(
            WorkflowExternalResponseOperationState.Resuming,
            attempt: 2,
            CreateLease(Now.AddMinutes(1)));

        var renewal = policy.RenewClaim(
            operation,
            operation.ConcurrencyVersion,
            operation.Lease!.OwnerId,
            operation.Lease.Epoch);

        Assert.Equal(operation.Lease.Epoch, renewal.Lease.Epoch);
        Assert.Equal(operation.Attempt, renewal.Attempt);
        Assert.Equal(operation.ConcurrencyVersion.Next(), renewal.ConcurrencyVersion);
        Assert.Equal(Now.AddMinutes(2), renewal.Lease.ExpiresAtUtc);
    }

    private static WorkflowExternalResponseLeasePolicy CreatePolicy(int maximumAttempts = 3)
        => new(new FixedTimeProvider(Now), TimeSpan.FromMinutes(2), maximumAttempts);

    private static WorkflowExternalResponseLease CreateLease(DateTimeOffset expiresAtUtc)
        => new(
            new WorkflowExternalResponseLeaseOwnerId("original-host"),
            new WorkflowExternalResponseLeaseEpoch(1),
            Now.AddMinutes(-1),
            expiresAtUtc);

    private static WorkflowExternalResponseOperationRecord CreateOperation(
        WorkflowExternalResponseOperationState state,
        int attempt,
        WorkflowExternalResponseLease? lease)
        => new(
            WorkflowExternalResponseOperationId.New(),
            WorkflowExternalRequestId.New(),
            WorkflowRunId.New(),
            WorkflowExternalRequestVersion.Initial,
            new WorkflowExternalResponseIdempotencyKeyHash(new string('a', 64)),
            new WorkflowExternalResponsePayloadHash(new string('b', 64)),
            new WorkflowExternalResponseActorScopeFingerprint(new string('c', 64)),
            new WorkflowExternalResponsePayload("{}"),
            new WorkflowLaunchActor(WorkflowLaunchActorKind.User, "lease-test"),
            new WorkflowLaunchCorrelationId("lease-test"),
            state,
            attempt,
            new WorkflowExternalResponseOperationConcurrencyVersion(7),
            Now.AddMinutes(-2))
        {
            Lease = lease
        };

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
