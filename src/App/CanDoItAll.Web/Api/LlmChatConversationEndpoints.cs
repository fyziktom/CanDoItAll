using CanDoItAll.AgentFramework.Llm.SimpleChats.Application;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Common;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Conversations;
using Microsoft.AspNetCore.Mvc;

namespace CanDoItAll.Web.Api;

internal static class LlmChatConversationEndpoints
{
    public static void MapLlmChatConversationEndpoints(this RouteGroupBuilder api)
    {
        var definitions = api.MapGroup("/llm-chats")
            .WithTags("LLM Chats")
            .DisableAntiforgery();
        definitions.MapPost("/{definitionId:guid}/conversations", CreateConversationAsync)
            .WithName("CreateLlmChatConversation")
            .WithDescription(
                "Creates an API-origin conversation. Creation is not idempotent; callers must not blindly retry an ambiguous response.")
            .Accepts<CreateLlmChatConversationApiRequest>("application/json")
            .Produces<LlmChatConversationApiResponse>(StatusCodes.Status201Created)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict)
            .ApplyApiAuthorization(api, ApiAuthorizationPolicies.ManageLlmChats);

        var conversations = api.MapGroup("/llm-conversations")
            .WithTags("LLM Chat Conversations")
            .DisableAntiforgery();
        conversations.MapGet(string.Empty, ListConversationsAsync)
            .WithName("ListLlmChatConversations")
            .WithDescription(
                "Lists a bounded page of conversations, optionally filtered by definition, using an opaque cursor.")
            .Produces<LlmChatApiPage<LlmChatConversationApiResponse>>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .ApplyApiAuthorization(api, ApiAuthorizationPolicies.ReadLlmChats);
        conversations.MapGet("/{conversationId:guid}", GetConversationAsync)
            .WithName("GetLlmChatConversation")
            .WithDescription(
                "Gets a conversation and a bounded page of its non-system transcript messages.")
            .Produces<LlmChatConversationApiResponse>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .ApplyApiAuthorization(api, ApiAuthorizationPolicies.ReadLlmChats);
        conversations.MapPatch("/{conversationId:guid}/title", RenameConversationAsync)
            .WithName("RenameLlmChatConversation")
            .WithDescription(
                "Renames a conversation using its expected transcript revision and concurrency token.")
            .Accepts<RenameLlmChatConversationApiRequest>("application/json")
            .Produces<LlmChatConversationApiResponse>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict)
            .ApplyApiAuthorization(api, ApiAuthorizationPolicies.ManageLlmChats);
        conversations.MapPost("/{conversationId:guid}/archive", ArchiveConversationAsync)
            .WithName("ArchiveLlmChatConversation")
            .WithDescription(
                "Archives a conversation using the expected concurrency token from the body or If-Match header.")
            .Accepts<LlmChatExpectedConcurrencyApiRequest>("application/json")
            .Produces<LlmChatConversationApiResponse>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict)
            .ApplyApiAuthorization(api, ApiAuthorizationPolicies.ManageLlmChats);
    }

    private static async Task<IResult> ListConversationsAsync(
        int? take,
        string? cursor,
        Guid? definitionId,
        ILlmChatConversationApplicationService service,
        CancellationToken cancellationToken)
    {
        if (!LlmChatApiCursorCodec.TryDecodeConversation(cursor, out var position))
        {
            return LlmChatApiResults.InvalidRequest("The conversation cursor is invalid.");
        }

        LlmChatDefinitionId? filter = null;
        if (definitionId is { } definitionValue)
        {
            if (!LlmChatApiIds.TryCreateDefinitionId(definitionValue, out var parsedDefinitionId, out var idError))
            {
                return idError!;
            }

            filter = parsedDefinitionId;
        }

        LlmChatConversationQuery query;
        try
        {
            query = new LlmChatConversationQuery(take ?? 50, filter, position);
        }
        catch (ArgumentOutOfRangeException)
        {
            return LlmChatApiResults.InvalidRequest("The conversation page size is invalid.");
        }

        var result = await service.ListPageAsync(query, cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
        {
            return LlmChatApiResults.FromFailure(result.Errors);
        }

        var page = result.Value!;
        return Results.Ok(new LlmChatApiPage<LlmChatConversationApiResponse>(
            [.. page.Items.Select(LlmChatApiMapper.ToResponse)],
            page.NextCursor is { } next ? LlmChatApiCursorCodec.Encode(next) : null));
    }

    private static async Task<IResult> CreateConversationAsync(
        Guid definitionId,
        HttpRequest httpRequest,
        HttpResponse response,
        ILlmChatConversationApplicationService service,
        CancellationToken cancellationToken)
    {
        if (!LlmChatApiIds.TryCreateDefinitionId(definitionId, out var id, out var idError))
        {
            return idError!;
        }

        var body = await LlmChatApiRequestReader
            .ReadAsync<CreateLlmChatConversationApiRequest>(httpRequest, cancellationToken)
            .ConfigureAwait(false);
        if (body.Error is not null)
        {
            return body.Error;
        }

        var result = await service.CreateAsync(
            new CreateLlmChatConversationCommand(
                id,
                body.Value!.Title,
                LlmChatConversationOrigin.Api),
            cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
        {
            return LlmChatApiResults.FromFailure(result.Errors);
        }

        var details = result.Value!;
        LlmChatApiResults.SetEtag(response, details.Conversation.ConcurrencyToken);
        return Results.Created(
            $"/api/llm-conversations/{details.Conversation.Id.Value:D}",
            LlmChatApiMapper.ToResponse(details));
    }

    private static async Task<IResult> GetConversationAsync(
        Guid conversationId,
        int? messageTake,
        string? messageCursor,
        HttpResponse response,
        ILlmChatConversationApplicationService service,
        CancellationToken cancellationToken)
    {
        if (!LlmChatApiIds.TryCreateConversationId(conversationId, out var id, out var idError))
        {
            return idError!;
        }

        if (!LlmChatApiCursorCodec.TryDecodeTranscript(messageCursor, out var position))
        {
            return LlmChatApiResults.InvalidRequest("The transcript message cursor is invalid.");
        }

        LlmChatTranscriptQuery query;
        try
        {
            query = new LlmChatTranscriptQuery(messageTake ?? 50, position);
        }
        catch (ArgumentOutOfRangeException)
        {
            return LlmChatApiResults.InvalidRequest("The transcript message page size is invalid.");
        }

        var result = await service.GetAsync(id, query, cancellationToken).ConfigureAwait(false);
        return ConversationMutationResult(result, response);
    }

    private static async Task<IResult> RenameConversationAsync(
        Guid conversationId,
        HttpRequest httpRequest,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        HttpResponse response,
        ILlmChatConversationApplicationService service,
        CancellationToken cancellationToken)
    {
        if (!LlmChatApiIds.TryCreateConversationId(conversationId, out var id, out var idError))
        {
            return idError!;
        }

        var body = await LlmChatApiRequestReader
            .ReadAsync<RenameLlmChatConversationApiRequest>(httpRequest, cancellationToken)
            .ConfigureAwait(false);
        if (body.Error is not null)
        {
            return body.Error;
        }

        if (!LlmChatApiResults.TryResolveExpectedConcurrencyToken(
                body.Value!.ExpectedConcurrencyToken,
                ifMatch,
                out var expectedToken,
                out var tokenError))
        {
            return tokenError!;
        }

        var result = await service.RenameAsync(
            new RenameLlmChatConversationCommand(
                id,
                body.Value.Title,
                expectedToken,
                body.Value.ExpectedTranscriptRevision),
            cancellationToken).ConfigureAwait(false);
        return ConversationMutationResult(result, response);
    }

    private static async Task<IResult> ArchiveConversationAsync(
        Guid conversationId,
        HttpRequest httpRequest,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        HttpResponse response,
        ILlmChatConversationApplicationService service,
        CancellationToken cancellationToken)
    {
        if (!LlmChatApiIds.TryCreateConversationId(conversationId, out var id, out var idError))
        {
            return idError!;
        }

        var body = await LlmChatApiRequestReader
            .ReadAsync<LlmChatExpectedConcurrencyApiRequest>(httpRequest, cancellationToken)
            .ConfigureAwait(false);
        if (body.Error is not null)
        {
            return body.Error;
        }

        if (!LlmChatApiResults.TryResolveExpectedConcurrencyToken(
                body.Value!.ExpectedConcurrencyToken,
                ifMatch,
                out var expectedToken,
                out var tokenError))
        {
            return tokenError!;
        }

        var result = await service.ArchiveAsync(
            new ArchiveLlmChatConversationCommand(id, expectedToken),
            cancellationToken).ConfigureAwait(false);
        return ConversationMutationResult(result, response);
    }

    private static IResult ConversationMutationResult(
        CanDoItAll.SharedKernel.Result<LlmChatConversationDetails> result,
        HttpResponse response)
    {
        if (result.IsFailure)
        {
            return LlmChatApiResults.FromFailure(result.Errors);
        }

        LlmChatApiResults.SetEtag(response, result.Value!.Conversation.ConcurrencyToken);
        return Results.Ok(LlmChatApiMapper.ToResponse(result.Value));
    }
}
