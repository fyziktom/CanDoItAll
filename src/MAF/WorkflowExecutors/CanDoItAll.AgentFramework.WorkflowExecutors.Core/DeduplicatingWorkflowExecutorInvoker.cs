using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public sealed class DeduplicatingWorkflowExecutorInvoker(
    WorkflowExecutorInvoker inner,
    IWorkflowExecutorCatalog catalog,
    IWorkflowExecutorInvocationDeduplicationStore store,
    TimeProvider? timeProvider = null) : IWorkflowExecutorInvoker
{
    private readonly WorkflowExecutorInvoker inner = inner ?? throw new ArgumentNullException(nameof(inner));
    private readonly IWorkflowExecutorCatalog catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    private readonly IWorkflowExecutorInvocationDeduplicationStore store =
        store ?? throw new ArgumentNullException(nameof(store));
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;

    public ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
        WorkflowDefinition definition,
        WorkflowNode node,
        WorkflowNodeInput input,
        CancellationToken cancellationToken = default)
        => ExecuteAsync(
            definition,
            node,
            input,
            WorkflowExecutorInvocationContext.Empty,
            cancellationToken);

    public async ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
        WorkflowDefinition definition,
        WorkflowNode node,
        WorkflowNodeInput input,
        WorkflowExecutorInvocationContext invocationContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(invocationContext);

        if (!TryCreateIdentity(
                definition,
                node,
                input,
                invocationContext,
                out var descriptor,
                out var identity))
        {
            return await inner.ExecuteAsync(
                definition,
                node,
                input,
                invocationContext,
                cancellationToken);
        }

        ValidateReplayAuthorization(definition, node, descriptor, input, invocationContext);
        var now = clock.GetUtcNow();
        var leaseDuration = WorkflowExecutorInvocationDeduplicationPolicy.ResolveLeaseDuration(
            node.Settings.ExecutionPolicy ?? descriptor.DefaultPolicy);
        var claimResult = await store.TryClaimAsync(
            new WorkflowExecutorInvocationClaimRequest(
                identity,
                new WorkflowExecutorInvocationLeaseOwnerId(Guid.NewGuid().ToString("N")),
                now,
                now + leaseDuration,
                WorkflowExecutorInvocationDeduplicationPolicy.MaximumAttempts),
            cancellationToken);

        if (claimResult.Outcome == WorkflowExecutorInvocationClaimOutcome.ReplayedCompleted)
        {
            return GetReplayResult(identity, descriptor, claimResult.Record);
        }

        if (claimResult.Outcome != WorkflowExecutorInvocationClaimOutcome.Claimed ||
            claimResult.Claim is null)
        {
            throw CreateClaimFailure(identity.Key, claimResult.Outcome);
        }

        var claimedContext = invocationContext with
        {
            IdempotencyKey = identity.IdempotencyKey
        };
        await using var heartbeat = new WorkflowExecutorInvocationLeaseHeartbeatSession(
            store,
            clock,
            leaseDuration,
            WorkflowExecutorInvocationDeduplicationPolicy.ResolveLeaseRenewalInterval(leaseDuration),
            claimResult.Claim);
        using var executionSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            heartbeat.LeaseLostToken);

        try
        {
            var result = await inner.ExecuteAsync(
                definition,
                node,
                input,
                claimedContext,
                executionSource.Token);
            await heartbeat.StopAsync();
            ThrowIfLeaseLost(identity.Key, heartbeat.Failure);
            var currentClaim = heartbeat.CurrentClaim;
            if (!WorkflowExecutorInvocationDeduplicationPolicy.CanPersistResult(descriptor, result))
            {
                await FailAsync(
                    currentClaim,
                    WorkflowExecutorInvocationState.FailedTerminal,
                    WorkflowExecutorInvocationFailureCode.UnsafeResultNotPersisted,
                    "The governed executor result is not safe for durable replay.",
                    CancellationToken.None);
                throw CreateFailure(
                    identity.Key,
                    "The governed executor completed, but its result was not safe for durable replay. The invocation is terminal and will not be repeated.");
            }

            var completedAtUtc = clock.GetUtcNow();
            var completion = await store.TryCompleteAsync(
                new WorkflowExecutorInvocationCompletionRequest(
                    identity.Key,
                    identity.InputHash,
                    currentClaim.ConcurrencyVersion,
                    currentClaim.Lease.OwnerId,
                    currentClaim.Lease.Epoch,
                    new WorkflowExecutorInvocationStoredResult(result, completedAtUtc)),
                CancellationToken.None);
            if (!completion.Succeeded)
            {
                throw CreateMutationFailure(
                    identity.Key,
                    completion.Outcome,
                    "The governed executor completed, but its durable deduplication result could not be finalized.");
            }

            return result;
        }
        catch (OperationCanceledException exception)
        {
            await heartbeat.StopAsync();
            if (heartbeat.Failure is { } leaseFailure)
            {
                throw CreateLeaseFailure(identity.Key, exception, leaseFailure);
            }

            try
            {
                await FailAsync(
                    heartbeat.CurrentClaim,
                    WorkflowExecutorInvocationState.FailedRetryable,
                    WorkflowExecutorInvocationFailureCode.Cancelled,
                    "The governed executor invocation was cancelled.",
                    CancellationToken.None);
            }
            catch (WorkflowExecutorInvocationDeduplicationException persistenceException)
            {
                throw new WorkflowExecutorInvocationDeduplicationException(
                    identity.Key,
                    "The governed executor was cancelled, and its retryable deduplication state could not be persisted.",
                    innerException: new AggregateException(exception, persistenceException));
            }

            throw;
        }
        catch (WorkflowExecutorInvocationDeduplicationException)
        {
            await heartbeat.StopAsync();
            throw;
        }
        catch (Exception exception)
        {
            await heartbeat.StopAsync();
            if (heartbeat.Failure is { } leaseFailure)
            {
                throw CreateLeaseFailure(identity.Key, exception, leaseFailure);
            }

            try
            {
                await FailAsync(
                    heartbeat.CurrentClaim,
                    WorkflowExecutorInvocationState.FailedRetryable,
                    WorkflowExecutorInvocationFailureCode.ExecutionFailed,
                    "The governed executor invocation failed and may be retried with the same participant key.",
                    CancellationToken.None);
            }
            catch (WorkflowExecutorInvocationDeduplicationException persistenceException)
            {
                throw new WorkflowExecutorInvocationDeduplicationException(
                    identity.Key,
                    "The governed executor failed, and its retryable deduplication state could not be persisted.",
                    innerException: new AggregateException(exception, persistenceException));
            }

            throw new WorkflowExecutorInvocationDeduplicationException(
                identity.Key,
                "The governed executor invocation failed. Its durable deduplication claim was retained for bounded recovery.",
                innerException: exception);
        }
    }

    private bool TryCreateIdentity(
        WorkflowDefinition definition,
        WorkflowNode node,
        WorkflowNodeInput input,
        WorkflowExecutorInvocationContext invocationContext,
        out WorkflowExecutorDescriptor descriptor,
        out WorkflowExecutorInvocationIdentity identity)
    {
        descriptor = null!;
        identity = null!;
        if (node.Settings.ExecutorId is not { } executorId ||
            !catalog.TryGetExecutor(executorId, out descriptor) ||
            !WorkflowExecutorInvocationDeduplicationPolicy.Participates(descriptor))
        {
            return false;
        }

        if (invocationContext == WorkflowExecutorInvocationContext.Empty)
        {
            return false;
        }

        var runId = WorkflowExecutorExecutionAuditScope.CurrentRunId;
        if (runId is null ||
            invocationContext.CausationRequestId is not { } requestId ||
            invocationContext.CausationRequestVersion is not { } requestVersion ||
            invocationContext.CausationOperationId is not { } operationId)
        {
            throw new InvalidOperationException(
                $"Participating workflow executor '{executorId}' requires run, request, request-version, and response-operation identity.");
        }

        identity = WorkflowExecutorInvocationKeyFactory.Create(
            runId.Value,
            definition.VersionId,
            node.Id,
            executorId,
            WorkflowExecutorInvocationDeduplicationPolicy.ResolveContractVersion(descriptor),
            requestId,
            requestVersion,
            operationId,
            invocationContext.InvocationGeneration,
            input);
        return true;
    }

    private void ValidateReplayAuthorization(
        WorkflowDefinition definition,
        WorkflowNode node,
        WorkflowExecutorDescriptor descriptor,
        WorkflowNodeInput input,
        WorkflowExecutorInvocationContext invocationContext)
    {
        var authorization = invocationContext.ApprovalAuthorization;
        if (!descriptor.PermissionPolicy.RequiresApproval)
        {
            if (authorization is not null)
            {
                throw new InvalidOperationException(
                    $"Workflow executor '{descriptor.Id}' received approval authorization although its permission policy does not require approval.");
            }

            return;
        }

        if (authorization is null)
        {
            throw new InvalidOperationException(
                $"Participating workflow executor '{descriptor.Id}' requires checkpoint-owned approval authorization before replay-safe invocation.");
        }

        WorkflowExecutorInvoker.ValidateApprovalAuthorization(
            definition,
            node,
            descriptor,
            input,
            invocationContext,
            clock.GetUtcNow());
        if (!authorization.Approved)
        {
            throw WorkflowExecutorFailureDiagnosticMapper.CreateApprovalDeniedException(
                definition,
                node,
                descriptor,
                authorization.Message);
        }
    }

    private static WorkflowNodeExecutionResult GetReplayResult(
        WorkflowExecutorInvocationIdentity requestedIdentity,
        WorkflowExecutorDescriptor descriptor,
        WorkflowExecutorInvocationRecord? record)
    {
        if (record is null ||
            record.Identity.ScopeKey != requestedIdentity.ScopeKey ||
            record.Identity.Key != requestedIdentity.Key ||
            record.Identity.InputHash != requestedIdentity.InputHash ||
            record.State != WorkflowExecutorInvocationState.Completed ||
            record.StoredResult is null ||
            record.StoredResult.Result.NodeId != requestedIdentity.NodeId ||
            !WorkflowExecutorInvocationDeduplicationPolicy.CanPersistResult(
                descriptor,
                record.StoredResult.Result))
        {
            throw CreateFailure(
                requestedIdentity.Key,
                "The completed workflow executor invocation record is missing, inconsistent, or unsafe to replay.");
        }

        return record.StoredResult.Result;
    }

    private async Task FailAsync(
        WorkflowExecutorInvocationClaim claim,
        WorkflowExecutorInvocationState failureState,
        WorkflowExecutorInvocationFailureCode failureCode,
        string safeMessage,
        CancellationToken cancellationToken)
    {
        var failure = await store.TryFailAsync(
            new WorkflowExecutorInvocationFailureRequest(
                claim.Identity.Key,
                claim.Identity.InputHash,
                claim.ConcurrencyVersion,
                claim.Lease.OwnerId,
                claim.Lease.Epoch,
                failureState,
                failureCode,
                safeMessage,
                clock.GetUtcNow()),
            cancellationToken);
        if (!failure.Succeeded)
        {
            throw CreateMutationFailure(
                claim.Identity.Key,
                failure.Outcome,
                "The governed executor failure could not be durably recorded.");
        }
    }

    private static WorkflowExecutorInvocationDeduplicationException CreateClaimFailure(
        WorkflowExecutorInvocationKey key,
        WorkflowExecutorInvocationClaimOutcome outcome)
    {
        var message = outcome switch
        {
            WorkflowExecutorInvocationClaimOutcome.ActiveLease =>
                "The governed executor invocation is already active under a valid lease.",
            WorkflowExecutorInvocationClaimOutcome.InputMismatch =>
                "The governed executor invocation scope was reused with different input.",
            WorkflowExecutorInvocationClaimOutcome.AttemptLimitReached =>
                "The governed executor invocation exhausted its bounded recovery attempts.",
            WorkflowExecutorInvocationClaimOutcome.FailedTerminal =>
                "The governed executor invocation is terminal and will not be repeated.",
            _ => "The governed executor invocation could not acquire its durable deduplication claim."
        };
        return new WorkflowExecutorInvocationDeduplicationException(key, message, outcome);
    }

    private static WorkflowExecutorInvocationDeduplicationException CreateMutationFailure(
        WorkflowExecutorInvocationKey key,
        WorkflowExecutorInvocationMutationOutcome outcome,
        string message)
        => new(key, message, mutationOutcome: outcome);

    private static WorkflowExecutorInvocationDeduplicationException CreateFailure(
        WorkflowExecutorInvocationKey key,
        string message)
        => new(key, message);

    private static void ThrowIfLeaseLost(
        WorkflowExecutorInvocationKey key,
        Exception? failure)
    {
        if (failure is not null)
        {
            throw CreateLeaseFailure(key, failure);
        }
    }

    private static WorkflowExecutorInvocationDeduplicationException CreateLeaseFailure(
        WorkflowExecutorInvocationKey key,
        params Exception[] failures)
        => new(
            key,
            "The governed executor invocation lost its durable lease and cannot finalize with a stale fencing version.",
            innerException: failures.Length == 1 ? failures[0] : new AggregateException(failures));
}
