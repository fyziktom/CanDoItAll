using CanDoItAll.Modules.LlmChats.Application;
using CanDoItAll.Modules.LlmChats.Common;
using CanDoItAll.Modules.LlmChats.Definitions;
using CanDoItAll.Modules.LlmChats.Ports;
using Microsoft.AspNetCore.Mvc;

namespace CanDoItAll.Web.Api;

internal static class LlmChatDefinitionEndpoints
{
    public static void MapLlmChatDefinitionEndpoints(this RouteGroupBuilder api)
    {
        var definitions = api.MapGroup("/llm-chats")
            .WithTags("LLM Chats")
            .DisableAntiforgery();
        definitions.MapGet("/provider-options", ListProviderOptionsAsync)
            .WithName("ListLlmChatProviderOptions")
            .Produces<LlmChatProviderOptionApiResponse[]>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError)
            .ApplyApiAuthorization(api, ApiAuthorizationPolicies.ReadLlmChats);
        definitions.MapGet(string.Empty, ListDefinitionsAsync)
            .WithName("ListLlmChatDefinitions")
            .Produces<LlmChatApiPage<LlmChatDefinitionApiResponse>>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .ApplyApiAuthorization(api, ApiAuthorizationPolicies.ReadLlmChats);
        definitions.MapPost(string.Empty, CreateDefinitionAsync)
            .WithName("CreateLlmChatDefinition")
            .Accepts<LlmChatDefinitionMutationApiRequest>("application/json")
            .Produces<LlmChatDefinitionApiResponse>(StatusCodes.Status201Created)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)
            .ApplyApiAuthorization(api, ApiAuthorizationPolicies.ManageLlmChats);
        definitions.MapGet("/{definitionId:guid}", GetDefinitionAsync)
            .WithName("GetLlmChatDefinition")
            .Produces<LlmChatDefinitionApiResponse>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .ApplyApiAuthorization(api, ApiAuthorizationPolicies.ReadLlmChats);
        definitions.MapGet("/{definitionId:guid}/editor", GetDefinitionEditorAsync)
            .WithName("GetLlmChatDefinitionEditor")
            .Produces<LlmChatDefinitionEditorApiResponse>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .ApplyApiAuthorization(api, ApiAuthorizationPolicies.ManageLlmChats);
        definitions.MapPut("/{definitionId:guid}", UpdateDefinitionAsync)
            .WithName("UpdateLlmChatDefinition")
            .Accepts<LlmChatDefinitionMutationApiRequest>("application/json")
            .Produces<LlmChatDefinitionApiResponse>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict)
            .Produces<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)
            .ApplyApiAuthorization(api, ApiAuthorizationPolicies.ManageLlmChats);
        MapDefinitionStatus(definitions, api, "activate", LlmChatDefinitionStatus.Active, "ActivateLlmChatDefinition");
        MapDefinitionStatus(definitions, api, "suspend", LlmChatDefinitionStatus.Suspended, "SuspendLlmChatDefinition");
        MapDefinitionStatus(definitions, api, "archive", LlmChatDefinitionStatus.Archived, "ArchiveLlmChatDefinition");
    }

    private static void MapDefinitionStatus(
        RouteGroupBuilder definitions,
        IEndpointRouteBuilder endpoints,
        string route,
        LlmChatDefinitionStatus status,
        string operationName)
        => definitions.MapPost($"/{{definitionId:guid}}/{route}", async (
                Guid definitionId,
                HttpRequest request,
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
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict)
            .ApplyApiAuthorization(endpoints, ApiAuthorizationPolicies.ManageLlmChats);

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
            page.NextCursor is { } next ? LlmChatApiCursorCodec.Encode(next) : null));
    }

    private static async Task<IResult> CreateDefinitionAsync(
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

    private static Task<IResult> GetDefinitionAsync(
        Guid definitionId,
        HttpResponse response,
        ILlmChatDefinitionApplicationService service,
        CancellationToken cancellationToken)
        => GetDefinitionCoreAsync(definitionId, response, service, includeEditorFields: false, cancellationToken);

    private static Task<IResult> GetDefinitionEditorAsync(
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

    private static async Task<IResult> UpdateDefinitionAsync(
        Guid definitionId,
        HttpRequest httpRequest,
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
