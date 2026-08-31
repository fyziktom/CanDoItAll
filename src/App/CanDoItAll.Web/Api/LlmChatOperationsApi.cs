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
            .WithDescription(
                "Admits one retry-safe turn. Retry the same logical request with the same operationId and identical body; use a new operationId only for an intentionally distinct turn. expectedTranscriptRevision is the optimistic transcript token.")
            .Accepts<SendLlmChatTurnApiRequest>("application/json")
            .Produces<LlmChatOperationApiResponse>(StatusCodes.Status202Accepted)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict)
            .Produces<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)
            .Produces<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)
            .Produces<ProblemDetails>(StatusCodes.Status504GatewayTimeout)
            .ApplyApiAuthorization(api, ApiAuthorizationPolicies.ExecuteLlmChats);
        conversations.MapPost(
                "/{conversationId:guid}/active-turns/{turnId:guid}/abandon",
                AbandonActiveTurnAsync)
            .WithName("AbandonLlmChatActiveTurn")
            .WithDescription(
                "Abandons only the exact RecoveryRequired turn after its live execution owner has drained.")
            .Produces<LlmChatOperationApiResponse>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict)
            .ApplyApiAuthorization(api, ApiAuthorizationPolicies.ExecuteLlmChats);

        var operations = api.MapGroup("/llm-chat-operations")
            .WithTags("LLM Chat Operations")
            .DisableAntiforgery();
        operations.MapGet("/{operationId:guid}", GetOperationAsync)
            .WithName("GetLlmChatOperation")
            .WithDescription(
                "Gets durable operation state, result metadata, and bounded provider invocation evidence.")
            .Produces<LlmChatOperationApiResponse>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .ApplyApiAuthorization(api, ApiAuthorizationPolicies.ReadLlmChats);
        operations.MapGet("/{operationId:guid}/events", StreamOperationEventsAsync)
            .WithName("StreamLlmChatOperationEvents")
            .WithDescription(
                "Replays durable operation events after Last-Event-ID or the after query cursor and follows committed updates until a terminal operation event. Bearer credentials are accepted only through the normal Authorization header, never query parameters.")
            .Produces<string>(
                StatusCodes.Status200OK,
                contentType: ServerSentEventResponseWriter.ContentType)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict)
            .ApplyApiAuthorization(api, ApiAuthorizationPolicies.ReadLlmChats);
        operations.MapPost("/{operationId:guid}/cancel", CancelOperationAsync)
            .WithName("CancelLlmChatOperation")
            .WithDescription("Durably requests cancellation and signals a live in-process provider call when owned here.")
            .Produces<LlmChatOperationApiResponse>(StatusCodes.Status200OK)
            .Produces<LlmChatOperationApiResponse>(StatusCodes.Status202Accepted)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .ApplyApiAuthorization(api, ApiAuthorizationPolicies.ExecuteLlmChats);
        operations.MapPost("/{operationId:guid}/reconcile", ReconcileOperationAsync)
            .WithName("ReconcileLlmChatOperation")
            .WithDescription(
                "Reconciles an operation only from durable transcript, invocation, dispatch, and lease evidence. Ambiguous post-dispatch work remains recovery-required and is never redispatched.")
            .Produces<LlmChatOperationApiResponse>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict)
            .ApplyApiAuthorization(api, ApiAuthorizationPolicies.ManageLlmChats);
        return api;
    }

    private static async Task<IResult> SendTurnAsync(
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

    private static async Task StreamOperationEventsAsync(
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

    private static async Task<IResult> GetOperationAsync(
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

    private static async Task<IResult> CancelOperationAsync(
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

    private static async Task<IResult> AbandonActiveTurnAsync(
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

    private static async Task<IResult> ReconcileOperationAsync(
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
