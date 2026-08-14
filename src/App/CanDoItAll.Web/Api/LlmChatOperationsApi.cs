using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.Modules.LlmChats.Application;
using CanDoItAll.Modules.LlmChats.Common;
using CanDoItAll.Modules.LlmChats.Operations;
using Microsoft.AspNetCore.Mvc;

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
                "Admits one retry-safe turn. operationId is the mandatory idempotency identity and expectedTranscriptRevision is the optimistic transcript token.")
            .Accepts<SendLlmChatTurnApiRequest>("application/json")
            .Produces<LlmChatOperationApiResponse>(StatusCodes.Status200OK)
            .Produces<LlmChatOperationApiResponse>(StatusCodes.Status202Accepted)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict)
            .Produces<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)
            .Produces<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)
            .Produces<ProblemDetails>(StatusCodes.Status504GatewayTimeout);
        conversations.MapPost(
                "/{conversationId:guid}/active-turns/{turnId:guid}/abandon",
                AbandonActiveTurnAsync)
            .WithName("AbandonLlmChatActiveTurn")
            .WithDescription(
                "Abandons only the exact RecoveryRequired turn after its live execution owner has drained.")
            .Produces<LlmChatOperationApiResponse>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        var operations = api.MapGroup("/llm-chat-operations")
            .WithTags("LLM Chat Operations")
            .DisableAntiforgery();
        operations.MapGet("/{operationId:guid}", GetOperationAsync)
            .WithName("GetLlmChatOperation")
            .Produces<LlmChatOperationApiResponse>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);
        operations.MapPost("/{operationId:guid}/cancel", CancelOperationAsync)
            .WithName("CancelLlmChatOperation")
            .WithDescription("Durably requests cancellation and signals a live in-process provider call when owned here.")
            .Produces<LlmChatOperationApiResponse>(StatusCodes.Status200OK)
            .Produces<LlmChatOperationApiResponse>(StatusCodes.Status202Accepted)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);
        return api;
    }

    private static async Task<IResult> SendTurnAsync(
        Guid conversationId,
        SendLlmChatTurnApiRequest request,
        HttpResponse response,
        ILlmChatOperationApplicationService service,
        CancellationToken cancellationToken)
    {
        if (!TryCreateSendCommand(conversationId, request, out var command, out var error))
        {
            return error!;
        }

        var result = await service.SendAsync(command!, cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
        {
            return LlmChatApiResults.FromFailure(result.Errors, request.OperationId);
        }

        var details = result.Value!;
        var location = SetOperationLocation(response, details.Operation.Id);
        return details.Operation.Status switch
        {
            LlmChatOperationStatus.Succeeded => Results.Ok(LlmChatOperationApiMapper.ToResponse(details)),
            LlmChatOperationStatus.Pending or
            LlmChatOperationStatus.Running or
            LlmChatOperationStatus.CancellationRequested => Results.Accepted(
                location,
                LlmChatOperationApiMapper.ToResponse(details)),
            _ => LlmChatApiResults.FromOperationFailure(details.Operation)
        };
    }

    private static async Task<IResult> GetOperationAsync(
        Guid operationId,
        ILlmChatOperationApplicationService service,
        CancellationToken cancellationToken)
    {
        if (!TryCreateOperationId(operationId, out var id, out var error))
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
        if (!TryCreateOperationId(operationId, out var id, out var error))
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
        if (!TryCreateConversationId(conversationId, out var conversation, out var conversationError))
        {
            return conversationError!;
        }

        if (!TryCreateOperationId(turnId, out var operation, out var operationError))
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

    private static bool TryCreateSendCommand(
        Guid conversationId,
        SendLlmChatTurnApiRequest request,
        out SendLlmChatTurnCommand? command,
        out IResult? error)
    {
        command = null;
        if (!TryCreateConversationId(conversationId, out var conversation, out error) ||
            !TryCreateOperationId(request.OperationId, out var operation, out error))
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

    private static bool TryCreateConversationId(
        Guid value,
        out LlmChatConversationId id,
        out IResult? error)
    {
        if (value == Guid.Empty)
        {
            id = default;
            error = LlmChatApiResults.InvalidRequest("A non-empty conversation id is required.");
            return false;
        }

        id = new LlmChatConversationId(value);
        error = null;
        return true;
    }

    private static bool TryCreateOperationId(
        Guid value,
        out LlmChatOperationId id,
        out IResult? error)
    {
        if (value == Guid.Empty)
        {
            id = default;
            error = LlmChatApiResults.InvalidRequest("A non-empty operation id is required.");
            return false;
        }

        id = new LlmChatOperationId(value);
        error = null;
        return true;
    }

    private static string SetOperationLocation(HttpResponse response, LlmChatOperationId operationId)
    {
        var location = $"/api/llm-chat-operations/{operationId.Value:D}";
        response.Headers.Location = location;
        return location;
    }
}
