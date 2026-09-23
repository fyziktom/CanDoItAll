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

    /// <summary>
    /// Read the catalog of provider models that this host shares with other hosts.
    /// </summary>
    /// <remarks>
    /// Returns the host's native shared-provider catalog: every published provider that is eligible for sharing, with
    /// its revision, purpose, health, default model and models (routing identifier, capabilities, public prices and
    /// reasoning support). Internal profile identifiers, credentials, private endpoint addresses and raw diagnostics
    /// are never included. Another CanDoItAll host uses this catalog to synchronize a shared-provider source;
    /// OpenAI-compatible clients can use <c>GET /api/shared-providers/openai/v1/models</c> instead. The read has no
    /// side effects.
    ///
    /// Conditional reads: the response carries a strong <c>ETag</c>, the quoted <c>catalogRevision</c>, for example
    /// <c>"sha256:9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08"</c>. Send a received entity tag in
    /// <c>If-None-Match</c> to get 304 without a body while the catalog is unchanged; <c>*</c> matches any catalog and
    /// weak tags match too. The header may hold at most 32 entity tags and 8,192 characters, and <c>*</c> only on its
    /// own.
    ///
    /// Every response carries <c>Cache-Control: private, no-cache</c> and <c>CanDoItAll-Request-Id</c>, which
    /// identifies the request for support. The optional <c>CanDoItAll-Access-Context-Ref</c> and
    /// <c>CanDoItAll-Access-Context-Type</c> headers are recorded for audit correlation only and never authorize
    /// anything. Failures use the general <c>errors</c> envelope.
    ///
    /// Authority: when API authorization is enabled, a bearer token with the <c>api</c> or
    /// <c>api.shared-providers.catalog.read</c> scope. Reading the catalog does not authorize invoking the models,
    /// which requires <c>api</c> or <c>api.shared-providers.invoke</c>.
    /// </remarks>
    /// <response code="200">The catalog; its <c>ETag</c> header is the quoted catalog revision.</response>
    /// <response code="304">
    /// The catalog still matches an entity tag sent in <c>If-None-Match</c>; no body. The current <c>ETag</c> is
    /// repeated.
    /// </response>
    /// <response code="400">
    /// <c>If-None-Match</c> is malformed, too long or lists too many entity tags
    /// (<c>shared-provider.catalog.if-none-match-invalid</c>), or an access-context header is invalid
    /// (<c>api.access-context-invalid</c>).
    /// </response>
    /// <response code="401">
    /// API authorization is enabled and no valid bearer token was sent (<c>shared-provider.catalog.unauthorized</c>).
    /// </response>
    /// <response code="403">The token lacks the catalog scope (<c>shared-provider.catalog.forbidden</c>).</response>
    /// <response code="503">
    /// The catalog could not be built (<c>shared-provider.catalog.unavailable</c>). Nothing was changed; try again
    /// later.
    /// </response>
    internal static async Task WriteNativeCatalogAsync(
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

        httpContext.Response.Headers.Vary = SharedProviderHeaders.CatalogFeatures;
        if (httpContext.Request.Headers[SharedProviderHeaders.CatalogFeatures] == SharedProviderProtocol.ImagePricingFeature) {
            httpContext.Response.Headers[SharedProviderHeaders.CatalogFeatures] = SharedProviderProtocol.ImagePricingFeature;
        } else {
            snapshot = snapshot.WithoutImageInputPrices();
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

    /// <summary>
    /// List the shared models in the OpenAI model-list format.
    /// </summary>
    /// <remarks>
    /// Returns every model of every shared publication as an OpenAI-style list, so that an OpenAI-compatible client
    /// configured with the base address <c>/api/shared-providers/openai/v1</c> can discover the routing identifiers to
    /// send as <c>model</c>. The list includes models that consumers do not suggest in pickers and carries only the
    /// routing identifiers; publications, capabilities and prices are in <c>GET /api/shared-providers/v1/catalog</c>.
    /// The read has no side effects.
    ///
    /// Conditional reads work as for the catalog: the <c>ETag</c> is the quoted catalog revision, and a matching
    /// <c>If-None-Match</c> gets 304 without a body. The header may hold at most 32 entity tags and 8,192 characters,
    /// and <c>*</c> only on its own.
    ///
    /// Every response carries <c>Cache-Control: private, no-cache</c> and <c>CanDoItAll-Request-Id</c>. The optional
    /// <c>CanDoItAll-Access-Context-Ref</c> and <c>CanDoItAll-Access-Context-Type</c> headers are recorded for audit
    /// correlation only. Failures use the OpenAI error envelope.
    ///
    /// Authority: when API authorization is enabled, a bearer token with the <c>api</c> or
    /// <c>api.shared-providers.catalog.read</c> scope. Listing models does not authorize invoking them, which requires
    /// <c>api</c> or <c>api.shared-providers.invoke</c>.
    /// </remarks>
    /// <response code="200">The model list; its <c>ETag</c> header is the quoted catalog revision.</response>
    /// <response code="304">
    /// The catalog still matches an entity tag sent in <c>If-None-Match</c>; no body. The current <c>ETag</c> is
    /// repeated.
    /// </response>
    /// <response code="400">
    /// <c>If-None-Match</c> is malformed, too long or lists too many entity tags
    /// (<c>shared_provider_invalid_if_none_match</c>), or an access-context header is invalid
    /// (<c>shared_provider_access_context_invalid</c>).
    /// </response>
    /// <response code="401">
    /// API authorization is enabled and no valid bearer token was sent (<c>shared_provider_unauthorized</c>).
    /// </response>
    /// <response code="403">The token lacks the catalog scope (<c>shared_provider_insufficient_scope</c>).</response>
    /// <response code="503">
    /// The catalog could not be built (<c>shared_provider_catalog_unavailable</c>). Nothing was changed; try again
    /// later.
    /// </response>
    internal static async Task WriteOpenAiModelsAsync(
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

    internal sealed class LogCategory
    {
    }
}
