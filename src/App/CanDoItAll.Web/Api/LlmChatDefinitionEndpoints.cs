using System.ComponentModel;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Application;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Common;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Definitions;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Ports;
using Microsoft.AspNetCore.Mvc;

namespace CanDoItAll.Web.Api;

internal static class LlmChatDefinitionEndpoints
{
    // The XML comment generator matches <param> tags by C# parameter name, which never equals the header name
    // "If-Match", so the header is described with DescriptionAttribute instead.
    private const string IfMatchDescription =
        "Strong ETag of the definition version you read, for example `\"3\"` including the quotes, as returned " +
        "in the `ETag` response header. It carries the same concurrency token as `expectedConcurrencyToken` in the " +
        "body: send at least one of them, and when both are present they must be equal. A weak ETag (`W/`), `*` or " +
        "a list is rejected with 400.";

    public static void MapLlmChatDefinitionEndpoints(this RouteGroupBuilder api)
    {
        var definitions = api.MapGroup("/llm-chats")
            .WithTags("LLM Chats")
            .DisableAntiforgery();
        definitions.MapGet("/provider-options", ListProviderOptionsAsync)
            .WithName("ListLlmChatProviderOptions")
            .Produces<LlmChatProviderOptionApiResponse[]>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ApplyApiAuthorization(api, ApiAuthorizationPolicies.ReadLlmChats);
        definitions.MapGet(string.Empty, ListDefinitionsAsync)
            .WithName("ListLlmChatDefinitions")
            .Produces<LlmChatApiPage<LlmChatDefinitionApiResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ApplyApiAuthorization(api, ApiAuthorizationPolicies.ReadLlmChats);
        definitions.MapPost(string.Empty, CreateDefinitionAsync)
            .WithName("CreateLlmChatDefinition")
            .Accepts<LlmChatDefinitionMutationApiRequest>("application/json")
            .Produces<LlmChatDefinitionApiResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .ApplyApiAuthorization(api, ApiAuthorizationPolicies.ManageLlmChats);
        definitions.MapGet("/{definitionId:guid}", GetDefinitionAsync)
            .WithName("GetLlmChatDefinition")
            .Produces<LlmChatDefinitionApiResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ApplyApiAuthorization(api, ApiAuthorizationPolicies.ReadLlmChats);
        definitions.MapGet("/{definitionId:guid}/editor", GetDefinitionEditorAsync)
            .WithName("GetLlmChatDefinitionEditor")
            .Produces<LlmChatDefinitionEditorApiResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ApplyApiAuthorization(api, ApiAuthorizationPolicies.ManageLlmChats);
        definitions.MapPut("/{definitionId:guid}", UpdateDefinitionAsync)
            .WithName("UpdateLlmChatDefinition")
            .Accepts<LlmChatDefinitionMutationApiRequest>("application/json")
            .Produces<LlmChatDefinitionApiResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .ApplyApiAuthorization(api, ApiAuthorizationPolicies.ManageLlmChats);
        MapDefinitionStatus(definitions, api, "activate", ActivateDefinitionAsync, "ActivateLlmChatDefinition");
        MapDefinitionStatus(definitions, api, "suspend", SuspendDefinitionAsync, "SuspendLlmChatDefinition");
        MapDefinitionStatus(definitions, api, "archive", ArchiveDefinitionAsync, "ArchiveLlmChatDefinition");
    }

    private static void MapDefinitionStatus(
        RouteGroupBuilder definitions,
        IEndpointRouteBuilder endpoints,
        string route,
        Delegate handler,
        string operationName)
        => definitions.MapPost($"/{{definitionId:guid}}/{route}", handler)
            .WithName(operationName)
            .Accepts<LlmChatExpectedConcurrencyApiRequest>("application/json")
            .Produces<LlmChatDefinitionApiResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .ApplyApiAuthorization(endpoints, ApiAuthorizationPolicies.ManageLlmChats);

    /// <summary>
    /// List the provider profiles, models and thinking-effort options that LLM Chat definitions can use.
    /// </summary>
    /// <remarks>
    /// Returns every enabled chat provider profile, ordered by name, with the models a definition may select and, for
    /// each model, the typed thinking-effort values it accepts. Read it before creating or updating a definition
    /// (<c>POST /api/llm-chats</c>, <c>PUT /api/llm-chats/{definitionId}</c>): <c>providerProfileId</c> and
    /// <c>model</c> come from here, and <c>thinkingEffort</c> must be null or one of the model's <c>allowedEfforts</c>.
    ///
    /// The response is credential-free: it never contains endpoints, credential names or values, or local paths.
    ///
    /// Authority: when API authorization is enabled, a bearer token with the exact scope <c>api.llm-chats.read</c>; the
    /// broad <c>api</c> scope is not accepted. With authorization disabled (the development default) the route is open.
    /// Failures use Problem Details (<c>application/problem+json</c>) with a stable <c>code</c> member.
    /// </remarks>
    /// <response code="200">
    /// The usable provider profiles; an empty array when no chat provider profile is enabled.
    /// </response>
    /// <response code="422">
    /// A provider model has invalid thinking-effort capability settings (<c>llm-chat.model-settings-invalid</c>); no
    /// list is returned. The provider configuration must be corrected first.
    /// </response>
    internal static async Task<IResult> ListProviderOptionsAsync(
        ILlmChatProviderResolver resolver,
        CancellationToken cancellationToken)
    {
        var result = await resolver.ListOptionsAsync(cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? Results.Ok(result.Value!.Select(LlmChatApiMapper.ToResponse).ToArray())
            : LlmChatApiResults.FromFailure(result.Errors);
    }

    /// <summary>
    /// List LLM Chat definitions one page at a time, optionally only those with a given lifecycle status.
    /// </summary>
    /// <remarks>
    /// Returns definitions ordered by their last update, newest first (ties by identifier). List items are summaries:
    /// <c>modelSettings</c>, <c>responseFormat</c> and <c>revisionReason</c> are omitted, so read
    /// <c>GET /api/llm-chats/{definitionId}</c> for the detail. The system prompt is never listed.
    ///
    /// Paging uses an opaque cursor, not an offset: omit <c>cursor</c> for the first page, then send the previous
    /// page's <c>nextCursor</c> unchanged, with the same <c>status</c> filter, until <c>nextCursor</c> is null. A
    /// definition that is updated while you page moves to the start of the order and can be missed by later pages.
    ///
    /// Authority: when API authorization is enabled, a bearer token with the exact scope <c>api.llm-chats.read</c>; the
    /// broad <c>api</c> scope is not accepted. With authorization disabled (the development default) the route is open.
    /// Failures use Problem Details (<c>application/problem+json</c>) with a stable <c>code</c> member.
    /// </remarks>
    /// <param name="take">Number of definitions per page, from 1 through 100. Omitted means 50.</param>
    /// <param name="cursor">
    /// Opaque continuation value from the previous page's <c>nextCursor</c>; omit it for the first page. It is not a
    /// page number or an offset. A malformed value, or a cursor of another list, is rejected.
    /// </param>
    /// <param name="status">
    /// Lifecycle status to include: <c>draft</c>, <c>active</c>, <c>suspended</c> or <c>archived</c>, matched
    /// case-insensitively. Omitted or empty includes every status; numbers are rejected.
    /// </param>
    /// <response code="200">
    /// One page of definitions. An empty <c>items</c> array with a null <c>nextCursor</c> means that nothing matched.
    /// </response>
    /// <response code="400">
    /// <c>llm-chat.invalid-request</c>: <c>take</c> is outside 1 through 100, <c>cursor</c> is invalid or <c>status</c>
    /// is unknown; <c>detail</c> names the problem. A <c>take</c> that is not an integer is rejected with HTTP 400
    /// before the operation runs, without this body.
    /// </response>
    /// <response code="409">
    /// <c>llm-chat.runtime-profile-changed</c>: the host's active database profile changed while the request ran.
    /// Repeat the request.
    /// </response>
    internal static async Task<IResult> ListDefinitionsAsync(
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
            page.NextCursor is { } next ? LlmChatApiCursorCodec.Encode(next) : null));
    }

    /// <summary>
    /// Create an LLM Chat definition with its first revision.
    /// </summary>
    /// <remarks>
    /// Validates the provider profile, model, thinking effort, model settings and response format, then stores a new
    /// definition in <c>draft</c> status with revision 1 and concurrency token 0. A draft cannot start conversations:
    /// activate it with <c>POST /api/llm-chats/{definitionId}/activate</c>. Take <c>providerProfileId</c>, <c>model</c>
    /// and <c>thinkingEffort</c> from <c>GET /api/llm-chats/provider-options</c>. The body is read strictly: unknown
    /// members are rejected, and <c>expectedConcurrencyToken</c> is ignored on create.
    ///
    /// Creation is not idempotent: every successful request creates another definition. After an ambiguous failure,
    /// such as a lost response, list the definitions before sending the request again.
    ///
    /// Authority: when API authorization is enabled, a bearer token with the exact scope <c>api.llm-chats.manage</c>;
    /// the broad <c>api</c> scope is not accepted. With authorization disabled (the development default) the route is
    /// open. Failures use Problem Details (<c>application/problem+json</c>) with a stable <c>code</c> member.
    /// </remarks>
    /// <param name="httpRequest">The definition to create, as a JSON object (<c>application/json</c>).</param>
    /// <response code="201">
    /// The definition was created in <c>draft</c> status. The <c>Location</c> header is its URL
    /// (<c>/api/llm-chats/{id}</c>) and the <c>ETag</c> header carries its concurrency token. The body is the
    /// definition detail, without the system prompt.
    /// </response>
    /// <response code="400">
    /// <c>llm-chat.invalid-request</c>, nothing was created: the body is missing, is not JSON, has unknown members or a
    /// wrong JSON type (for example a number as <c>thinkingEffort</c>); a required value is blank or null (name,
    /// provider profile, model) or the system prompt is null (an empty system prompt is allowed); a length or count
    /// limit is exceeded; <c>temperature</c> is outside 0 through
    /// 2; <c>timeoutSeconds</c> is not positive or exceeds 1,800; <c>modelParameterConfiguration</c> or <c>schema</c>
    /// is not a JSON object or has duplicate member names; the model parameters contain a thinking-effort setting; the
    /// avatar is not an HTTP(S) URL or bundled avatar; or tags repeat after normalization. Some rejections explain the
    /// problem in <c>detail</c>.
    /// </response>
    /// <response code="409">
    /// <c>llm-chat.runtime-profile-changed</c>: the host's active database profile changed while the request ran. The
    /// definition may or may not have been created; list the definitions before sending the request again.
    /// </response>
    /// <response code="422">
    /// The provider selection cannot be used, nothing was created: <c>llm-chat.provider-not-found</c> (no provider
    /// profile has this identifier), <c>llm-chat.model-not-supported</c> (the profile does not offer the model),
    /// <c>llm-chat.thinking-effort-not-supported</c> (the model does not accept the thinking effort) or
    /// <c>llm-chat.model-settings-invalid</c> (the model's thinking-effort capability settings are invalid).
    /// </response>
    /// <response code="503">
    /// <c>llm-chat.provider-unavailable</c>: the provider profile exists but is disabled or does not serve chat;
    /// nothing was created. Choose a profile from the provider options.
    /// </response>
    internal static async Task<IResult> CreateDefinitionAsync(
        HttpRequest httpRequest,
        HttpResponse response,
        ILlmChatDefinitionApplicationService service,
        CancellationToken cancellationToken)
    {
        var body = await LlmChatApiRequestReader
            .ReadAsync<LlmChatDefinitionMutationApiRequest>(httpRequest, cancellationToken)
            .ConfigureAwait(false);
        if (body.Error is not null)
        {
            return body.Error;
        }

        if (!LlmChatApiMapper.TryMapCreate(body.Value!, out var command, out var mappingError))
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

    /// <summary>
    /// Read an LLM Chat definition without its system prompt.
    /// </summary>
    /// <remarks>
    /// Returns the definition's lifecycle status and the settings of its current revision, including model settings and
    /// response format, but never the system prompt or provider credentials. To edit the definition read
    /// <c>GET /api/llm-chats/{definitionId}/editor</c> instead, which also returns the system prompt.
    ///
    /// The <c>ETag</c> response header carries the concurrency token as a strong ETag, for example <c>"3"</c>; send it
    /// as <c>If-Match</c>, or its number as <c>expectedConcurrencyToken</c>, with the next update or lifecycle change.
    ///
    /// Authority: when API authorization is enabled, a bearer token with the exact scope <c>api.llm-chats.read</c>; the
    /// broad <c>api</c> scope is not accepted. With authorization disabled (the development default) the route is open.
    /// Failures use Problem Details (<c>application/problem+json</c>) with a stable <c>code</c> member.
    /// </remarks>
    /// <param name="definitionId">
    /// Identifier of the definition, as returned in <c>id</c> by <c>POST /api/llm-chats</c> or
    /// <c>GET /api/llm-chats</c>.
    /// </param>
    /// <response code="200">The definition detail.</response>
    /// <response code="400"><c>llm-chat.invalid-request</c>: the identifier is the empty GUID.</response>
    /// <response code="404"><c>llm-chat.definition-not-found</c>: no definition has this identifier.</response>
    /// <response code="409">
    /// <c>llm-chat.runtime-profile-changed</c>: the host's active database profile changed while the request ran.
    /// Repeat the request.
    /// </response>
    internal static Task<IResult> GetDefinitionAsync(
        Guid definitionId,
        HttpResponse response,
        ILlmChatDefinitionApplicationService service,
        CancellationToken cancellationToken)
        => GetDefinitionCoreAsync(definitionId, response, service, includeEditorFields: false, cancellationToken);

    /// <summary>
    /// Read the editable current revision of an LLM Chat definition, including its system prompt.
    /// </summary>
    /// <remarks>
    /// Returns every value that <c>PUT /api/llm-chats/{definitionId}</c> needs. Because an update replaces the whole
    /// configuration, read this resource first, change only the values you intend to change and send all of them back
    /// with its <c>concurrencyToken</c> (also in the <c>ETag</c> response header).
    ///
    /// This is the only definition read that returns the system prompt, which is why it requires the manage scope. It
    /// never returns provider credentials, endpoints or local paths.
    ///
    /// Authority: when API authorization is enabled, a bearer token with the exact scope <c>api.llm-chats.manage</c>;
    /// the broad <c>api</c> scope is not accepted. With authorization disabled (the development default) the route is
    /// open. Failures use Problem Details (<c>application/problem+json</c>) with a stable <c>code</c> member.
    /// </remarks>
    /// <param name="definitionId">
    /// Identifier of the definition, as returned in <c>id</c> by <c>POST /api/llm-chats</c> or
    /// <c>GET /api/llm-chats</c>.
    /// </param>
    /// <response code="200">The editable current revision, with the definition's concurrency token.</response>
    /// <response code="400"><c>llm-chat.invalid-request</c>: the identifier is the empty GUID.</response>
    /// <response code="404"><c>llm-chat.definition-not-found</c>: no definition has this identifier.</response>
    /// <response code="409">
    /// <c>llm-chat.runtime-profile-changed</c>: the host's active database profile changed while the request ran.
    /// Repeat the request.
    /// </response>
    internal static Task<IResult> GetDefinitionEditorAsync(
        Guid definitionId,
        HttpResponse response,
        ILlmChatDefinitionApplicationService service,
        CancellationToken cancellationToken)
        => GetDefinitionCoreAsync(definitionId, response, service, includeEditorFields: true, cancellationToken);

    private static async Task<IResult> GetDefinitionCoreAsync(
        Guid definitionId,
        HttpResponse response,
        ILlmChatDefinitionApplicationService service,
        bool includeEditorFields,
        CancellationToken cancellationToken)
    {
        if (!LlmChatApiIds.TryCreateDefinitionId(definitionId, out var id, out var error))
        {
            return error!;
        }

        var result = await service.GetAsync(id, cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
        {
            return LlmChatApiResults.FromFailure(result.Errors);
        }

        LlmChatApiResults.SetEtag(response, result.Value!.Definition.ConcurrencyToken);
        return Results.Ok(includeEditorFields
            ? LlmChatApiMapper.ToEditorResponse(result.Value)
            : LlmChatApiMapper.ToDetailResponse(result.Value));
    }

    /// <summary>
    /// Replace the configuration of an LLM Chat definition by appending a new immutable revision.
    /// </summary>
    /// <remarks>
    /// The body replaces the whole editable configuration: every member becomes part of the new revision, and a member
    /// you omit or send as null takes its empty or default meaning (for example, no <c>tags</c> removes all tags and no
    /// <c>responseFormat</c> removes the response format). Read <c>GET /api/llm-chats/{definitionId}/editor</c> first
    /// and send back every value you do not intend to change, including the system prompt.
    ///
    /// On success <c>currentRevision</c> and the concurrency token each increase by one and the lifecycle status stays
    /// as it was. Existing conversations stay pinned to the revision they were created with; only conversations created
    /// afterwards use the new revision. An archived definition cannot be updated.
    ///
    /// Concurrency: send the concurrency token you read as <c>expectedConcurrencyToken</c> or as <c>If-Match</c> (for
    /// example <c>If-Match: "3"</c>); when both are present they must be equal. When the definition changed since your
    /// read, the update is rejected with 409 <c>llm-chat.definition-concurrency-conflict</c>: read the editor resource
    /// again and reapply your change.
    ///
    /// Authority: when API authorization is enabled, a bearer token with the exact scope <c>api.llm-chats.manage</c>;
    /// the broad <c>api</c> scope is not accepted. With authorization disabled (the development default) the route is
    /// open. Failures use Problem Details (<c>application/problem+json</c>) with a stable <c>code</c> member.
    /// </remarks>
    /// <param name="definitionId">
    /// Identifier of the definition to update, as returned in <c>id</c> by <c>POST /api/llm-chats</c> or
    /// <c>GET /api/llm-chats</c>.
    /// </param>
    /// <param name="httpRequest">
    /// The complete new configuration, as a JSON object (<c>application/json</c>), usually with
    /// <c>expectedConcurrencyToken</c>.
    /// </param>
    /// <response code="200">
    /// The new revision was committed. The body is the definition detail and the <c>ETag</c> header carries the new
    /// concurrency token.
    /// </response>
    /// <response code="400">
    /// <c>llm-chat.invalid-request</c>, nothing was changed: the identifier is the empty GUID; the body is missing, is
    /// not JSON, has unknown members or a wrong JSON type; no concurrency token was sent, <c>If-Match</c> is not a
    /// single strong numeric ETag, a token is negative or the two tokens differ; or a value is invalid as described for
    /// <c>POST /api/llm-chats</c>.
    /// </response>
    /// <response code="404"><c>llm-chat.definition-not-found</c>: no definition has this identifier.</response>
    /// <response code="409">
    /// Nothing was changed: <c>llm-chat.definition-concurrency-conflict</c> (the definition changed since your read),
    /// <c>llm-chat.definition-not-active</c> (the definition is archived and read-only) or
    /// <c>llm-chat.runtime-profile-changed</c> (the host's active database profile changed; read the definition before
    /// deciding whether to repeat the update).
    /// </response>
    /// <response code="422">
    /// The provider selection cannot be used, nothing was changed: <c>llm-chat.provider-not-found</c>,
    /// <c>llm-chat.model-not-supported</c>, <c>llm-chat.thinking-effort-not-supported</c> or
    /// <c>llm-chat.model-settings-invalid</c>, as for <c>POST /api/llm-chats</c>.
    /// </response>
    /// <response code="503">
    /// <c>llm-chat.provider-unavailable</c>: the provider profile exists but is disabled or does not serve chat;
    /// nothing was changed.
    /// </response>
    internal static async Task<IResult> UpdateDefinitionAsync(
        Guid definitionId,
        HttpRequest httpRequest,
        [Description(IfMatchDescription)]
        [FromHeader(Name = "If-Match")] string? ifMatch,
        HttpResponse response,
        ILlmChatDefinitionApplicationService service,
        CancellationToken cancellationToken)
    {
        if (!LlmChatApiIds.TryCreateDefinitionId(definitionId, out var id, out var idError))
        {
            return idError!;
        }

        var body = await LlmChatApiRequestReader
            .ReadAsync<LlmChatDefinitionMutationApiRequest>(httpRequest, cancellationToken)
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

        if (!LlmChatApiMapper.TryMapUpdate(
                id,
                body.Value,
                expectedToken,
                out var command,
                out var mappingError))
        {
            return LlmChatApiResults.InvalidRequest(mappingError);
        }

        var result = await service.UpdateAsync(command!, cancellationToken).ConfigureAwait(false);
        return DefinitionMutationResult(result, response);
    }

    /// <summary>
    /// Activate an LLM Chat definition so that it can start conversations and turns.
    /// </summary>
    /// <remarks>
    /// Moves a <c>draft</c> or <c>suspended</c> definition to <c>active</c>. Only an active definition can create
    /// conversations (<c>POST /api/llm-chats/{definitionId}/conversations</c>) and admit turns in them. Activating an
    /// active definition changes nothing and returns it unchanged, provided the concurrency token matches.
    ///
    /// Lifecycle: draft may become active or archived, active may become suspended or archived, suspended may become
    /// active or archived, and archived is final; any other change is rejected with 409
    /// <c>llm-chat.definition-not-active</c>. A lifecycle change creates no revision; it increases the concurrency
    /// token by one.
    ///
    /// Send the concurrency token you read as <c>expectedConcurrencyToken</c> in the JSON body or as <c>If-Match</c>. A
    /// JSON body is required even with <c>If-Match</c>; <c>{}</c> is then enough.
    ///
    /// Authority: when API authorization is enabled, a bearer token with the exact scope <c>api.llm-chats.manage</c>;
    /// the broad <c>api</c> scope is not accepted. With authorization disabled (the development default) the route is
    /// open. Failures use Problem Details (<c>application/problem+json</c>) with a stable <c>code</c> member.
    /// </remarks>
    /// <param name="definitionId">
    /// Identifier of the definition, as returned in <c>id</c> by <c>POST /api/llm-chats</c> or
    /// <c>GET /api/llm-chats</c>.
    /// </param>
    /// <param name="request">
    /// JSON object with the expected concurrency token, for example <c>{ "expectedConcurrencyToken": 0 }</c>, or
    /// <c>{}</c> when the token is sent as <c>If-Match</c>.
    /// </param>
    /// <response code="200">
    /// The definition is active. The <c>ETag</c> header carries its concurrency token, increased by one unless it was
    /// already active.
    /// </response>
    /// <response code="400">
    /// <c>llm-chat.invalid-request</c>, nothing was changed: the identifier is the empty GUID; the body is missing, is
    /// not JSON or has other members; no concurrency token was sent; <c>If-Match</c> is not a single strong numeric
    /// ETag; a token is negative; or the two tokens differ.
    /// </response>
    /// <response code="404"><c>llm-chat.definition-not-found</c>: no definition has this identifier.</response>
    /// <response code="409">
    /// Nothing was changed: <c>llm-chat.definition-concurrency-conflict</c> (the definition changed since your read;
    /// read it again), <c>llm-chat.definition-not-active</c> (the definition is archived) or
    /// <c>llm-chat.runtime-profile-changed</c> (the host's active database profile changed; read the definition before
    /// repeating the request).
    /// </response>
    /// <response code="500">
    /// <c>llm-chat.storage-corrupted</c>: the definition's current revision is missing from storage; nothing was
    /// changed.
    /// </response>
    internal static Task<IResult> ActivateDefinitionAsync(
        Guid definitionId,
        HttpRequest request,
        [Description(IfMatchDescription)]
        [FromHeader(Name = "If-Match")] string? ifMatch,
        HttpResponse response,
        ILlmChatDefinitionApplicationService service,
        CancellationToken cancellationToken)
        => ChangeDefinitionStatusAsync(
            definitionId,
            LlmChatDefinitionStatus.Active,
            request,
            ifMatch,
            response,
            service,
            cancellationToken);

    /// <summary>
    /// Suspend an active LLM Chat definition so that it cannot start conversations or turns until reactivated.
    /// </summary>
    /// <remarks>
    /// Moves an <c>active</c> definition to <c>suspended</c>. While it is suspended, creating a conversation from it
    /// and sending turns in its conversations are rejected with 409 <c>llm-chat.definition-not-active</c>, and a turn
    /// that has not yet reached its provider fails with that code. Conversations and transcripts stay readable.
    /// Reactivate with <c>POST /api/llm-chats/{definitionId}/activate</c>. Suspending a suspended definition changes
    /// nothing and returns it unchanged, provided the concurrency token matches.
    ///
    /// Lifecycle: draft may become active or archived, active may become suspended or archived, suspended may become
    /// active or archived, and archived is final; any other change, such as suspending a draft, is rejected with 409
    /// <c>llm-chat.definition-not-active</c>. A lifecycle change creates no revision; it increases the concurrency
    /// token by one.
    ///
    /// Send the concurrency token you read as <c>expectedConcurrencyToken</c> in the JSON body or as <c>If-Match</c>. A
    /// JSON body is required even with <c>If-Match</c>; <c>{}</c> is then enough.
    ///
    /// Authority: when API authorization is enabled, a bearer token with the exact scope <c>api.llm-chats.manage</c>;
    /// the broad <c>api</c> scope is not accepted. With authorization disabled (the development default) the route is
    /// open. Failures use Problem Details (<c>application/problem+json</c>) with a stable <c>code</c> member.
    /// </remarks>
    /// <param name="definitionId">
    /// Identifier of the definition, as returned in <c>id</c> by <c>POST /api/llm-chats</c> or
    /// <c>GET /api/llm-chats</c>.
    /// </param>
    /// <param name="request">
    /// JSON object with the expected concurrency token, for example <c>{ "expectedConcurrencyToken": 1 }</c>, or
    /// <c>{}</c> when the token is sent as <c>If-Match</c>.
    /// </param>
    /// <response code="200">
    /// The definition is suspended. The <c>ETag</c> header carries its concurrency token, increased by one unless it
    /// was already suspended.
    /// </response>
    /// <response code="400">
    /// <c>llm-chat.invalid-request</c>, nothing was changed: the identifier is the empty GUID; the body is missing, is
    /// not JSON or has other members; no concurrency token was sent; <c>If-Match</c> is not a single strong numeric
    /// ETag; a token is negative; or the two tokens differ.
    /// </response>
    /// <response code="404"><c>llm-chat.definition-not-found</c>: no definition has this identifier.</response>
    /// <response code="409">
    /// Nothing was changed: <c>llm-chat.definition-concurrency-conflict</c> (the definition changed since your read;
    /// read it again), <c>llm-chat.definition-not-active</c> (a draft or archived definition cannot be suspended) or
    /// <c>llm-chat.runtime-profile-changed</c> (the host's active database profile changed; read the definition before
    /// repeating the request).
    /// </response>
    /// <response code="500">
    /// <c>llm-chat.storage-corrupted</c>: the definition's current revision is missing from storage; nothing was
    /// changed.
    /// </response>
    internal static Task<IResult> SuspendDefinitionAsync(
        Guid definitionId,
        HttpRequest request,
        [Description(IfMatchDescription)]
        [FromHeader(Name = "If-Match")] string? ifMatch,
        HttpResponse response,
        ILlmChatDefinitionApplicationService service,
        CancellationToken cancellationToken)
        => ChangeDefinitionStatusAsync(
            definitionId,
            LlmChatDefinitionStatus.Suspended,
            request,
            ifMatch,
            response,
            service,
            cancellationToken);

    /// <summary>
    /// Archive an LLM Chat definition permanently.
    /// </summary>
    /// <remarks>
    /// Moves a <c>draft</c>, <c>active</c> or <c>suspended</c> definition to <c>archived</c>. Archiving is final: an
    /// archived definition cannot be reactivated or updated and cannot start conversations or turns, while its existing
    /// conversations and transcripts stay readable. Archiving an archived definition changes nothing and returns it
    /// unchanged, provided the concurrency token matches. A lifecycle change creates no revision; it increases the
    /// concurrency token by one.
    ///
    /// Send the concurrency token you read as <c>expectedConcurrencyToken</c> in the JSON body or as <c>If-Match</c>. A
    /// JSON body is required even with <c>If-Match</c>; <c>{}</c> is then enough.
    ///
    /// Authority: when API authorization is enabled, a bearer token with the exact scope <c>api.llm-chats.manage</c>;
    /// the broad <c>api</c> scope is not accepted. With authorization disabled (the development default) the route is
    /// open. Failures use Problem Details (<c>application/problem+json</c>) with a stable <c>code</c> member.
    /// </remarks>
    /// <param name="definitionId">
    /// Identifier of the definition, as returned in <c>id</c> by <c>POST /api/llm-chats</c> or
    /// <c>GET /api/llm-chats</c>.
    /// </param>
    /// <param name="request">
    /// JSON object with the expected concurrency token, for example <c>{ "expectedConcurrencyToken": 2 }</c>, or
    /// <c>{}</c> when the token is sent as <c>If-Match</c>.
    /// </param>
    /// <response code="200">
    /// The definition is archived. The <c>ETag</c> header carries its concurrency token, increased by one unless it was
    /// already archived.
    /// </response>
    /// <response code="400">
    /// <c>llm-chat.invalid-request</c>, nothing was changed: the identifier is the empty GUID; the body is missing, is
    /// not JSON or has other members; no concurrency token was sent; <c>If-Match</c> is not a single strong numeric
    /// ETag; a token is negative; or the two tokens differ.
    /// </response>
    /// <response code="404"><c>llm-chat.definition-not-found</c>: no definition has this identifier.</response>
    /// <response code="409">
    /// Nothing was changed: <c>llm-chat.definition-concurrency-conflict</c> (the definition changed since your read;
    /// read it again) or <c>llm-chat.runtime-profile-changed</c> (the host's active database profile changed; read the
    /// definition before repeating the request).
    /// </response>
    /// <response code="500">
    /// <c>llm-chat.storage-corrupted</c>: the definition's current revision is missing from storage; nothing was
    /// changed.
    /// </response>
    internal static Task<IResult> ArchiveDefinitionAsync(
        Guid definitionId,
        HttpRequest request,
        [Description(IfMatchDescription)]
        [FromHeader(Name = "If-Match")] string? ifMatch,
        HttpResponse response,
        ILlmChatDefinitionApplicationService service,
        CancellationToken cancellationToken)
        => ChangeDefinitionStatusAsync(
            definitionId,
            LlmChatDefinitionStatus.Archived,
            request,
            ifMatch,
            response,
            service,
            cancellationToken);

    private static async Task<IResult> ChangeDefinitionStatusAsync(
        Guid definitionId,
        LlmChatDefinitionStatus status,
        HttpRequest httpRequest,
        string? ifMatch,
        HttpResponse response,
        ILlmChatDefinitionApplicationService service,
        CancellationToken cancellationToken)
    {
        if (!LlmChatApiIds.TryCreateDefinitionId(definitionId, out var id, out var idError))
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

        var result = await service.ChangeStatusAsync(
            new ChangeLlmChatDefinitionStatusCommand(id, status, expectedToken),
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

    private static bool TryCreateDefinitionQuery(
        int take,
        string? cursor,
        string? status,
        out LlmChatDefinitionQuery? query,
        out IResult? error)
    {
        query = null;
        error = null;
        if (!LlmChatApiCursorCodec.TryDecodeDefinition(cursor, out var position))
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
            query = new LlmChatDefinitionQuery(take, parsedStatus, position);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            error = LlmChatApiResults.InvalidRequest("The definition page size is invalid.");
            return false;
        }
    }
}
