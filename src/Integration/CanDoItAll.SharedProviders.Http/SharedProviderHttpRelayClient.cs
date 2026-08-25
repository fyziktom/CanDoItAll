using System.Buffers;
using System.Net.Http.Headers;
using System.Text.Json;
using CanDoItAll.SharedProviders.Abstractions;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.SharedProviders.Http;

internal sealed class SharedProviderHttpRelayClient(
    IHttpClientFactory httpClientFactory,
    ILogger<SharedProviderHttpRelayClient> logger)
{
    internal const string ClientName = "SharedProviderRelay";
    internal const int MaximumBufferedResponseBytes = 64 * 1024 * 1024;

    public async ValueTask<SharedProviderRelayDispatchResult> DispatchAsync(
        SharedProviderRelayDispatchRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var client = httpClientFactory.CreateClient(ClientName);
        var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(request.Target.Timeout);
        HttpResponseMessage? response = null;
        try
        {
            using var upstreamRequest = CreateRequest(request);
            response = await client.SendAsync(
                upstreamRequest,
                HttpCompletionOption.ResponseHeadersRead,
                timeoutSource.Token).ConfigureAwait(false);
            var headers = SharedProviderRelayResponseHeaderPolicy.Project(response);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await ReadBoundedAsync(
                    response.Content,
                    SharedProviderFailure.MaximumMessageLength,
                    timeoutSource.Token).ConfigureAwait(false);
                var failure = SharedProviderRelayFailureMapper.FromUpstream(
                    response.StatusCode,
                    response.Headers.TryGetValues("retry-after", out var retryValues)
                        ? retryValues.Take(2).ToArray() is [var retryAfter]
                            ? retryAfter
                            : null
                        : null,
                    System.Text.Encoding.UTF8.GetString(errorBody));
                logger.LogWarning(
                    "Shared-provider upstream rejected {Operation} for connector {ConnectorPluginKey} with HTTP {StatusCode}.",
                    request.Request.Operation,
                    request.Target.ConnectorPluginKey,
                    (int)response.StatusCode);
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
                    response,
                    client,
                    responseStream,
                    timeoutSource,
                    cancellationToken,
                    request.Request.Operation,
                    request.Request.RoutingModelId,
                    SharedProviderRelayTimeouts.StreamingIdle,
                    headers);
                response = null;
                timeoutSource = null!;
                client = null!;
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
            response?.Dispose();
            timeoutSource?.Dispose();
            client?.Dispose();
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

    private static HttpRequestMessage CreateRequest(SharedProviderRelayDispatchRequest dispatch)
    {
        var payload = dispatch.Request.CreateUpstreamPayload(dispatch.Target.UpstreamModelId);
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            SharedProviderRelayUriPolicy.Resolve(dispatch.Target, dispatch.Request.Operation))
        {
            Content = new ReadOnlyMemoryContent(payload)
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json")
        {
            CharSet = "utf-8"
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(
            dispatch.Request.Stream ? "text/event-stream" : "application/json"));
        if (dispatch.Target.Credential is not null)
        {
            dispatch.Target.Credential.UseValue(value =>
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", value);
                return true;
            });
        }

        return request;
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
        ReadOnlySpan<byte> payloadUtf8,
        SharedProviderRoutingModelId publicModelId,
        SharedProviderRelayOperation operation)
    {
        if (payloadUtf8.IsEmpty)
        {
            throw new InvalidDataException("The upstream response body is empty.");
        }

        try
        {
            using var document = JsonDocument.Parse(payloadUtf8.ToArray(), JsonOptions);
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                document.RootElement.TryGetProperty("error", out _))
            {
                throw new InvalidDataException("The upstream response envelope is invalid.");
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
                operation);
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
        if (root.EnumerateObject().Any(property => property.Name is not ("created" or "data")) ||
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
}
