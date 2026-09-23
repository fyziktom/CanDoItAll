using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Application;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Common;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Operations;
using CanDoItAll.Web.Api.Streaming;
using CanDoItAll.Modules.Workspace.ApiAccess;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Web.Api;

internal static class LlmChatOperationsApi
{
    public static RouteGroupBuilder MapLlmChatOperationsApi(this RouteGroupBuilder api)
    {
        var conversations = api.MapGroup("/llm-conversations")
            .WithTags("LLM Chat Turns")
            .DisableAntiforgery();
        conversations.MapPost("/{conversationId:guid}/turns", SendTurnAsync)
            .WithName("SendLlmChatTurn")
            .Accepts<SendLlmChatTurnApiRequest>("application/json")
            .Produces<LlmChatOperationApiResponse>(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .ApplyApiAuthorization(api, ApiAuthorizationPolicies.ExecuteLlmChats);
        conversations.MapPost(
                "/{conversationId:guid}/active-turns/{turnId:guid}/abandon",
                AbandonActiveTurnAsync)
            .WithName("AbandonLlmChatActiveTurn")
            .Produces<LlmChatOperationApiResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ApplyApiAuthorization(api, ApiAuthorizationPolicies.ExecuteLlmChats);

        var operations = api.MapGroup("/llm-chat-operations")
            .WithTags("LLM Chat Operations")
            .DisableAntiforgery();
        operations.MapGet("/{operationId:guid}", GetOperationAsync)
            .WithName("GetLlmChatOperation")
            .Produces<LlmChatOperationApiResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ApplyApiAuthorization(api, ApiAuthorizationPolicies.ReadLlmChats);
        operations.MapGet("/{operationId:guid}/events", StreamOperationEventsAsync)
            .WithName("StreamLlmChatOperationEvents")
            .Produces<string>(
                StatusCodes.Status200OK,
                contentType: ServerSentEventResponseWriter.ContentType)
            .ProducesApiErrors(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ApplyApiAuthorization(api, ApiAuthorizationPolicies.ReadLlmChats);
        operations.MapPost("/{operationId:guid}/cancel", CancelOperationAsync)
            .WithName("CancelLlmChatOperation")
            .Produces<LlmChatOperationApiResponse>(StatusCodes.Status200OK)
            .Produces<LlmChatOperationApiResponse>(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ApplyApiAuthorization(api, ApiAuthorizationPolicies.ExecuteLlmChats);
        operations.MapPost("/{operationId:guid}/reconcile", ReconcileOperationAsync)
            .WithName("ReconcileLlmChatOperation")
            .Produces<LlmChatOperationApiResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ApplyApiAuthorization(api, ApiAuthorizationPolicies.ManageLlmChats);
        return api;
    }

    /// <summary>
    /// Admit one user message as a new conversation turn; the model's reply is produced asynchronously.
    /// </summary>
    /// <remarks>
    /// Records the user message in the transcript, queues the turn as a durable operation and returns HTTP 202 at once.
    /// The provider is called later by a background dispatcher, never by this request: HTTP 202 means the turn was
    /// admitted, not that a reply exists. Follow the operation with <c>GET /api/llm-chat-operations/{operationId}</c>
    /// (the <c>Location</c> header) or its event stream (<c>eventsUrl</c>) until <c>status</c> is <c>succeeded</c>,
    /// <c>failed</c>, <c>cancelled</c> or <c>recoveryRequired</c>. On success the reply is in <c>assistantMessage</c>
    /// and in the transcript.
    ///
    /// Preconditions: the conversation is not archived, its definition is <c>active</c>, no other turn of the
    /// conversation is active or unfinished, and <c>expectedTranscriptRevision</c> equals the conversation's current
    /// <c>transcriptRevision</c> (from <c>GET /api/llm-conversations/{conversationId}</c>, or
    /// <c>resultingTranscriptRevision</c> of the previous successful turn). The turn uses the definition revision the
    /// conversation is pinned to.
    ///
    /// Idempotency: <c>operationId</c> is a GUID you generate, and it identifies both the operation and the turn.
    /// Repeating the request with the same <c>operationId</c> and an identical body never calls the provider again; it
    /// returns the recorded operation with <c>replayed</c> true, whatever its status. Reusing an <c>operationId</c>
    /// with a different body is rejected with 409 <c>llm-chat.operation-id-conflict</c>. After a lost or ambiguous
    /// response, resend the identical request. To try again after a failed or cancelled turn, read the conversation for
    /// its current <c>transcriptRevision</c> and send a new turn with a new <c>operationId</c>.
    ///
    /// Authority: when API authorization is enabled, a bearer token with the exact scope <c>api.llm-chats.execute</c>;
    /// the broad <c>api</c> scope is not accepted. With authorization disabled (the development default) the route is
    /// open. Failures use Problem Details (<c>application/problem+json</c>) with a stable <c>code</c> member; failures
    /// after the body was read also carry the requested <c>operationId</c> and <c>retryable</c>.
    /// </remarks>
    /// <param name="conversationId">
    /// Identifier of the conversation, as returned in <c>id</c> by conversation creation or
    /// <c>GET /api/llm-conversations</c>.
    /// </param>
    /// <param name="httpRequest">
    /// The turn as a JSON object (<c>application/json</c>) with a new <c>operationId</c>, the conversation's current
    /// <c>transcriptRevision</c> as <c>expectedTranscriptRevision</c>, and the user <c>message</c>.
    /// </param>
    /// <response code="202">
    /// The turn was admitted, or an identical earlier request was replayed (<c>replayed</c> true). The body is the
    /// operation, usually in <c>pending</c> status; a replay returns its current status. The <c>Location</c> header is
    /// the operation's status URL.
    /// </response>
    /// <response code="400">
    /// <c>llm-chat.invalid-request</c>, nothing was admitted: the body is missing, is not JSON or has unknown members;
    /// <c>operationId</c> or the conversation identifier is the empty GUID; <c>expectedTranscriptRevision</c> is
    /// negative; or the message is blank or longer than 400,000 characters.
    /// </response>
    /// <response code="404"><c>llm-chat.conversation-not-found</c>: no conversation has this identifier.</response>
    /// <response code="409">
    /// The turn was not admitted: <c>llm-chat.operation-id-conflict</c> (the <c>operationId</c> belongs to a different
    /// request), <c>llm-chat.transcript-revision-conflict</c> (read the conversation for its current revision),
    /// <c>llm-chat.active-turn-conflict</c> (another turn is active or unfinished; follow it first),
    /// <c>llm-chat.conversation-archived</c>, <c>llm-chat.definition-not-active</c>, <c>llm-chat.storage-conflict</c>
    /// (a concurrent change; retryable) or <c>llm-chat.runtime-profile-changed</c> (retryable: resend the identical
    /// request).
    /// </response>
    /// <response code="422">
    /// The provider selection of the pinned definition revision is no longer usable, nothing was admitted:
    /// <c>llm-chat.provider-not-found</c>, <c>llm-chat.provider-kind-mismatch</c> (the profile now has another provider
    /// kind), <c>llm-chat.model-not-supported</c>, <c>llm-chat.thinking-effort-not-supported</c> or
    /// <c>llm-chat.model-settings-invalid</c>. The conversation stays pinned to its revision, so updating the
    /// definition does not repair it: restore the provider configuration, or update the definition and start a new
    /// conversation.
    /// </response>
    /// <response code="500">
    /// <c>llm-chat.storage-corrupted</c>: stored conversation, definition or transcript state is missing or
    /// inconsistent, or admission failed unexpectedly. Read <c>GET /api/llm-chat-operations/{operationId}</c> before
    /// resending: HTTP 404 there means the turn was not admitted.
    /// </response>
    /// <response code="503">
    /// Nothing was admitted and <c>retryable</c> is true: <c>llm-chat.dispatcher-unavailable</c> (this host runs no LLM
    /// Chat dispatcher) or <c>llm-chat.provider-unavailable</c> (the definition's provider profile is disabled or no
    /// longer serves chat). Resend the identical request later.
    /// </response>
    internal static async Task<IResult> SendTurnAsync(
        Guid conversationId,
        HttpRequest httpRequest,
        HttpResponse response,
        ILlmChatOperationApplicationService service,
        CancellationToken cancellationToken)
    {
        var body = await LlmChatApiRequestReader
            .ReadAsync<SendLlmChatTurnApiRequest>(httpRequest, cancellationToken)
            .ConfigureAwait(false);
        if (body.Error is not null)
        {
            return body.Error;
        }

        if (!TryCreateSendCommand(conversationId, body.Value!, out var command, out var error))
        {
            return error!;
        }

        var result = await service.SendAsync(command! with {
            HistoryCaller = ProviderHistoryRequestContext.Caller(httpRequest.HttpContext)
        }, cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
        {
            return LlmChatApiResults.FromFailure(result.Errors, body.Value!.OperationId);
        }

        var details = result.Value!;
        var location = SetOperationLocation(response, details.Operation.Id);
        return Results.Accepted(location, LlmChatOperationApiMapper.ToResponse(details));
    }

    /// <summary>
    /// Stream the events of an LLM Chat turn operation as server-sent events.
    /// </summary>
    /// <remarks>
    /// Opens a <c>text/event-stream</c> response that first replays the operation's retained events after your cursor,
    /// then delivers new events as they are committed, until the operation ends. Use it to show a reply while it is
    /// generated; the authoritative result stays <c>GET /api/llm-chat-operations/{operationId}</c>.
    ///
    /// Every event has an <c>id</c> (the event sequence, increasing by one per event of the operation), an <c>event</c>
    /// (the event type) and one <c>data</c> line with a JSON object of contract
    /// <c>candoitall.llm-chat-operation-event.v1</c>: <c>schema</c>, <c>operationId</c>, <c>conversationId</c>,
    /// <c>sequence</c>, <c>occurredAtUtc</c>, <c>eventKind</c> (the event type again), <c>operationState</c> (a
    /// camel-case operation status; <c>running</c> for attempt and delta events) and <c>payload</c>, whose members that
    /// do not apply are null. Event types:
    ///
    /// - <c>llm.operation.accepted</c>: the turn was admitted (state <c>pending</c>).
    /// - <c>llm.operation.claimed</c>: a dispatcher started executing it (state <c>running</c>).
    /// - <c>llm.provider.attempt-started</c>: a provider attempt began; payload <c>attemptOrdinal</c>, <c>model</c> and
    /// <c>deliveryMode</c> (<c>incremental</c> or <c>completedFallback</c>).
    /// - <c>llm.response.delta</c>: a chunk of reply text; payload <c>attemptOrdinal</c>, <c>text</c> and
    /// <c>aggregateCharacterCount</c> (characters delivered so far).
    /// - <c>llm.response.completed</c> and <c>llm.provider.attempt-finished</c>: an attempt succeeded, or ended
    /// otherwise; payload adds <c>finishReason</c>, <c>outcome</c> (<c>succeeded</c>, <c>failed</c> or
    /// <c>cancelled</c>), <c>usage</c>, <c>failureCode</c> and <c>retryable</c>.
    /// - <c>llm.operation.cancellation-requested</c>: cancellation was requested; payload <c>cancellationGeneration</c>
    /// and <c>cancellationRequestedAtUtc</c>.
    /// - <c>llm.operation.succeeded</c>, <c>llm.operation.failed</c>, <c>llm.operation.cancelled</c> and
    /// <c>llm.operation.recovery-required</c>: final events; payload <c>outputIncomplete</c>, <c>usage</c> (except for
    /// recovery), <c>failureCode</c> and <c>retryable</c> (except for success), and on success <c>model</c>,
    /// <c>assistantMessageId</c> and <c>transcriptRevision</c>.
    ///
    /// Delta text is provisional: it becomes the reply only when <c>llm.operation.succeeded</c> follows. After a
    /// failure, cancellation or recovery (<c>outputIncomplete</c> true) discard it.
    ///
    /// Resuming: send the <c>id</c> of the last event you processed as the <c>Last-Event-ID</c> header or as the
    /// <c>after</c> query parameter (a non-negative integer); when both are present they must be equal. Only later
    /// events are sent; omit both to start from the first event. When the cursor lies outside the retained events, the
    /// stream first sends a <c>stream.gap</c> event whose data holds <c>reason</c> (<c>cursorBeforeRetention</c> or
    /// <c>cursorAheadOfStream</c>), <c>requestedAfterSequence</c>, <c>firstAvailableSequence</c>,
    /// <c>lastAvailableSequence</c>, <c>resumeAfterSequence</c> and <c>snapshotUrl</c> (the operation's status URL);
    /// read that URL to recover what you missed. Events of finished operations are deleted after a retention period, 7
    /// days by default.
    ///
    /// While no event arrives, the server writes comment lines (<c>: heartbeat</c> and a timestamp), every 15 seconds
    /// by default. It closes the stream after a final event; reconnecting after that returns an empty stream that
    /// closes at once. The server can also end the stream early, for example when the host's active database profile
    /// changes: reconnect with the last <c>id</c>. Disconnecting does not cancel the operation; use
    /// <c>POST /api/llm-chat-operations/{operationId}/cancel</c>.
    ///
    /// Authority: when API authorization is enabled, a bearer token with the exact scope <c>api.llm-chats.read</c>; the
    /// broad <c>api</c> scope is not accepted. Send it in the <c>Authorization</c> header; tokens in query parameters
    /// are not accepted. With authorization disabled (the development default) the route is open.
    /// </remarks>
    /// <param name="operationId">
    /// Identifier of the operation: the <c>operationId</c> sent with the turn, also returned as
    /// <c>activeOperationId</c> by the conversation read.
    /// </param>
    /// <response code="200">
    /// The server-sent event stream (<c>text/event-stream</c>) described above, uncached and unbuffered. It ends after
    /// a final event.
    /// </response>
    /// <response code="400">
    /// Rejected before streaming. An invalid <c>Last-Event-ID</c> or <c>after</c> (not a single non-negative integer,
    /// or the two differ) returns this general error envelope (<c>errors</c> array, <c>application/json</c>) with code
    /// <c>llm-chat.stream-cursor-invalid</c>; start again from the operation's <c>lastEventSequence</c>. The empty GUID
    /// as operation identifier is rejected differently, with Problem Details (<c>application/problem+json</c>) and code
    /// <c>llm-chat.invalid-request</c>.
    /// </response>
    /// <response code="404"><c>llm-chat.operation-not-found</c>: no operation has this identifier.</response>
    /// <response code="409">
    /// <c>llm-chat.runtime-profile-changed</c>: the host's active database profile changed while the stream was opened.
    /// Reconnect.
    /// </response>
    internal static async Task StreamOperationEventsAsync(
        Guid operationId,
        HttpContext context,
        LlmChatOperationEventStreamSessionFactory streamSessionFactory,
        IOptions<ApiAccessOptions> apiOptions)
    {
        if (!LlmChatApiIds.TryCreateOperationId(operationId, out var id, out var error))
        {
            await error!.ExecuteAsync(context);
            return;
        }

        var opened = await streamSessionFactory.OpenAsync(id, context.RequestAborted).ConfigureAwait(false);
        if (opened.IsFailure)
        {
            await LlmChatApiResults.FromFailure(opened.Errors, operationId).ExecuteAsync(context);
            return;
        }

        await using var session = opened.Value!;
        var reader = new LlmChatOperationEventReplayReader(session, apiOptions.Value.ServerSentEvents);
        await ServerSentEventResponseWriter.WriteAsync(
            context,
            reader,
            static item => item.EventKind,
            static item => item.IsTerminal,
            session.ProfileLifetime,
            LlmChatErrorCodes.StreamCursorInvalid);
    }

    /// <summary>
    /// Read the current state of an LLM Chat turn operation.
    /// </summary>
    /// <remarks>
    /// Returns the durable state of an operation created by <c>POST /api/llm-conversations/{conversationId}/turns</c>:
    /// its status, the assistant reply once committed, the recorded failure and up to 100 provider invocation attempts
    /// in order. Poll it until <c>status</c> is <c>succeeded</c>, <c>failed</c> or <c>cancelled</c>, which are final,
    /// or <c>recoveryRequired</c>, which needs a decision (reconcile, then abandon if the outcome stays unknown). For
    /// live progress, use the event stream (<c>eventsUrl</c>) instead of frequent polling.
    ///
    /// Invocation attempts are an allowlisted projection of the provider evidence: provider kind, model, delivery mode,
    /// finish reason, requested and effective thinking effort, outcome, token usage and failure code. Provider profile
    /// identity, correlation identifiers and raw provider errors are not exposed.
    ///
    /// Authority: when API authorization is enabled, a bearer token with the exact scope <c>api.llm-chats.read</c>; the
    /// broad <c>api</c> scope is not accepted. With authorization disabled (the development default) the route is open.
    /// Failures use Problem Details (<c>application/problem+json</c>) with a stable <c>code</c> member and the
    /// requested <c>operationId</c>.
    /// </remarks>
    /// <param name="operationId">
    /// Identifier of the operation: the <c>operationId</c> sent with the turn, also returned as
    /// <c>activeOperationId</c> by the conversation read.
    /// </param>
    /// <response code="200">The operation.</response>
    /// <response code="400"><c>llm-chat.invalid-request</c>: the identifier is the empty GUID.</response>
    /// <response code="404"><c>llm-chat.operation-not-found</c>: no operation has this identifier.</response>
    /// <response code="409">
    /// <c>llm-chat.runtime-profile-changed</c>: the host's active database profile changed while the request ran.
    /// Repeat the request.
    /// </response>
    internal static async Task<IResult> GetOperationAsync(
        Guid operationId,
        ILlmChatOperationApplicationService service,
        CancellationToken cancellationToken)
    {
        if (!LlmChatApiIds.TryCreateOperationId(operationId, out var id, out var error))
        {
            return error!;
        }

        var result = await service.GetAsync(id, cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? Results.Ok(LlmChatOperationApiMapper.ToResponse(result.Value!))
            : LlmChatApiResults.FromFailure(result.Errors, operationId);
    }

    /// <summary>
    /// Request cancellation of an LLM Chat turn operation.
    /// </summary>
    /// <remarks>
    /// Records a durable cancellation request for a <c>pending</c> or <c>running</c> operation and signals its provider
    /// call when this host executes it; a dispatcher on another host notices the request at its next lease heartbeat.
    /// Cancellation is asynchronous: while the operation is <c>pending</c>, <c>running</c> or
    /// <c>cancellationRequested</c> the response is HTTP 202, and you follow the operation until it is final. It
    /// normally ends <c>cancelled</c>, with the user message removed from the transcript; depending on what the
    /// provider call had already done it can also end <c>failed</c> or <c>recoveryRequired</c>.
    ///
    /// For an operation that is already <c>succeeded</c>, <c>failed</c>, <c>cancelled</c> or <c>recoveryRequired</c>,
    /// nothing changes and the response is HTTP 200 with its current state; a committed reply is never withdrawn.
    /// Repeating the request is safe.
    ///
    /// Authority: when API authorization is enabled, a bearer token with the exact scope <c>api.llm-chats.execute</c>;
    /// the broad <c>api</c> scope is not accepted. With authorization disabled (the development default) the route is
    /// open. Failures use Problem Details (<c>application/problem+json</c>) with a stable <c>code</c> member and the
    /// requested <c>operationId</c>.
    /// </remarks>
    /// <param name="operationId">
    /// Identifier of the operation: the <c>operationId</c> sent with the turn, also returned as
    /// <c>activeOperationId</c> by the conversation read.
    /// </param>
    /// <response code="200">
    /// The operation was already final or requires recovery, so nothing changed; the body shows its state. The
    /// <c>Location</c> header is its status URL.
    /// </response>
    /// <response code="202">
    /// Cancellation is recorded and not yet settled; the body shows the operation, usually
    /// <c>cancellationRequested</c>. Follow the <c>Location</c> header until the operation is final.
    /// </response>
    /// <response code="400"><c>llm-chat.invalid-request</c>: the identifier is the empty GUID.</response>
    /// <response code="404"><c>llm-chat.operation-not-found</c>: no operation has this identifier.</response>
    /// <response code="409">
    /// <c>llm-chat.runtime-profile-changed</c>: the host's active database profile changed while the request ran.
    /// Repeat the request.
    /// </response>
    internal static async Task<IResult> CancelOperationAsync(
        Guid operationId,
        HttpResponse response,
        ILlmChatOperationApplicationService service,
        CancellationToken cancellationToken)
    {
        if (!LlmChatApiIds.TryCreateOperationId(operationId, out var id, out var error))
        {
            return error!;
        }

        var result = await service.CancelAsync(id, cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
        {
            return LlmChatApiResults.FromFailure(result.Errors, operationId);
        }

        var details = result.Value!;
        var location = SetOperationLocation(response, details.Operation.Id);
        var body = LlmChatOperationApiMapper.ToResponse(details);
        return details.Operation.Status is
            LlmChatOperationStatus.Pending or
            LlmChatOperationStatus.Running or
            LlmChatOperationStatus.CancellationRequested
                ? Results.Accepted(location, body)
                : Results.Ok(body);
    }

    /// <summary>
    /// Abandon a turn that requires recovery, removing its user message from the transcript.
    /// </summary>
    /// <remarks>
    /// Use this only for an operation in <c>recoveryRequired</c> status whose outcome reconciliation could not prove
    /// (<c>POST /api/llm-chat-operations/{operationId}/reconcile</c> left it in that status), once you accept that its
    /// reply is lost. The operation must belong to the conversation, must be in <c>recoveryRequired</c> status, must
    /// not be owned by a live execution and must still be the conversation's active turn. The provider is never called.
    ///
    /// On success the pending user message is removed, the transcript revision increases by one and the operation ends
    /// as <c>failed</c>, with its recovery reason (or <c>llm-chat.operation-recovery-required</c>) as failure code. The
    /// conversation then accepts new turns; read it for its current <c>transcriptRevision</c>. If the response still
    /// shows <c>recoveryRequired</c>, the abandonment did not complete: read the conversation before trying again.
    ///
    /// Authority: when API authorization is enabled, a bearer token with the exact scope <c>api.llm-chats.execute</c>;
    /// the broad <c>api</c> scope is not accepted. With authorization disabled (the development default) the route is
    /// open. Failures use Problem Details (<c>application/problem+json</c>) with a stable <c>code</c> member and the
    /// requested <c>turnId</c> as <c>operationId</c>.
    /// </remarks>
    /// <param name="conversationId">
    /// Identifier of the conversation, as returned in <c>id</c> by conversation creation or
    /// <c>GET /api/llm-conversations</c>.
    /// </param>
    /// <param name="turnId">
    /// Identifier of the turn to abandon, which is its operation identifier: the conversation's
    /// <c>activeOperationId</c>, equal to the <c>operationId</c> sent with the turn.
    /// </param>
    /// <response code="200">
    /// The body is the operation: <c>failed</c> when the turn was abandoned, still <c>recoveryRequired</c> when the
    /// abandonment did not complete. The <c>Location</c> header is its status URL.
    /// </response>
    /// <response code="400"><c>llm-chat.invalid-request</c>: an identifier is the empty GUID.</response>
    /// <response code="404">
    /// <c>llm-chat.operation-not-found</c>: no operation has the identifier <c>turnId</c>.
    /// </response>
    /// <response code="409">
    /// <c>llm-chat.operation-recovery-required</c>: the operation is not in <c>recoveryRequired</c> status, belongs to
    /// another conversation, is still owned by a live execution or is no longer the active turn; nothing was changed.
    /// Or <c>llm-chat.runtime-profile-changed</c>: the host's active database profile changed; read the operation
    /// before repeating the request.
    /// </response>
    internal static async Task<IResult> AbandonActiveTurnAsync(
        Guid conversationId,
        Guid turnId,
        HttpResponse response,
        ILlmChatOperationApplicationService service,
        CancellationToken cancellationToken)
    {
        if (!LlmChatApiIds.TryCreateConversationId(conversationId, out var conversation, out var conversationError))
        {
            return conversationError!;
        }

        if (!LlmChatApiIds.TryCreateOperationId(turnId, out var operation, out var operationError))
        {
            return operationError!;
        }

        var result = await service.AbandonActiveTurnAsync(
            new AbandonLlmChatActiveTurnCommand(conversation, operation),
            cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
        {
            return LlmChatApiResults.FromFailure(result.Errors, turnId);
        }

        SetOperationLocation(response, result.Value!.Operation.Id);
        return Results.Ok(LlmChatOperationApiMapper.ToResponse(result.Value));
    }

    /// <summary>
    /// Settle an interrupted LLM Chat turn operation from its durable evidence, without calling the provider.
    /// </summary>
    /// <remarks>
    /// Use this for an operation whose provider call may have started but that is no longer progressing, typically one
    /// in <c>recoveryRequired</c> status. Reconciliation never calls the provider and never dispatches the turn again.
    /// It inspects the stored transcript and invocation evidence and settles the operation only where that evidence
    /// proves the outcome: <c>succeeded</c> when the assistant reply was committed, <c>failed</c> or <c>cancelled</c>
    /// when the last attempt recorded a failure or cancellation (the user message is then removed from the transcript).
    /// Otherwise the operation stays in, or moves to, <c>recoveryRequired</c>; if you accept that the reply is lost,
    /// abandon the turn with <c>POST /api/llm-conversations/{conversationId}/active-turns/{turnId}/abandon</c>.
    ///
    /// An operation that is already final, or whose provider call never started, is returned unchanged. An operation
    /// still owned by a live execution is rejected with 409 <c>llm-chat.active-turn-conflict</c>: a running execution
    /// keeps renewing its lease, so retry once the execution has stopped and its lease has expired (10 seconds after
    /// its last heartbeat by default).
    ///
    /// Authority: when API authorization is enabled, a bearer token with the exact scope <c>api.llm-chats.manage</c>;
    /// the broad <c>api</c> scope is not accepted. With authorization disabled (the development default) the route is
    /// open. Failures use Problem Details (<c>application/problem+json</c>) with a stable <c>code</c> member and the
    /// requested <c>operationId</c>.
    /// </remarks>
    /// <param name="operationId">
    /// Identifier of the operation: the <c>operationId</c> sent with the turn, also returned as
    /// <c>activeOperationId</c> by the conversation read.
    /// </param>
    /// <response code="200">
    /// The operation after reconciliation; check <c>status</c>, which is unchanged when nothing could be proven or the
    /// operation was already final. The <c>Location</c> header is its status URL.
    /// </response>
    /// <response code="400"><c>llm-chat.invalid-request</c>: the identifier is the empty GUID.</response>
    /// <response code="404"><c>llm-chat.operation-not-found</c>: no operation has this identifier.</response>
    /// <response code="409">
    /// <c>llm-chat.active-turn-conflict</c> (a live execution still owns the operation; nothing was changed) or
    /// <c>llm-chat.runtime-profile-changed</c> (the host's active database profile changed; repeat the request).
    /// </response>
    internal static async Task<IResult> ReconcileOperationAsync(
        Guid operationId,
        HttpResponse response,
        ILlmChatOperationApplicationService service,
        CancellationToken cancellationToken)
    {
        if (!LlmChatApiIds.TryCreateOperationId(operationId, out var id, out var error))
        {
            return error!;
        }

        var result = await service.ReconcileAsync(id, cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
        {
            return LlmChatApiResults.FromFailure(result.Errors, operationId);
        }

        SetOperationLocation(response, result.Value!.Operation.Id);
        return Results.Ok(LlmChatOperationApiMapper.ToResponse(result.Value));
    }

    private static bool TryCreateSendCommand(
        Guid conversationId,
        SendLlmChatTurnApiRequest request,
        out SendLlmChatTurnCommand? command,
        out IResult? error)
    {
        command = null;
        if (!LlmChatApiIds.TryCreateConversationId(conversationId, out var conversation, out error) ||
            !LlmChatApiIds.TryCreateOperationId(request.OperationId, out var operation, out error))
        {
            return false;
        }

        if (request.ExpectedTranscriptRevision < 0)
        {
            error = LlmChatApiResults.InvalidRequest("Expected transcript revision cannot be negative.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(request.Message) || request.Message.Length > LlmMessage.MaximumTextLength)
        {
            error = LlmChatApiResults.InvalidRequest(
                $"A turn message is required and cannot exceed {LlmMessage.MaximumTextLength} characters.");
            return false;
        }

        command = new SendLlmChatTurnCommand(
            operation,
            conversation,
            request.ExpectedTranscriptRevision,
            request.Message);
        return true;
    }

    private static string SetOperationLocation(HttpResponse response, LlmChatOperationId operationId)
    {
        var location = LlmChatOperationApiRoutes.Status(operationId.Value);
        response.Headers.Location = location;
        return location;
    }
}
