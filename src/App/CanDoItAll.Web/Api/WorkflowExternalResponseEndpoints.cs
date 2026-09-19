using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using System.Text.Json;

namespace CanDoItAll.Web.Api;

internal static class WorkflowExternalResponseEndpoints
{
    public static RouteGroupBuilder MapWorkflowExternalResponseApi(
        this RouteGroupBuilder workflows)
    {
        workflows.MapPost("/external-requests/{requestId:guid}/response", SubmitAsync)
            .WithName("RespondToWorkflowExternalRequest")
            .WithMetadata(WorkflowExternalResponseOpenApiContract.Instance)
            .Produces<WorkflowExternalResponseApiResponse>(StatusCodes.Status200OK)
            .Produces<WorkflowExternalResponseApiResponse>(StatusCodes.Status202Accepted)
            .Produces<WorkflowExternalResponseApiResponse>(StatusCodes.Status400BadRequest)
            .Produces<WorkflowExternalResponseApiResponse>(StatusCodes.Status401Unauthorized)
            .Produces<WorkflowExternalResponseApiResponse>(StatusCodes.Status403Forbidden)
            .Produces<WorkflowExternalResponseApiResponse>(StatusCodes.Status404NotFound)
            .Produces<WorkflowExternalResponseApiResponse>(StatusCodes.Status409Conflict)
            .Produces<WorkflowExternalResponseApiResponse>(StatusCodes.Status410Gone)
            .Produces<WorkflowExternalResponseApiResponse>(StatusCodes.Status422UnprocessableEntity)
            .Produces<WorkflowExternalResponseApiResponse>(StatusCodes.Status500InternalServerError)
            .Produces<WorkflowExternalResponseApiResponse>(StatusCodes.Status503ServiceUnavailable)
            .ApplyApiAuthorization(workflows, ApiAuthorizationPolicies.RespondWorkflows);

        workflows.MapGet("/external-response-operations/{operationId:guid}", GetStatusAsync)
            .WithName("GetWorkflowExternalResponseOperation")
            .Produces<WorkflowExternalResponseApiResponse>(StatusCodes.Status200OK)
            .Produces<WorkflowExternalResponseApiResponse>(StatusCodes.Status202Accepted)
            .Produces<WorkflowExternalResponseApiResponse>(StatusCodes.Status400BadRequest)
            .Produces<WorkflowExternalResponseApiResponse>(StatusCodes.Status401Unauthorized)
            .Produces<WorkflowExternalResponseApiResponse>(StatusCodes.Status403Forbidden)
            .Produces<WorkflowExternalResponseApiResponse>(StatusCodes.Status404NotFound)
            .Produces<WorkflowExternalResponseApiResponse>(StatusCodes.Status409Conflict)
            .Produces<WorkflowExternalResponseApiResponse>(StatusCodes.Status410Gone)
            .Produces<WorkflowExternalResponseApiResponse>(StatusCodes.Status422UnprocessableEntity)
            .Produces<WorkflowExternalResponseApiResponse>(StatusCodes.Status500InternalServerError)
            .Produces<WorkflowExternalResponseApiResponse>(StatusCodes.Status503ServiceUnavailable)
            .ApplyApiAuthorization(workflows, ApiAuthorizationPolicies.RespondWorkflows);

        return workflows;
    }

    /// <summary>
    /// Answer a pending human-input or approval request of a waiting workflow run.
    /// </summary>
    /// <remarks>
    /// Submits one response to an external request that a run is waiting for, validates it against the request's
    /// persisted response contract and resumes the run. This is the only HTTP way to answer an external request.
    ///
    /// Before submitting, read the request with <c>GET /api/workflows/runs/{runId}/pending-requests</c> or
    /// <c>GET /api/workflows/runs/{runId}/detail</c>: its <c>state</c> must be Pending, send its <c>version</c> as
    /// <c>expectedRequestVersion</c>, and build <c>response</c> from its <c>responseContract</c>.
    ///
    /// The body is a JSON object with exactly two members, <c>expectedRequestVersion</c> (a positive integer) and
    /// <c>response</c> (a JSON value, not a string containing encoded JSON). Member names are case-sensitive; unknown,
    /// duplicate or missing members, comments, trailing commas, nesting deeper than 32 levels and bodies larger than
    /// 70 KiB are rejected before anything changes. The <c>response</c> must also fit
    /// <c>responseContract.maximumPayloadBytes</c> (UTF-8 bytes of its JSON text):
    ///
    /// - Approval and tool-approval requests: an object with the boolean <c>approved</c> and optionally a
    /// <c>message</c> string of at most 4,096 characters, for example
    /// <c>{ "approved": true, "message": "Reviewed and approved." }</c>. <c>approved</c> false denies the request and
    /// the governed step is not executed.
    /// - Human-input requests: a JSON value that satisfies <c>responseContract.schema</c>, which is checked strictly.
    ///
    /// Idempotency: the <c>Idempotency-Key</c> header is required. An external request accepts one answer attempt,
    /// identified by its key and caller. Resending the same body with the same key returns that attempt
    /// (<c>replayed</c> true) and continues it when it was accepted but not finished or failed with a retryable
    /// error; the request is never answered twice. The same key with a different response, or another key or caller
    /// for a request that already has an attempt, is rejected with HTTP 409.
    ///
    /// Outcomes: HTTP 200 when the run completed, waits for another request (<c>nextPendingRequest</c>) or the
    /// approval was denied; HTTP 202 while the run is still resuming. Read the attempt later with
    /// <c>GET /api/workflows/external-response-operations/{operationId}</c> and the run with
    /// <c>GET /api/workflows/runs/{runId}</c>. Every response of this operation, success or failure, uses the same
    /// allowlisted body; branch on <c>outcome</c>. The body never contains the raw request or response JSON, event
    /// payloads, checkpoint references or hashes, artifact storage paths, the idempotency key, leases or authorization
    /// material.
    ///
    /// Authority: the caller must be authenticated with a bearer token issued by this host that carries a subject and
    /// the exact <c>api.workflows.respond</c> scope; the broad <c>api</c> scope is not enough. The token's subject is
    /// recorded as the responder and must equal the request's intended approver when the request names one. The
    /// request must belong to the current database profile and to a workspace scope the caller may answer. With API
    /// authorization disabled (the development default) no caller is authenticated, so this operation always returns
    /// HTTP 401: it is not open.
    /// </remarks>
    /// <param name="requestId">
    /// Identifier of the external request, as returned in <c>id</c> by
    /// <c>GET /api/workflows/runs/{runId}/pending-requests</c> or in <c>nextPendingRequest.id</c>.
    /// </param>
    /// <response code="200">
    /// The response was accepted and processed: <c>outcome</c> 0 Completed (the run completed), 1 WaitingAgain (the run
    /// waits for <c>nextPendingRequest</c>) or 2 Denied (the approval was denied and the governed step was not run).
    /// </response>
    /// <response code="202">
    /// <c>outcome</c> 3 Resuming: the response was accepted and the run is still resuming. Poll
    /// <c>GET /api/workflows/external-response-operations/{operationId}</c> until it reports a final outcome.
    /// </response>
    /// <response code="400">
    /// <c>outcome</c> 6 InvalidResponse: the <c>Idempotency-Key</c> header, the body or the <c>response</c> value was
    /// rejected (for example a schema mismatch or a response over the payload limit). Nothing was recorded; correct
    /// the request and send it again.
    /// </response>
    /// <response code="401">
    /// No authenticated caller: <c>outcome</c> 4 Unauthenticated, including every call while API authorization is
    /// disabled, or a token without a subject or with invalid timestamps. With API authorization enabled, a request
    /// without a valid bearer token is rejected before this operation runs with the general error envelope
    /// (<c>api.authorization-required</c>) instead of this body.
    /// </response>
    /// <response code="403">
    /// The caller may not answer this request: <c>outcome</c> 5 Forbidden (another database profile, workspace scope
    /// or intended responder, a missing response capability, an expired authentication, or an autonomous actor
    /// approving) or 18 AuthorizationContextUnavailable (the request's stored authorization context is incomplete).
    /// A token without the <c>api.workflows.respond</c> scope is rejected before this operation runs with the general
    /// error envelope (<c>api.authorization-forbidden</c>) instead of this body.
    /// </response>
    /// <response code="404">
    /// <c>outcome</c> 7 RequestNotFound or 8 RunNotFound: the request or its run does not exist.
    /// </response>
    /// <response code="409">
    /// The request cannot take this response now; nothing new was recorded: <c>outcome</c> 10 RequestVersionMismatch
    /// (read the request again and use its current <c>version</c>), 11 RequestNotPending (it was already answered),
    /// 12 RunNotWaiting, 13 IdempotencyConflict (the key was used with a different response) or 14
    /// ActiveOperationConflict (the request already has an attempt under another key or caller; read that attempt
    /// with <c>operationId</c> when it is returned, instead of answering again).
    /// </response>
    /// <response code="410">
    /// <c>outcome</c> 15 Cancelled or 16 Superseded: the request or the attempt was cancelled, or a newer request
    /// replaced it. Do not retry; read the run's pending requests.
    /// </response>
    /// <response code="422">
    /// The run's stored state cannot be resumed with this response: <c>outcome</c> 17 LegacyNonResumable, 19
    /// CheckpointMissing, 20 CheckpointCorrupt, 21 CheckpointIncompatible, 22 TopologyMismatch, 23
    /// WorkflowVersionMismatch or 24 RequestMismatch. Retrying does not help.
    /// </response>
    /// <response code="500">
    /// <c>outcome</c> 27 TerminalFailure: the attempt failed for an unclassified reason; the message is redacted. Read
    /// the operation, if one was returned, and the run before deciding what to do.
    /// </response>
    /// <response code="503">
    /// <c>outcome</c> 25 BackendUnavailable or 26 RetryableFailure: a temporary failure. Resend the same body with the
    /// same <c>Idempotency-Key</c> later; the recorded attempt continues instead of starting a second one.
    /// </response>
    internal static async Task<IResult> SubmitAsync(
        Guid requestId,
        HttpContext httpContext,
        IWorkflowExternalResponseService responseService,
        WorkflowExternalResponseApiActorResolver actorResolver,
        ILogger<WorkflowExternalResponseApiLog> logger,
        CancellationToken cancellationToken)
    {
        var idempotency = WorkflowExternalResponseIdempotencyKeyParser.Parse(httpContext.Request);
        if (!idempotency.Succeeded)
        {
            return WorkflowExternalResponseApiMapper.InvalidRequest(idempotency.SafeMessage);
        }

        var body = await WorkflowExternalResponseRequestReader
            .ReadAsync(httpContext.Request, cancellationToken)
            .ConfigureAwait(false);
        if (!body.Succeeded)
        {
            return WorkflowExternalResponseApiMapper.InvalidRequest(body.SafeMessage);
        }

        try
        {
            var request = body.Request!;
            var result = await responseService.SubmitAsync(
                new WorkflowExternalResponseCommand(
                    actorResolver.Resolve(httpContext.User),
                    new WorkflowExternalRequestId(requestId),
                    new WorkflowExternalRequestVersion(request.ExpectedRequestVersion),
                    request.Response,
                    idempotency.Key!.Value,
                    new WorkflowLaunchCorrelationId(httpContext.TraceIdentifier)),
                cancellationToken).ConfigureAwait(false);
            return WorkflowExternalResponseApiMapper.Map(result);
        }
        catch (Exception exception) when (exception is ArgumentException or JsonException)
        {
            return WorkflowExternalResponseApiMapper.InvalidRequest(
                "The workflow external response request is invalid.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Workflow external response submission failed for request {RequestId} and trace {TraceIdentifier}.",
                requestId,
                httpContext.TraceIdentifier);
            return WorkflowExternalResponseApiMapper.UnexpectedFailure();
        }
    }

    /// <summary>
    /// Read the current status of an accepted external-response attempt.
    /// </summary>
    /// <remarks>
    /// Returns the operation that a submission to <c>POST /api/workflows/external-requests/{requestId}/response</c>
    /// recorded, with its state, final outcome, run state and the next pending request when the run waits again. Use
    /// it to follow a submission that returned HTTP 202 or whose response was lost. Reading never changes the
    /// operation and never continues it: when it stays Resuming or reports a retryable failure, resend the original
    /// submission with the same <c>Idempotency-Key</c>, which continues the recorded attempt.
    ///
    /// The body and the status mapping are the same as for the submission, with <c>replayed</c> always false. The
    /// caller is authorized again against the request's current policy, so any caller allowed to answer the request
    /// can read its operations. The body never contains the raw request or response JSON, event payloads,
    /// checkpoint references or hashes, artifact storage paths, the idempotency key, leases or authorization
    /// material.
    ///
    /// Authority: the caller must be authenticated with a bearer token issued by this host that carries a subject and
    /// the exact <c>api.workflows.respond</c> scope; the broad <c>api</c> scope is not enough. With API authorization
    /// disabled (the development default) no caller is authenticated, so this operation always returns HTTP 401.
    /// </remarks>
    /// <param name="operationId">
    /// Identifier of the attempt, as returned in <c>operationId</c> by the submission.
    /// </param>
    /// <response code="200">
    /// The attempt finished: <c>outcome</c> 0 Completed, 1 WaitingAgain (see <c>nextPendingRequest</c>) or 2 Denied.
    /// </response>
    /// <response code="202">
    /// <c>outcome</c> 3 Resuming: the attempt was accepted and has not finished. Read again later.
    /// </response>
    /// <response code="400">
    /// <c>outcome</c> 6 InvalidResponse: the operation identifier is the empty GUID.
    /// </response>
    /// <response code="401">
    /// No authenticated caller: <c>outcome</c> 4 Unauthenticated, including every call while API authorization is
    /// disabled. With API authorization enabled, a request without a valid bearer token is rejected before this
    /// operation runs with the general error envelope (<c>api.authorization-required</c>) instead of this body.
    /// </response>
    /// <response code="403">
    /// The caller may not read this attempt: <c>outcome</c> 5 Forbidden or 18 AuthorizationContextUnavailable. A token
    /// without the <c>api.workflows.respond</c> scope is rejected before this operation runs with the general error
    /// envelope (<c>api.authorization-forbidden</c>) instead of this body.
    /// </response>
    /// <response code="404">
    /// <c>outcome</c> 9 OperationNotFound, 7 RequestNotFound or 8 RunNotFound: the attempt, its request or its run does
    /// not exist.
    /// </response>
    /// <response code="409">
    /// Part of the status map shared with the submission route; reading an attempt does not return it.
    /// </response>
    /// <response code="410">
    /// <c>outcome</c> 15 Cancelled: the attempt or its request was cancelled.
    /// </response>
    /// <response code="422">
    /// The attempt ended because the run's stored state could not be resumed: <c>outcome</c> 17 LegacyNonResumable,
    /// 19 CheckpointMissing, 20 CheckpointCorrupt, 21 CheckpointIncompatible, 22 TopologyMismatch, 23
    /// WorkflowVersionMismatch or 24 RequestMismatch.
    /// </response>
    /// <response code="500">
    /// <c>outcome</c> 27 TerminalFailure: the attempt failed for an unclassified reason, or reading it failed; the
    /// message is redacted.
    /// </response>
    /// <response code="503">
    /// <c>outcome</c> 25 BackendUnavailable or 26 RetryableFailure: the attempt failed temporarily. Resend the original
    /// submission with the same <c>Idempotency-Key</c> to continue it.
    /// </response>
    internal static async Task<IResult> GetStatusAsync(
        Guid operationId,
        HttpContext httpContext,
        IWorkflowExternalResponseService responseService,
        WorkflowExternalResponseApiActorResolver actorResolver,
        ILogger<WorkflowExternalResponseApiLog> logger,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await responseService.GetStatusAsync(
                new WorkflowExternalResponseStatusQuery(
                    actorResolver.Resolve(httpContext.User),
                    new WorkflowExternalResponseOperationId(operationId),
                    new WorkflowLaunchCorrelationId(httpContext.TraceIdentifier)),
                cancellationToken).ConfigureAwait(false);
            return WorkflowExternalResponseApiMapper.Map(result with { Replayed = false });
        }
        catch (ArgumentException)
        {
            return WorkflowExternalResponseApiMapper.InvalidRequest(
                "The workflow external response operation id is invalid.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Workflow external response status failed for operation {OperationId} and trace {TraceIdentifier}.",
                operationId,
                httpContext.TraceIdentifier);
            return WorkflowExternalResponseApiMapper.UnexpectedFailure();
        }
    }

    internal sealed class WorkflowExternalResponseApiLog;
}
