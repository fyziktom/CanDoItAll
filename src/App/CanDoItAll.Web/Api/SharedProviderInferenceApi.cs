using System.Buffers;
using System.Diagnostics;
using System.Net.Mime;
using System.Security.Claims;
using System.Text.Json;
using CanDoItAll.SharedProviders.Abstractions;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Net.Http.Headers;

namespace CanDoItAll.Web.Api;

internal static class SharedProviderInferenceApi
{
    internal const int MaximumRequestBodyBytes =
        SharedProviderRelaySupportDescriptor.MaximumAllowedRequestBytes;

    private const int RequestBufferSize = 64 * 1024;
    private const string AuthorizationDisabledSubject = "api-authorization-disabled";

    private static readonly SharedProviderFailure InvalidContentTypeFailure = CreateFailure(
        SharedProviderFailureCategory.Validation,
        "shared_provider_content_type_invalid",
        "Content-Type must be application/json with UTF-8 encoding.",
        HeaderNames.ContentType);

    private static readonly SharedProviderFailure InvalidRequestBodyFailure = CreateFailure(
        SharedProviderFailureCategory.Validation,
        "shared_provider_request_invalid",
        "The request body must contain a bounded JSON object.");

    private static readonly SharedProviderFailure RequestTooLargeFailure = CreateFailure(
        SharedProviderFailureCategory.Validation,
        "shared_provider_request_too_large",
        "The request body exceeds the allowed size.");

    private static readonly SharedProviderFailure InvalidSubjectFailure = CreateFailure(
        SharedProviderFailureCategory.Unauthorized,
        "shared_provider_subject_invalid",
        "The authenticated caller identity is invalid.");

    private static readonly SharedProviderFailure RelayUnavailableFailure = CreateFailure(
        SharedProviderFailureCategory.Unavailable,
        "shared_provider_relay_unavailable",
        "The shared-provider relay is temporarily unavailable.");

    public static IEndpointRouteBuilder MapSharedProviderInferenceApi(
        this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        MapEndpoint(
            endpoints,
            SharedProviderRoutes.Responses,
            SharedProviderRelayOperation.Responses,
            "CreateSharedProviderOpenAiResponse",
            "Create a shared-provider response",
            supportsStreaming: true);
        MapEndpoint(
            endpoints,
            SharedProviderRoutes.ChatCompletions,
            SharedProviderRelayOperation.ChatCompletions,
            "CreateSharedProviderOpenAiChatCompletion",
            "Create a shared-provider chat completion",
            supportsStreaming: true);
        MapEndpoint(
            endpoints,
            SharedProviderRoutes.ImageGenerations,
            SharedProviderRelayOperation.ImageGenerations,
            "CreateSharedProviderOpenAiImageGeneration",
            "Create shared-provider images",
            supportsStreaming: false);

        return endpoints;
    }

    private static void MapEndpoint(
        IEndpointRouteBuilder endpoints,
        string route,
        SharedProviderRelayOperation operation,
        string endpointName,
        string summary,
        bool supportsStreaming)
    {
        var endpoint = endpoints.MapPost(
            route,
            (
                HttpContext httpContext,
                ISharedProviderRelayApplicationService relayService,
                IAccessContextReferenceAccessor accessContextAccessor,
                ILogger<LogCategory> logger) => InvokeAsync(
                    httpContext,
                    relayService,
                    accessContextAccessor,
                    operation,
                    logger));
        endpoint
            .WithName(endpointName)
            .WithTags("Shared Providers")
            .WithSummary(summary)
            .WithDescription(
                "Accepts a bounded OpenAI-compatible JSON request and relays only to its exact published routing model.")
            .WithMetadata(SharedProviderInferenceOpenApiContract.For(operation))
            .Accepts<JsonElement>(MediaTypeNames.Application.Json)
            .Produces(
                StatusCodes.Status200OK,
                typeof(JsonElement),
                MediaTypeNames.Application.Json)
            .Produces<SharedProviderOpenAiErrorEnvelope>(StatusCodes.Status400BadRequest)
            .Produces<SharedProviderOpenAiErrorEnvelope>(StatusCodes.Status401Unauthorized)
            .Produces<SharedProviderOpenAiErrorEnvelope>(StatusCodes.Status403Forbidden)
            .Produces<SharedProviderOpenAiErrorEnvelope>(StatusCodes.Status404NotFound)
            .Produces<SharedProviderOpenAiErrorEnvelope>(StatusCodes.Status409Conflict)
            .Produces<SharedProviderOpenAiErrorEnvelope>(StatusCodes.Status429TooManyRequests)
            .Produces<SharedProviderOpenAiErrorEnvelope>(StatusCodes.Status502BadGateway)
            .Produces<SharedProviderOpenAiErrorEnvelope>(StatusCodes.Status503ServiceUnavailable)
            .Produces<SharedProviderOpenAiErrorEnvelope>(StatusCodes.Status504GatewayTimeout);
        if (supportsStreaming)
        {
            endpoint.Produces(
                StatusCodes.Status200OK,
                typeof(string),
                "text/event-stream");
        }

        endpoint.DisableAntiforgery();
        endpoint.ApplyApiAuthorization(
            endpoints,
            ApiAuthorizationPolicies.InvokeSharedProviders);
    }

    private static async Task InvokeAsync(
        HttpContext httpContext,
        ISharedProviderRelayApplicationService relayService,
        IAccessContextReferenceAccessor accessContextAccessor,
        SharedProviderRelayOperation operation,
        ILogger logger)
    {
        if (!HasStrictJsonContentType(httpContext.Request))
        {
            await SharedProviderApiResponseWriter.WriteRelayFailureAsync(
                httpContext,
                InvalidContentTypeFailure);
            return;
        }

        var bodyResult = await ReadRequestBodyAsync(httpContext, logger);
        if (bodyResult.Failure is { } bodyFailure)
        {
            await SharedProviderApiResponseWriter.WriteRelayFailureAsync(
                httpContext,
                bodyFailure);
            return;
        }

        if (!TryCreateRequestContext(
                httpContext,
                accessContextAccessor,
                out var requestContext))
        {
            await SharedProviderApiResponseWriter.WriteRelayFailureAsync(
                httpContext,
                InvalidSubjectFailure);
            return;
        }

        SharedProviderRelayDispatchResult result;
        try
        {
            result = await relayService.InvokeAsync(
                new SharedProviderRelayApplicationRequest(
                    operation,
                    bodyResult.Payload!,
                    requestContext),
                httpContext.RequestAborted);
        }
        catch (OperationCanceledException) when (httpContext.RequestAborted.IsCancellationRequested)
        {
            return;
        }
        catch (OperationCanceledException)
        {
            await SharedProviderApiResponseWriter.WriteRelayFailureAsync(
                httpContext,
                CreateFailure(
                    SharedProviderFailureCategory.Timeout,
                    "shared_provider_relay_timeout",
                    "The shared-provider relay timed out."));
            return;
        }
        catch (Exception exception)
        {
            logger.LogError(
                "Shared-provider relay failed before response start for operation {Operation}, request {RequestId}, and failure type {FailureType}.",
                operation,
                requestContext.RequestId,
                exception.GetType().Name);
            await SharedProviderApiResponseWriter.WriteRelayFailureAsync(
                httpContext,
                RelayUnavailableFailure);
            return;
        }

        await WriteDispatchResultAsync(httpContext, operation, result, logger);
    }

    private static async Task WriteDispatchResultAsync(
        HttpContext httpContext,
        SharedProviderRelayOperation operation,
        SharedProviderRelayDispatchResult result,
        ILogger logger)
    {
        switch (result)
        {
            case SharedProviderRelayDispatchResult.Buffered buffered:
                await WriteBufferedAsync(httpContext, buffered);
                return;
            case SharedProviderRelayDispatchResult.Streaming streaming:
                await WriteStreamingAsync(httpContext, operation, streaming.Stream, logger);
                return;
            case SharedProviderRelayDispatchResult.Failed failed:
                await SharedProviderApiResponseWriter.WriteRelayFailureAsync(
                    httpContext,
                    failed.Failure);
                return;
            default:
                logger.LogError(
                    "Shared-provider relay returned an unsupported result for operation {Operation} and request {RequestId}.",
                    operation,
                    httpContext.TraceIdentifier);
                await SharedProviderApiResponseWriter.WriteRelayFailureAsync(
                    httpContext,
                    RelayUnavailableFailure);
                return;
        }
    }

    private static async Task WriteBufferedAsync(
        HttpContext httpContext,
        SharedProviderRelayDispatchResult.Buffered buffered)
    {
        SharedProviderApiResponseWriter.ApplyInferenceHeaders(httpContext);
        httpContext.Response.StatusCode = StatusCodes.Status200OK;
        httpContext.Response.ContentType = buffered.ContentType;
        httpContext.Response.ContentLength = buffered.PayloadUtf8.Length;
        await httpContext.Response.Body.WriteAsync(
            buffered.PayloadUtf8,
            httpContext.RequestAborted);
    }

    private static async Task WriteStreamingAsync(
        HttpContext httpContext,
        SharedProviderRelayOperation operation,
        ISharedProviderRelayStream stream,
        ILogger logger)
    {
        SharedProviderOpenAiServerSentEventWriter.Prepare(httpContext);
        try
        {
            await httpContext.Response.StartAsync(httpContext.RequestAborted);
            await httpContext.Response.Body.FlushAsync(httpContext.RequestAborted);
            await foreach (var frame in stream
                               .ReadFramesAsync(httpContext.RequestAborted)
                               .WithCancellation(httpContext.RequestAborted))
            {
                await SharedProviderOpenAiServerSentEventWriter.WriteFrameAsync(
                    httpContext.Response,
                    frame,
                    httpContext.RequestAborted);
            }

            var completion = await stream.Completion;
            if (completion.Failure is { } completionFailure)
            {
                logger.LogWarning(
                    "Shared-provider stream completed with failure {FailureCode} for operation {Operation} and request {RequestId} after headers started.",
                    completionFailure.Code.Value,
                    operation,
                    httpContext.TraceIdentifier);
            }
        }
        catch (OperationCanceledException) when (httpContext.RequestAborted.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                "Shared-provider stream ended after headers started for operation {Operation}, request {RequestId}, and failure type {FailureType}.",
                operation,
                httpContext.TraceIdentifier,
                exception.GetType().Name);
        }
        finally
        {
            try
            {
                await stream.DisposeAsync();
            }
            catch (OperationCanceledException) when (httpContext.RequestAborted.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    "Shared-provider stream disposal failed for operation {Operation}, request {RequestId}, and failure type {FailureType}.",
                    operation,
                    httpContext.TraceIdentifier,
                    exception.GetType().Name);
            }
        }
    }

    private static async Task<RequestBodyReadResult> ReadRequestBodyAsync(
        HttpContext httpContext,
        ILogger logger)
    {
        var request = httpContext.Request;
        if (request.ContentLength is 0)
        {
            return RequestBodyReadResult.Failed(InvalidRequestBodyFailure);
        }

        if (request.ContentLength > MaximumRequestBodyBytes)
        {
            return RequestBodyReadResult.Failed(RequestTooLargeFailure);
        }

        var maximumBodySize = httpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (maximumBodySize is { IsReadOnly: false })
        {
            maximumBodySize.MaxRequestBodySize = MaximumRequestBodyBytes + 1L;
        }

        int initialCapacity = request.ContentLength is > 0
            ? checked((int)request.ContentLength.Value)
            : RequestBufferSize;
        using var payload = new MemoryStream(initialCapacity);
        byte[] buffer = ArrayPool<byte>.Shared.Rent(RequestBufferSize);
        try
        {
            while (true)
            {
                int read = await request.Body.ReadAsync(
                    buffer.AsMemory(0, RequestBufferSize),
                    httpContext.RequestAborted);
                if (read == 0)
                {
                    break;
                }

                if (payload.Length + read > MaximumRequestBodyBytes)
                {
                    return RequestBodyReadResult.Failed(RequestTooLargeFailure);
                }

                await payload.WriteAsync(
                    buffer.AsMemory(0, read),
                    httpContext.RequestAborted);
            }
        }
        catch (OperationCanceledException) when (httpContext.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is BadHttpRequestException or IOException)
        {
            logger.LogWarning(
                "Shared-provider request body could not be read for request {RequestId} and failure type {FailureType}.",
                httpContext.TraceIdentifier,
                exception.GetType().Name);
            return RequestBodyReadResult.Failed(InvalidRequestBodyFailure);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        return payload.Length == 0
            ? RequestBodyReadResult.Failed(InvalidRequestBodyFailure)
            : RequestBodyReadResult.Succeeded(payload.ToArray());
    }

    private static bool HasStrictJsonContentType(HttpRequest request)
    {
        if (!MediaTypeHeaderValue.TryParse(request.ContentType, out var contentType) ||
            !string.Equals(
                contentType.MediaType.Value,
                MediaTypeNames.Application.Json,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var charsetSeen = false;
        foreach (var parameter in contentType.Parameters)
        {
            if (!string.Equals(parameter.Name.Value, "charset", StringComparison.OrdinalIgnoreCase) ||
                charsetSeen ||
                !string.Equals(
                    parameter.Value.Value?.Trim('"'),
                    "utf-8",
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            charsetSeen = true;
        }

        return true;
    }

    private static bool TryCreateRequestContext(
        HttpContext httpContext,
        IAccessContextReferenceAccessor accessContextAccessor,
        out SharedProviderRelayRequestContext requestContext)
    {
        string? subject = ResolveSubject(httpContext.User);
        if (!IsBoundedExactText(subject, 256))
        {
            requestContext = null!;
            return false;
        }

        string requestId = IsBoundedExactText(httpContext.TraceIdentifier, 128)
            ? httpContext.TraceIdentifier
            : Guid.NewGuid().ToString("N");
        httpContext.TraceIdentifier = requestId;
        string traceId = Activity.Current?.TraceId.ToString() is { } activityTraceId &&
            IsBoundedExactText(activityTraceId, 128)
                ? activityTraceId
                : requestId;
        requestContext = new SharedProviderRelayRequestContext(
            requestId,
            subject!,
            accessContextAccessor.Current,
            traceId,
            requestId);
        return true;
    }

    private static string? ResolveSubject(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true)
        {
            return AuthorizationDisabledSubject;
        }

        return principal.FindFirstValue("sub") ??
            principal.FindFirstValue(ClaimTypes.NameIdentifier) ??
            principal.Identity.Name;
    }

    private static bool IsBoundedExactText(string? value, int maximumLength)
        => value is { Length: > 0 } &&
            value.Length <= maximumLength &&
            value == value.Trim() &&
            !value.Any(char.IsControl);

    private static SharedProviderFailure CreateFailure(
        SharedProviderFailureCategory category,
        string code,
        string message,
        string? parameter = null)
        => new(
            category,
            new SharedProviderFailureCode(code),
            message,
            parameter);

    private sealed record RequestBodyReadResult(
        byte[]? Payload,
        SharedProviderFailure? Failure)
    {
        public static RequestBodyReadResult Succeeded(byte[] payload) => new(payload, null);

        public static RequestBodyReadResult Failed(SharedProviderFailure failure) => new(null, failure);
    }

    private sealed class LogCategory
    {
    }
}
