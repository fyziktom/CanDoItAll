using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.SharedKernel;
using CanDoItAll.SharedKernel.Streaming;
using CanDoItAll.Web.Api.Streaming;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Web.Api;

internal static class AgentEventsApi
{
    public static RouteGroupBuilder MapAgentEventsApi(this RouteGroupBuilder group)
    {
        var agents = group.MapGroup("/agents")
            .WithTags("Agents")
            .DisableAntiforgery();

        agents.MapGet(
                "/execution-operations/{operationId:guid}/events/stream",
                StreamExistingOperationAsync)
            .WithName("StreamAgentExecutionOperationEvents")
            .Produces<string>(
                StatusCodes.Status200OK,
                contentType: ServerSentEventResponseWriter.ContentType)
            .ProducesApiErrors(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status401Unauthorized,
                StatusCodes.Status403Forbidden,
                StatusCodes.Status404NotFound);

        agents.MapPost("/{agentId:guid}/chat/stream", StreamChatAsync)
            .WithName("StreamAgentChatMessage")
            .Accepts<AgentChatApiRequest>("application/json")
            .Produces<string>(
                StatusCodes.Status200OK,
                contentType: ServerSentEventResponseWriter.ContentType)
            .ProducesApiErrors(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status401Unauthorized,
                StatusCodes.Status403Forbidden,
                StatusCodes.Status409Conflict,
                StatusCodes.Status410Gone,
                StatusCodes.Status422UnprocessableEntity,
                StatusCodes.Status500InternalServerError,
                StatusCodes.Status503ServiceUnavailable);

        agents.MapPost("/execution-runs/stream", StreamExecutionRunAsync)
            .WithName("StreamAgentExecutionRun")
            .Accepts<AgentExecutionRunApiRequest>("application/json")
            .Produces<string>(
                StatusCodes.Status200OK,
                contentType: ServerSentEventResponseWriter.ContentType)
            .ProducesApiErrors(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status401Unauthorized,
                StatusCodes.Status403Forbidden,
                StatusCodes.Status409Conflict,
                StatusCodes.Status410Gone,
                StatusCodes.Status422UnprocessableEntity,
                StatusCodes.Status500InternalServerError,
                StatusCodes.Status503ServiceUnavailable);

        agents.MapPost("/{agentId:guid}/execution-runs/stream", StreamScopedExecutionRunAsync)
            .WithName("StreamAgentScopedExecutionRun")
            .Accepts<AgentExecutionRunStartApiRequest>("application/json")
            .Produces<string>(
                StatusCodes.Status200OK,
                contentType: ServerSentEventResponseWriter.ContentType)
            .ProducesApiErrors(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status401Unauthorized,
                StatusCodes.Status403Forbidden,
                StatusCodes.Status409Conflict,
                StatusCodes.Status410Gone,
                StatusCodes.Status422UnprocessableEntity,
                StatusCodes.Status500InternalServerError,
                StatusCodes.Status503ServiceUnavailable);

        agents.MapPost(
                "/execution-runs/{executionRunId:guid}/pending-approvals/stream",
                StreamApprovalContinuationAsync)
            .WithName("StreamAgentExecutionApprovalResponse")
            .Accepts<PendingApprovalApiRequest>("application/json")
            .Produces<string>(
                StatusCodes.Status200OK,
                contentType: ServerSentEventResponseWriter.ContentType)
            .ProducesApiErrors(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status401Unauthorized,
                StatusCodes.Status403Forbidden,
                StatusCodes.Status409Conflict,
                StatusCodes.Status410Gone,
                StatusCodes.Status422UnprocessableEntity,
                StatusCodes.Status500InternalServerError,
                StatusCodes.Status503ServiceUnavailable);

        return group;
    }

    /// <summary>
    /// Stream the retained activity of an agent execution operation as server-sent events.
    /// </summary>
    /// <remarks>
    /// Opens a <c>text/event-stream</c> response that replays and then follows the activity of one agent execution
    /// operation: a chat message, an execution run, an approval response, a recovery or a cancellation reconciliation
    /// started through the <c>/api/agents</c> operations. Use it to follow an operation from another connection or to
    /// catch up after a dropped connection; the streaming commands (the <c>POST</c> operations under
    /// <c>/api/agents</c> whose route ends with <c>/stream</c>) already deliver the same activity events. The operation identifier is the <c>activityOperationId</c> sent with the
    /// command or, when none was sent, the value of the command's <c>X-CanDoItAll-Agent-Operation-Id</c> response
    /// header.
    ///
    /// Activity is held only in the memory of this server process, for the current database profile: at most the
    /// last 256 events of each operation, kept for up to 10 minutes after the operation finished (less when many
    /// other operations finish in the meantime). It is not a durable record: read the execution run for results.
    ///
    /// To resume, send the <c>id</c> of the last event received in the <c>Last-Event-ID</c> header or in the
    /// <c>after</c> query parameter (a non-negative integer; each at most once; when both are sent they must be
    /// equal). Only events with a larger <c>id</c> follow. Without a cursor the stream starts at the first retained
    /// event, after a <c>stream.gap</c> when earlier events were already discarded.
    ///
    /// Events; each <c>data:</c> line is one JSON object with camel-case members and camel-case string enum values:
    ///
    /// - <c>agent.activity</c>, <c>agent.approval.waiting</c>, <c>agent.activity.completed</c>,
    /// <c>agent.activity.failed</c> and <c>agent.activity.cancelled</c>: one activity, with <c>id:</c> set to its
    /// sequence number (1 for the first activity of the operation). Data: <c>operationId</c>; <c>phase</c>
    /// (<c>accepted</c>, <c>capturingContext</c>, <c>resolvingPreparation</c>, <c>resolvingProvider</c>,
    /// <c>resolvingSession</c>, <c>creatingExecution</c>, <c>preparingInput</c>, <c>preparingCapabilities</c>,
    /// <c>preparingRuntime</c>, <c>waitingForProvider</c>, <c>streaming</c>, <c>usingTool</c>,
    /// <c>awaitingApproval</c>, <c>persistingResult</c>, <c>completed</c>, <c>failed</c> or <c>cancelled</c>);
    /// <c>occurredAtUtc</c>; <c>agentId</c>; <c>message</c> (human-readable progress text, at most 2048
    /// characters); <c>chatSessionId</c> and <c>executionRunId</c> (null until known); <c>terminalOutcome</c>
    /// (<c>succeeded</c>, <c>failed</c>, <c>cancelled</c> or <c>suspended</c>; null before the last activity);
    /// <c>errorCode</c> (only on failure, for example <c>agent-execution-unhandled</c>).
    /// - <c>stream.gap</c>: events between the cursor and the oldest retained event were discarded. Data:
    /// <c>operationId</c>, <c>requestedFromInclusive</c> and <c>availableFromInclusive</c>. Its <c>id:</c> is the
    /// sequence just before the oldest retained event, which follows.
    /// - <c>stream.evicted</c>: the finished operation's events were discarded. Data: <c>operationId</c>,
    /// <c>reason</c> (<c>terminalRetentionExpired</c>, <c>terminalCapacityExceeded</c> or
    /// <c>partitionCapacityPressure</c>) and <c>evictedAtUtc</c>. Nothing follows.
    ///
    /// While no event arrives, a comment line <c>: heartbeat</c> followed by a UTC timestamp is sent every heartbeat
    /// interval (15 seconds by default). The server ends the response after the activity whose
    /// <c>terminalOutcome</c> is set (<c>suspended</c> means the run waits for tool approval) or after
    /// <c>stream.evicted</c>; a cursor at or after the last activity of a finished operation gives an empty stream.
    /// If the connection closes before that, reconnect with the last <c>id</c> received or read the execution run.
    /// This stream never carries the command's result or failure details: read them with
    /// <c>GET /api/agents/execution-runs/{executionRunId}</c>.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host; the stream is not
    /// limited to the caller that started the operation. Reading it does not change the operation.
    /// </remarks>
    /// <param name="operationId">
    /// Identifier of the agent execution operation (not the empty GUID): the <c>activityOperationId</c> sent with the
    /// command or the value of its <c>X-CanDoItAll-Agent-Operation-Id</c> response header.
    /// </param>
    /// <response code="200">
    /// The <c>text/event-stream</c> of the operation's activity described above. It stays open until the operation
    /// reaches its last activity or the client disconnects.
    /// </response>
    /// <response code="400">
    /// Rejected before streaming: the empty GUID as operation identifier (<c>agents.execution-operation-invalid</c>), a
    /// malformed, repeated or conflicting cursor (<c>sse.cursor-invalid</c>), or the largest 64-bit integer as cursor
    /// (<c>sse.cursor-exhausted</c>).
    /// </response>
    /// <response code="401">
    /// API authorization is enabled and the request has no valid bearer token (<c>api.authorization-required</c>).
    /// </response>
    /// <response code="403">
    /// An authorization policy rejected the authenticated caller (<c>api.authorization-forbidden</c>). This operation
    /// itself requires only a valid bearer token.
    /// </response>
    /// <response code="404">
    /// No activity is known for this operation (<c>agents.execution-operation-not-found</c>): the identifier is wrong,
    /// the operation ran in another server process or under another database profile, or its events were discarded
    /// and the record of that discard has also been dropped (at the latest 15 minutes later). Read the execution run
    /// instead.
    /// </response>
    internal static async Task<IResult> StreamExistingOperationAsync(
        Guid operationId,
        HttpContext context,
        ICurrentProfileAgentExecutionActivityReader activityReader,
        IOptions<ApiAccessOptions> apiOptions)
    {
        if (operationId == Guid.Empty)
        {
            return ApiEndpointResults.BadRequest(
                "The agent execution operation id cannot be empty.",
                "agents.execution-operation-invalid");
        }

        if (!ServerSentEventCursor.TryResolve(
                context.Request,
                out var afterExclusive,
                out var error))
        {
            return ApiEndpointResults.BadRequest(
                error ?? "The SSE cursor is invalid.",
                ServerSentEventResponseWriter.InvalidCursorCode);
        }

        if (afterExclusive == long.MaxValue)
        {
            return ApiEndpointResults.BadRequest(
                "The SSE cursor is exhausted.",
                "sse.cursor-exhausted");
        }

        var fromInclusive = afterExclusive == 0
            ? StreamSequence.Beginning
            : new StreamSequence(afterExclusive + 1);
        var typedOperationId = new AgentExecutionOperationId(operationId);
        await using (var validationReader = activityReader.OpenReader(
                         typedOperationId,
                         StreamSequence.Beginning))
        {
            var validationRead = await validationReader.ReadAsync(context.RequestAborted);
            if (validationRead is SequencedStreamUnknown<AgentExecutionActivity>)
            {
                return ApiEndpointResults.NotFound(
                    "The agent execution operation stream was not found.",
                    "agents.execution-operation-not-found");
            }
        }

        await using var reader = activityReader.OpenReader(
            typedOperationId,
            fromInclusive);
        ServerSentEventResponseWriter.Prepare(context.Response);
        var firstRead = await AgentActivityServerSentEventWriter.ReadWithHeartbeatAsync(
            context,
            reader,
            apiOptions.Value.ServerSentEvents.HeartbeatInterval);
        await AgentActivityServerSentEventWriter.PumpAsync(
            context,
            typedOperationId,
            reader,
            firstRead,
            apiOptions.Value.ServerSentEvents.HeartbeatInterval);
        return Results.Empty;
    }

    /// <summary>
    /// Send a chat message to an agent and stream the command's progress and result as server-sent events.
    /// </summary>
    /// <remarks>
    /// Streaming form of <c>POST /api/agents/{agentId}/chat</c>: the same request body and the same chat command, but
    /// the response is a <c>text/event-stream</c> that reports the command's activity while it runs and ends with its
    /// result, instead of one JSON object after the command finished. The message is added to the given chat session,
    /// or to a new session when <c>chatSessionId</c> is omitted, and runs as an agent execution run.
    ///
    /// Before the stream starts the request is checked and the command is admitted; a rejection is returned as JSON
    /// with one of the error statuses below. After HTTP 200, every outcome, including a failure, arrives as an event.
    /// The stream and the 409, 410, 422, 500 and 503 responses carry the command's activity operation identifier in
    /// the <c>X-CanDoItAll-Agent-Operation-Id</c> header: the <c>activityOperationId</c> from the body, or a new one
    /// when none was sent. Use a new GUID for every command.
    ///
    /// Events, in order; each <c>data:</c> line is one JSON object with camel-case members, and enum values are
    /// camel-case strings even where the JSON operations write integers:
    ///
    /// - Activity events with <c>id:</c> set to their sequence number (1, 2 and so on): <c>agent.activity</c> while
    /// the command progresses, <c>agent.approval.waiting</c> when the run waits for tool approval, and last
    /// <c>agent.activity.completed</c>, <c>agent.activity.failed</c> or <c>agent.activity.cancelled</c>. The data is
    /// the activity object described for <c>GET /api/agents/execution-operations/{operationId}/events/stream</c>; the
    /// activity whose <c>terminalOutcome</c> is set is the last one (<c>suspended</c> when the run waits for
    /// approval).
    /// - <c>stream.gap</c> or <c>stream.evicted</c>, as described for that operation, only when this connection falls
    /// behind the 256 retained activity events.
    /// - <c>agent.approval.required</c>, without <c>id:</c>, when the run waits for tool approvals. Data:
    /// <c>operationId</c>, <c>executionRunId</c> and <c>approvals</c>, each with <c>approvalId</c>, <c>toolName</c>,
    /// <c>toolKind</c> and <c>requestedAtUtc</c>. Answer them with
    /// <c>POST /api/agents/execution-runs/{executionRunId}/pending-approvals</c> or its streaming form.
    /// - Last, without <c>id:</c>, either <c>agent.command.completed</c> with <c>operationId</c> and <c>result</c>
    /// (the object that <c>POST /api/agents/{agentId}/chat</c> returns) or <c>agent.command.failed</c> with
    /// <c>operationId</c>, <c>code</c>, <c>message</c>, <c>correlationId</c>, <c>agentId</c>, <c>executionRunId</c>,
    /// <c>chatSessionId</c> and <c>providerFailureCategory</c>. A run that started and then failed reports
    /// <c>agents.run-failed</c>, or for a provider failure <c>agents.provider-request-incompatible</c>,
    /// <c>agents.provider-configuration-invalid</c>, <c>agents.provider-quota-unavailable</c>,
    /// <c>agents.provider-rate-limited</c> or <c>agents.provider-failed</c> with the matching
    /// <c>providerFailureCategory</c>. Any other failure, including a request rejected after the stream started such
    /// as an unknown agent or chat session, reports <c>agents.command-failed</c> without further detail.
    ///
    /// While no activity arrives, a comment line <c>: heartbeat</c> followed by a UTC timestamp is sent every
    /// heartbeat interval (15 seconds by default). The server closes the response after the last event.
    ///
    /// The command runs only while this response is open: closing the connection cancels it, and work that already
    /// completed, such as tool calls, is not undone. The last events are not replayable. After a dropped connection,
    /// read the run with <c>GET /api/agents/execution-runs/{executionRunId}</c> (its identifier appears in the
    /// activity events) before sending the message again.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host.
    /// </remarks>
    /// <param name="agentId">
    /// Identifier of the agent that receives the message (not the empty GUID), as listed by <c>GET /api/agents</c>.
    /// </param>
    /// <param name="request">
    /// The chat message, the same body as for <c>POST /api/agents/{agentId}/chat</c>: <c>prompt</c> (required, not
    /// blank); <c>chatSessionId</c> of an existing chat session of this agent, or omitted to start a new session;
    /// optional <c>attachmentPaths</c> with up to 8 image paths returned by
    /// <c>POST /api/agents/attachments/images</c>; optional <c>activityOperationId</c>, a new GUID that identifies
    /// this command's activity.
    /// </param>
    /// <response code="200">
    /// The <c>text/event-stream</c> described above. Unless the client disconnects it ends with
    /// <c>agent.command.completed</c> or <c>agent.command.failed</c>.
    /// </response>
    /// <response code="400">
    /// Rejected before the stream started (<c>agents.request-invalid</c>): the empty GUID as agent or chat session
    /// identifier, or a blank prompt. A body the framework cannot bind, for example malformed JSON or an
    /// <c>activityOperationId</c> that is not a non-empty GUID string, is rejected with HTTP 400 before the operation
    /// runs and has no error envelope.
    /// </response>
    /// <response code="401">
    /// API authorization is enabled and the request has no valid bearer token (<c>api.authorization-required</c>).
    /// </response>
    /// <response code="403">
    /// An authorization policy rejected the authenticated caller (<c>api.authorization-forbidden</c>). This operation
    /// itself requires only a valid bearer token.
    /// </response>
    /// <response code="409">
    /// The <c>activityOperationId</c> belongs to an operation whose activity is still retained
    /// (<c>agents.execution-operation-duplicate</c>). Nothing was started; send a new GUID.
    /// </response>
    /// <response code="410">
    /// The <c>activityOperationId</c> belongs to an earlier operation whose activity was discarded
    /// (<c>agents.execution-operation-evicted</c>). Nothing was started; send a new GUID.
    /// </response>
    /// <response code="422">
    /// The run failed before the stream started because the provider profile is not usable
    /// (<c>agents.provider-configuration-invalid</c>). Run failures normally arrive in the stream instead.
    /// </response>
    /// <response code="500">
    /// The run failed before the stream started (<c>agents.run-failed</c>). Run failures normally arrive in the
    /// stream instead; read the run named in <c>executionRunId</c> before retrying.
    /// </response>
    /// <response code="503">
    /// The server is tracking too many unfinished operations (<c>agents.execution-operation-capacity-exhausted</c>;
    /// nothing was started, retry later), or the run failed before the stream started because the provider was
    /// unavailable (<c>agents.provider-quota-unavailable</c>, <c>agents.provider-rate-limited</c> or
    /// <c>agents.provider-failed</c>).
    /// </response>
    internal static Task<IResult> StreamChatAsync(
        Guid agentId,
        AgentChatApiRequest request,
        HttpContext context,
        IAgentFrameworkWorkspaceService workspaceService,
        ICurrentProfileAgentExecutionActivityReader activityReader,
        IOptions<ApiAccessOptions> apiOptions,
        ILoggerFactory loggerFactory)
    {
        var validation = AgentApiRequestValidation.ValidateCommand(
            context,
            agentId,
            request.ChatSessionId,
            request.Prompt);
        if (validation is not null)
        {
            return Task.FromResult(validation);
        }

        var operationId = request.ActivityOperationId ?? AgentExecutionOperationId.New();
        return StreamCommandAsync(
            context,
            operationId,
            agentId,
            null,
            request.ChatSessionId,
            cancellationToken => workspaceService.SendMessageAsync(
                agentId,
                request.ChatSessionId,
                request.Prompt,
                new AgentChatRunOptions(operationId) { Context = ProviderHistoryRequestContext.ForExecution(null, context) },
                cancellationToken,
                request.AttachmentPaths),
            static result => result.ExecutionRunId,
            AgentApiResponseMapper.ToChatRunResult,
            workspaceService,
            activityReader,
            apiOptions.Value.ServerSentEvents.HeartbeatInterval,
            loggerFactory.CreateLogger("CanDoItAll.Web.Api.AgentChatStream"));
    }

    /// <summary>
    /// Start an agent execution run and stream its progress and result as server-sent events.
    /// </summary>
    /// <remarks>
    /// Streaming form of <c>POST /api/agents/execution-runs</c>: the same request body and the same run, but the
    /// response is a <c>text/event-stream</c> that reports the run's activity while it runs and ends with its result,
    /// instead of one JSON object after the run finished. <c>POST /api/agents/{agentId}/execution-runs/stream</c> is
    /// the same operation with the agent in the route.
    ///
    /// Before the stream starts the request is checked and the command is admitted; a rejection is returned as JSON
    /// with one of the error statuses below. After HTTP 200, every outcome, including a failure, arrives as an event.
    /// The stream and the 409, 410, 422, 500 and 503 responses carry the command's activity operation identifier in
    /// the <c>X-CanDoItAll-Agent-Operation-Id</c> header: the <c>activityOperationId</c> from the body, or a new one
    /// when none was sent. Use a new GUID for every command.
    ///
    /// Events, in order; each <c>data:</c> line is one JSON object with camel-case members, and enum values are
    /// camel-case strings even where the JSON operations write integers:
    ///
    /// - Activity events with <c>id:</c> set to their sequence number (1, 2 and so on): <c>agent.activity</c> while
    /// the run progresses, <c>agent.approval.waiting</c> when it waits for tool approval, and last
    /// <c>agent.activity.completed</c>, <c>agent.activity.failed</c> or <c>agent.activity.cancelled</c>. The data is
    /// the activity object described for <c>GET /api/agents/execution-operations/{operationId}/events/stream</c>; the
    /// activity whose <c>terminalOutcome</c> is set is the last one (<c>suspended</c> when the run waits for
    /// approval).
    /// - <c>stream.gap</c> or <c>stream.evicted</c>, as described for that operation, only when this connection falls
    /// behind the 256 retained activity events.
    /// - <c>agent.approval.required</c>, without <c>id:</c>, when the run waits for tool approvals. Data:
    /// <c>operationId</c>, <c>executionRunId</c> and <c>approvals</c>, each with <c>approvalId</c>, <c>toolName</c>,
    /// <c>toolKind</c> and <c>requestedAtUtc</c>. Answer them with
    /// <c>POST /api/agents/execution-runs/{executionRunId}/pending-approvals</c> or its streaming form.
    /// - Last, without <c>id:</c>, either <c>agent.command.completed</c> with <c>operationId</c> and <c>result</c>
    /// (the object that <c>POST /api/agents/execution-runs</c> returns) or <c>agent.command.failed</c> with
    /// <c>operationId</c>, <c>code</c>, <c>message</c>, <c>correlationId</c>, <c>agentId</c>, <c>executionRunId</c>,
    /// <c>chatSessionId</c> and <c>providerFailureCategory</c>. A run that started and then failed reports
    /// <c>agents.run-failed</c>, or for a provider failure <c>agents.provider-request-incompatible</c>,
    /// <c>agents.provider-configuration-invalid</c>, <c>agents.provider-quota-unavailable</c>,
    /// <c>agents.provider-rate-limited</c> or <c>agents.provider-failed</c> with the matching
    /// <c>providerFailureCategory</c>. Any other failure, including a request rejected after the stream started such
    /// as an unknown agent or an invalid structured-output contract, reports <c>agents.command-failed</c> without
    /// further detail.
    ///
    /// While no activity arrives, a comment line <c>: heartbeat</c> followed by a UTC timestamp is sent every
    /// heartbeat interval (15 seconds by default). The server closes the response after the last event.
    ///
    /// The run lives only as long as this response: closing the connection cancels it, and work that already
    /// completed, such as tool calls, is not undone. The last events are not replayable. After a dropped connection,
    /// read the run with <c>GET /api/agents/execution-runs/{executionRunId}</c> (its identifier appears in the
    /// activity events) before starting it again.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host.
    /// </remarks>
    /// <param name="request">
    /// The run to start, the same body as for <c>POST /api/agents/execution-runs</c>: <c>agentId</c> (required, not
    /// the empty GUID) and <c>prompt</c> (required, not blank); optionally <c>chatSessionId</c> of a chat session of
    /// that agent to record the run in, invocation <c>context</c>, <c>autoApprovePendingToolCalls</c>, a
    /// <c>structuredOutput</c> JSON Schema contract, up to 8 <c>inputAttachmentPaths</c> returned by
    /// <c>POST /api/agents/attachments/images</c>, and <c>activityOperationId</c>, a new GUID that identifies this
    /// command's activity.
    /// </param>
    /// <response code="200">
    /// The <c>text/event-stream</c> described above. Unless the client disconnects it ends with
    /// <c>agent.command.completed</c> or <c>agent.command.failed</c>.
    /// </response>
    /// <response code="400">
    /// Rejected before the stream started (<c>agents.request-invalid</c>): the empty GUID as agent or chat session
    /// identifier, or a blank prompt. A body the framework cannot bind, for example malformed JSON or an
    /// <c>activityOperationId</c> that is not a non-empty GUID string, is rejected with HTTP 400 before the operation
    /// runs and has no error envelope.
    /// </response>
    /// <response code="401">
    /// API authorization is enabled and the request has no valid bearer token (<c>api.authorization-required</c>).
    /// </response>
    /// <response code="403">
    /// An authorization policy rejected the authenticated caller (<c>api.authorization-forbidden</c>). This operation
    /// itself requires only a valid bearer token.
    /// </response>
    /// <response code="409">
    /// The <c>activityOperationId</c> belongs to an operation whose activity is still retained
    /// (<c>agents.execution-operation-duplicate</c>). Nothing was started; send a new GUID.
    /// </response>
    /// <response code="410">
    /// The <c>activityOperationId</c> belongs to an earlier operation whose activity was discarded
    /// (<c>agents.execution-operation-evicted</c>). Nothing was started; send a new GUID.
    /// </response>
    /// <response code="422">
    /// The run failed before the stream started because the provider profile is not usable
    /// (<c>agents.provider-configuration-invalid</c>). Run failures normally arrive in the stream instead.
    /// </response>
    /// <response code="500">
    /// The run failed before the stream started (<c>agents.run-failed</c>). Run failures normally arrive in the
    /// stream instead; read the run named in <c>executionRunId</c> before retrying.
    /// </response>
    /// <response code="503">
    /// The server is tracking too many unfinished operations (<c>agents.execution-operation-capacity-exhausted</c>;
    /// nothing was started, retry later), or the run failed before the stream started because the provider was
    /// unavailable (<c>agents.provider-quota-unavailable</c>, <c>agents.provider-rate-limited</c> or
    /// <c>agents.provider-failed</c>).
    /// </response>
    internal static Task<IResult> StreamExecutionRunAsync(
        AgentExecutionRunApiRequest request,
        HttpContext context,
        IAgentFrameworkWorkspaceService workspaceService,
        ICurrentProfileAgentExecutionActivityReader activityReader,
        IOptions<ApiAccessOptions> apiOptions,
        ILoggerFactory loggerFactory)
    {
        var validation = AgentApiRequestValidation.ValidateCommand(
            context,
            request.AgentId,
            request.ChatSessionId,
            request.Prompt);
        if (validation is not null)
        {
            return Task.FromResult(validation);
        }

        var operationId = request.ActivityOperationId ?? AgentExecutionOperationId.New();
        var executionRequest = new ExecutionRunRequest(
            request.AgentId,
            request.Prompt,
            operationId,
            request.ChatSessionId,
            ProviderHistoryRequestContext.ForExecution(request.Context, context),
            request.AutoApprovePendingToolCalls,
            InputAttachmentPaths: request.InputAttachmentPaths,
            JsonSchemaOutput: request.StructuredOutput);
        return StreamCommandAsync(
            context,
            operationId,
            request.AgentId,
            null,
            request.ChatSessionId,
            cancellationToken => workspaceService.ExecuteRunAsync(
                executionRequest,
                cancellationToken),
            static result => result.ExecutionRunId,
            AgentApiResponseMapper.ToExecutionRunResult,
            workspaceService,
            activityReader,
            apiOptions.Value.ServerSentEvents.HeartbeatInterval,
            loggerFactory.CreateLogger("CanDoItAll.Web.Api.AgentExecutionStream"));
    }

    /// <summary>
    /// Start an execution run of the agent in the route and stream its progress and result as server-sent events.
    /// </summary>
    /// <remarks>
    /// Streaming form of <c>POST /api/agents/{agentId}/execution-runs</c>: the same request body and the same run, but
    /// the response is a <c>text/event-stream</c> that reports the run's activity while it runs and ends with its
    /// result, instead of one JSON object after the run finished. It behaves like
    /// <c>POST /api/agents/execution-runs/stream</c> with the agent taken from the route.
    ///
    /// Before the stream starts the request is checked and the command is admitted; a rejection is returned as JSON
    /// with one of the error statuses below. After HTTP 200, every outcome, including a failure, arrives as an event.
    /// The stream and the 409, 410, 422, 500 and 503 responses carry the command's activity operation identifier in
    /// the <c>X-CanDoItAll-Agent-Operation-Id</c> header: the <c>activityOperationId</c> from the body, or a new one
    /// when none was sent. Use a new GUID for every command.
    ///
    /// Events, in order; each <c>data:</c> line is one JSON object with camel-case members, and enum values are
    /// camel-case strings even where the JSON operations write integers:
    ///
    /// - Activity events with <c>id:</c> set to their sequence number (1, 2 and so on): <c>agent.activity</c> while
    /// the run progresses, <c>agent.approval.waiting</c> when it waits for tool approval, and last
    /// <c>agent.activity.completed</c>, <c>agent.activity.failed</c> or <c>agent.activity.cancelled</c>. The data is
    /// the activity object described for <c>GET /api/agents/execution-operations/{operationId}/events/stream</c>; the
    /// activity whose <c>terminalOutcome</c> is set is the last one (<c>suspended</c> when the run waits for
    /// approval).
    /// - <c>stream.gap</c> or <c>stream.evicted</c>, as described for that operation, only when this connection falls
    /// behind the 256 retained activity events.
    /// - <c>agent.approval.required</c>, without <c>id:</c>, when the run waits for tool approvals. Data:
    /// <c>operationId</c>, <c>executionRunId</c> and <c>approvals</c>, each with <c>approvalId</c>, <c>toolName</c>,
    /// <c>toolKind</c> and <c>requestedAtUtc</c>. Answer them with
    /// <c>POST /api/agents/execution-runs/{executionRunId}/pending-approvals</c> or its streaming form.
    /// - Last, without <c>id:</c>, either <c>agent.command.completed</c> with <c>operationId</c> and <c>result</c>
    /// (the object that <c>POST /api/agents/{agentId}/execution-runs</c> returns) or <c>agent.command.failed</c> with
    /// <c>operationId</c>, <c>code</c>, <c>message</c>, <c>correlationId</c>, <c>agentId</c>, <c>executionRunId</c>,
    /// <c>chatSessionId</c> and <c>providerFailureCategory</c>. A run that started and then failed reports
    /// <c>agents.run-failed</c>, or for a provider failure <c>agents.provider-request-incompatible</c>,
    /// <c>agents.provider-configuration-invalid</c>, <c>agents.provider-quota-unavailable</c>,
    /// <c>agents.provider-rate-limited</c> or <c>agents.provider-failed</c> with the matching
    /// <c>providerFailureCategory</c>. Any other failure, including a request rejected after the stream started such
    /// as an unknown agent or an invalid structured-output contract, reports <c>agents.command-failed</c> without
    /// further detail.
    ///
    /// While no activity arrives, a comment line <c>: heartbeat</c> followed by a UTC timestamp is sent every
    /// heartbeat interval (15 seconds by default). The server closes the response after the last event.
    ///
    /// The run lives only as long as this response: closing the connection cancels it, and work that already
    /// completed, such as tool calls, is not undone. The last events are not replayable. After a dropped connection,
    /// read the run with <c>GET /api/agents/execution-runs/{executionRunId}</c> (its identifier appears in the
    /// activity events) before starting it again.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host.
    /// </remarks>
    /// <param name="agentId">
    /// Identifier of the agent to run (not the empty GUID), as listed by <c>GET /api/agents</c>.
    /// </param>
    /// <param name="request">
    /// The run to start, the same body as for <c>POST /api/agents/{agentId}/execution-runs</c>: <c>prompt</c>
    /// (required, not blank); optionally <c>chatSessionId</c> of a chat session of this agent to record the run in,
    /// invocation <c>context</c>, <c>autoApprovePendingToolCalls</c>, a <c>structuredOutput</c> JSON Schema contract,
    /// up to 8 <c>inputAttachmentPaths</c> returned by <c>POST /api/agents/attachments/images</c>, and
    /// <c>activityOperationId</c>, a new GUID that identifies this command's activity.
    /// </param>
    /// <response code="200">
    /// The <c>text/event-stream</c> described above. Unless the client disconnects it ends with
    /// <c>agent.command.completed</c> or <c>agent.command.failed</c>.
    /// </response>
    /// <response code="400">
    /// Rejected before the stream started (<c>agents.request-invalid</c>): the empty GUID as agent or chat session
    /// identifier, or a blank prompt. A body the framework cannot bind, for example malformed JSON or an
    /// <c>activityOperationId</c> that is not a non-empty GUID string, is rejected with HTTP 400 before the operation
    /// runs and has no error envelope.
    /// </response>
    /// <response code="401">
    /// API authorization is enabled and the request has no valid bearer token (<c>api.authorization-required</c>).
    /// </response>
    /// <response code="403">
    /// An authorization policy rejected the authenticated caller (<c>api.authorization-forbidden</c>). This operation
    /// itself requires only a valid bearer token.
    /// </response>
    /// <response code="409">
    /// The <c>activityOperationId</c> belongs to an operation whose activity is still retained
    /// (<c>agents.execution-operation-duplicate</c>). Nothing was started; send a new GUID.
    /// </response>
    /// <response code="410">
    /// The <c>activityOperationId</c> belongs to an earlier operation whose activity was discarded
    /// (<c>agents.execution-operation-evicted</c>). Nothing was started; send a new GUID.
    /// </response>
    /// <response code="422">
    /// The run failed before the stream started because the provider profile is not usable
    /// (<c>agents.provider-configuration-invalid</c>). Run failures normally arrive in the stream instead.
    /// </response>
    /// <response code="500">
    /// The run failed before the stream started (<c>agents.run-failed</c>). Run failures normally arrive in the
    /// stream instead; read the run named in <c>executionRunId</c> before retrying.
    /// </response>
    /// <response code="503">
    /// The server is tracking too many unfinished operations (<c>agents.execution-operation-capacity-exhausted</c>;
    /// nothing was started, retry later), or the run failed before the stream started because the provider was
    /// unavailable (<c>agents.provider-quota-unavailable</c>, <c>agents.provider-rate-limited</c> or
    /// <c>agents.provider-failed</c>).
    /// </response>
    internal static Task<IResult> StreamScopedExecutionRunAsync(
        Guid agentId,
        AgentExecutionRunStartApiRequest request,
        HttpContext context,
        IAgentFrameworkWorkspaceService workspaceService,
        ICurrentProfileAgentExecutionActivityReader activityReader,
        IOptions<ApiAccessOptions> apiOptions,
        ILoggerFactory loggerFactory)
    {
        var validation = AgentApiRequestValidation.ValidateCommand(
            context,
            agentId,
            request.ChatSessionId,
            request.Prompt);
        if (validation is not null)
        {
            return Task.FromResult(validation);
        }

        var operationId = request.ActivityOperationId ?? AgentExecutionOperationId.New();
        var executionRequest = new ExecutionRunRequest(
            agentId,
            request.Prompt,
            operationId,
            request.ChatSessionId,
            ProviderHistoryRequestContext.ForExecution(request.Context, context),
            request.AutoApprovePendingToolCalls,
            InputAttachmentPaths: request.InputAttachmentPaths,
            JsonSchemaOutput: request.StructuredOutput);
        return StreamCommandAsync(
            context,
            operationId,
            agentId,
            null,
            request.ChatSessionId,
            cancellationToken => workspaceService.ExecuteRunAsync(
                executionRequest,
                cancellationToken),
            static result => result.ExecutionRunId,
            AgentApiResponseMapper.ToExecutionRunResult,
            workspaceService,
            activityReader,
            apiOptions.Value.ServerSentEvents.HeartbeatInterval,
            loggerFactory.CreateLogger("CanDoItAll.Web.Api.AgentExecutionStream"));
    }

    /// <summary>
    /// Answer the pending tool approvals of an agent execution run and stream the continued run as server-sent
    /// events.
    /// </summary>
    /// <remarks>
    /// Streaming form of <c>POST /api/agents/execution-runs/{executionRunId}/pending-approvals</c>: the same request
    /// body and the same continuation, but the response is a <c>text/event-stream</c> that reports the continued run's
    /// activity and ends with its result. Read the run's current approvals first
    /// (<c>GET /api/agents/execution-runs/{executionRunId}/approvals</c>) and send one decision per pending approval in
    /// <c>decisions</c>. When <c>decisions</c> is absent or empty, <c>approved</c> is applied to every approval
    /// pending when the request arrives.
    ///
    /// Differences from the JSON operation: a decision set that does not exactly match the pending approvals, an
    /// unknown run and a run that another request is already continuing are reported in the stream as
    /// <c>agent.command.failed</c> with <c>agents.command-failed</c>, not as HTTP 400
    /// <c>agents.approval-decision-mismatch</c>. Without <c>decisions</c>, the pending approvals are read before the
    /// stream starts, and an unknown run then fails with HTTP 500 without an error envelope. A run that already
    /// finished and has nothing pending ends with <c>agent.command.completed</c> carrying its stored result.
    ///
    /// Before the stream starts the request is checked and the command is admitted; a rejection is returned as JSON
    /// with one of the error statuses below. The stream and the 409, 410, 422, 500 and 503 responses carry the
    /// command's activity operation identifier in the <c>X-CanDoItAll-Agent-Operation-Id</c> header: the
    /// <c>activityOperationId</c> from the body, or a new one when none was sent. Use a new GUID for every command.
    ///
    /// Events, in order; each <c>data:</c> line is one JSON object with camel-case members, and enum values are
    /// camel-case strings even where the JSON operations write integers:
    ///
    /// - Activity events with <c>id:</c> set to their sequence number (1, 2 and so on): <c>agent.activity</c> while
    /// the run progresses, <c>agent.approval.waiting</c> when it waits for tool approval again, and last
    /// <c>agent.activity.completed</c>, <c>agent.activity.failed</c> or <c>agent.activity.cancelled</c>. The data is
    /// the activity object described for <c>GET /api/agents/execution-operations/{operationId}/events/stream</c>; the
    /// activity whose <c>terminalOutcome</c> is set is the last one (<c>suspended</c> when the run waits for
    /// approval).
    /// - <c>stream.gap</c> or <c>stream.evicted</c>, as described for that operation, only when this connection falls
    /// behind the 256 retained activity events.
    /// - <c>agent.approval.required</c>, without <c>id:</c>, when the run waits for new tool approvals. Data:
    /// <c>operationId</c>, <c>executionRunId</c> and <c>approvals</c>, each with <c>approvalId</c>, <c>toolName</c>,
    /// <c>toolKind</c> and <c>requestedAtUtc</c>.
    /// - Last, without <c>id:</c>, either <c>agent.command.completed</c> with <c>operationId</c> and <c>result</c>
    /// (the object that <c>POST /api/agents/execution-runs/{executionRunId}/pending-approvals</c> returns) or
    /// <c>agent.command.failed</c> with <c>operationId</c>, <c>code</c>, <c>message</c>, <c>correlationId</c>,
    /// <c>agentId</c>, <c>executionRunId</c>, <c>chatSessionId</c> and <c>providerFailureCategory</c>. A continued
    /// run that failed reports <c>agents.run-failed</c>, or for a provider failure
    /// <c>agents.provider-request-incompatible</c>, <c>agents.provider-configuration-invalid</c>,
    /// <c>agents.provider-quota-unavailable</c>, <c>agents.provider-rate-limited</c> or <c>agents.provider-failed</c>
    /// with the matching <c>providerFailureCategory</c>. Any other failure reports <c>agents.command-failed</c>
    /// without further detail.
    ///
    /// While no activity arrives, a comment line <c>: heartbeat</c> followed by a UTC timestamp is sent every
    /// heartbeat interval (15 seconds by default). The server closes the response after the last event.
    ///
    /// The continuation lives only as long as this response: closing the connection cancels it, and work that
    /// already completed, such as approved tool calls, is not undone. The last events are not replayable. After a
    /// dropped connection or a failure, read the run and its approvals again before sending new decisions.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host.
    /// </remarks>
    /// <param name="executionRunId">
    /// Identifier of the execution run waiting for approval (not the empty GUID): the <c>executionRunId</c> of the
    /// run or of its <c>agent.approval.required</c> event.
    /// </param>
    /// <param name="request">
    /// The decisions, the same body as for <c>POST /api/agents/execution-runs/{executionRunId}/pending-approvals</c>:
    /// <c>decisions</c> with one <c>approvalId</c> and <c>approved</c> flag per pending approval; <c>approved</c>,
    /// applied to every pending approval when <c>decisions</c> is absent or empty; <c>autoApprovePendingToolCalls</c>;
    /// optional <c>activityOperationId</c>, a new GUID that identifies this command's activity.
    /// </param>
    /// <response code="200">
    /// The <c>text/event-stream</c> described above. Unless the client disconnects it ends with
    /// <c>agent.command.completed</c> or <c>agent.command.failed</c>.
    /// </response>
    /// <response code="400">
    /// Rejected before the stream started (<c>agents.request-invalid</c>): the empty GUID as run identifier. A body the
    /// framework cannot bind, for example malformed JSON or an <c>activityOperationId</c> that is not a non-empty GUID
    /// string, is rejected with HTTP 400 before the operation runs and has no error envelope.
    /// </response>
    /// <response code="401">
    /// API authorization is enabled and the request has no valid bearer token (<c>api.authorization-required</c>).
    /// </response>
    /// <response code="403">
    /// An authorization policy rejected the authenticated caller (<c>api.authorization-forbidden</c>). This operation
    /// itself requires only a valid bearer token.
    /// </response>
    /// <response code="409">
    /// The <c>activityOperationId</c> belongs to an operation whose activity is still retained
    /// (<c>agents.execution-operation-duplicate</c>). Nothing was started; send a new GUID.
    /// </response>
    /// <response code="410">
    /// The <c>activityOperationId</c> belongs to an earlier operation whose activity was discarded
    /// (<c>agents.execution-operation-evicted</c>). Nothing was started; send a new GUID.
    /// </response>
    /// <response code="422">
    /// The continued run failed before the stream started because the provider profile is not usable
    /// (<c>agents.provider-configuration-invalid</c>). Run failures normally arrive in the stream instead.
    /// </response>
    /// <response code="500">
    /// The continued run failed before the stream started (<c>agents.run-failed</c>). Run failures normally arrive in
    /// the stream instead; read the run before retrying.
    /// </response>
    /// <response code="503">
    /// The server is tracking too many unfinished operations (<c>agents.execution-operation-capacity-exhausted</c>;
    /// nothing was started, retry later), or the continued run failed before the stream started because the provider
    /// was unavailable (<c>agents.provider-quota-unavailable</c>, <c>agents.provider-rate-limited</c> or
    /// <c>agents.provider-failed</c>).
    /// </response>
    internal static async Task<IResult> StreamApprovalContinuationAsync(
        Guid executionRunId,
        PendingApprovalApiRequest request,
        HttpContext context,
        IAgentFrameworkWorkspaceService workspaceService,
        ICurrentProfileAgentExecutionActivityReader activityReader,
        IOptions<ApiAccessOptions> apiOptions,
        ILoggerFactory loggerFactory)
    {
        var validation = AgentApiRequestValidation.ValidateExecutionRun(
            context,
            executionRunId);
        if (validation is not null)
        {
            return validation;
        }

        var operationId = request.ActivityOperationId ?? AgentExecutionOperationId.New();

        // Resolved here (before StreamCommandAsync starts the command) rather than inside the
        // startCommand lambda so ContinueExecutionRunAsync's own synchronous admission-rejection
        // throw keeps reaching StreamCommandAsync's existing catch clauses unchanged — wrapping
        // the whole thing in an async lambda would defer every exception (including admission
        // rejection) into the completion Task instead.
        var decisions = await AgentApprovalDecisionRequestMapper.ResolveDecisionsAsync(
            workspaceService,
            executionRunId,
            request,
            context.RequestAborted);

        return await StreamCommandAsync(
            context,
            operationId,
            null,
            executionRunId,
            null,
            cancellationToken => workspaceService.ContinueExecutionRunAsync(
                executionRunId,
                operationId,
                decisions,
                request.AutoApprovePendingToolCalls,
                cancellationToken),
            static result => result.ExecutionRunId,
            AgentApiResponseMapper.ToExecutionRunResult,
            workspaceService,
            activityReader,
            apiOptions.Value.ServerSentEvents.HeartbeatInterval,
            loggerFactory.CreateLogger("CanDoItAll.Web.Api.AgentApprovalStream"));
    }

    private static async Task<IResult> StreamCommandAsync<TResult, TApiResult>(
        HttpContext context,
        AgentExecutionOperationId operationId,
        Guid? agentId,
        Guid? knownExecutionRunId,
        Guid? chatSessionId,
        Func<CancellationToken, Task<TResult>> startCommand,
        Func<TResult, Guid> executionRunId,
        Func<TResult, TApiResult> projectResult,
        IAgentFrameworkWorkspaceService workspaceService,
        ICurrentProfileAgentExecutionActivityReader activityReader,
        TimeSpan heartbeatInterval,
        ILogger logger)
    {
        AgentActivityApiResults.SetOperationIdHeader(
            context.Response,
            operationId);
        using var commandLifetime =
            CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
        Task<TResult> completion;
        try
        {
            completion = startCommand(commandLifetime.Token);
        }
        catch (AgentExecutionActivityAdmissionException exception)
        {
            return AgentActivityApiResults.FromAdmissionException(
                context,
                exception,
                agentId,
                knownExecutionRunId,
                chatSessionId);
        }
        catch (AgentJsonSchemaOutputContractException exception)
        {
            return ApiEndpointResults.AgentValidationFailure(
                context,
                exception.Message,
                exception.Code,
                agentId,
                knownExecutionRunId,
                chatSessionId);
        }
        catch (ArgumentException)
        {
            return ApiEndpointResults.AgentValidationFailure(
                context,
                "The agent command request was invalid.",
                AgentApiRequestValidation.InvalidRequestCode,
                agentId,
                knownExecutionRunId,
                chatSessionId);
        }
        catch (AgentChatRunFailedException exception)
        {
            return ApiEndpointResults.AgentRunFailure(context, exception);
        }
        catch (AgentRunFailedException exception)
        {
            return ApiEndpointResults.AgentRunFailure(context, exception);
        }

        var completionObserved = false;
        try
        {
            await using var reader = activityReader.OpenReader(
                operationId,
                StreamSequence.Beginning);
            var firstRead = await reader.ReadAsync(context.RequestAborted);
            if (firstRead is SequencedStreamUnknown<AgentExecutionActivity>)
            {
                throw new InvalidOperationException(
                    "The admitted agent activity stream could not be resolved.");
            }

            ServerSentEventResponseWriter.Prepare(context.Response);
            await AgentActivityServerSentEventWriter.PumpAsync(
                context,
                operationId,
                reader,
                firstRead,
                heartbeatInterval);

            TResult result;
            try
            {
                result = await completion;
            }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
            {
                return Results.Empty;
            }
            catch (AgentChatRunFailedException exception)
            {
                logger.LogWarning(
                    "Agent API operation failed after its activity stream started. CorrelationId={CorrelationId} AgentExecutionOperationId={AgentExecutionOperationId} AgentId={AgentId} ChatSessionId={ChatSessionId} ExecutionRunId={ExecutionRunId} FailureCategory={FailureCategory}.",
                    context.TraceIdentifier,
                    operationId,
                    exception.AgentId,
                    exception.ChatSessionId,
                    exception.ExecutionRunId,
                    exception.FailureCategory);
                await ServerSentEventResponseWriter.WriteEventAsync(
                    context.Response,
                    AgentServerEventNames.CommandFailed,
                    CreateCommandFailed(
                        operationId,
                        ApiEndpointResults.AgentRunFailureResponse(
                            context,
                            exception)),
                    context.RequestAborted);
                return Results.Empty;
            }
            catch (AgentRunFailedException exception)
            {
                logger.LogWarning(
                    "Agent API operation failed after its activity stream started. CorrelationId={CorrelationId} AgentExecutionOperationId={AgentExecutionOperationId} AgentId={AgentId} ChatSessionId={ChatSessionId} ExecutionRunId={ExecutionRunId} FailureCategory={FailureCategory}.",
                    context.TraceIdentifier,
                    operationId,
                    exception.AgentId,
                    exception.ChatSessionId,
                    exception.ExecutionRunId,
                    exception.FailureCategory);
                await ServerSentEventResponseWriter.WriteEventAsync(
                    context.Response,
                    AgentServerEventNames.CommandFailed,
                    CreateCommandFailed(
                        operationId,
                        ApiEndpointResults.AgentRunFailureResponse(
                            context,
                            exception)),
                    context.RequestAborted);
                return Results.Empty;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    "Agent API operation failed after its activity stream started. CorrelationId={CorrelationId} AgentExecutionOperationId={AgentExecutionOperationId} AgentId={AgentId} ChatSessionId={ChatSessionId} ExecutionRunId={ExecutionRunId} FailureType={FailureType}.",
                    context.TraceIdentifier,
                    operationId,
                    agentId,
                    chatSessionId,
                    knownExecutionRunId,
                    exception.GetType().Name);
                await ServerSentEventResponseWriter.WriteEventAsync(
                    context.Response,
                    AgentServerEventNames.CommandFailed,
                    CreateCommandFailed(
                        operationId,
                        ApiEndpointResults.AgentCommandFailureResponse(
                            context,
                            agentId,
                            knownExecutionRunId,
                            chatSessionId)),
                    context.RequestAborted);
                return Results.Empty;
            }
            finally
            {
                completionObserved = true;
            }

            var runId = executionRunId(result);
            var detail = await workspaceService.GetExecutionRunDetailAsync(
                runId,
                context.RequestAborted);
            var pendingApprovals =
                AgentActivityServerSentEventWriter.CreatePendingApprovals(
                    detail.Approvals);
            if (pendingApprovals.Count > 0)
            {
                await ServerSentEventResponseWriter.WriteEventAsync(
                    context.Response,
                    AgentServerEventNames.ApprovalRequired,
                    new AgentApprovalRequired(operationId, runId, pendingApprovals),
                    context.RequestAborted);
            }

            await ServerSentEventResponseWriter.WriteEventAsync(
                context.Response,
                AgentServerEventNames.CommandCompleted,
                new AgentCommandCompleted<TApiResult>(
                    operationId,
                    projectResult(result)),
                context.RequestAborted);
            return Results.Empty;
        }
        finally
        {
            if (!completionObserved)
            {
                await ApiCommandTaskLifetime.CancelAndObserveAsync(
                    commandLifetime,
                    completion,
                    logger,
                    operationId);
            }
        }
    }

    private static AgentCommandFailed CreateCommandFailed(
        AgentExecutionOperationId operationId,
        ApiErrorResponse response)
    {
        var error = response.Errors.Single();
        return new AgentCommandFailed(
            operationId,
            error.Code,
            error.Message,
            response.CorrelationId,
            response.AgentId,
            response.ExecutionRunId,
            response.ChatSessionId,
            response.ProviderFailureCategory);
    }
}
