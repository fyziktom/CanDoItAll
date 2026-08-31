using System.Buffers;
using System.Net.Http.Headers;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Providers;
using CanDoItAll.SharedProviders.Abstractions;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.SharedProviders.Http;

internal sealed class SharedProviderHttpRelayClient(
    IProviderInferenceRelayRuntime inferenceRelayRuntime,
    ILogger<SharedProviderHttpRelayClient> logger)
{
    internal const string ClientName = "SharedProviderRelay";
    internal const int MaximumBufferedResponseBytes = 64 * 1024 * 1024;

    public async ValueTask<SharedProviderRelayDispatchResult> DispatchAsync(
        SharedProviderRelayDispatchRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var thinking = SharedProviderRelayThinkingPolicy.Apply(
            request.Request.Operation,
            request.Request.CreateUpstreamPayload(request.Target.UpstreamModelId),
            request.Target.Thinking);
        if (thinking.Failure is not null) {
            return new SharedProviderRelayDispatchResult.Failed(thinking.Failure);
        }
        var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(request.Target.Timeout);
        ProviderInferenceRelayTransportResponse? transportResponse = null;
        try
        {
            transportResponse = await inferenceRelayRuntime.SendAsync(
                CreateRuntimeRequest(request, thinking.Payload),
                timeoutSource.Token).ConfigureAwait(false);
            var response = transportResponse.Response;
            var headers = SharedProviderRelayResponseHeaderPolicy.Project(response);
            logger.LogInformation(
                "Shared-provider request {RequestId}: model {Model}, thinking {ThinkingEffort}, agent override {IsOverride}, upstream HTTP {StatusCode}, upstream request {UpstreamRequestId}.",
                request.Context?.RequestId,
                request.Target.UpstreamModelId,
                thinking.Effort.HasValue ? SharedProviderThinkingCapability.FormatEffort(thinking.Effort.Value) : "omitted",
                thinking.IsOverride,
                (int)response.StatusCode,
                headers.UpstreamRequestId);
            if (!response.IsSuccessStatusCode)
            {
                var failure = SharedProviderRelayFailureMapper.FromUpstream(
                    response.StatusCode,
                    response.Headers.TryGetValues("retry-after", out var retryValues)
                        ? retryValues.Take(2).ToArray() is [var retryAfter]
                            ? retryAfter
                            : null
                        : null,
                    rawResponseBody: null);
                var errorBody = await ReadDiagnosticPrefixAsync(response.Content, timeoutSource.Token, cancellationToken)
                    .ConfigureAwait(false);
                var diagnostic = SharedProviderRelayFailureMapper.DescribeUpstreamFailure(errorBody);
                logger.LogWarning(
                    "Shared-provider upstream rejected {Operation} for connector {ConnectorPluginKey} with HTTP {StatusCode}: {Code}, parameter {Parameter}.",
                    request.Request.Operation,
                    request.Target.ConnectorPluginKey,
                    (int)response.StatusCode,
                    diagnostic.Code,
                    diagnostic.Parameter);
                return new SharedProviderRelayDispatchResult.Failed(failure);
            }

            if (request.Request.Stream)
            {
                if (!IsContentType(response.Content.Headers.ContentType, "text/event-stream"))
                {
                    return InvalidUpstreamResponse();
                }

                var responseStream = await response.Content
                    .ReadAsStreamAsync(timeoutSource.Token)
                    .ConfigureAwait(false);
                var relayStream = new SharedProviderSseRelayStream(
                    transportResponse,
                    responseStream,
                    timeoutSource,
                    cancellationToken,
                    request.Request.Operation,
                    request.Request.RoutingModelId,
                    SharedProviderRelayTimeouts.StreamingIdle,
                    headers);
                transportResponse = null;
                timeoutSource = null!;
                return new SharedProviderRelayDispatchResult.Streaming(relayStream);
            }

            if (!IsContentType(response.Content.Headers.ContentType, "application/json"))
            {
                return InvalidUpstreamResponse();
            }

            var payload = await ReadBoundedAsync(
                response.Content,
                MaximumBufferedResponseBytes,
                timeoutSource.Token).ConfigureAwait(false);
            var rewritten = SharedProviderRelayResponsePolicy.RewriteBuffered(
                payload,
                request.Request.RoutingModelId,
                request.Request.Operation);
            var usage = SharedProviderRelayUsageExtractor.ExtractBuffered(
                request.Request.Operation,
                rewritten);
            return new SharedProviderRelayDispatchResult.Buffered(
                rewritten,
                "application/json",
                headers,
                usage);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Failed(
                SharedProviderFailureCategory.Cancelled,
                "shared_provider_request_cancelled",
                "The shared-provider request was cancelled.");
        }
        catch (OperationCanceledException)
        {
            return Failed(
                SharedProviderFailureCategory.Timeout,
                "shared_provider_upstream_timeout",
                "The upstream provider did not respond before the timeout.");
        }
        catch (InvalidDataException)
        {
            logger.LogWarning(
                "Shared-provider upstream returned an invalid response for {Operation} through connector {ConnectorPluginKey}.",
                request.Request.Operation,
                request.Target.ConnectorPluginKey);
            return InvalidUpstreamResponse();
        }
        catch (HttpRequestException)
        {
            logger.LogWarning(
                "Shared-provider upstream transport failed for {Operation} through connector {ConnectorPluginKey}.",
                request.Request.Operation,
                request.Target.ConnectorPluginKey);
            return Failed(
                SharedProviderFailureCategory.Unavailable,
                "shared_provider_upstream_unavailable",
                "The upstream provider is unavailable.");
        }
        finally
        {
            transportResponse?.Dispose();
            timeoutSource?.Dispose();
        }
    }

    private async ValueTask<ReadOnlyMemory<byte>> ReadDiagnosticPrefixAsync(
        HttpContent content, CancellationToken timeoutToken, CancellationToken callerToken) {
        var buffer = new byte[SharedProviderFailure.MaximumMessageLength];
        try {
            await using var source = await content.ReadAsStreamAsync(timeoutToken).ConfigureAwait(false);
            var length = await source.ReadAtLeastAsync(buffer, buffer.Length, throwOnEndOfStream: false, timeoutToken)
                .ConfigureAwait(false);
            return buffer.AsMemory(0, length);
        } catch (Exception exception) when (
            exception is IOException or HttpRequestException ||
            exception is OperationCanceledException && !callerToken.IsCancellationRequested) {
            logger.LogWarning("Shared-provider error diagnostics could not be read; preserving the upstream HTTP failure. Failure type {FailureType}.",
                exception.GetType().Name);
            return ReadOnlyMemory<byte>.Empty;
        }
    }

    internal static async ValueTask<byte[]> ReadBoundedAsync(
        HttpContent content,
        int maximumBytes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (maximumBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumBytes));
        }

        if (content.Headers.ContentLength is > 0 && content.Headers.ContentLength > maximumBytes)
        {
            throw new InvalidDataException("The upstream response exceeds the relay byte limit.");
        }

        await using var source = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var destination = new MemoryStream(
            content.Headers.ContentLength is > 0
                ? checked((int)content.Headers.ContentLength.Value)
                : 0);
        var buffer = ArrayPool<byte>.Shared.Rent(16 * 1024);
        try
        {
            while (true)
            {
                var read = await source
                    .ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)
                    .ConfigureAwait(false);
                if (read == 0)
                {
                    return destination.ToArray();
                }

                if (destination.Length + read > maximumBytes)
                {
                    throw new InvalidDataException("The upstream response exceeds the relay byte limit.");
                }

                destination.Write(buffer, 0, read);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static ProviderInferenceRelayRequest CreateRuntimeRequest(
        SharedProviderRelayDispatchRequest dispatch,
        ReadOnlyMemory<byte> payload)
    {
        var kind = dispatch.Target.ConnectorPluginKey switch
        {
            SharedProviderConnectorPluginKeys.OpenAi => ProviderKind.OpenAi,
            SharedProviderConnectorPluginKeys.OllamaLocal or
                SharedProviderConnectorPluginKeys.OllamaRemote => ProviderKind.Ollama,
            _ => throw new InvalidOperationException(
                "The relay target connector has no MAF provider driver mapping.")
        };
        var operation = dispatch.Request.Operation switch
        {
            SharedProviderRelayOperation.ChatCompletions =>
                ProviderInferenceRelayOperation.ChatCompletions,
            SharedProviderRelayOperation.Responses =>
                ProviderInferenceRelayOperation.Responses,
            SharedProviderRelayOperation.ImageGenerations =>
                ProviderInferenceRelayOperation.ImageGenerations,
            _ => throw new InvalidOperationException(
                "The relay operation has no MAF inference mapping.")
        };
        var transport = operation == ProviderInferenceRelayOperation.Responses
            ? ProviderTransportKind.Responses
            : ProviderTransportKind.ChatCompletions;
        var credential = dispatch.Target.Credential?.UseValue(
            value => new ProviderInferenceRelayCredential(value));
        var provider = new ProviderProfile(
            dispatch.Target.ProviderProfileId,
            "Shared-provider relay",
            kind,
            dispatch.Target.BaseUri.AbsoluteUri,
            string.Empty,
            dispatch.Target.UpstreamModelId,
            transport,
            true,
            dispatch.Target.Support.StreamingMode != SharedProviderStreamingMode.None,
            dispatch.Target.Support.SupportsFunctionTools,
            transport == ProviderTransportKind.ChatCompletions,
            transport == ProviderTransportKind.Responses,
            dispatch.Target.ConfigurationJson,
            string.Empty,
            "Not checked",
            null,
            [dispatch.Target.UpstreamModelId])
        {
            ConnectorPluginKey = dispatch.Target.ConnectorPluginKey,
            ModelSelectionConstraint = new ProviderModelSelectionConstraint(
                [dispatch.Target.UpstreamModelId]),
            Purpose = dispatch.Target.Purpose == SharedProviderPurpose.ImageGeneration
                ? ProviderProfilePurpose.ImageGeneration
                : ProviderProfilePurpose.Chat
        };

        return new ProviderInferenceRelayRequest(
            provider,
            dispatch.Target.UpstreamModelId,
            operation,
            payload.Span,
            dispatch.Request.Stream,
            credential);
    }

    private static bool IsContentType(MediaTypeHeaderValue? contentType, string expected)
        => contentType?.MediaType is { } mediaType &&
            string.Equals(mediaType, expected, StringComparison.OrdinalIgnoreCase);

    private static SharedProviderRelayDispatchResult.Failed InvalidUpstreamResponse()
        => Failed(
            SharedProviderFailureCategory.UpstreamFailure,
            "shared_provider_upstream_response_invalid",
            "The upstream provider returned an invalid response.");

    private static SharedProviderRelayDispatchResult.Failed Failed(
        SharedProviderFailureCategory category,
        string code,
        string message)
        => new(new SharedProviderFailure(
            category,
            new SharedProviderFailureCode(code),
            message));
}

internal static class SharedProviderRelayResponsePolicy
{
    private static readonly JsonDocumentOptions JsonOptions = new()
    {
        AllowTrailingCommas = false,
        CommentHandling = JsonCommentHandling.Disallow,
        MaxDepth = 64
    };

    public static byte[] RewriteBuffered(
        ReadOnlyMemory<byte> payloadUtf8,
        SharedProviderRoutingModelId publicModelId,
        SharedProviderRelayOperation operation,
        bool isStreamingEvent = false)
    {
        if (payloadUtf8.IsEmpty)
        {
            throw new InvalidDataException("The upstream response body is empty.");
        }

        try
        {
            using var document = JsonDocument.Parse(payloadUtf8, JsonOptions);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                root.TryGetProperty("error", out var error) && error.ValueKind != JsonValueKind.Null) {
                throw new InvalidDataException("The upstream response envelope is invalid.");
            }
            if (!isStreamingEvent && operation == SharedProviderRelayOperation.Responses &&
                (!root.TryGetProperty("status", out var status) || status.ValueKind != JsonValueKind.String ||
                    status.GetString() != "completed")) {
                throw new InvalidDataException("The upstream provider did not complete the response.");
            }

            if (operation == SharedProviderRelayOperation.ImageGenerations)
            {
                return RewriteImageResponse(document.RootElement);
            }

            using var output = new MemoryStream();
            using (var writer = new Utf8JsonWriter(output))
            {
                WriteElement(writer, document.RootElement, publicModelId.Value, rewriteModel: true);
            }

            if (output.Length > SharedProviderHttpRelayClient.MaximumBufferedResponseBytes)
            {
                throw new InvalidDataException("The upstream response exceeds the relay byte limit.");
            }

            return output.ToArray();
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The upstream response is not valid JSON.", exception);
        }
    }

    public static string RewriteServerSentEventData(
        string data,
        SharedProviderRoutingModelId publicModelId,
        SharedProviderRelayOperation operation)
    {
        if (string.Equals(data, "[DONE]", StringComparison.Ordinal))
        {
            return data;
        }

        byte[] rewritten;
        try
        {
            rewritten = RewriteBuffered(
                System.Text.Encoding.UTF8.GetBytes(data),
                publicModelId,
                operation,
                isStreamingEvent: true);
        }
        catch (InvalidDataException exception)
        {
            throw new InvalidDataException("The upstream SSE frame is invalid.", exception);
        }

        return System.Text.Encoding.UTF8.GetString(rewritten);
    }

    private static void WriteElement(
        Utf8JsonWriter writer,
        JsonElement element,
        string publicModelId,
        bool rewriteModel)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            element.WriteTo(writer);
            return;
        }

        writer.WriteStartObject();
        foreach (var property in element.EnumerateObject())
        {
            writer.WritePropertyName(property.Name);
            if (rewriteModel && property.NameEquals("model") && property.Value.ValueKind == JsonValueKind.String)
            {
                writer.WriteStringValue(publicModelId);
            }
            else if (property.NameEquals("response") && property.Value.ValueKind == JsonValueKind.Object)
            {
                WriteElement(writer, property.Value, publicModelId, rewriteModel: true);
            }
            else
            {
                property.Value.WriteTo(writer);
            }
        }

        writer.WriteEndObject();
    }

    private static byte[] RewriteImageResponse(JsonElement root)
    {
        if (root.EnumerateObject().Any(property => !IsValidImageMetadata(property)) ||
            !root.TryGetProperty("data", out var data) ||
            data.ValueKind != JsonValueKind.Array ||
            data.GetArrayLength() is < 1 or > SharedProviderRelaySupportDescriptor.MaximumAllowedImageCount)
        {
            throw new InvalidDataException("The upstream image response envelope is invalid.");
        }

        const int maximumImageBytes = 8 * 1024 * 1024;
        const int maximumTotalImageBytes = 32 * 1024 * 1024;
        var images = new List<(byte[] Bytes, string? RevisedPrompt)>(data.GetArrayLength());
        long totalBytes = 0;
        foreach (var item in data.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object ||
                item.EnumerateObject().Any(property => property.Name is not ("b64_json" or "revised_prompt")) ||
                !item.TryGetProperty("b64_json", out var base64) ||
                base64.ValueKind != JsonValueKind.String ||
                !base64.TryGetBytesFromBase64(out var bytes) ||
                bytes.Length is <= 0 or > maximumImageBytes)
            {
                throw new InvalidDataException("The upstream image response contains invalid image data.");
            }

            totalBytes += bytes.Length;
            if (totalBytes > maximumTotalImageBytes)
            {
                throw new InvalidDataException("The upstream image response exceeds the relay byte limit.");
            }

            string? revisedPrompt = null;
            if (item.TryGetProperty("revised_prompt", out var revised))
            {
                if (revised.ValueKind != JsonValueKind.String ||
                    revised.GetString() is not { Length: <= 1024 * 1024 } value ||
                    value.Contains('\0'))
                {
                    throw new InvalidDataException("The upstream image response contains an invalid revised prompt.");
                }

                revisedPrompt = value;
            }

            images.Add((bytes, revisedPrompt));
        }

        using var output = new MemoryStream();
        using (var writer = new Utf8JsonWriter(output))
        {
            writer.WriteStartObject();
            if (root.TryGetProperty("created", out var created) &&
                created.ValueKind == JsonValueKind.Number &&
                created.TryGetInt64(out var createdValue) &&
                createdValue >= 0)
            {
                writer.WriteNumber("created", createdValue);
            }

            foreach (var property in root.EnumerateObject()) {
                if (property.Name is not ("created" or "data")) {
                    property.WriteTo(writer);
                }
            }

            writer.WriteStartArray("data");
            foreach (var image in images)
            {
                writer.WriteStartObject();
                writer.WriteBase64String("b64_json", image.Bytes);
                if (image.RevisedPrompt is not null)
                {
                    writer.WriteString("revised_prompt", image.RevisedPrompt);
                }

                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return output.ToArray();
    }

    private static bool IsValidImageMetadata(JsonProperty property) => property.Name switch {
        "created" or "data" => true,
        "usage" => IsValidImageUsage(property.Value),
        _ => property.Value.ValueKind == JsonValueKind.String && property.Name switch {
            "background" => property.Value.GetString() is "transparent" or "opaque",
            "output_format" => property.Value.GetString() is "png" or "jpeg" or "webp",
            "quality" => property.Value.GetString() is "low" or "medium" or "high",
            "size" => property.Value.GetString() is "1024x1024" or "1024x1536" or "1536x1024",
            _ => false
        }
    };

    private static bool IsValidImageUsage(JsonElement usage) {
        if (usage.ValueKind != JsonValueKind.Object ||
            !HasTokenCount(usage, "input_tokens") || !HasTokenCount(usage, "output_tokens") ||
            !HasTokenCount(usage, "total_tokens")) {
            return false;
        }

        return usage.EnumerateObject().All(property => property.Name switch {
            "input_tokens" or "output_tokens" or "total_tokens" => IsTokenCount(property.Value),
            "input_tokens_details" or "output_tokens_details" =>
                property.Value.ValueKind == JsonValueKind.Object &&
                HasTokenCount(property.Value, "text_tokens") && HasTokenCount(property.Value, "image_tokens") &&
                property.Value.EnumerateObject().All(detail =>
                    detail.Name is "text_tokens" or "image_tokens" && IsTokenCount(detail.Value)),
            _ => false
        });
    }

    private static bool HasTokenCount(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && IsTokenCount(value);

    private static bool IsTokenCount(JsonElement value) =>
        value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var count) && count >= 0;
}
