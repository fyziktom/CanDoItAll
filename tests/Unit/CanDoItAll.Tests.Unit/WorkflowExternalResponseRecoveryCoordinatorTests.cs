using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class WorkflowExternalResponseRecoveryCoordinatorTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-08-21T12:00:00Z");

    [Fact]
    public async Task RecoverAsync_ListsABoundedBatchAndContinuesEveryOperation()
    {
        var operations = new[] { CreateOperation(1), CreateOperation(2) };
        var store = new RecordingOperationStore(operations);
        var continuation = new RecordingContinuation();
        var coordinator = new WorkflowExternalResponseRecoveryCoordinator(
            store,
            continuation,
            new FixedTimeProvider(Now));

        var results = await coordinator.RecoverAsync(maximumCount: 2);

        Assert.Equal(Now, store.AsOfUtc);
        Assert.Equal(2, store.MaximumCount);
        Assert.Equal(operations.Select(operation => operation.Id), continuation.OperationIds);
        Assert.Single(continuation.LeaseOwnerIds.Distinct());
        Assert.Equal(2, results.Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(129)]
    public async Task RecoverAsync_RejectsAnUnboundedBatchBeforeStoreAccess(int maximumCount)
    {
        var store = new RecordingOperationStore([]);
        var coordinator = new WorkflowExternalResponseRecoveryCoordinator(
            store,
            new RecordingContinuation(),
            new FixedTimeProvider(Now));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => coordinator.RecoverAsync(maximumCount));

        Assert.Null(store.AsOfUtc);
    }

    private static WorkflowExternalResponseOperationRecord CreateOperation(int index)
        => new(
            WorkflowExternalResponseOperationId.New(),
            WorkflowExternalRequestId.New(),
            WorkflowRunId.New(),
            WorkflowExternalRequestVersion.Initial,
            new WorkflowExternalResponseIdempotencyKeyHash(new string((char)('0' + index), 64)),
            new WorkflowExternalResponsePayloadHash(new string((char)('2' + index), 64)),
            new WorkflowExternalResponseActorScopeFingerprint(new string((char)('4' + index), 64)),
            new WorkflowExternalResponsePayload("{}"),
            new WorkflowLaunchActor(WorkflowLaunchActorKind.User, $"recovery-user-{index}"),
            new WorkflowLaunchCorrelationId($"recovery-{index}"),
            WorkflowExternalResponseOperationState.Accepted,
            Attempt: 0,
            WorkflowExternalResponseOperationConcurrencyVersion.Initial,
            Now.AddMinutes(-index));

    private sealed class RecordingOperationStore(
        IReadOnlyList<WorkflowExternalResponseOperationRecord> operations) :
        IWorkflowExternalResponseOperationStore
    {
        public DateTimeOffset? AsOfUtc { get; private set; }

        public int? MaximumCount { get; private set; }

        public Task<IReadOnlyList<WorkflowExternalResponseOperationRecord>> ListRecoverableAsync(
            DateTimeOffset asOfUtc,
            int maximumCount,
            CancellationToken cancellationToken = default)
        {
            AsOfUtc = asOfUtc;
            MaximumCount = maximumCount;
            return Task.FromResult(operations);
        }

        public Task<WorkflowExternalResponseOperationRecord?> GetAsync(
            WorkflowExternalResponseOperationId operationId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<WorkflowExternalResponseOperationCreateResult> CreateOrReplayAsync(
            WorkflowExternalResponseOperationCreateRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<WorkflowExternalResponseOperationClaimResult> TryClaimAsync(
            WorkflowExternalResponseOperationClaimRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<WorkflowExternalResponseOperationMutationResult> TryRenewLeaseAsync(
            WorkflowExternalResponseOperationLeaseRenewalRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<WorkflowExternalResponseOperationMutationResult> TryMarkResumingAsync(
            WorkflowExternalResponseOperationMarkResumingRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<WorkflowExternalResponseOperationMutationResult> TryCompleteAsync(
            WorkflowExternalResponseOperationCompletionRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<WorkflowExternalResponseOperationMutationResult> TryFailAsync(
            WorkflowExternalResponseOperationFailureRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<WorkflowExternalResponseOperationMutationResult> TryReleaseLeaseAsync(
            WorkflowExternalResponseOperationLeaseReleaseRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class RecordingContinuation : IWorkflowExternalResponseContinuation
    {
        public List<WorkflowExternalResponseOperationId> OperationIds { get; } = [];

        public List<WorkflowExternalResponseLeaseOwnerId> LeaseOwnerIds { get; } = [];

        public Task<WorkflowExternalResponseContinuationResult> ContinueAsync(
            WorkflowExternalResponseContinuationRequest request,
            CancellationToken cancellationToken = default)
        {
            OperationIds.Add(request.OperationId);
            LeaseOwnerIds.Add(request.LeaseOwnerId);
            return Task.FromResult(new WorkflowExternalResponseContinuationResult(
                WorkflowExternalResponseContinuationOutcome.Completed,
                Operation: null,
                Run: null,
                NextRequest: null,
                "Recovered."));
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
