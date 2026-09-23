using CanDoItAll.SharedKernel;
using CanDoItAll.SharedProviders.Abstractions;
using System.Globalization;
using Microsoft.Net.Http.Headers;

namespace CanDoItAll.Web.Api;

internal static class SharedProviderApiResponseWriter
{
    internal const string NativeUnauthorizedCode = "shared-provider.catalog.unauthorized";
    internal const string NativeForbiddenCode = "shared-provider.catalog.forbidden";
    internal const string NativeInvalidIfNoneMatchCode = "shared-provider.catalog.if-none-match-invalid";
    internal const string NativeCatalogUnavailableCode = "shared-provider.catalog.unavailable";
    internal const string OpenAiUnauthorizedCode = "shared_provider_unauthorized";
    internal const string OpenAiInsufficientScopeCode = "shared_provider_insufficient_scope";
    internal const string OpenAiInvalidIfNoneMatchCode = "shared_provider_invalid_if_none_match";
    internal const string OpenAiInvalidAccessContextCode = "shared_provider_access_context_invalid";
    internal const string OpenAiCatalogUnavailableCode = "shared_provider_catalog_unavailable";

    private const string SharedProviderPathPrefix = "/api/shared-providers";
    private const string PrivateNoCache = "private, no-cache";
    private const string PrivateNoStoreNoCache = "private, no-store, no-cache";

    public static void ApplyCommonHeaders(HttpContext httpContext)
    {
        httpContext.Response.Headers[HeaderNames.CacheControl] = PrivateNoCache;
        httpContext.Response.Headers[SharedProviderHeaders.RequestId] = httpContext.TraceIdentifier;
    }

    public static void ApplyCatalogHeaders(
        HttpContext httpContext,
        SharedProviderCatalogEntityTag entityTag)
    {
        ApplyCommonHeaders(httpContext);
        httpContext.Response.Headers[HeaderNames.ETag] = entityTag.Value;
    }

    public static void ApplyInferenceHeaders(HttpContext httpContext)
    {
        httpContext.Response.Headers[HeaderNames.CacheControl] = PrivateNoStoreNoCache;
        httpContext.Response.Headers[SharedProviderHeaders.RequestId] = httpContext.TraceIdentifier;
        httpContext.Response.Headers[HeaderNames.XContentTypeOptions] = "nosniff";
        httpContext.Response.Headers.Remove(HeaderNames.ETag);
    }

    public static Task WriteAuthorizationErrorAsync(
        HttpContext httpContext,
        int statusCode)
    {
        if (!IsSharedProviderPath(httpContext.Request.Path))
        {
            return WriteNativeErrorAsync(
                httpContext,
                statusCode,
                statusCode == StatusCodes.Status401Unauthorized
                    ? "api.authorization-required"
                    : "api.authorization-forbidden",
                statusCode == StatusCodes.Status401Unauthorized
                    ? "A valid bearer token is required."
                    : "The bearer token does not authorize this operation.");
        }

        ApplyPathHeaders(httpContext);
        if (IsOpenAiPath(httpContext.Request.Path))
        {
            return statusCode == StatusCodes.Status401Unauthorized
                ? WriteOpenAiErrorAsync(
                    httpContext,
                    statusCode,
                    "A valid bearer token is required.",
                    SharedProviderOpenAiConstants.AuthenticationErrorType,
                    parameter: null,
                    OpenAiUnauthorizedCode)
                : WriteOpenAiErrorAsync(
                    httpContext,
                    statusCode,
                    "The bearer token does not authorize this operation.",
                    SharedProviderOpenAiConstants.PermissionErrorType,
                    parameter: null,
                    OpenAiInsufficientScopeCode);
        }

        return WriteNativeErrorAsync(
            httpContext,
            statusCode,
            statusCode == StatusCodes.Status401Unauthorized
                ? NativeUnauthorizedCode
                : NativeForbiddenCode,
            statusCode == StatusCodes.Status401Unauthorized
                ? "A valid bearer token is required."
                : "The bearer token does not authorize this operation.");
    }

    public static Task WriteInvalidIfNoneMatchAsync(HttpContext httpContext)
    {
        ApplyCommonHeaders(httpContext);
        const string message = "If-None-Match must contain a bounded RFC 9110 entity-tag list.";
        return IsOpenAiPath(httpContext.Request.Path)
            ? WriteOpenAiErrorAsync(
                httpContext,
                StatusCodes.Status400BadRequest,
                message,
                SharedProviderOpenAiConstants.InvalidRequestErrorType,
                HeaderNames.IfNoneMatch,
                OpenAiInvalidIfNoneMatchCode)
            : WriteNativeErrorAsync(
                httpContext,
                StatusCodes.Status400BadRequest,
                NativeInvalidIfNoneMatchCode,
                message);
    }

    public static Task WriteInvalidAccessContextAsync(
        HttpContext httpContext,
        string parameter,
        string nativeMessage)
    {
        if (IsSharedProviderPath(httpContext.Request.Path))
        {
            ApplyPathHeaders(httpContext);
        }

        return IsOpenAiPath(httpContext.Request.Path)
            ? WriteOpenAiErrorAsync(
                httpContext,
                StatusCodes.Status400BadRequest,
                nativeMessage,
                SharedProviderOpenAiConstants.InvalidRequestErrorType,
                parameter,
                OpenAiInvalidAccessContextCode)
            : WriteNativeErrorAsync(
                httpContext,
                StatusCodes.Status400BadRequest,
                AccessContextReferenceMiddleware.InvalidAccessContextErrorCode,
                nativeMessage);
    }

    public static Task WriteCatalogUnavailableAsync(HttpContext httpContext)
    {
        ApplyCommonHeaders(httpContext);
        const string message = "The shared-provider catalog is temporarily unavailable.";
        return IsOpenAiPath(httpContext.Request.Path)
            ? WriteOpenAiErrorAsync(
                httpContext,
                StatusCodes.Status503ServiceUnavailable,
                message,
                SharedProviderOpenAiConstants.ApiErrorType,
                parameter: null,
                OpenAiCatalogUnavailableCode)
            : WriteNativeErrorAsync(
                httpContext,
                StatusCodes.Status503ServiceUnavailable,
                NativeCatalogUnavailableCode,
                message);
    }

    public static Task WriteRelayFailureAsync(
        HttpContext httpContext,
        SharedProviderFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);

        ApplyInferenceHeaders(httpContext);
        if (failure.RetryAfter is { } retryAfter)
        {
            long retryAfterSeconds = checked((long)Math.Ceiling(retryAfter.TotalSeconds));
            httpContext.Response.Headers[HeaderNames.RetryAfter] =
                retryAfterSeconds.ToString(CultureInfo.InvariantCulture);
        }

        return WriteOpenAiErrorAsync(
            httpContext,
            MapStatusCode(failure.Category),
            failure.SanitizedMessage,
            MapErrorType(failure.Category),
            failure.Parameter,
            failure.Code.Value);
    }

    private static Task WriteNativeErrorAsync(
        HttpContext httpContext,
        int statusCode,
        string code,
        string message)
    {
        httpContext.Response.StatusCode = statusCode;
        return httpContext.Response.WriteAsJsonAsync(
            new ApiErrorResponse(
                [new ApiErrorItem(code, message, ErrorSeverity.Error)]),
            httpContext.RequestAborted);
    }

    private static Task WriteOpenAiErrorAsync(
        HttpContext httpContext,
        int statusCode,
        string message,
        string type,
        string? parameter,
        string code)
    {
        httpContext.Response.StatusCode = statusCode;
        return httpContext.Response.WriteAsJsonAsync(
            new SharedProviderOpenAiErrorEnvelope(
                new SharedProviderOpenAiError(message, type, parameter, code)),
            SharedProviderProtocolJson.Options,
            httpContext.RequestAborted);
    }

    private static void ApplyPathHeaders(HttpContext httpContext)
    {
        if (IsInferencePath(httpContext.Request.Path))
        {
            ApplyInferenceHeaders(httpContext);
            return;
        }

        ApplyCommonHeaders(httpContext);
    }

    private static int MapStatusCode(SharedProviderFailureCategory category) => category switch
    {
        SharedProviderFailureCategory.Validation or
            SharedProviderFailureCategory.VersionUnsupported => StatusCodes.Status400BadRequest,
        SharedProviderFailureCategory.Unauthorized => StatusCodes.Status401Unauthorized,
        SharedProviderFailureCategory.InsufficientScope => StatusCodes.Status403Forbidden,
        SharedProviderFailureCategory.NotFound => StatusCodes.Status404NotFound,
        SharedProviderFailureCategory.Conflict => StatusCodes.Status409Conflict,
        SharedProviderFailureCategory.RateLimited => StatusCodes.Status429TooManyRequests,
        SharedProviderFailureCategory.UpstreamFailure => StatusCodes.Status502BadGateway,
        SharedProviderFailureCategory.Unavailable or
            SharedProviderFailureCategory.Cancelled => StatusCodes.Status503ServiceUnavailable,
        SharedProviderFailureCategory.Timeout => StatusCodes.Status504GatewayTimeout,
        _ => StatusCodes.Status502BadGateway
    };

    private static string MapErrorType(SharedProviderFailureCategory category) => category switch
    {
        SharedProviderFailureCategory.Validation or
            SharedProviderFailureCategory.NotFound or
            SharedProviderFailureCategory.VersionUnsupported =>
                SharedProviderOpenAiConstants.InvalidRequestErrorType,
        SharedProviderFailureCategory.Unauthorized =>
            SharedProviderOpenAiConstants.AuthenticationErrorType,
        SharedProviderFailureCategory.InsufficientScope =>
            SharedProviderOpenAiConstants.PermissionErrorType,
        SharedProviderFailureCategory.Conflict =>
            SharedProviderOpenAiConstants.ConflictErrorType,
        SharedProviderFailureCategory.RateLimited =>
            SharedProviderOpenAiConstants.RateLimitErrorType,
        SharedProviderFailureCategory.Timeout =>
            SharedProviderOpenAiConstants.TimeoutErrorType,
        _ => SharedProviderOpenAiConstants.ApiErrorType
    };

    private static bool IsSharedProviderPath(PathString path)
        => path.StartsWithSegments(
            SharedProviderPathPrefix,
            StringComparison.OrdinalIgnoreCase);

    private static bool IsOpenAiPath(PathString path)
        => path.StartsWithSegments(
            SharedProviderRoutes.OpenAiBase,
            StringComparison.OrdinalIgnoreCase);

    private static bool IsInferencePath(PathString path)
        => path.Equals(SharedProviderRoutes.Responses, StringComparison.OrdinalIgnoreCase) ||
            path.Equals(SharedProviderRoutes.ChatCompletions, StringComparison.OrdinalIgnoreCase) ||
            path.Equals(SharedProviderRoutes.ImageGenerations, StringComparison.OrdinalIgnoreCase);
}
