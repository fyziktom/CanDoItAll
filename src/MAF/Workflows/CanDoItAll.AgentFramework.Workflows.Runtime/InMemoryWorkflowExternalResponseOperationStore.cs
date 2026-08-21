using System.Security.Cryptography;
using System.Text;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;

namespace CanDoItAll.AgentFramework.Core;

internal sealed record InMemoryWorkflowOperationCompletionPlan(
    WorkflowExternalResponseOperationRecord Operation);

public sealed class InMemoryWorkflowExternalResponseOperationStore(
    IWorkflowRunStore runStore,
    IWorkflowExternalRequestBoundaryStore boundaryStore) : IWorkflowExternalResponseOperationStore
{
    private readonly Lock gate = new();
    private readonly Dictionary<WorkflowExternalResponseOperationId, WorkflowExternalResponseOperationRecord> operations = [];
    private readonly Dictionary<(WorkflowExternalRequestId RequestId, WorkflowExternalResponseIdempotencyKeyHash KeyHash), WorkflowExternalResponseOperationId> operationsByKey = [];
    private readonly IWorkflowRunStore runStore = runStore ?? throw new ArgumentNullException(nameof(runStore));
    private readonly IWorkflowExternalRequestBoundaryStore boundaryStore = boundaryStore ?? throw new ArgumentNullException(nameof(boundaryStore));

    internal InMemoryWorkflowHitlMutationCoordinator MutationCoordinator { get; } =
        (boundaryStore as InMemoryWorkflowExternalRequestBoundaryStore)?.MutationCoordinator ??
        new InMemoryWorkflowHitlMutationCoordinator();

    internal IWorkflowRunStore RunStore => runStore;

    internal IWorkflowExternalRequestBoundaryStore BoundaryStore => boundaryStore;

    public async Task<WorkflowExternalResponseOperationCreateResult> CreateOrReplayAsync(
        WorkflowExternalResponseOperationCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var actualPayloadHash = Convert.ToHexStringLower(SHA256.HashData(
            Encoding.UTF8.GetBytes(request.Fingerprint.CanonicalPayload.Json)));
        if (!string.Equals(
                actualPayloadHash,
                request.Fingerprint.PayloadHash.Value,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Workflow external response fingerprint does not match its canonical payload.",
                nameof(request));
        }

        await MutationCoordinator.EnterAsync(cancellationToken);
        try
        {
            var operationKey = (request.RequestId, request.Fingerprint.IdempotencyKeyHash);
            lock (gate)
            {
                if (operationsByKey.TryGetValue(operationKey, out var existingId))
                {
                    var existing = operations[existingId];
                    if (existing.ExpectedRequestVersion != request.ExpectedRequestVersion ||
                        existing.ResponsePayloadHash != request.Fingerprint.PayloadHash ||
                        existing.ActorScopeFingerprint != request.Fingerprint.ActorScopeFingerprint)
                    {
                        return new WorkflowExternalResponseOperationCreateResult(
                            WorkflowExternalResponseOperationCreateOutcome.IdempotencyConflict,
                            existing);
                    }

                    return new WorkflowExternalResponseOperationCreateResult(
                        WorkflowExternalResponseOperationCreateOutcome.Replayed,
                        existing,
                        new WorkflowExternalResponseOperationReplay(
                            existing.Id,
                            existing.State,
                            existing.FinalResult,
                            request.AcceptedAtUtc));
                }
            }

            var externalRequest = await runStore.GetExternalRequestAsync(request.RequestId, cancellationToken);
            if (externalRequest is null)
            {
                return CreateFailure(WorkflowExternalResponseOperationCreateOutcome.RequestNotFound);
            }

            var run = await runStore.GetRunAsync(request.RunId, cancellationToken);
            if (run is null)
            {
                return CreateFailure(WorkflowExternalResponseOperationCreateOutcome.RunNotFound);
            }

            var boundary = await boundaryStore.ReadAsync(request.RequestId, cancellationToken);
            if (boundary.Outcome == WorkflowExternalRequestBoundaryReadOutcome.LegacyNonResumable)
            {
                return CreateFailure(WorkflowExternalResponseOperationCreateOutcome.LegacyNonResumable);
            }

            if (!boundary.Succeeded || boundary.Boundary is null)
            {
                return CreateFailure(WorkflowExternalResponseOperationCreateOutcome.RequestNotFound);
            }

            if (externalRequest.RunId != request.RunId || run.RunId != request.RunId)
            {
                return CreateFailure(WorkflowExternalResponseOperationCreateOutcome.RunNotFound);
            }

            if (externalRequest.Version != request.ExpectedRequestVersion ||
                boundary.Boundary.RequestVersion != request.ExpectedRequestVersion)
            {
                return CreateFailure(WorkflowExternalResponseOperationCreateOutcome.RequestVersionMismatch);
            }

            if (externalRequest.EffectiveState != WorkflowExternalRequestState.Pending ||
                boundary.Boundary.State != WorkflowExternalRequestState.Pending ||
                externalRequest.RespondedAtUtc.HasValue)
            {
                return CreateFailure(WorkflowExternalResponseOperationCreateOutcome.RequestNotPending);
            }

            if (run.State != WorkflowRunState.WaitingForInput)
            {
                return CreateFailure(WorkflowExternalResponseOperationCreateOutcome.RunNotWaiting);
            }

            lock (gate)
            {
                var active = operations.Values.FirstOrDefault(candidate =>
                    candidate.RequestId == request.RequestId &&
                    !WorkflowExternalResponseOperationTransitionRules.IsTerminal(candidate.State));
                if (active is not null)
                {
                    return new WorkflowExternalResponseOperationCreateResult(
                        WorkflowExternalResponseOperationCreateOutcome.ActiveOperationConflict,
                        active);
                }

                if (operations.ContainsKey(request.OperationId))
                {
                    throw new ArgumentException(
                        $"Workflow external response operation id '{request.OperationId}' already exists.",
                        nameof(request));
                }
            }

            var claimedBoundaryRecord = boundary.Boundary with
            {
                State = WorkflowExternalRequestState.ResponseClaimed
            };
            WorkflowExternalRequestBoundarySaveResult claimedBoundary;
            if (boundaryStore is InMemoryWorkflowExternalRequestBoundaryStore inMemoryBoundaryStore)
            {
                var preparation = inMemoryBoundaryStore.PrepareUpsert(
                    claimedBoundaryRecord,
                    externalRequest,
                    run);
                claimedBoundary = preparation.Result;
                if (preparation.Succeeded && preparation.Plan is not null)
                {
                    inMemoryBoundaryStore.ApplyUpsert(preparation.Plan);
                }
            }
            else
            {
                claimedBoundary = await boundaryStore.UpsertAsync(
                    claimedBoundaryRecord,
                    cancellationToken);
            }

            if (!claimedBoundary.Succeeded)
            {
                return CreateFailure(
                    claimedBoundary.Outcome == WorkflowExternalRequestBoundarySaveOutcome.RequestNotFound
                        ? WorkflowExternalResponseOperationCreateOutcome.RequestNotFound
                        : WorkflowExternalResponseOperationCreateOutcome.RequestVersionMismatch);
            }

            var operation = new WorkflowExternalResponseOperationRecord(
                request.OperationId,
                request.RequestId,
                request.RunId,
                request.ExpectedRequestVersion,
                request.Fingerprint.IdempotencyKeyHash,
                request.Fingerprint.PayloadHash,
                request.Fingerprint.ActorScopeFingerprint,
                request.Fingerprint.CanonicalPayload,
                request.Actor,
                request.CorrelationId,
                WorkflowExternalResponseOperationState.Accepted,
                Attempt: 0,
                WorkflowExternalResponseOperationConcurrencyVersion.Initial,
                request.AcceptedAtUtc);
            lock (gate)
            {
                operations.Add(operation.Id, operation);
                operationsByKey.Add(operationKey, operation.Id);
            }

            return new WorkflowExternalResponseOperationCreateResult(
                WorkflowExternalResponseOperationCreateOutcome.Created,
                operation);
        }
        finally
        {
            MutationCoordinator.Exit();
        }
    }

    public Task<WorkflowExternalResponseOperationRecord?> GetAsync(
        WorkflowExternalResponseOperationId operationId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            operations.TryGetValue(operationId, out var operation);
            return Task.FromResult(operation);
        }
    }

    public Task<IReadOnlyList<WorkflowExternalResponseOperationRecord>> ListRecoverableAsync(
        DateTimeOffset asOfUtc,
        int maximumCount,
        CancellationToken cancellationToken = default)
    {
        if (maximumCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumCount));
        }

        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            var recoverable = operations.Values
                .Where(operation => operation.State is
                        WorkflowExternalResponseOperationState.Accepted or
                        WorkflowExternalResponseOperationState.FailedRetryable ||
                    operation.State is WorkflowExternalResponseOperationState.Claimed or WorkflowExternalResponseOperationState.Resuming &&
                    (operation.Lease is null || operation.Lease.IsExpired(asOfUtc)))
                .OrderBy(operation => operation.AcceptedAtUtc)
                .ThenBy(operation => operation.Id.Value)
                .Take(maximumCount)
                .ToArray();
            return Task.FromResult<IReadOnlyList<WorkflowExternalResponseOperationRecord>>(recoverable);
        }
    }

    public async Task<WorkflowExternalResponseOperationClaimResult> TryClaimAsync(
        WorkflowExternalResponseOperationClaimRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.LeaseExpiresAtUtc <= request.ClaimedAtUtc)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                "Workflow external response lease must expire after it is claimed.");
        }

        if (request.MaximumAttempts <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request));
        }

        await MutationCoordinator.EnterAsync(cancellationToken);
        try
        {
            WorkflowExternalResponseOperationRecord? snapshot;
            lock (gate)
            {
                operations.TryGetValue(request.OperationId, out snapshot);
            }

            if (snapshot is null)
            {
                return new WorkflowExternalResponseOperationClaimResult(
                    WorkflowExternalResponseOperationClaimOutcome.NotFound,
                    Operation: null,
                    Claim: null);
            }

            var run = await runStore.GetRunAsync(snapshot.RunId, cancellationToken);
            var externalRequest = await runStore.GetExternalRequestAsync(snapshot.RequestId, cancellationToken);
            var boundary = await boundaryStore.ReadAsync(snapshot.RequestId, cancellationToken);
            lock (gate)
            {
                if (!operations.TryGetValue(request.OperationId, out var current))
                {
                    return new WorkflowExternalResponseOperationClaimResult(
                        WorkflowExternalResponseOperationClaimOutcome.NotFound,
                        Operation: null,
                        Claim: null);
                }

                if (current.ConcurrencyVersion != request.ExpectedVersion)
                {
                    return ClaimFailure(WorkflowExternalResponseOperationClaimOutcome.ConcurrencyConflict, current);
                }

                if (WorkflowExternalResponseOperationTransitionRules.IsTerminal(current.State) ||
                    current.State is not (
                        WorkflowExternalResponseOperationState.Accepted or
                        WorkflowExternalResponseOperationState.FailedRetryable or
                        WorkflowExternalResponseOperationState.Claimed or
                        WorkflowExternalResponseOperationState.Resuming))
                {
                    return ClaimFailure(WorkflowExternalResponseOperationClaimOutcome.InvalidState, current);
                }

                if (current.Lease is { } activeLease && !activeLease.IsExpired(request.ClaimedAtUtc))
                {
                    return ClaimFailure(WorkflowExternalResponseOperationClaimOutcome.ActiveLease, current);
                }

                if (run?.State != WorkflowRunState.WaitingForInput ||
                    externalRequest?.EffectiveState != WorkflowExternalRequestState.Pending ||
                    externalRequest.RespondedAtUtc.HasValue ||
                    externalRequest.Version != current.ExpectedRequestVersion ||
                    boundary.Boundary is not { State: WorkflowExternalRequestState.ResponseClaimed } requestBoundary ||
                    requestBoundary.RequestVersion != current.ExpectedRequestVersion)
                {
                    return ClaimFailure(WorkflowExternalResponseOperationClaimOutcome.InvalidState, current);
                }

                if (current.Attempt >= request.MaximumAttempts)
                {
                    var exhausted = CreateAttemptLimitReached(current, run.State, request.ClaimedAtUtc);
                    operations[current.Id] = exhausted;
                    return ClaimFailure(
                        WorkflowExternalResponseOperationClaimOutcome.AttemptLimitReached,
                        exhausted);
                }

                var competingLease = operations.Values.FirstOrDefault(candidate =>
                    candidate.Id != current.Id &&
                    candidate.RunId == current.RunId &&
                    candidate.State is (
                        WorkflowExternalResponseOperationState.Claimed or
                        WorkflowExternalResponseOperationState.Resuming) &&
                    candidate.Lease is { } lease &&
                    !lease.IsExpired(request.ClaimedAtUtc));
                if (competingLease is not null)
                {
                    return ClaimFailure(WorkflowExternalResponseOperationClaimOutcome.ActiveLease, current);
                }

                var recovery = current.State is WorkflowExternalResponseOperationState.Claimed or
                    WorkflowExternalResponseOperationState.Resuming
                    ? WorkflowExternalResponseOperationRecoveryRules.CreateExpiredLeaseRecovery(current.State)
                    : null;
                var epoch = current.Lease is null
                    ? new WorkflowExternalResponseLeaseEpoch(1)
                    : current.Lease.Epoch.Next();
                var lease = new WorkflowExternalResponseLease(
                    request.LeaseOwnerId,
                    epoch,
                    request.ClaimedAtUtc,
                    request.LeaseExpiresAtUtc);
                var claimed = current with
                {
                    State = WorkflowExternalResponseOperationState.Claimed,
                    Attempt = checked(current.Attempt + 1),
                    Lease = lease,
                    StartedAtUtc = null,
                    ConcurrencyVersion = current.ConcurrencyVersion.Next()
                };
                operations[current.Id] = claimed;
                return new WorkflowExternalResponseOperationClaimResult(
                    WorkflowExternalResponseOperationClaimOutcome.Claimed,
                    claimed,
                    new WorkflowExternalResponseOperationClaim(
                        claimed.Id,
                        lease,
                        claimed.Attempt,
                        claimed.ConcurrencyVersion)
                    {
                        Recovery = recovery
                    });
            }
        }
        finally
        {
            MutationCoordinator.Exit();
        }
    }

    public Task<WorkflowExternalResponseOperationMutationResult> TryRenewLeaseAsync(
        WorkflowExternalResponseOperationLeaseRenewalRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.LeaseExpiresAtUtc <= request.RenewedAtUtc)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                "Renewed workflow response lease must expire after the renewal time.");
        }

        return ExecuteMutationAsync(cancellationToken, () =>
        {
            lock (gate)
            {
                if (!TryValidateLease(
                        request.OperationId,
                        request.ExpectedVersion,
                        request.LeaseOwnerId,
                        request.LeaseEpoch,
                        request.RenewedAtUtc,
                        out var current,
                        out var failure))
                {
                    return failure;
                }

                var renewed = current! with
                {
                    Lease = new WorkflowExternalResponseLease(
                        request.LeaseOwnerId,
                        request.LeaseEpoch,
                        current.Lease!.AcquiredAtUtc,
                        request.LeaseExpiresAtUtc),
                    ConcurrencyVersion = current.ConcurrencyVersion.Next()
                };
                operations[renewed.Id] = renewed;
                return Updated(renewed);
            }
        });
    }

    public Task<WorkflowExternalResponseOperationMutationResult> TryMarkResumingAsync(
        WorkflowExternalResponseOperationMarkResumingRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ExecuteMutationAsync(cancellationToken, () =>
        {
            lock (gate)
            {
                if (!TryValidateLease(
                        request.OperationId,
                        request.ExpectedVersion,
                        request.LeaseOwnerId,
                        request.LeaseEpoch,
                        request.StartedAtUtc,
                        out var current,
                        out var failure))
                {
                    return failure;
                }

                if (current!.State != WorkflowExternalResponseOperationState.Claimed)
                {
                    return InvalidTransition(current);
                }

                var resuming = current with
                {
                    State = WorkflowExternalResponseOperationState.Resuming,
                    StartedAtUtc = request.StartedAtUtc,
                    ConcurrencyVersion = current.ConcurrencyVersion.Next()
                };
                operations[resuming.Id] = resuming;
                return Updated(resuming);
            }
        });
    }

    public Task<WorkflowExternalResponseOperationMutationResult> TryCompleteAsync(
        WorkflowExternalResponseOperationCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ExecuteMutationAsync(cancellationToken, () =>
        {
            var result = TryPrepareCompletion(request, out var plan);
            if (plan is null)
            {
                return result;
            }

            ApplyCompletion(plan);
            return result;
        });
    }

    internal WorkflowExternalResponseOperationMutationResult TryPrepareCompletion(
        WorkflowExternalResponseOperationCompletionRequest request,
        out InMemoryWorkflowOperationCompletionPlan? plan)
    {
        ArgumentNullException.ThrowIfNull(request);
        plan = null;
        lock (gate)
        {
            if (!TryValidateLease(
                    request.OperationId,
                    request.ExpectedVersion,
                    request.LeaseOwnerId,
                    request.LeaseEpoch,
                    request.CompletedAtUtc,
                    out var current,
                    out var failure))
            {
                return failure;
            }

            if (!WorkflowExternalResponseOperationTransitionRules.CanTransition(current!.State, request.FinalResult.State))
            {
                return InvalidTransition(current);
            }

            var completed = current with
            {
                State = request.FinalResult.State,
                Lease = null,
                CompletedAtUtc = request.CompletedAtUtc,
                OutcomeCode = request.FinalResult.OutcomeCode,
                SafeMessage = request.FinalResult.SafeMessage,
                FinalResult = request.FinalResult,
                ConcurrencyVersion = current.ConcurrencyVersion.Next()
            };
            plan = new InMemoryWorkflowOperationCompletionPlan(completed);
            return Updated(completed);
        }
    }

    internal void ApplyCompletion(InMemoryWorkflowOperationCompletionPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        lock (gate)
        {
            operations[plan.Operation.Id] = plan.Operation;
        }
    }

    public Task<WorkflowExternalResponseOperationMutationResult> TryFailAsync(
        WorkflowExternalResponseOperationFailureRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ExecuteMutationAsync(cancellationToken, () =>
        {
            lock (gate)
            {
                if (!TryValidateLease(
                        request.OperationId,
                        request.ExpectedVersion,
                        request.LeaseOwnerId,
                        request.LeaseEpoch,
                        request.FailedAtUtc,
                        out var current,
                        out var failure))
                {
                    return failure;
                }

                if (request.FailureState is not (
                        WorkflowExternalResponseOperationState.FailedRetryable or
                        WorkflowExternalResponseOperationState.FailedTerminal) ||
                    !WorkflowExternalResponseOperationTransitionRules.CanTransition(current!.State, request.FailureState))
                {
                    return InvalidTransition(current!);
                }

                var failed = current with
                {
                    State = request.FailureState,
                    Lease = null,
                    CompletedAtUtc = request.FailureState == WorkflowExternalResponseOperationState.FailedTerminal
                        ? request.FailedAtUtc
                        : null,
                    OutcomeCode = request.OutcomeCode,
                    SafeMessage = request.SafeMessage,
                    ConcurrencyVersion = current.ConcurrencyVersion.Next()
                };
                operations[failed.Id] = failed;
                return Updated(failed);
            }
        });
    }

    public Task<WorkflowExternalResponseOperationMutationResult> TryReleaseLeaseAsync(
        WorkflowExternalResponseOperationLeaseReleaseRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ExecuteMutationAsync(cancellationToken, () =>
        {
            lock (gate)
            {
                if (!TryValidateLease(
                        request.OperationId,
                        request.ExpectedVersion,
                        request.LeaseOwnerId,
                        request.LeaseEpoch,
                        request.ReleasedAtUtc,
                        out var current,
                        out var failure,
                        requireUnexpired: false))
                {
                    return failure;
                }

                var released = current! with
                {
                    Lease = null,
                    ConcurrencyVersion = current.ConcurrencyVersion.Next()
                };
                operations[released.Id] = released;
                return Updated(released);
            }
        });
    }

    internal InMemoryWorkflowBoundaryCancellationResult TryPrepareCancellationForBoundary(
        WorkflowExternalRequestId requestId,
        DateTimeOffset cancelledAtUtc,
        string safeReason,
        out InMemoryWorkflowBoundaryCancellationPlan? plan)
    {
        lock (gate)
        {
            var current = operations.Values
                .Where(operation => operation.RequestId == requestId)
                .OrderByDescending(operation => operation.AcceptedAtUtc)
                .FirstOrDefault();
            return InMemoryWorkflowExternalResponseCancellation.Prepare(
                current,
                cancelledAtUtc,
                safeReason,
                out plan);
        }
    }

    internal void ApplyBoundaryCancellation(InMemoryWorkflowBoundaryCancellationPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (plan.Operation is not { } operation)
        {
            return;
        }

        lock (gate)
        {
            operations[operation.Id] = operation;
        }
    }

    private bool TryValidateLease(
        WorkflowExternalResponseOperationId operationId,
        WorkflowExternalResponseOperationConcurrencyVersion expectedVersion,
        WorkflowExternalResponseLeaseOwnerId ownerId,
        WorkflowExternalResponseLeaseEpoch epoch,
        DateTimeOffset asOfUtc,
        out WorkflowExternalResponseOperationRecord? operation,
        out WorkflowExternalResponseOperationMutationResult failure,
        bool requireUnexpired = true)
    {
        if (!operations.TryGetValue(operationId, out operation))
        {
            failure = new WorkflowExternalResponseOperationMutationResult(
                WorkflowExternalResponseOperationMutationOutcome.NotFound,
                Operation: null);
            return false;
        }

        if (operation.ConcurrencyVersion != expectedVersion)
        {
            failure = new WorkflowExternalResponseOperationMutationResult(
                WorkflowExternalResponseOperationMutationOutcome.ConcurrencyConflict,
                operation);
            return false;
        }

        if (operation.Lease is not { } lease || lease.OwnerId != ownerId || lease.Epoch != epoch)
        {
            failure = new WorkflowExternalResponseOperationMutationResult(
                WorkflowExternalResponseOperationMutationOutcome.LeaseConflict,
                operation);
            return false;
        }

        if (requireUnexpired && lease.IsExpired(asOfUtc))
        {
            failure = new WorkflowExternalResponseOperationMutationResult(
                WorkflowExternalResponseOperationMutationOutcome.LeaseExpired,
                operation);
            return false;
        }

        failure = null!;
        return true;
    }

    private static WorkflowExternalResponseOperationCreateResult CreateFailure(
        WorkflowExternalResponseOperationCreateOutcome outcome)
        => new(outcome, Operation: null);

    private static WorkflowExternalResponseOperationClaimResult ClaimFailure(
        WorkflowExternalResponseOperationClaimOutcome outcome,
        WorkflowExternalResponseOperationRecord operation)
        => new(outcome, operation, Claim: null);

    private static WorkflowExternalResponseOperationMutationResult Updated(
        WorkflowExternalResponseOperationRecord operation)
        => new(WorkflowExternalResponseOperationMutationOutcome.Updated, operation);

    private static WorkflowExternalResponseOperationRecord CreateAttemptLimitReached(
        WorkflowExternalResponseOperationRecord operation,
        WorkflowRunState runState,
        DateTimeOffset completedAtUtc)
    {
        const string safeMessage = "Workflow response recovery reached its retry-attempt limit.";
        WorkflowExternalResponseOperationTransitionRules.ThrowIfInvalidTransition(
            operation.State,
            WorkflowExternalResponseOperationState.FailedTerminal);
        var finalResult = new WorkflowExternalResponseOperationFinalResult(
            WorkflowExternalResponseOperationState.FailedTerminal,
            WorkflowExternalResponseOperationOutcomeCode.AttemptLimitReached,
            safeMessage,
            runState);
        return operation with
        {
            State = WorkflowExternalResponseOperationState.FailedTerminal,
            Lease = null,
            CompletedAtUtc = completedAtUtc,
            OutcomeCode = WorkflowExternalResponseOperationOutcomeCode.AttemptLimitReached,
            SafeMessage = safeMessage,
            FinalResult = finalResult,
            ConcurrencyVersion = operation.ConcurrencyVersion.Next()
        };
    }

    private static WorkflowExternalResponseOperationMutationResult InvalidTransition(
        WorkflowExternalResponseOperationRecord operation)
        => new(WorkflowExternalResponseOperationMutationOutcome.InvalidTransition, operation);

    private async Task<T> ExecuteMutationAsync<T>(
        CancellationToken cancellationToken,
        Func<T> mutation)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        await MutationCoordinator.EnterAsync(cancellationToken);
        try
        {
            return mutation();
        }
        finally
        {
            MutationCoordinator.Exit();
        }
    }
}
