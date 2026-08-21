using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.AgentFramework.Core;

public sealed class WorkflowExternalResponseService : IWorkflowExternalResponseService
{
    private readonly IWorkflowRunStore runStore;
    private readonly IWorkflowExternalRequestBoundaryStore boundaryStore;
    private readonly IWorkflowExternalResponseOperationStore operationStore;
    private readonly IWorkflowExternalResponseContinuation continuation;
    private readonly IWorkflowExternalRequestAuthorizer authorizer;
    private readonly IWorkflowExternalResponseValidator validator;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<WorkflowExternalResponseService> logger;
    private readonly WorkflowExternalResponseServiceResultMapper resultMapper = new();
    private readonly WorkflowExternalResponseLeaseOwnerId leaseOwnerId = new(
        $"{Environment.MachineName}:{Environment.ProcessId}:{Guid.NewGuid():N}");

    public WorkflowExternalResponseService(
        IWorkflowRunStore runStore,
        IWorkflowExternalRequestBoundaryStore boundaryStore,
        IWorkflowExternalResponseOperationStore operationStore,
        IWorkflowExternalResponseContinuation continuation,
        IWorkflowExternalRequestAuthorizer authorizer,
        IWorkflowExternalResponseValidator validator,
        TimeProvider timeProvider,
        ILogger<WorkflowExternalResponseService> logger)
    {
        this.runStore = runStore ?? throw new ArgumentNullException(nameof(runStore));
        this.boundaryStore = boundaryStore ?? throw new ArgumentNullException(nameof(boundaryStore));
        this.operationStore = operationStore ?? throw new ArgumentNullException(nameof(operationStore));
        this.continuation = continuation ?? throw new ArgumentNullException(nameof(continuation));
        this.authorizer = authorizer ?? throw new ArgumentNullException(nameof(authorizer));
        this.validator = validator ?? throw new ArgumentNullException(nameof(validator));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<WorkflowExternalResponseServiceResult> SubmitAsync(
        WorkflowExternalResponseCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.ActorContext is WorkflowExternalResponseActorContext.Unauthenticated unauthenticated)
        {
            LogRejected(
                command.RequestId,
                runId: null,
                command.CorrelationId,
                WorkflowExternalResponseServiceOutcome.Unauthenticated,
                authorizedActorKind: null);
            return resultMapper.Failure(
                WorkflowExternalResponseServiceOutcome.Unauthenticated,
                unauthenticated.SafeReason);
        }

        var loaded = await LoadContextAsync(command.RequestId, cancellationToken);
        if (loaded.Failure is not null)
        {
            return loaded.Failure;
        }

        var context = loaded.Context!;
        var now = timeProvider.GetUtcNow();
        var authorization = await authorizer.AuthorizeAsync(
            new WorkflowExternalRequestAuthorizationRequest(
                command.ActorContext,
                context.Run,
                context.Request,
                context.Boundary,
                now),
            cancellationToken);
        if (!authorization.Succeeded || authorization.Authorization is null)
        {
            var failure = resultMapper.AuthorizationFailure(
                authorization,
                context.Run,
                context.Request);
            LogRejected(
                command.RequestId,
                context.Run.RunId,
                command.CorrelationId,
                failure.Outcome,
                GetActorKind(command.ActorContext));
            return failure;
        }

        var validation = validator.Validate(
            new WorkflowExternalResponseValidationRequest(
                context.Run,
                context.Request,
                context.Boundary,
                command.ExpectedRequestVersion,
                command.Response));
        if (!validation.Succeeded ||
            validation.CanonicalPayload is null ||
            validation.Action is null)
        {
            var failure = resultMapper.ValidationFailure(
                validation,
                context.Run,
                context.Request);
            LogRejected(
                command.RequestId,
                context.Run.RunId,
                command.CorrelationId,
                failure.Outcome,
                authorizedActorKind: authorization.Authorization.Actor.Kind);
            return failure;
        }

        var canonicalPayload = validation.CanonicalPayload.Value;
        var authorizedAction = validation.Action.Value;
        var authorizedActor = authorization.Authorization;
        WorkflowExternalResponseFingerprint fingerprint;
        try
        {
            fingerprint = WorkflowExternalResponseFingerprintFactory.Create(
                command.RequestId,
                command.ExpectedRequestVersion,
                authorizedActor.Actor,
                authorizedActor.AuthorizationScope,
                authorizedActor.AuthorizationPolicyFingerprint,
                command.IdempotencyKey,
                canonicalPayload.Json);
        }
        catch (ArgumentException exception)
        {
            return resultMapper.Failure(
                WorkflowExternalResponseServiceOutcome.InvalidResponse,
                exception.Message,
                context.Run,
                context.Request);
        }

        var creation = await operationStore.CreateOrReplayAsync(
            new WorkflowExternalResponseOperationCreateRequest(
                WorkflowExternalResponseOperationId.New(),
                command.RequestId,
                context.Run.RunId,
                command.ExpectedRequestVersion,
                fingerprint,
                authorizedActor.Actor,
                command.CorrelationId,
                now),
            cancellationToken);
        if (!creation.Succeeded || creation.Operation is null)
        {
            return resultMapper.CreationFailure(
                creation,
                context.Run,
                context.Request);
        }

        logger.LogInformation(
            "Workflow external response operation {OperationId} was {CreateOutcome} for request {RequestId}, run {RunId}, action {Action}, actor kind {ActorKind}, correlation {CorrelationId}.",
            creation.Operation.Id,
            creation.Outcome,
            command.RequestId,
            context.Run.RunId,
            authorizedAction,
            authorizedActor.Actor.Kind,
            command.CorrelationId);

        if (creation.Outcome == WorkflowExternalResponseOperationCreateOutcome.Replayed &&
            !ShouldContinueReplay(creation.Operation, now))
        {
            return await MapOperationAsync(
                creation.Operation,
                context.Run,
                context.Request,
                replayed: true,
                cancellationToken);
        }

        var result = await continuation.ContinueAsync(
            new WorkflowExternalResponseContinuationRequest(
                creation.Operation.Id,
                leaseOwnerId),
            cancellationToken);
        return await MapContinuationAsync(
            result,
            context.Run,
            context.Request,
            creation.Outcome == WorkflowExternalResponseOperationCreateOutcome.Replayed,
            cancellationToken);
    }

    public async Task<WorkflowExternalResponseServiceResult> GetStatusAsync(
        WorkflowExternalResponseStatusQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(query.ActorContext);
        if (query.ActorContext is WorkflowExternalResponseActorContext.Unauthenticated unauthenticated)
        {
            return resultMapper.Failure(
                WorkflowExternalResponseServiceOutcome.Unauthenticated,
                unauthenticated.SafeReason);
        }

        var operation = await operationStore.GetAsync(query.OperationId, cancellationToken);
        if (operation is null)
        {
            return resultMapper.Failure(
                WorkflowExternalResponseServiceOutcome.OperationNotFound,
                "The workflow external response operation was not found.");
        }

        var loaded = await LoadContextAsync(operation.RequestId, cancellationToken);
        if (loaded.Failure is not null)
        {
            return loaded.Failure;
        }

        var context = loaded.Context!;
        if (operation.RunId != context.Run.RunId ||
            operation.ExpectedRequestVersion != context.Boundary.RequestVersion)
        {
            return resultMapper.Failure(
                WorkflowExternalResponseServiceOutcome.RequestMismatch,
                "The workflow response operation does not match its persisted request boundary.",
                context.Run,
                context.Request,
                operation);
        }

        var authorization = await authorizer.AuthorizeAsync(
            new WorkflowExternalRequestAuthorizationRequest(
                query.ActorContext,
                context.Run,
                context.Request,
                context.Boundary,
                timeProvider.GetUtcNow()),
            cancellationToken);
        if (!authorization.Succeeded)
        {
            var failure = resultMapper.AuthorizationFailure(
                authorization,
                context.Run,
                context.Request);
            LogRejected(
                operation.RequestId,
                context.Run.RunId,
                query.CorrelationId,
                failure.Outcome,
                GetActorKind(query.ActorContext));
            return failure;
        }

        return await MapOperationAsync(
            operation,
            context.Run,
            context.Request,
            replayed: false,
            cancellationToken);
    }

    private async Task<LoadedContext> LoadContextAsync(
        WorkflowExternalRequestId requestId,
        CancellationToken cancellationToken)
    {
        var request = await runStore.GetExternalRequestAsync(requestId, cancellationToken);
        if (request is null)
        {
            return LoadedContext.FromFailure(resultMapper.Failure(
                WorkflowExternalResponseServiceOutcome.RequestNotFound,
                "The workflow external request was not found."));
        }

        var run = await runStore.GetRunAsync(request.RunId, cancellationToken);
        if (run is null)
        {
            return LoadedContext.FromFailure(resultMapper.Failure(
                WorkflowExternalResponseServiceOutcome.RunNotFound,
                "The workflow run was not found.",
                request: request));
        }

        var boundary = await boundaryStore.ReadAsync(requestId, cancellationToken);
        if (!boundary.Succeeded || boundary.Boundary is null)
        {
            var outcome = boundary.Outcome switch
            {
                WorkflowExternalRequestBoundaryReadOutcome.RequestNotFound =>
                    WorkflowExternalResponseServiceOutcome.RequestNotFound,
                WorkflowExternalRequestBoundaryReadOutcome.LegacyNonResumable =>
                    WorkflowExternalResponseServiceOutcome.LegacyNonResumable,
                _ => WorkflowExternalResponseServiceOutcome.CheckpointMissing
            };
            return LoadedContext.FromFailure(resultMapper.Failure(
                outcome,
                outcome == WorkflowExternalResponseServiceOutcome.LegacyNonResumable
                    ? "The workflow request is a legacy metadata-only wait and cannot be resumed."
                    : "The workflow request has no usable persisted response boundary.",
                run,
                request));
        }

        return LoadedContext.FromContext(new SubmissionContext(
            run,
            HydrateRequest(request, boundary.Boundary),
            boundary.Boundary));
    }

    private async Task<WorkflowExternalResponseServiceResult> MapContinuationAsync(
        WorkflowExternalResponseContinuationResult result,
        WorkflowRunSnapshot fallbackRun,
        WorkflowExternalRequestRecord request,
        bool replayed,
        CancellationToken cancellationToken)
    {
        if (result.Operation is not null)
        {
            return await MapOperationAsync(
                result.Operation,
                result.Run ?? fallbackRun,
                request,
                replayed || result.Outcome == WorkflowExternalResponseContinuationOutcome.Replayed,
                cancellationToken,
                result.NextRequest,
                result.SafeMessage);
        }

        return resultMapper.ContinuationFailure(result, fallbackRun, request);
    }

    private async Task<WorkflowExternalResponseServiceResult> MapOperationAsync(
        WorkflowExternalResponseOperationRecord operation,
        WorkflowRunSnapshot run,
        WorkflowExternalRequestRecord request,
        bool replayed,
        CancellationToken cancellationToken,
        WorkflowExternalRequestRecord? nextRequest = null,
        string? safeMessage = null)
    {
        var currentRequest = await ReloadRequestAsync(
            operation.RequestId,
            request,
            cancellationToken);
        if (nextRequest is null && operation.FinalResult?.NextExternalRequestId is { } nextRequestId)
        {
            nextRequest = await runStore.GetExternalRequestAsync(nextRequestId, cancellationToken);
            if (nextRequest is not null)
            {
                var nextBoundary = await boundaryStore.ReadAsync(nextRequestId, cancellationToken);
                if (nextBoundary.Succeeded && nextBoundary.Boundary is not null)
                {
                    nextRequest = HydrateRequest(nextRequest, nextBoundary.Boundary);
                }
            }
        }

        return resultMapper.MapOperation(
            operation,
            run,
            currentRequest,
            nextRequest,
            replayed,
            safeMessage);
    }

    private async Task<WorkflowExternalRequestRecord> ReloadRequestAsync(
        WorkflowExternalRequestId requestId,
        WorkflowExternalRequestRecord fallback,
        CancellationToken cancellationToken)
    {
        var current = await runStore.GetExternalRequestAsync(requestId, cancellationToken);
        if (current is null)
        {
            return fallback;
        }

        var boundary = await boundaryStore.ReadAsync(requestId, cancellationToken);
        return boundary.Succeeded && boundary.Boundary is not null
            ? HydrateRequest(current, boundary.Boundary)
            : current;
    }

    private static bool ShouldContinueReplay(
        WorkflowExternalResponseOperationRecord operation,
        DateTimeOffset now)
        => operation.State is WorkflowExternalResponseOperationState.Accepted or
               WorkflowExternalResponseOperationState.FailedRetryable ||
           ((operation.State is WorkflowExternalResponseOperationState.Claimed or
                WorkflowExternalResponseOperationState.Resuming) &&
            operation.Lease is { } lease &&
            lease.IsExpired(now));

    private static WorkflowExternalRequestRecord HydrateRequest(
        WorkflowExternalRequestRecord request,
        WorkflowExternalRequestBoundaryRecord boundary)
        => request with
        {
            Version = boundary.RequestVersion,
            State = boundary.State,
            ResponseContract = boundary.ResponseContract,
            Continuation = boundary.Continuation,
            AuthorizationPolicy = boundary.AuthorizationPolicy
        };

    private void LogRejected(
        WorkflowExternalRequestId requestId,
        WorkflowRunId? runId,
        WorkflowLaunchCorrelationId correlationId,
        WorkflowExternalResponseServiceOutcome outcome,
        WorkflowLaunchActorKind? authorizedActorKind)
        => logger.LogWarning(
            "Workflow external response was rejected with {Outcome} for request {RequestId}, run {RunId}, actor kind {ActorKind}, correlation {CorrelationId}. No response payload or idempotency key is logged.",
            outcome,
            requestId,
            runId,
            authorizedActorKind,
            correlationId);

    private static WorkflowLaunchActorKind? GetActorKind(
        WorkflowExternalResponseActorContext actorContext)
        => actorContext is WorkflowExternalResponseActorContext.Authenticated authenticated
            ? authenticated.Actor.Kind
            : null;

    private sealed record SubmissionContext(
        WorkflowRunSnapshot Run,
        WorkflowExternalRequestRecord Request,
        WorkflowExternalRequestBoundaryRecord Boundary);

    private sealed record LoadedContext(
        SubmissionContext? Context,
        WorkflowExternalResponseServiceResult? Failure)
    {
        public static LoadedContext FromContext(SubmissionContext context) => new(context, null);

        public static LoadedContext FromFailure(WorkflowExternalResponseServiceResult failure) => new(null, failure);
    }
}
