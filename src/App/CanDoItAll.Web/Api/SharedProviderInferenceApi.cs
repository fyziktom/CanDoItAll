using System.Buffers;
using System.Diagnostics;
using System.Net.Mime;
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
            CreateResponseAsync,
            "CreateSharedProviderOpenAiResponse",
            supportsStreaming: true);
        MapEndpoint(
            endpoints,
            SharedProviderRoutes.ChatCompletions,
            SharedProviderRelayOperation.ChatCompletions,
            CreateChatCompletionAsync,
            "CreateSharedProviderOpenAiChatCompletion",
            supportsStreaming: true);
        MapEndpoint(
            endpoints,
            SharedProviderRoutes.ImageGenerations,
            SharedProviderRelayOperation.ImageGenerations,
            CreateImageGenerationAsync,
            "CreateSharedProviderOpenAiImageGeneration",
            supportsStreaming: false);

        return endpoints;
    }

    private static void MapEndpoint(
        IEndpointRouteBuilder endpoints,
        string route,
        SharedProviderRelayOperation operation,
        Delegate handler,
        string endpointName,
        bool supportsStreaming)
    {
        var endpoint = endpoints.MapPost(
            route,
            handler);
        endpoint
            .WithName(endpointName)
            .WithTags("Shared Providers")
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

    /// <summary>
    /// Create a model response through a shared provider, using the OpenAI Responses request format.
    /// </summary>
    /// <remarks>
    /// Relays a stateless OpenAI Responses request to the shared model named by <c>model</c>, a routing identifier from
    /// <c>GET /api/shared-providers/v1/catalog</c> or <c>GET /api/shared-providers/openai/v1/models</c>; it never falls
    /// back to another model or provider. The request must fit the strict subset in the request schema: unknown or
    /// duplicate members, remote URLs, files, hosted tools, stored responses, background mode and
    /// <c>previous_response_id</c> are rejected. <c>store</c> is treated as false when omitted and may only be false;
    /// <c>background</c> may only be false. Every feature the request uses (streaming, function tools, structured
    /// output, image input, a reasoning effort) must be supported by the model.
    ///
    /// Without <c>stream</c>, or with false, the body is the provider's completed response object as JSON, with
    /// <c>model</c> replaced by the routing identifier; a response that is not completed or carries an error is
    /// reported as HTTP 502 instead. With <c>stream</c> true, the body is <c>text/event-stream</c>: the provider's
    /// server-sent events, relayed with <c>model</c> replaced. Once the stream has started the status stays 200 even if
    /// the provider fails: the host then aborts the connection without a terminal event. Treat a stream that ends
    /// without its terminal event as failed and never use its partial output as a complete answer.
    ///
    /// Each call is a new, separately recorded invocation (caller, publication, model, usage and the prices in effect)
    /// and can be charged; there is no idempotency key. After HTTP 502, 503 or 504, and after an aborted stream, the
    /// provider may still have processed the request. Responses carry
    /// <c>Cache-Control: private, no-store, no-cache</c>, <c>X-Content-Type-Options: nosniff</c> and
    /// <c>CanDoItAll-Request-Id</c>, which identifies the invocation for support. Failures use the OpenAI error
    /// envelope; branch on <c>error.code</c>.
    ///
    /// Authority: when API authorization is enabled, a bearer token with the <c>api</c> or
    /// <c>api.shared-providers.invoke</c> scope. The token's subject is recorded as the caller.
    /// </remarks>
    /// <response code="200">
    /// The provider's result: the completed response object as JSON, or with <c>stream</c> true the relayed
    /// server-sent events.
    /// </response>
    /// <response code="400">
    /// Rejected before dispatch: the content type is not JSON (<c>shared_provider_content_type_invalid</c>), the body
    /// is empty, unreadable or too large (<c>shared_provider_request_invalid</c>,
    /// <c>shared_provider_request_too_large</c>), a member is unsupported or out of range, or the model lacks a feature
    /// the request uses (<c>shared_provider_request_invalid</c> with <c>param</c> naming the member,
    /// <c>shared_provider_capability_not_supported</c>, <c>shared_provider_thinking_effort_not_supported</c>), or an
    /// access-context header is invalid (<c>shared_provider_access_context_invalid</c>).
    /// </response>
    /// <response code="401">
    /// No valid bearer token (<c>shared_provider_unauthorized</c>), or the token's subject cannot identify the caller
    /// (<c>shared_provider_subject_invalid</c>).
    /// </response>
    /// <response code="403">The token lacks the invoke scope (<c>shared_provider_insufficient_scope</c>).</response>
    /// <response code="404">
    /// <c>model</c> is not a routing identifier of a currently shared model (<c>shared_provider_model_not_found</c>).
    /// </response>
    /// <response code="409">
    /// The model does not support the Responses operation (<c>shared_provider_operation_mismatch</c>) or has no
    /// available relay adapter (<c>shared_provider_adapter_not_available</c>); check its capabilities in the catalog.
    /// </response>
    /// <response code="429">
    /// The upstream provider rate limited the request (<c>shared_provider_upstream_rate_limited</c>). When
    /// <c>Retry-After</c> is present, wait that many seconds before sending a new request.
    /// </response>
    /// <response code="502">
    /// The upstream provider failed or returned an invalid, incomplete or failed response, for example
    /// <c>shared_provider_upstream_failure</c>, <c>shared_provider_upstream_failed</c> or
    /// <c>shared_provider_upstream_response_invalid</c>. The provider may have processed the request.
    /// </response>
    /// <response code="503">
    /// The relay, the provider target, the invocation record or the upstream provider is unavailable, for example
    /// <c>shared_provider_relay_unavailable</c>, <c>shared_provider_target_unavailable</c>,
    /// <c>shared_provider_audit_unavailable</c> or <c>shared_provider_upstream_unavailable</c>, or the publisher's
    /// default reasoning effort is invalid (<c>shared_provider_thinking_default_invalid</c>).
    /// </response>
    /// <response code="504">
    /// The relay or the upstream provider timed out (<c>shared_provider_relay_timeout</c>,
    /// <c>shared_provider_upstream_timeout</c>). The provider may have processed the request.
    /// </response>
    internal static Task CreateResponseAsync(
        HttpContext httpContext,
        ISharedProviderRelayApplicationService relayService,
        IAccessContextReferenceAccessor accessContextAccessor,
        ILogger<LogCategory> logger) => InvokeAsync(
            httpContext,
            relayService,
            accessContextAccessor,
            SharedProviderRelayOperation.Responses,
            logger);

    /// <summary>
    /// Create a chat completion through a shared provider, using the OpenAI Chat Completions request format.
    /// </summary>
    /// <remarks>
    /// Relays an OpenAI Chat Completions request to the shared model named by <c>model</c>, a routing identifier from
    /// <c>GET /api/shared-providers/v1/catalog</c> or <c>GET /api/shared-providers/openai/v1/models</c>; it never falls
    /// back to another model or provider. The request must fit the strict subset in the request schema: unknown or
    /// duplicate members, remote image URLs and hosted tools are rejected. Every assistant tool call must be answered
    /// by a <c>tool</c> message with its <c>tool_call_id</c> before the next other message; <c>stream_options</c> is
    /// accepted only with <c>stream</c> true; at most one of <c>max_tokens</c> and <c>max_completion_tokens</c>. Every
    /// feature the request uses (streaming, function tools, structured output, image input, a reasoning effort) must be
    /// supported by the model.
    ///
    /// Without <c>stream</c>, or with false, the body is the provider's chat completion object as JSON, with
    /// <c>model</c> replaced by the routing identifier; a response that carries an error is reported as HTTP 502
    /// instead. With <c>stream</c> true, the body is <c>text/event-stream</c>: the provider's server-sent events,
    /// relayed with <c>model</c> replaced, ending with <c>data: [DONE]</c> when the provider sends it. Once the stream
    /// has started the status stays 200 even if the provider fails: the host then aborts the connection without a
    /// terminal event. Treat a stream that ends without its terminal event as failed and never use its partial output
    /// as a complete answer.
    ///
    /// Each call is a new, separately recorded invocation (caller, publication, model, usage and the prices in effect)
    /// and can be charged; there is no idempotency key. After HTTP 502, 503 or 504, and after an aborted stream, the
    /// provider may still have processed the request. Responses carry
    /// <c>Cache-Control: private, no-store, no-cache</c>, <c>X-Content-Type-Options: nosniff</c> and
    /// <c>CanDoItAll-Request-Id</c>, which identifies the invocation for support. Failures use the OpenAI error
    /// envelope; branch on <c>error.code</c>.
    ///
    /// Authority: when API authorization is enabled, a bearer token with the <c>api</c> or
    /// <c>api.shared-providers.invoke</c> scope. The token's subject is recorded as the caller.
    /// </remarks>
    /// <response code="200">
    /// The provider's result: the chat completion object as JSON, or with <c>stream</c> true the relayed server-sent
    /// events.
    /// </response>
    /// <response code="400">
    /// Rejected before dispatch: the content type is not JSON (<c>shared_provider_content_type_invalid</c>), the body
    /// is empty, unreadable or too large (<c>shared_provider_request_invalid</c>,
    /// <c>shared_provider_request_too_large</c>), a member is unsupported or out of range, the tool-call sequence is
    /// broken, or the model lacks a feature the request uses (<c>shared_provider_request_invalid</c> with
    /// <c>param</c> naming the member, <c>shared_provider_capability_not_supported</c>,
    /// <c>shared_provider_thinking_effort_not_supported</c>), or an access-context header is invalid
    /// (<c>shared_provider_access_context_invalid</c>).
    /// </response>
    /// <response code="401">
    /// No valid bearer token (<c>shared_provider_unauthorized</c>), or the token's subject cannot identify the caller
    /// (<c>shared_provider_subject_invalid</c>).
    /// </response>
    /// <response code="403">The token lacks the invoke scope (<c>shared_provider_insufficient_scope</c>).</response>
    /// <response code="404">
    /// <c>model</c> is not a routing identifier of a currently shared model (<c>shared_provider_model_not_found</c>).
    /// </response>
    /// <response code="409">
    /// The model does not support the chat completions operation (<c>shared_provider_operation_mismatch</c>) or has
    /// no available relay adapter (<c>shared_provider_adapter_not_available</c>); check its capabilities in the
    /// catalog.
    /// </response>
    /// <response code="429">
    /// The upstream provider rate limited the request (<c>shared_provider_upstream_rate_limited</c>). When
    /// <c>Retry-After</c> is present, wait that many seconds before sending a new request.
    /// </response>
    /// <response code="502">
    /// The upstream provider failed or returned an invalid or failed response, for example
    /// <c>shared_provider_upstream_failure</c>, <c>shared_provider_upstream_failed</c> or
    /// <c>shared_provider_upstream_response_invalid</c>. The provider may have processed the request.
    /// </response>
    /// <response code="503">
    /// The relay, the provider target, the invocation record or the upstream provider is unavailable, for example
    /// <c>shared_provider_relay_unavailable</c>, <c>shared_provider_target_unavailable</c>,
    /// <c>shared_provider_audit_unavailable</c> or <c>shared_provider_upstream_unavailable</c>, or the publisher's
    /// default reasoning effort is invalid (<c>shared_provider_thinking_default_invalid</c>).
    /// </response>
    /// <response code="504">
    /// The relay or the upstream provider timed out (<c>shared_provider_relay_timeout</c>,
    /// <c>shared_provider_upstream_timeout</c>). The provider may have processed the request.
    /// </response>
    internal static Task CreateChatCompletionAsync(
        HttpContext httpContext,
        ISharedProviderRelayApplicationService relayService,
        IAccessContextReferenceAccessor accessContextAccessor,
        ILogger<LogCategory> logger) => InvokeAsync(
            httpContext,
            relayService,
            accessContextAccessor,
            SharedProviderRelayOperation.ChatCompletions,
            logger);

    /// <summary>
    /// Generate images through a shared image-generation provider, using the OpenAI Images request format.
    /// </summary>
    /// <remarks>
    /// Relays an OpenAI image generation request to the shared model named by <c>model</c>, a routing identifier of an
    /// <c>image-generation</c> publication from <c>GET /api/shared-providers/v1/catalog</c>; it never falls back to
    /// another model or provider. The request must fit the strict subset in the request schema; unknown or duplicate
    /// members are rejected. Images are returned only as base64 data (<c>response_format</c> may only be
    /// <c>b64_json</c>), never as URLs, and this operation cannot stream.
    ///
    /// The JSON body has a <c>data</c> array with one object per image, holding <c>b64_json</c> and, when the provider
    /// returns it, <c>revised_prompt</c>, plus <c>created</c> when the provider reports a creation time. It holds 1 to
    /// 16 images, each at most 8 MiB decoded and 32 MiB in total; a larger or malformed result is reported as HTTP 502.
    ///
    /// Each call is a new, separately recorded invocation (caller, publication, model and the prices in effect) and can
    /// be charged; there is no idempotency key. After HTTP 502, 503 or 504 the provider may still have generated the
    /// images. Responses carry <c>Cache-Control: private, no-store, no-cache</c>, <c>X-Content-Type-Options:
    /// nosniff</c> and <c>CanDoItAll-Request-Id</c>, which identifies the invocation for support. Failures use the
    /// OpenAI error envelope; branch on <c>error.code</c>.
    ///
    /// Authority: when API authorization is enabled, a bearer token with the <c>api</c> or
    /// <c>api.shared-providers.invoke</c> scope. The token's subject is recorded as the caller.
    /// </remarks>
    /// <response code="200">The generated images as base64 data.</response>
    /// <response code="400">
    /// Rejected before dispatch: the content type is not JSON (<c>shared_provider_content_type_invalid</c>), the body
    /// is empty, unreadable or too large (<c>shared_provider_request_invalid</c>,
    /// <c>shared_provider_request_too_large</c>), a member is unsupported or out of range, for example <c>n</c> above
    /// the provider's limit (<c>shared_provider_request_invalid</c> with <c>param</c> naming the member,
    /// <c>shared_provider_capability_not_supported</c>), or an access-context header is invalid
    /// (<c>shared_provider_access_context_invalid</c>).
    /// </response>
    /// <response code="401">
    /// No valid bearer token (<c>shared_provider_unauthorized</c>), or the token's subject cannot identify the caller
    /// (<c>shared_provider_subject_invalid</c>).
    /// </response>
    /// <response code="403">The token lacks the invoke scope (<c>shared_provider_insufficient_scope</c>).</response>
    /// <response code="404">
    /// <c>model</c> is not a routing identifier of a currently shared model (<c>shared_provider_model_not_found</c>).
    /// </response>
    /// <response code="409">
    /// The model does not support image generation (<c>shared_provider_operation_mismatch</c>) or has no available
    /// relay adapter (<c>shared_provider_adapter_not_available</c>); check its capabilities in the catalog.
    /// </response>
    /// <response code="429">
    /// The upstream provider rate limited the request (<c>shared_provider_upstream_rate_limited</c>). When
    /// <c>Retry-After</c> is present, wait that many seconds before sending a new request.
    /// </response>
    /// <response code="502">
    /// The upstream provider failed or returned invalid image data, for example <c>shared_provider_upstream_failed</c>,
    /// <c>shared_provider_upstream_response_invalid</c>, <c>shared_provider_image_upstream_failure</c> or
    /// <c>shared_provider_image_result_invalid</c>. The provider may have generated the images.
    /// </response>
    /// <response code="503">
    /// The relay, the provider target, the invocation record or the upstream provider is unavailable, for example
    /// <c>shared_provider_relay_unavailable</c>, <c>shared_provider_target_unavailable</c>,
    /// <c>shared_provider_audit_unavailable</c> or <c>shared_provider_upstream_unavailable</c>.
    /// </response>
    /// <response code="504">
    /// The relay or the upstream provider timed out (<c>shared_provider_relay_timeout</c>,
    /// <c>shared_provider_upstream_timeout</c>). The provider may have generated the images.
    /// </response>
    internal static Task CreateImageGenerationAsync(
        HttpContext httpContext,
        ISharedProviderRelayApplicationService relayService,
        IAccessContextReferenceAccessor accessContextAccessor,
        ILogger<LogCategory> logger) => InvokeAsync(
            httpContext,
            relayService,
            accessContextAccessor,
            SharedProviderRelayOperation.ImageGenerations,
            logger);
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
                logger.LogWarning(
                    "Shared-provider relay rejected operation {Operation} for trace {TraceId}. Category={FailureCategory}, Code={FailureCode}, Parameter={FailureParameter}, Reason={FailureReason}.",
                    operation,
                    httpContext.TraceIdentifier,
                    failed.Failure.Category,
                    failed.Failure.Code.Value,
                    failed.Failure.Parameter,
                    failed.Failure.SanitizedMessage);
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
                httpContext.Abort();
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
            httpContext.Abort();
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
        string? subject = SharedProviderCallerSnapshot.Subject(httpContext.User);
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
            requestId)
        {
            CallerIdentity = SharedProviderCallerSnapshot.From(httpContext),
            AccessContextReferenceType = accessContextAccessor.CurrentType
        };
        return true;
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

    internal sealed class LogCategory
    {
    }
}
