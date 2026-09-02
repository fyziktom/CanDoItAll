using System.Net.Mime;
using System.Text;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using CanDoItAll.SharedProviders.Abstractions;
using Microsoft.Net.Http.Headers;

namespace CanDoItAll.Web.Api;

internal static class SharedProviderCatalogApi
{
    internal const int MaximumIfNoneMatchLength = 8 * 1024;
    private const int MaximumIfNoneMatchEntityTags = 32;

    public static IEndpointRouteBuilder MapSharedProviderCatalogApi(
        this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var nativeCatalog = endpoints.MapGet(
                SharedProviderRoutes.Catalog,
                WriteNativeCatalogAsync)
            .WithName("GetSharedProviderCatalog")
            .WithTags("Shared Providers")
            .WithMetadata(SharedProviderCatalogOpenApiContract.Instance)
            .WithSummary("Get the sanitized shared-provider catalog")
            .WithDescription("Returns only explicitly published and supported provider routes.")
            .Produces<SharedProviderCatalogDocument>(
                StatusCodes.Status200OK,
                MediaTypeNames.Application.Json)
            .Produces(StatusCodes.Status304NotModified)
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ApiErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ApiErrorResponse>(StatusCodes.Status403Forbidden)
            .Produces<ApiErrorResponse>(StatusCodes.Status503ServiceUnavailable);
        nativeCatalog.ApplyApiAuthorization(
            endpoints,
            ApiAuthorizationPolicies.ReadSharedProviderCatalog);

        var openAiModels = endpoints.MapGet(
                SharedProviderRoutes.Models,
                WriteOpenAiModelsAsync)
            .WithName("GetSharedProviderOpenAiModels")
            .WithTags("Shared Providers")
            .WithMetadata(SharedProviderCatalogOpenApiContract.Instance)
            .WithSummary("List shared-provider models using the OpenAI envelope")
            .WithDescription("Returns public routing model IDs without upstream provider details.")
            .Produces<SharedProviderOpenAiModelList>(
                StatusCodes.Status200OK,
                MediaTypeNames.Application.Json)
            .Produces(StatusCodes.Status304NotModified)
            .Produces<SharedProviderOpenAiErrorEnvelope>(StatusCodes.Status400BadRequest)
            .Produces<SharedProviderOpenAiErrorEnvelope>(StatusCodes.Status401Unauthorized)
            .Produces<SharedProviderOpenAiErrorEnvelope>(StatusCodes.Status403Forbidden)
            .Produces<SharedProviderOpenAiErrorEnvelope>(StatusCodes.Status503ServiceUnavailable);
        openAiModels.ApplyApiAuthorization(
            endpoints,
            ApiAuthorizationPolicies.ReadSharedProviderCatalog);

        return endpoints;
    }

    private static async Task WriteNativeCatalogAsync(
        HttpContext httpContext,
        ISharedProviderCatalogQueryService queryService,
        ILogger<LogCategory> logger)
    {
        if (!TryReadIfNoneMatch(httpContext, out var validators))
        {
            await SharedProviderApiResponseWriter.WriteInvalidIfNoneMatchAsync(httpContext);
            return;
        }

        var snapshot = await TryGetSnapshotAsync(httpContext, queryService, logger);
        if (snapshot is null)
        {
            return;
        }

        SharedProviderApiResponseWriter.ApplyCatalogHeaders(httpContext, snapshot.EntityTag);
        if (Matches(validators, snapshot.EntityTag))
        {
            httpContext.Response.StatusCode = StatusCodes.Status304NotModified;
            return;
        }

        httpContext.Response.StatusCode = StatusCodes.Status200OK;
        httpContext.Response.ContentType = MediaTypeNames.Application.Json;
        await httpContext.Response.WriteAsync(
            SharedProviderProtocolJson.SerializeCatalog(snapshot.Catalog),
            Encoding.UTF8,
            httpContext.RequestAborted);
    }

    private static async Task WriteOpenAiModelsAsync(
        HttpContext httpContext,
        ISharedProviderCatalogQueryService queryService,
        ILogger<LogCategory> logger)
    {
        if (!TryReadIfNoneMatch(httpContext, out var validators))
        {
            await SharedProviderApiResponseWriter.WriteInvalidIfNoneMatchAsync(httpContext);
            return;
        }

        var snapshot = await TryGetSnapshotAsync(httpContext, queryService, logger);
        if (snapshot is null)
        {
            return;
        }

        SharedProviderApiResponseWriter.ApplyCatalogHeaders(httpContext, snapshot.EntityTag);
        if (Matches(validators, snapshot.EntityTag))
        {
            httpContext.Response.StatusCode = StatusCodes.Status304NotModified;
            return;
        }

        var models = snapshot.Catalog.Providers
            .SelectMany(publication => publication.Models)
            .Select(model => new SharedProviderOpenAiModel(
                model.Id,
                SharedProviderOpenAiConstants.ModelObject,
                created: 0,
                SharedProviderOpenAiConstants.OwnedBy))
            .ToArray();
        httpContext.Response.StatusCode = StatusCodes.Status200OK;
        await httpContext.Response.WriteAsJsonAsync(
            new SharedProviderOpenAiModelList(
                SharedProviderOpenAiConstants.ListObject,
                models),
            SharedProviderProtocolJson.Options,
            httpContext.RequestAborted);
    }

    private static async Task<SharedProviderCatalogSnapshot?> TryGetSnapshotAsync(
        HttpContext httpContext,
        ISharedProviderCatalogQueryService queryService,
        ILogger logger)
    {
        try
        {
            return await queryService.GetSnapshotAsync(httpContext.RequestAborted);
        }
        catch (OperationCanceledException) when (httpContext.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                "Shared-provider catalog projection failed with {FailureType} for trace {TraceIdentifier}.",
                exception.GetType().Name,
                httpContext.TraceIdentifier);
            await SharedProviderApiResponseWriter.WriteCatalogUnavailableAsync(httpContext);
            return null;
        }
    }

    private static bool TryReadIfNoneMatch(
        HttpContext httpContext,
        out IReadOnlyList<EntityTagHeaderValue>? validators)
    {
        validators = null;
        if (!httpContext.Request.Headers.TryGetValue(HeaderNames.IfNoneMatch, out var rawValues))
        {
            return true;
        }

        string[] values = rawValues
            .Select(value => value ?? string.Empty)
            .ToArray();
        long totalLength = values.Sum(value => (long)value.Length);
        if (values.Length == 0 || totalLength > MaximumIfNoneMatchLength ||
            !EntityTagHeaderValue.TryParseStrictList(values, out var parsed) ||
            parsed.Count == 0 || parsed.Count > MaximumIfNoneMatchEntityTags ||
            parsed.Count > 1 && parsed.Any(IsWildcard))
        {
            return false;
        }

        validators = parsed.ToArray();
        return true;
    }

    private static bool Matches(
        IReadOnlyList<EntityTagHeaderValue>? validators,
        SharedProviderCatalogEntityTag currentEntityTag)
    {
        if (validators is null)
        {
            return false;
        }

        var current = EntityTagHeaderValue.Parse(currentEntityTag.Value);
        return validators.Any(candidate =>
            IsWildcard(candidate) ||
            candidate.Compare(current, useStrongComparison: false));
    }

    private static bool IsWildcard(EntityTagHeaderValue candidate)
        => string.Equals(candidate.Tag.Value, "*", StringComparison.Ordinal);

    private sealed class LogCategory
    {
    }
}
