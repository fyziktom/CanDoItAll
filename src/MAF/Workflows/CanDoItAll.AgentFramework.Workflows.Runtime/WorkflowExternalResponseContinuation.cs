using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;

namespace CanDoItAll.AgentFramework.Core;

public sealed class WorkflowExternalResponseContinuation : IWorkflowExternalResponseContinuation
{
    internal static readonly TimeSpan DefaultLeaseDuration = TimeSpan.FromMinutes(2);
    internal static readonly TimeSpan DefaultRenewalInterval = TimeSpan.FromSeconds(30);
    internal const int DefaultMaximumAttempts = 3;

    private readonly IReadOnlyDictionary<WorkflowRuntimeBackendKind, IWorkflowExecutionBackend> backends;
    private readonly IWorkflowExternalResponseOperationStore operationStore;
    private readonly IWorkflowResumeBoundaryStore boundaryStore;
    private readonly IWorkflowActiveRunRegistry activeRuns;
    private readonly IWorkflowExternalResponseValidator validator;
    private readonly TimeProvider timeProvider;
    private readonly WorkflowExternalResponseLeaseHeartbeat heartbeat;
    private readonly WorkflowExternalResponseResultMapper resultMapper = new();
    private readonly WorkflowExternalResponseRecoveryHook? recoveryHook;

    public WorkflowExternalResponseContinuation(
        IEnumerable<IWorkflowExecutionBackend> backends,
        IWorkflowExternalResponseOperationStore operationStore,
        IWorkflowResumeBoundaryStore boundaryStore,
        IWorkflowActiveRunRegistry activeRuns,
        IWorkflowExternalResponseValidator validator,
        TimeProvider timeProvider,
        WorkflowExternalResponseRecoveryHook? recoveryHook = null)
    {
        ArgumentNullException.ThrowIfNull(backends);
        ArgumentNullException.ThrowIfNull(operationStore);
        ArgumentNullException.ThrowIfNull(boundaryStore);
        ArgumentNullException.ThrowIfNull(activeRuns);
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(timeProvider);

        this.backends = backends.ToDictionary(item => item.Descriptor.Kind);
        this.operationStore = operationStore;
        this.boundaryStore = boundaryStore;
        this.activeRuns = activeRuns;
        this.validator = validator;
        this.timeProvider = timeProvider;
        this.recoveryHook = recoveryHook;
        heartbeat = new WorkflowExternalResponseLeaseHeartbeat(
            operationStore,
            timeProvider,
            DefaultLeaseDuration,
            DefaultRenewalInterval);
    }

    public async Task<WorkflowExternalResponseContinuationResult> ContinueAsync(
        WorkflowExternalResponseContinuationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var operation = await operationStore.GetAsync(request.OperationId, cancellationToken);
        if (operation is null)
        {
            return resultMapper.CreateResult(
                WorkflowExternalResponseContinuationOutcome.NotFound,
                operation: null,
                "The workflow external response operation was not found.");
        }

        if (WorkflowExternalResponseOperationTransitionRules.IsTerminal(operation.State))
        {
            return resultMapper.Replay(operation);
        }

        await InvokeRecoveryHookAsync(
            WorkflowExternalResponseRecoveryPoint.AcceptedBeforeClaim,
            operation.Id,
            CancellationToken.None);

        var now = timeProvider.GetUtcNow();
        var claim = await operationStore.TryClaimAsync(
            new WorkflowExternalResponseOperationClaimRequest(
                operation.Id,
                operation.ConcurrencyVersion,
                request.LeaseOwnerId,
                now,
                now.Add(DefaultLeaseDuration),
                DefaultMaximumAttempts),
            cancellationToken);
        if (!claim.Succeeded || claim.Operation is null || claim.Claim is null)
        {
            return resultMapper.ClaimFailure(claim);
        }

        var claimed = claim.Operation;
        var lease = claim.Claim.Lease;
        var resuming = await operationStore.TryMarkResumingAsync(
            new WorkflowExternalResponseOperationMarkResumingRequest(
                claimed.Id,
                claimed.ConcurrencyVersion,
                request.LeaseOwnerId,
                lease.Epoch,
                timeProvider.GetUtcNow()),
            CancellationToken.None);
        if (!resuming.Succeeded || resuming.Operation is null)
        {
            return resultMapper.MutationFailure(resuming, claimed);
        }

        operation = resuming.Operation;
        await using var leaseHeartbeat = heartbeat.Start(
            operation,
            request.LeaseOwnerId,
            CancellationToken.None);
        var boundary = await boundaryStore.LoadAsync(
            new WorkflowResumeBoundaryLoadRequest(operation.Id),
            CancellationToken.None);
        if (!boundary.Succeeded || boundary.Context is null)
        {
            await leaseHeartbeat.StopAsync();
            operation = leaseHeartbeat.CurrentOperation;
            if (leaseHeartbeat.Failure is not null)
            {
                return await FailRetryableAsync(
                    operation,
                    request.LeaseOwnerId,
                    WorkflowExternalResponseOperationOutcomeCode.ResumeFailed,
                    "Workflow response recovery lost its durable lease and can be retried by a new owner.",
                    CancellationToken.None);
            }

            return await FailTerminalAsync(
                operation,
                request.LeaseOwnerId,
                resultMapper.MapBoundaryFailure(boundary.Outcome),
                "The workflow response cannot resume because its persisted boundary is unavailable or incompatible.",
                CancellationToken.None);
        }

        if (await FailIfLeaseLostAsync(leaseHeartbeat, request.LeaseOwnerId) is { } boundaryLeaseFailure)
        {
            return boundaryLeaseFailure;
        }

        operation = leaseHeartbeat.CurrentOperation;
        var context = boundary.Context;
        WorkflowExternalResponseValidationResult validation;
        JsonElement response;
        if (context.Request.EffectiveState != WorkflowExternalRequestState.Pending ||
            context.Boundary.State != WorkflowExternalRequestState.ResponseClaimed)
        {
            validation = new WorkflowExternalResponseValidationResult(
                WorkflowExternalResponseValidationOutcome.BoundaryMismatch,
                CanonicalPayload: null,
                Action: null,
                "The persisted workflow external response claim state is invalid.");
            response = default;
        }
        else
        {
            try
            {
                using var responseDocument = JsonDocument.Parse(operation.ResponsePayload.Json);
                response = responseDocument.RootElement.Clone();
                validation = validator.Validate(
                    new WorkflowExternalResponseValidationRequest(
                        context.Run,
                        context.Request with { State = context.Boundary.State },
                        context.Boundary,
                        operation.ExpectedRequestVersion,
                        response));
            }
            catch (JsonException)
            {
                validation = new WorkflowExternalResponseValidationResult(
                    WorkflowExternalResponseValidationOutcome.SchemaMismatch,
                    CanonicalPayload: null,
                    Action: null,
                    "The persisted workflow external response is not valid JSON.");
                response = default;
            }
        }

        if (!validation.Succeeded || validation.Action is null)
        {
            await leaseHeartbeat.StopAsync();
            if (await FailIfLeaseLostAsync(leaseHeartbeat, request.LeaseOwnerId) is { } validationLeaseFailure)
            {
                return validationLeaseFailure;
            }

            return await FailTerminalAsync(
                leaseHeartbeat.CurrentOperation,
                request.LeaseOwnerId,
                validation.Outcome == WorkflowExternalResponseValidationOutcome.BoundaryMismatch
                    ? WorkflowExternalResponseOperationOutcomeCode.RequestMismatch
                    : WorkflowExternalResponseOperationOutcomeCode.ResponseRejected,
                validation.SafeMessage,
                CancellationToken.None);
        }

        var authorization = WorkflowExternalResponseAuthorizationFactory.Create(
            operation,
            context.Run,
            context.Request,
            context.Boundary,
            validation.Action.Value,
            timeProvider.GetUtcNow());
        if (!authorization.Succeeded || authorization.Authorization is null)
        {
            await leaseHeartbeat.StopAsync();
            if (await FailIfLeaseLostAsync(leaseHeartbeat, request.LeaseOwnerId) is { } authorizationLeaseFailure)
            {
                return authorizationLeaseFailure;
            }

            return await FailTerminalAsync(
                leaseHeartbeat.CurrentOperation,
                request.LeaseOwnerId,
                authorization.Outcome == WorkflowExternalResponseAuthorizationOutcome.LinkageMismatch
                    ? WorkflowExternalResponseOperationOutcomeCode.RequestMismatch
                    : WorkflowExternalResponseOperationOutcomeCode.ResponseRejected,
                authorization.SafeMessage,
                CancellationToken.None);
        }

        if (await FailIfLeaseLostAsync(leaseHeartbeat, request.LeaseOwnerId) is { } authorizationBoundaryLeaseFailure)
        {
            return authorizationBoundaryLeaseFailure;
        }

        operation = leaseHeartbeat.CurrentOperation;
        if (!backends.TryGetValue(context.Run.Backend, out var backend) ||
            backend is not IWorkflowExternalResponseBackend resumeBackend ||
            !backend.Descriptor.SupportsExternalResponseResume)
        {
            await leaseHeartbeat.StopAsync();
            return await FailRetryableAsync(
                leaseHeartbeat.CurrentOperation,
                request.LeaseOwnerId,
                WorkflowExternalResponseOperationOutcomeCode.BackendUnavailable,
                "The workflow runtime backend is unavailable for external-response recovery.",
                CancellationToken.None);
        }

        await InvokeRecoveryHookAsync(
            WorkflowExternalResponseRecoveryPoint.ClaimedBeforeResponseDelivery,
            operation.Id,
            CancellationToken.None);

        if (await FailIfLeaseLostAsync(leaseHeartbeat, request.LeaseOwnerId) is { } preDeliveryHookLeaseFailure)
        {
            return preDeliveryHookLeaseFailure;
        }

        operation = leaseHeartbeat.CurrentOperation;
        if (!activeRuns.TryRegister(
                context.Run.RunId,
                backend.Descriptor.SupportsActiveCancellation,
                leaseHeartbeat.LeaseLostToken,
                out var activeRun))
        {
            await leaseHeartbeat.StopAsync();
            return await FailRetryableAsync(
                leaseHeartbeat.CurrentOperation,
                request.LeaseOwnerId,
                WorkflowExternalResponseOperationOutcomeCode.ResumeFailed,
                "The workflow run is already active in this runtime host.",
                CancellationToken.None);
        }

        WorkflowBackendStartResult backendResult;
        using (activeRun)
        {
            try
            {
                backendResult = await resumeBackend.ResumeAsync(
                    new WorkflowBackendResumeRequest(
                        context.Run,
                        context.Request,
                        response,
                        operation.Id,
                        context.Boundary.RequestVersion.Value,
                        authorization.Authorization),
                    activeRun.Token);
            }
            catch (OperationCanceledException) when (leaseHeartbeat.Failure is not null)
            {
                await leaseHeartbeat.StopAsync();
                return await FailRetryableAsync(
                    leaseHeartbeat.CurrentOperation,
                    request.LeaseOwnerId,
                    WorkflowExternalResponseOperationOutcomeCode.ResumeFailed,
                    "Workflow response recovery lost its durable lease and can be retried by a new owner.",
                    CancellationToken.None);
            }
            catch (OperationCanceledException) when (activeRun.IsCancellationRequested)
            {
                backendResult = resultMapper.CreateCancelledResult(context.Run, timeProvider.GetUtcNow());
            }
            catch (WorkflowBackendResumeException exception)
            {
                await leaseHeartbeat.StopAsync();
                if (leaseHeartbeat.Failure is not null)
                {
                    return await FailRetryableAsync(
                        leaseHeartbeat.CurrentOperation,
                        request.LeaseOwnerId,
                        WorkflowExternalResponseOperationOutcomeCode.ResumeFailed,
                        "Workflow response recovery lost its durable lease and can be retried by a new owner.",
                        CancellationToken.None);
                }

                return await FailTerminalAsync(
                    leaseHeartbeat.CurrentOperation,
                    request.LeaseOwnerId,
                    resultMapper.MapBackendResumeFailure(exception.Kind),
                    exception.SafeMessage,
                    CancellationToken.None);
            }
            catch (JsonException)
            {
                await leaseHeartbeat.StopAsync();
                if (leaseHeartbeat.Failure is not null)
                {
                    return await FailRetryableAsync(
                        leaseHeartbeat.CurrentOperation,
                        request.LeaseOwnerId,
                        WorkflowExternalResponseOperationOutcomeCode.ResumeFailed,
                        "Workflow response recovery lost its durable lease and can be retried by a new owner.",
                        CancellationToken.None);
                }

                return await FailTerminalAsync(
                    leaseHeartbeat.CurrentOperation,
                    request.LeaseOwnerId,
                    WorkflowExternalResponseOperationOutcomeCode.ResponseRejected,
                    "The persisted response is not valid JSON.",
                    CancellationToken.None);
            }
            catch (Exception)
            {
                await leaseHeartbeat.StopAsync();
                return await FailRetryableAsync(
                    leaseHeartbeat.CurrentOperation,
                    request.LeaseOwnerId,
                    WorkflowExternalResponseOperationOutcomeCode.ResumeFailed,
                    "The workflow backend did not complete response recovery; the operation remains recoverable.",
                    CancellationToken.None);
            }

            if (!activeRun.TryClaimCompletion())
            {
                backendResult = resultMapper.CreateCancelledResult(context.Run, timeProvider.GetUtcNow());
            }
        }

        if (await FailIfLeaseLostAsync(leaseHeartbeat, request.LeaseOwnerId) is { } deliveryLeaseFailure)
        {
            return deliveryLeaseFailure;
        }

        operation = leaseHeartbeat.CurrentOperation;
        await InvokeRecoveryHookAsync(
            WorkflowExternalResponseRecoveryPoint.ResponseDeliveredBeforeCommit,
            operation.Id,
            CancellationToken.None);

        if (await FailIfLeaseLostAsync(leaseHeartbeat, request.LeaseOwnerId) is { } postDeliveryHookLeaseFailure)
        {
            return postDeliveryHookLeaseFailure;
        }

        operation = leaseHeartbeat.CurrentOperation;
        WorkflowExternalResponseOperationFinalResult finalResult;
        try
        {
            finalResult = resultMapper.CreateFinalResult(
                validation.Action.Value,
                backendResult);
        }
        catch (WorkflowBackendResumeException exception)
        {
            await leaseHeartbeat.StopAsync();
            if (await FailIfLeaseLostAsync(leaseHeartbeat, request.LeaseOwnerId) is { } resultLeaseFailure)
            {
                return resultLeaseFailure;
            }

            return await FailTerminalAsync(
                leaseHeartbeat.CurrentOperation,
                request.LeaseOwnerId,
                resultMapper.MapBackendResumeFailure(exception.Kind),
                exception.SafeMessage,
                CancellationToken.None);
        }

        await leaseHeartbeat.StopAsync();
        if (await FailIfLeaseLostAsync(leaseHeartbeat, request.LeaseOwnerId) is { } commitLeaseFailure)
        {
            return commitLeaseFailure;
        }

        operation = leaseHeartbeat.CurrentOperation;
        var commit = await boundaryStore.TryCommitAsync(
            new WorkflowResumeBoundaryCommitRequest(
                operation.Id,
                operation.ConcurrencyVersion,
                request.LeaseOwnerId,
                operation.Lease!.Epoch,
                context.Boundary.RequestVersion,
                backendResult,
                finalResult,
                timeProvider.GetUtcNow()),
            CancellationToken.None);
        if (!commit.Succeeded || commit.Operation is null)
        {
            return new WorkflowExternalResponseContinuationResult(
                commit.Outcome == WorkflowResumeBoundaryCommitOutcome.CancellationWon
                    ? WorkflowExternalResponseContinuationOutcome.Cancelled
                    : WorkflowExternalResponseContinuationOutcome.ClaimConflict,
                commit.Operation ?? operation,
                commit.Run,
                commit.NextRequest,
                $"Workflow response boundary commit did not succeed: {commit.Outcome}.");
        }

        return new WorkflowExternalResponseContinuationResult(
            resultMapper.MapContinuationOutcome(commit.Operation.State),
            commit.Operation,
            commit.Run,
            commit.NextRequest,
            commit.Operation.SafeMessage);
    }

    private async Task<WorkflowExternalResponseContinuationResult> FailRetryableAsync(
        WorkflowExternalResponseOperationRecord operation,
        WorkflowExternalResponseLeaseOwnerId ownerId,
        WorkflowExternalResponseOperationOutcomeCode outcomeCode,
        string safeMessage,
        CancellationToken cancellationToken)
        => await FailAsync(
            operation,
            ownerId,
            WorkflowExternalResponseOperationState.FailedRetryable,
            WorkflowExternalResponseContinuationOutcome.FailedRetryable,
            outcomeCode,
            safeMessage,
            cancellationToken);

    private async Task<WorkflowExternalResponseContinuationResult?> FailIfLeaseLostAsync(
        WorkflowExternalResponseLeaseHeartbeatSession leaseHeartbeat,
        WorkflowExternalResponseLeaseOwnerId ownerId)
    {
        var operation = leaseHeartbeat.CurrentOperation;
        if (leaseHeartbeat.Failure is null &&
            operation.Lease is { } lease &&
            !lease.IsExpired(timeProvider.GetUtcNow()))
        {
            return null;
        }

        await leaseHeartbeat.StopAsync();
        return await FailRetryableAsync(
            leaseHeartbeat.CurrentOperation,
            ownerId,
            WorkflowExternalResponseOperationOutcomeCode.ResumeFailed,
            "Workflow response recovery lost its durable lease and can be retried by a new owner.",
            CancellationToken.None);
    }

    private async Task<WorkflowExternalResponseContinuationResult> FailTerminalAsync(
        WorkflowExternalResponseOperationRecord operation,
        WorkflowExternalResponseLeaseOwnerId ownerId,
        WorkflowExternalResponseOperationOutcomeCode outcomeCode,
        string safeMessage,
        CancellationToken cancellationToken)
        => await FailAsync(
            operation,
            ownerId,
            WorkflowExternalResponseOperationState.FailedTerminal,
            WorkflowExternalResponseContinuationOutcome.FailedTerminal,
            outcomeCode,
            safeMessage,
            cancellationToken);

    private async Task<WorkflowExternalResponseContinuationResult> FailAsync(
        WorkflowExternalResponseOperationRecord operation,
        WorkflowExternalResponseLeaseOwnerId ownerId,
        WorkflowExternalResponseOperationState failureState,
        WorkflowExternalResponseContinuationOutcome continuationOutcome,
        WorkflowExternalResponseOperationOutcomeCode outcomeCode,
        string safeMessage,
        CancellationToken cancellationToken)
    {
        if (operation.Lease is not { } lease)
        {
            return resultMapper.CreateResult(
                WorkflowExternalResponseContinuationOutcome.ClaimConflict,
                operation,
                "The workflow response operation no longer owns a durable lease.");
        }

        var failure = await operationStore.TryFailAsync(
            new WorkflowExternalResponseOperationFailureRequest(
                operation.Id,
                operation.ConcurrencyVersion,
                ownerId,
                lease.Epoch,
                failureState,
                outcomeCode,
                safeMessage,
                timeProvider.GetUtcNow()),
            cancellationToken);
        return failure.Succeeded && failure.Operation is not null
            ? resultMapper.CreateResult(continuationOutcome, failure.Operation, safeMessage)
            : resultMapper.MutationFailure(failure, operation);
    }

    private ValueTask InvokeRecoveryHookAsync(
        WorkflowExternalResponseRecoveryPoint point,
        WorkflowExternalResponseOperationId operationId,
        CancellationToken cancellationToken)
        => recoveryHook is null
            ? ValueTask.CompletedTask
            : recoveryHook(point, operationId, cancellationToken);
}
