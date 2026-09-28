using System.ComponentModel;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Application;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Common;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Conversations;
using Microsoft.AspNetCore.Mvc;

namespace CanDoItAll.Web.Api;

internal static class LlmChatConversationEndpoints
{
    // The XML comment generator matches <param> tags by C# parameter name, which never equals the header name
    // "If-Match", so the header is described with DescriptionAttribute instead.
    private const string IfMatchDescription =
        "Strong ETag of the conversation version you read, for example `\"2\"` including the quotes, as " +
        "returned in the `ETag` response header. It carries the same concurrency token as " +
        "`expectedConcurrencyToken` in the body: send at least one of them, and when both are present they must be " +
        "equal. A weak ETag (`W/`), `*` or a list is rejected with 400.";

    public static void MapLlmChatConversationEndpoints(this RouteGroupBuilder api)
    {
        var definitions = api.MapGroup("/llm-chats")
            .WithTags("LLM Chats")
            .DisableAntiforgery();
        definitions.MapPost("/{definitionId:guid}/conversations", CreateConversationAsync)
            .WithName("CreateLlmChatConversation")
            .Accepts<CreateLlmChatConversationApiRequest>("application/json")
            .Produces<LlmChatConversationApiResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .ApplyApiAuthorization(api, ApiAuthorizationPolicies.ManageLlmChats);

        var conversations = api.MapGroup("/llm-conversations")
            .WithTags("LLM Chat Conversations")
            .DisableAntiforgery();
        conversations.MapGet(string.Empty, ListConversationsAsync)
            .WithName("ListLlmChatConversations")
            .Produces<LlmChatApiPage<LlmChatConversationApiResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ApplyApiAuthorization(api, ApiAuthorizationPolicies.ReadLlmChats);
        conversations.MapGet("/{conversationId:guid}", GetConversationAsync)
            .WithName("GetLlmChatConversation")
            .Produces<LlmChatConversationApiResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ApplyApiAuthorization(api, ApiAuthorizationPolicies.ReadLlmChats);
        conversations.MapPatch("/{conversationId:guid}/title", RenameConversationAsync)
            .WithName("RenameLlmChatConversation")
            .Accepts<RenameLlmChatConversationApiRequest>("application/json")
            .Produces<LlmChatConversationApiResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .ApplyApiAuthorization(api, ApiAuthorizationPolicies.ManageLlmChats);
        conversations.MapPost("/{conversationId:guid}/archive", ArchiveConversationAsync)
            .WithName("ArchiveLlmChatConversation")
            .Accepts<LlmChatExpectedConcurrencyApiRequest>("application/json")
            .Produces<LlmChatConversationApiResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .ApplyApiAuthorization(api, ApiAuthorizationPolicies.ManageLlmChats);
    }

    /// <summary>
    /// List LLM Chat conversations one page at a time, optionally only those of one definition.
    /// </summary>
    /// <remarks>
    /// Returns conversations of every status and origin, ordered by their last change, newest first (ties by
    /// identifier). Items carry the conversation metadata and transcript state but no messages; read
    /// <c>GET /api/llm-conversations/{conversationId}</c> for the transcript.
    ///
    /// Paging uses an opaque cursor, not an offset: omit <c>cursor</c> for the first page, then send the previous
    /// page's <c>nextCursor</c> unchanged, with the same <c>definitionId</c> filter, until <c>nextCursor</c> is null. A
    /// conversation that changes while you page (a turn, a rename or archiving) moves to the start of the order and can
    /// be missed by later pages.
    ///
    /// Authority: when API authorization is enabled, a bearer token with the exact scope <c>api.llm-chats.read</c>; the
    /// broad <c>api</c> scope is not accepted. With authorization disabled (the development default) the route is open.
    /// Failures use Problem Details (<c>application/problem+json</c>) with a stable <c>code</c> member.
    /// </remarks>
    /// <param name="take">Number of conversations per page, from 1 through 100. Omitted means 50.</param>
    /// <param name="cursor">
    /// Opaque continuation value from the previous page's <c>nextCursor</c>; omit it for the first page. It is not a
    /// page number or an offset. A malformed value, or a cursor of another list, is rejected.
    /// </param>
    /// <param name="definitionId">
    /// Only conversations created from this definition (its <c>id</c> from <c>GET /api/llm-chats</c>). Omitted lists
    /// the conversations of all definitions. The empty GUID is rejected; an unknown definition yields an empty page,
    /// not 404.
    /// </param>
    /// <response code="200">
    /// One page of conversations. An empty <c>items</c> array with a null <c>nextCursor</c> means that nothing matched.
    /// </response>
    /// <response code="400">
    /// <c>llm-chat.invalid-request</c>: <c>take</c> is outside 1 through 100, <c>cursor</c> is invalid or
    /// <c>definitionId</c> is the empty GUID; <c>detail</c> names the problem. A <c>take</c> that is not an integer, or
    /// a <c>definitionId</c> that is not a GUID, is rejected with HTTP 400 before the operation runs, without this
    /// body.
    /// </response>
    /// <response code="409">
    /// <c>llm-chat.runtime-profile-changed</c>: the host's active database profile changed while the request ran.
    /// Repeat the request.
    /// </response>
    internal static async Task<IResult> ListConversationsAsync(
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

    /// <summary>
    /// Start a new conversation from an active LLM Chat definition.
    /// </summary>
    /// <remarks>
    /// Creates an empty conversation pinned to the definition's current revision (<c>definitionRevision</c>); later
    /// updates of the definition do not change it. The definition must be <c>active</c>. When the definition has a
    /// system prompt, it becomes the first transcript entry, so <c>transcriptRevision</c> starts at 1 instead of 0;
    /// system messages are never returned by the transcript reads. The conversation's <c>origin</c> is always
    /// <c>api</c>, and the body accepts only <c>title</c>.
    ///
    /// Next, send turns with <c>POST /api/llm-conversations/{conversationId}/turns</c>, using <c>id</c> from the
    /// response and its <c>transcriptRevision</c> as the first <c>expectedTranscriptRevision</c>.
    ///
    /// Creation is not idempotent: every successful request creates another conversation. After an ambiguous failure,
    /// list the definition's conversations (<c>GET /api/llm-conversations</c> with the <c>definitionId</c> query
    /// parameter) before sending the request again. When the provider profile or model of the definition's current
    /// revision is no longer usable, creation currently fails with HTTP 500 without a Problem Details body; compare the
    /// definition with <c>GET /api/llm-chats/provider-options</c>.
    ///
    /// Authority: when API authorization is enabled, a bearer token with the exact scope <c>api.llm-chats.manage</c>;
    /// the broad <c>api</c> scope is not accepted. With authorization disabled (the development default) the route is
    /// open. Failures use Problem Details (<c>application/problem+json</c>) with a stable <c>code</c> member.
    /// </remarks>
    /// <param name="definitionId">
    /// Identifier of the active definition to use, as returned in <c>id</c> by <c>GET /api/llm-chats</c>.
    /// </param>
    /// <param name="httpRequest">
    /// The conversation to create, as a JSON object (<c>application/json</c>) with only <c>title</c>, for example
    /// <c>{ "title": "Quarterly report questions" }</c>.
    /// </param>
    /// <response code="201">
    /// The conversation was created. The <c>Location</c> header is its URL (<c>/api/llm-conversations/{id}</c>) and the
    /// <c>ETag</c> header carries its concurrency token. The body has no <c>messages</c>.
    /// </response>
    /// <response code="400">
    /// <c>llm-chat.invalid-request</c>, nothing was created: the definition identifier is the empty GUID, the body is
    /// missing, is not JSON or has members other than <c>title</c>, or the title is longer than 200 characters.
    /// </response>
    /// <response code="404"><c>llm-chat.definition-not-found</c>: no definition has this identifier.</response>
    /// <response code="409">
    /// <c>llm-chat.definition-not-active</c> (the definition is draft, suspended or archived; nothing was created) or
    /// <c>llm-chat.runtime-profile-changed</c> (the host's active database profile changed; the conversation may or may
    /// not exist, so list the conversations before sending the request again).
    /// </response>
    /// <response code="500">
    /// <c>llm-chat.storage-corrupted</c>: the definition's current revision is missing from storage; nothing was
    /// created.
    /// </response>
    internal static async Task<IResult> CreateConversationAsync(
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

    /// <summary>
    /// Read a conversation with one page of its transcript messages.
    /// </summary>
    /// <remarks>
    /// Returns the conversation metadata, its transcript state (<c>transcriptRevision</c>, <c>hasActiveTurn</c>,
    /// <c>activeOperationId</c>) and up to <c>messageTake</c> user and assistant messages, oldest first. System
    /// messages are never returned. The first page starts at the beginning of the transcript: follow
    /// <c>nextMessageCursor</c> as <c>messageCursor</c> until it is omitted to reach the latest messages.
    ///
    /// Use <c>transcriptRevision</c> as <c>expectedTranscriptRevision</c> for the next turn. While
    /// <c>hasActiveTurn</c> is true, the active turn's user message is already in the transcript and its reply is not;
    /// follow <c>activeOperationId</c> with <c>GET /api/llm-chat-operations/{operationId}</c> or its event stream. When
    /// the turn fails or is cancelled, its user message is normally removed again.
    ///
    /// The <c>ETag</c> response header carries the conversation's concurrency token, needed to rename or archive it.
    ///
    /// Authority: when API authorization is enabled, a bearer token with the exact scope <c>api.llm-chats.read</c>; the
    /// broad <c>api</c> scope is not accepted. With authorization disabled (the development default) the route is open.
    /// Failures use Problem Details (<c>application/problem+json</c>) with a stable <c>code</c> member.
    /// </remarks>
    /// <param name="conversationId">
    /// Identifier of the conversation, as returned in <c>id</c> by conversation creation or
    /// <c>GET /api/llm-conversations</c>.
    /// </param>
    /// <param name="messageTake">Number of messages per page, from 1 through 100. Omitted means 50.</param>
    /// <param name="messageCursor">
    /// Opaque continuation value from <c>nextMessageCursor</c> of a previous read of this conversation; omit it for the
    /// first page (the oldest messages). A malformed value, or a cursor of another kind, is rejected.
    /// </param>
    /// <response code="200">
    /// The conversation with <c>messages</c> (possibly empty) and, when more messages follow,
    /// <c>nextMessageCursor</c>.
    /// </response>
    /// <response code="400">
    /// <c>llm-chat.invalid-request</c>: the identifier is the empty GUID, <c>messageCursor</c> is invalid or
    /// <c>messageTake</c> is outside 1 through 100. A <c>messageTake</c> that is not an integer is rejected with HTTP
    /// 400 before the operation runs, without this body.
    /// </response>
    /// <response code="404"><c>llm-chat.conversation-not-found</c>: no conversation has this identifier.</response>
    /// <response code="409">
    /// <c>llm-chat.runtime-profile-changed</c>: the host's active database profile changed while the request ran.
    /// Repeat the request.
    /// </response>
    internal static async Task<IResult> GetConversationAsync(
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

    /// <summary>
    /// Rename a conversation.
    /// </summary>
    /// <remarks>
    /// Changes the title of an active conversation. Both preconditions come from your last read of the conversation:
    /// its concurrency token (in the body as <c>expectedConcurrencyToken</c> or as <c>If-Match</c>) and its
    /// <c>transcriptRevision</c> (as <c>expectedTranscriptRevision</c>). On success the concurrency token and the
    /// transcript revision each increase by one, so use the returned <c>transcriptRevision</c> for the next turn. An
    /// archived conversation cannot be renamed.
    ///
    /// A stale <c>expectedTranscriptRevision</c>, or a turn that is still active, currently fails with HTTP 500 without
    /// a Problem Details body instead of a 409 conflict, and nothing is changed; read the conversation again before
    /// retrying.
    ///
    /// Authority: when API authorization is enabled, a bearer token with the exact scope <c>api.llm-chats.manage</c>;
    /// the broad <c>api</c> scope is not accepted. With authorization disabled (the development default) the route is
    /// open. Failures use Problem Details (<c>application/problem+json</c>) with a stable <c>code</c> member.
    /// </remarks>
    /// <param name="conversationId">
    /// Identifier of the conversation, as returned in <c>id</c> by conversation creation or
    /// <c>GET /api/llm-conversations</c>.
    /// </param>
    /// <param name="httpRequest">
    /// The new title with the preconditions, as a JSON object (<c>application/json</c>), for example
    /// <c>{ "title": "Budget review", "expectedTranscriptRevision": 5, "expectedConcurrencyToken": 0 }</c>.
    /// </param>
    /// <response code="200">
    /// The conversation was renamed. The body is the conversation without messages and the <c>ETag</c> header carries
    /// its new concurrency token.
    /// </response>
    /// <response code="400">
    /// <c>llm-chat.invalid-request</c>, nothing was changed: the identifier is the empty GUID; the body is missing, is
    /// not JSON or has unknown members; no concurrency token was sent; <c>If-Match</c> is not a single strong numeric
    /// ETag; a token is negative or the two tokens differ; or the title is longer than 200 characters.
    /// </response>
    /// <response code="404"><c>llm-chat.conversation-not-found</c>: no conversation has this identifier.</response>
    /// <response code="409">
    /// Nothing was changed: <c>llm-chat.conversation-archived</c> (the conversation is read-only),
    /// <c>llm-chat.storage-conflict</c> (the conversation changed since your read; read it again) or
    /// <c>llm-chat.runtime-profile-changed</c> (the host's active database profile changed; read the conversation
    /// before repeating the request).
    /// </response>
    /// <response code="500">
    /// <c>llm-chat.storage-corrupted</c>: the conversation's definition or pinned revision is missing from storage.
    /// </response>
    internal static async Task<IResult> RenameConversationAsync(
        Guid conversationId,
        HttpRequest httpRequest,
        [Description(IfMatchDescription)]
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

    /// <summary>
    /// Archive a conversation, making it read-only.
    /// </summary>
    /// <remarks>
    /// An archived conversation keeps its transcript readable but accepts no turns and cannot be renamed; this API
    /// offers no way back. A conversation whose turn is still active or unfinished (including one that requires
    /// recovery) cannot be archived: follow that operation until it ends, or settle it with cancel, reconcile or
    /// abandon, then retry. Archiving an archived conversation changes nothing and returns it unchanged, provided the
    /// concurrency token matches. The concurrency token increases by one; the transcript revision does not change.
    ///
    /// Send the concurrency token you read as <c>expectedConcurrencyToken</c> in the JSON body or as <c>If-Match</c>. A
    /// JSON body is required even with <c>If-Match</c>; <c>{}</c> is then enough.
    ///
    /// Authority: when API authorization is enabled, a bearer token with the exact scope <c>api.llm-chats.manage</c>;
    /// the broad <c>api</c> scope is not accepted. With authorization disabled (the development default) the route is
    /// open. Failures use Problem Details (<c>application/problem+json</c>) with a stable <c>code</c> member.
    /// </remarks>
    /// <param name="conversationId">
    /// Identifier of the conversation, as returned in <c>id</c> by conversation creation or
    /// <c>GET /api/llm-conversations</c>.
    /// </param>
    /// <param name="httpRequest">
    /// JSON object with the expected concurrency token, for example <c>{ "expectedConcurrencyToken": 1 }</c>, or
    /// <c>{}</c> when the token is sent as <c>If-Match</c>.
    /// </param>
    /// <response code="200">
    /// The conversation is archived. The body is the conversation without messages and the <c>ETag</c> header carries
    /// its concurrency token.
    /// </response>
    /// <response code="400">
    /// <c>llm-chat.invalid-request</c>, nothing was changed: the identifier is the empty GUID; the body is missing, is
    /// not JSON or has other members; no concurrency token was sent; <c>If-Match</c> is not a single strong numeric
    /// ETag; a token is negative; or the two tokens differ.
    /// </response>
    /// <response code="404"><c>llm-chat.conversation-not-found</c>: no conversation has this identifier.</response>
    /// <response code="409">
    /// Nothing was changed: <c>llm-chat.storage-conflict</c> (the conversation changed since your read; read it again),
    /// <c>llm-chat.active-turn-conflict</c> (a turn is active or unfinished) or <c>llm-chat.runtime-profile-changed</c>
    /// (the host's active database profile changed; read the conversation before repeating the request).
    /// </response>
    /// <response code="500">
    /// <c>llm-chat.storage-corrupted</c>: the conversation's transcript, definition or pinned revision is missing from
    /// storage; nothing was changed.
    /// </response>
    internal static async Task<IResult> ArchiveConversationAsync(
        Guid conversationId,
        HttpRequest httpRequest,
        [Description(IfMatchDescription)]
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
