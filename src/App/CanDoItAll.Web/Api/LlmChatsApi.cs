using CanDoItAll.Modules.LlmChats.Application;
using CanDoItAll.Modules.LlmChats.Common;
using CanDoItAll.Modules.LlmChats.Conversations;
using CanDoItAll.Modules.LlmChats.Definitions;
using CanDoItAll.Modules.LlmChats.Ports;
using Microsoft.AspNetCore.Mvc;

namespace CanDoItAll.Web.Api;

internal static class LlmChatsApi
{
    public static RouteGroupBuilder MapLlmChatsApi(this RouteGroupBuilder api)
    {
        var definitions = api.MapGroup("/llm-chats")
            .WithTags("LLM Chats")
            .DisableAntiforgery();
        definitions.MapGet("/provider-options", ListProviderOptionsAsync)
            .WithName("ListLlmChatProviderOptions")
            .Produces<LlmChatProviderOptionApiResponse[]>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);
        definitions.MapGet(string.Empty, ListDefinitionsAsync)
            .WithName("ListLlmChatDefinitions")
            .Produces<LlmChatApiPage<LlmChatDefinitionApiResponse>>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest);
        definitions.MapPost(string.Empty, CreateDefinitionAsync)
            .WithName("CreateLlmChatDefinition")
            .Accepts<LlmChatDefinitionMutationApiRequest>("application/json")
            .Produces<LlmChatDefinitionApiResponse>(StatusCodes.Status201Created)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status422UnprocessableEntity);
        definitions.MapGet("/{definitionId:guid}", GetDefinitionAsync)
            .WithName("GetLlmChatDefinition")
            .Produces<LlmChatDefinitionApiResponse>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);
        definitions.MapPut("/{definitionId:guid}", UpdateDefinitionAsync)
            .WithName("UpdateLlmChatDefinition")
            .Accepts<LlmChatDefinitionMutationApiRequest>("application/json")
            .Produces<LlmChatDefinitionApiResponse>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict)
            .Produces<ProblemDetails>(StatusCodes.Status422UnprocessableEntity);
        MapDefinitionStatus(definitions, "activate", LlmChatDefinitionStatus.Active, "ActivateLlmChatDefinition");
        MapDefinitionStatus(definitions, "suspend", LlmChatDefinitionStatus.Suspended, "SuspendLlmChatDefinition");
        MapDefinitionStatus(definitions, "archive", LlmChatDefinitionStatus.Archived, "ArchiveLlmChatDefinition");
        definitions.MapPost("/{definitionId:guid}/conversations", CreateConversationAsync)
            .WithName("CreateLlmChatConversation")
            .Accepts<CreateLlmChatConversationApiRequest>("application/json")
            .Produces<LlmChatConversationApiResponse>(StatusCodes.Status201Created)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        var conversations = api.MapGroup("/llm-conversations")
            .WithTags("LLM Chat Conversations")
            .DisableAntiforgery();
        conversations.MapGet(string.Empty, ListConversationsAsync)
            .WithName("ListLlmChatConversations")
            .Produces<LlmChatApiPage<LlmChatConversationApiResponse>>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest);
        conversations.MapGet("/{conversationId:guid}", GetConversationAsync)
            .WithName("GetLlmChatConversation")
            .Produces<LlmChatConversationApiResponse>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);
        conversations.MapPatch("/{conversationId:guid}/title", RenameConversationAsync)
            .WithName("RenameLlmChatConversation")
            .Accepts<RenameLlmChatConversationApiRequest>("application/json")
            .Produces<LlmChatConversationApiResponse>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict);
        conversations.MapPost("/{conversationId:guid}/archive", ArchiveConversationAsync)
            .WithName("ArchiveLlmChatConversation")
            .Accepts<LlmChatExpectedConcurrencyApiRequest>("application/json")
            .Produces<LlmChatConversationApiResponse>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict);
        return api;
    }

    private static void MapDefinitionStatus(
        RouteGroupBuilder definitions,
        string route,
        LlmChatDefinitionStatus status,
        string operationName)
        => definitions.MapPost($"/{{definitionId:guid}}/{route}", async (
                Guid definitionId,
                LlmChatExpectedConcurrencyApiRequest request,
                [FromHeader(Name = "If-Match")] string? ifMatch,
                HttpResponse response,
                ILlmChatDefinitionApplicationService service,
                CancellationToken cancellationToken) =>
            await ChangeDefinitionStatusAsync(
                definitionId,
                status,
                request,
                ifMatch,
                response,
                service,
                cancellationToken).ConfigureAwait(false))
            .WithName(operationName)
            .Accepts<LlmChatExpectedConcurrencyApiRequest>("application/json")
            .Produces<LlmChatDefinitionApiResponse>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

    private static async Task<IResult> ListProviderOptionsAsync(
        ILlmChatProviderResolver resolver,
        CancellationToken cancellationToken)
    {
        var result = await resolver.ListOptionsAsync(cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? Results.Ok(result.Value!.Select(LlmChatApiMapper.ToResponse).ToArray())
            : LlmChatApiResults.FromFailure(result.Errors);
    }

    private static async Task<IResult> ListDefinitionsAsync(
        int? take,
        string? cursor,
        string? status,
        ILlmChatDefinitionApplicationService service,
        CancellationToken cancellationToken)
    {
        if (!TryCreateDefinitionQuery(take ?? 50, cursor, status, out var query, out var error))
        {
            return error!;
        }

        var result = await service.ListPageAsync(query!, cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
        {
            return LlmChatApiResults.FromFailure(result.Errors);
        }

        var page = result.Value!;
        return Results.Ok(new LlmChatApiPage<LlmChatDefinitionApiResponse>(
            [.. page.Items.Select(LlmChatApiMapper.ToListResponse)],
            page.NextOffset is { } next ? LlmChatApiCursorCodec.Encode(next) : null));
    }

    private static async Task<IResult> CreateDefinitionAsync(
        LlmChatDefinitionMutationApiRequest request,
        HttpResponse response,
        ILlmChatDefinitionApplicationService service,
        CancellationToken cancellationToken)
    {
        if (!LlmChatApiMapper.TryMapCreate(request, out var command, out var mappingError))
        {
            return LlmChatApiResults.InvalidRequest(mappingError);
        }

        var result = await service.CreateAsync(command!, cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
        {
            return LlmChatApiResults.FromFailure(result.Errors);
        }

        var details = result.Value!;
        LlmChatApiResults.SetEtag(response, details.Definition.ConcurrencyToken);
        return Results.Created(
            $"/api/llm-chats/{details.Definition.Id.Value:D}",
            LlmChatApiMapper.ToDetailResponse(details));
    }

    private static async Task<IResult> GetDefinitionAsync(
        Guid definitionId,
        HttpResponse response,
        ILlmChatDefinitionApplicationService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetAsync(new LlmChatDefinitionId(definitionId), cancellationToken)
            .ConfigureAwait(false);
        if (result.IsFailure)
        {
            return LlmChatApiResults.FromFailure(result.Errors);
        }

        LlmChatApiResults.SetEtag(response, result.Value!.Definition.ConcurrencyToken);
        return Results.Ok(LlmChatApiMapper.ToDetailResponse(result.Value));
    }

    private static async Task<IResult> UpdateDefinitionAsync(
        Guid definitionId,
        LlmChatDefinitionMutationApiRequest request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        HttpResponse response,
        ILlmChatDefinitionApplicationService service,
        CancellationToken cancellationToken)
    {
        if (!LlmChatApiResults.TryResolveExpectedConcurrencyToken(
                request.ExpectedConcurrencyToken,
                ifMatch,
                out var expectedToken,
                out var tokenError))
        {
            return tokenError!;
        }

        if (!LlmChatApiMapper.TryMapUpdate(
                new LlmChatDefinitionId(definitionId),
                request,
                expectedToken,
                out var command,
                out var mappingError))
        {
            return LlmChatApiResults.InvalidRequest(mappingError);
        }

        var result = await service.UpdateAsync(command!, cancellationToken).ConfigureAwait(false);
        return DefinitionMutationResult(result, response);
    }

    private static async Task<IResult> ChangeDefinitionStatusAsync(
        Guid definitionId,
        LlmChatDefinitionStatus status,
        LlmChatExpectedConcurrencyApiRequest request,
        string? ifMatch,
        HttpResponse response,
        ILlmChatDefinitionApplicationService service,
        CancellationToken cancellationToken)
    {
        if (!LlmChatApiResults.TryResolveExpectedConcurrencyToken(
                request.ExpectedConcurrencyToken,
                ifMatch,
                out var expectedToken,
                out var tokenError))
        {
            return tokenError!;
        }

        var result = await service.ChangeStatusAsync(
            new ChangeLlmChatDefinitionStatusCommand(
                new LlmChatDefinitionId(definitionId),
                status,
                expectedToken),
            cancellationToken).ConfigureAwait(false);
        return DefinitionMutationResult(result, response);
    }

    private static IResult DefinitionMutationResult(
        CanDoItAll.SharedKernel.Result<LlmChatDefinitionDetails> result,
        HttpResponse response)
    {
        if (result.IsFailure)
        {
            return LlmChatApiResults.FromFailure(result.Errors);
        }

        LlmChatApiResults.SetEtag(response, result.Value!.Definition.ConcurrencyToken);
        return Results.Ok(LlmChatApiMapper.ToDetailResponse(result.Value));
    }

    private static async Task<IResult> ListConversationsAsync(
        int? take,
        string? cursor,
        Guid? definitionId,
        ILlmChatConversationApplicationService service,
        CancellationToken cancellationToken)
    {
        if (!LlmChatApiCursorCodec.TryDecode(cursor, out var offset))
        {
            return LlmChatApiResults.InvalidRequest("The conversation cursor is invalid.");
        }

        LlmChatConversationQuery query;
        try
        {
            query = new LlmChatConversationQuery(
                take ?? 50,
                definitionId is { } id ? new LlmChatDefinitionId(id) : null,
                offset);
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
            page.NextOffset is { } next ? LlmChatApiCursorCodec.Encode(next) : null));
    }

    private static async Task<IResult> CreateConversationAsync(
        Guid definitionId,
        CreateLlmChatConversationApiRequest request,
        HttpResponse response,
        ILlmChatConversationApplicationService service,
        CancellationToken cancellationToken)
    {
        var result = await service.CreateAsync(
            new CreateLlmChatConversationCommand(
                new LlmChatDefinitionId(definitionId),
                request.Title,
                request.Origin),
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
        if (!LlmChatApiCursorCodec.TryDecode(messageCursor, out var offset))
        {
            return LlmChatApiResults.InvalidRequest("The transcript message cursor is invalid.");
        }

        LlmChatTranscriptQuery query;
        try
        {
            query = new LlmChatTranscriptQuery(messageTake ?? 50, offset);
        }
        catch (ArgumentOutOfRangeException)
        {
            return LlmChatApiResults.InvalidRequest("The transcript message page size is invalid.");
        }

        var result = await service.GetAsync(
                new LlmChatConversationId(conversationId),
                query,
                cancellationToken)
            .ConfigureAwait(false);
        return ConversationMutationResult(result, response);
    }

    private static async Task<IResult> RenameConversationAsync(
        Guid conversationId,
        RenameLlmChatConversationApiRequest request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        HttpResponse response,
        ILlmChatConversationApplicationService service,
        CancellationToken cancellationToken)
    {
        if (!LlmChatApiResults.TryResolveExpectedConcurrencyToken(
                request.ExpectedConcurrencyToken,
                ifMatch,
                out var expectedToken,
                out var tokenError))
        {
            return tokenError!;
        }

        var result = await service.RenameAsync(
            new RenameLlmChatConversationCommand(
                new LlmChatConversationId(conversationId),
                request.Title,
                expectedToken,
                request.ExpectedTranscriptRevision),
            cancellationToken).ConfigureAwait(false);
        return ConversationMutationResult(result, response);
    }

    private static async Task<IResult> ArchiveConversationAsync(
        Guid conversationId,
        LlmChatExpectedConcurrencyApiRequest request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        HttpResponse response,
        ILlmChatConversationApplicationService service,
        CancellationToken cancellationToken)
    {
        if (!LlmChatApiResults.TryResolveExpectedConcurrencyToken(
                request.ExpectedConcurrencyToken,
                ifMatch,
                out var expectedToken,
                out var tokenError))
        {
            return tokenError!;
        }

        var result = await service.ArchiveAsync(
            new ArchiveLlmChatConversationCommand(new LlmChatConversationId(conversationId), expectedToken),
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

    private static bool TryCreateDefinitionQuery(
        int take,
        string? cursor,
        string? status,
        out LlmChatDefinitionQuery? query,
        out IResult? error)
    {
        query = null;
        error = null;
        if (!LlmChatApiCursorCodec.TryDecode(cursor, out var offset))
        {
            error = LlmChatApiResults.InvalidRequest("The definition cursor is invalid.");
            return false;
        }

        LlmChatDefinitionStatus? parsedStatus = null;
        if (!string.IsNullOrWhiteSpace(status) &&
            (!Enum.TryParse<LlmChatDefinitionStatus>(status, ignoreCase: true, out var value) ||
             !Enum.IsDefined(value) ||
             int.TryParse(status, out _)))
        {
            error = LlmChatApiResults.InvalidRequest("The definition status is invalid.");
            return false;
        }
        else if (!string.IsNullOrWhiteSpace(status))
        {
            parsedStatus = Enum.Parse<LlmChatDefinitionStatus>(status, ignoreCase: true);
        }

        try
        {
            query = new LlmChatDefinitionQuery(take == 0 ? 50 : take, parsedStatus, offset);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            error = LlmChatApiResults.InvalidRequest("The definition page size is invalid.");
            return false;
        }
    }
}
