using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using CanDoItAll.SharedProviders.Abstractions;

namespace CanDoItAll.SharedProviders.Http;

public static class SharedProviderRelayUriPolicy
{
    public static Uri Resolve(
        SharedProviderRelayTarget target,
        SharedProviderRelayOperation operation)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (!Enum.IsDefined(operation))
        {
            throw new ArgumentOutOfRangeException(nameof(operation));
        }

        if (!target.Supports(operation))
        {
            throw new ArgumentException(
                "The relay target does not support the requested operation.",
                nameof(operation));
        }

        return target.ConnectorPluginKey switch
        {
            SharedProviderConnectorPluginKeys.OpenAi => ResolveOpenAi(target.BaseUri, operation),
            SharedProviderConnectorPluginKeys.OllamaLocal or
            SharedProviderConnectorPluginKeys.OllamaRemote => ResolveOllama(target.BaseUri, operation),
            SharedProviderConnectorPluginKeys.ComfyUiLocal => throw new InvalidOperationException(
                "The image-capability relay does not expose a direct upstream URI."),
            _ => throw new InvalidOperationException(
                "The relay target connector has no registered URI policy.")
        };
    }

    private static Uri ResolveOpenAi(Uri baseUri, SharedProviderRelayOperation operation)
    {
        var root = baseUri.AbsoluteUri.TrimEnd('/');
        if (root.EndsWith("/models", StringComparison.OrdinalIgnoreCase))
        {
            root = root[..^"/models".Length];
        }

        var relativePath = operation switch
        {
            SharedProviderRelayOperation.ChatCompletions => "chat/completions",
            SharedProviderRelayOperation.Responses => "responses",
            SharedProviderRelayOperation.ImageGenerations => "images/generations",
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };

        return new Uri($"{root}/{relativePath}", UriKind.Absolute);
    }

    private static Uri ResolveOllama(Uri baseUri, SharedProviderRelayOperation operation)
    {
        if (operation != SharedProviderRelayOperation.ChatCompletions)
        {
            throw new ArgumentException(
                "The Ollama relay target does not support the requested operation.",
                nameof(operation));
        }

        var root = baseUri.AbsoluteUri.TrimEnd('/');
        if (!baseUri.AbsolutePath.TrimEnd('/').EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
        {
            root = $"{root}/v1";
        }

        return new Uri($"{root}/chat/completions", UriKind.Absolute);
    }
}

public static class SharedProviderRelayFailureMapper
{
    public static (string Code, string Parameter) DescribeUpstreamFailure(ReadOnlyMemory<byte> body) {
        try {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty("error", out var error) || error.ValueKind != JsonValueKind.Object) {
                return ("unclassified", "unclassified");
            }
            var code = error.TryGetProperty("code", out var codeValue) && codeValue.ValueKind == JsonValueKind.String
                ? codeValue.GetString() switch {
                    "unsupported_value" => "unsupported_value",
                    "unsupported_parameter" => "unsupported_parameter",
                    "invalid_value" => "invalid_value",
                    "invalid_request_error" => "invalid_request_error",
                    _ => "unclassified"
                } : "unclassified";
            var parameter = error.TryGetProperty("param", out var parameterValue) && parameterValue.ValueKind == JsonValueKind.String
                ? parameterValue.GetString() switch {
                    "temperature" => "temperature",
                    "reasoning_effort" => "reasoning_effort",
                    "reasoning.effort" => "reasoning.effort",
                    "tools" => "tools",
                    "model" => "model",
                    _ => "unclassified"
                } : "unclassified";
            return (code, parameter);
        } catch (JsonException) {
            return ("unclassified", "unclassified");
        }
    }

    public static SharedProviderFailure FromUpstream(
        HttpStatusCode statusCode,
        string? retryAfter,
        string? rawResponseBody)
    {
        _ = rawResponseBody;
        var category = statusCode switch
        {
            HttpStatusCode.TooManyRequests => SharedProviderFailureCategory.RateLimited,
            HttpStatusCode.RequestTimeout or HttpStatusCode.GatewayTimeout =>
                SharedProviderFailureCategory.Timeout,
            _ => SharedProviderFailureCategory.UpstreamFailure
        };
        var code = category switch
        {
            SharedProviderFailureCategory.RateLimited => "shared_provider_upstream_rate_limited",
            SharedProviderFailureCategory.Timeout => "shared_provider_upstream_timeout",
            _ => "shared_provider_upstream_failed"
        };
        var message = category switch
        {
            SharedProviderFailureCategory.RateLimited =>
                "The upstream provider rate limited the request.",
            SharedProviderFailureCategory.Timeout =>
                "The upstream provider did not respond before the timeout.",
            _ => "The upstream provider could not complete the request."
        };

        return new SharedProviderFailure(
            category,
            new SharedProviderFailureCode(code),
            message,
            retryAfter: category == SharedProviderFailureCategory.RateLimited
                ? SharedProviderRelayRetryAfterParser.Parse(retryAfter, DateTimeOffset.UtcNow)
                : null);
    }
}

public static class SharedProviderRelayResponseHeaderPolicy
{
    private static readonly string[] RequestIdHeaderNames =
    [
        "x-request-id",
        "openai-request-id",
        "request-id"
    ];

    public static SharedProviderRelayResponseHeaders Project(HttpResponseMessage response)
    {
        ArgumentNullException.ThrowIfNull(response);

        return new SharedProviderRelayResponseHeaders(
            ReadRequestId(response.Headers),
            ReadRetryAfter(response.Headers));
    }

    private static string? ReadRequestId(HttpResponseHeaders headers)
    {
        foreach (var headerName in RequestIdHeaderNames)
        {
            if (!headers.TryGetValues(headerName, out var values))
            {
                continue;
            }

            var candidates = values.Take(2).ToArray();
            if (candidates.Length != 1)
            {
                return null;
            }

            var value = candidates[0];
            return value is { Length: > 0 and <= 256 } &&
                value == value.Trim() &&
                !value.Any(char.IsControl)
                    ? value
                    : null;
        }

        return null;
    }

    private static TimeSpan? ReadRetryAfter(HttpResponseHeaders headers)
    {
        if (!headers.TryGetValues("retry-after", out var values))
        {
            return null;
        }

        var candidates = values.Take(2).ToArray();
        return candidates.Length == 1
            ? SharedProviderRelayRetryAfterParser.Parse(candidates[0], DateTimeOffset.UtcNow)
            : null;
    }
}

public static class SharedProviderRelayUsageExtractor
{
    public static SharedProviderRelayUsage ExtractBuffered(
        SharedProviderRelayOperation operation,
        ReadOnlySpan<byte> payloadUtf8)
    {
        if (!Enum.IsDefined(operation))
        {
            throw new ArgumentOutOfRangeException(nameof(operation));
        }

        if (payloadUtf8.IsEmpty)
        {
            return SharedProviderRelayUsage.Unavailable;
        }

        try
        {
            using var document = JsonDocument.Parse(payloadUtf8.ToArray(), StrictJsonOptions);
            return ExtractFromRoot(operation, document.RootElement);
        }
        catch (JsonException)
        {
            return SharedProviderRelayUsage.Unavailable;
        }
    }

    public static SharedProviderRelayUsage ExtractServerSentEvents(
        SharedProviderRelayOperation operation,
        IReadOnlyList<SharedProviderRelayStreamFrame> frames)
    {
        if (!Enum.IsDefined(operation))
        {
            throw new ArgumentOutOfRangeException(nameof(operation));
        }

        ArgumentNullException.ThrowIfNull(frames);
        for (var index = frames.Count - 1; index >= 0; index--)
        {
            var frame = frames[index];
            if (frame is null || frame.IsDone)
            {
                continue;
            }

            var usage = ExtractBuffered(
                operation,
                System.Text.Encoding.UTF8.GetBytes(frame.Data));
            if (usage.Completeness != SharedProviderRelayUsageCompleteness.Unavailable)
            {
                return usage;
            }
        }

        return SharedProviderRelayUsage.Unavailable;
    }

    private static JsonDocumentOptions StrictJsonOptions { get; } = new()
    {
        AllowTrailingCommas = false,
        CommentHandling = JsonCommentHandling.Disallow,
        MaxDepth = 32
    };

    private static SharedProviderRelayUsage ExtractFromRoot(
        SharedProviderRelayOperation operation,
        JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return SharedProviderRelayUsage.Unavailable;
        }

        if (operation == SharedProviderRelayOperation.ImageGenerations)
        {
            return root.TryGetProperty("data", out var data) &&
                data.ValueKind == JsonValueKind.Array &&
                data.GetArrayLength() > 0
                    ? new SharedProviderRelayUsage(
                        inputTokens: null,
                        outputTokens: null,
                        imageCount: data.GetArrayLength(),
                        SharedProviderRelayUsageCompleteness.Complete)
                    : SharedProviderRelayUsage.Unavailable;
        }

        if (!TryGetUsage(root, out var usage))
        {
            return SharedProviderRelayUsage.Unavailable;
        }

        var inputProperty = operation == SharedProviderRelayOperation.ChatCompletions
            ? "prompt_tokens"
            : "input_tokens";
        var outputProperty = operation == SharedProviderRelayOperation.ChatCompletions
            ? "completion_tokens"
            : "output_tokens";
        var input = ReadTokenCount(usage, inputProperty);
        var output = ReadTokenCount(usage, outputProperty);
        if (input.IsInvalid || output.IsInvalid)
        {
            return SharedProviderRelayUsage.Unavailable;
        }

        var completeness = (input.Value.HasValue, output.Value.HasValue) switch
        {
            (true, true) => SharedProviderRelayUsageCompleteness.Complete,
            (true, false) or (false, true) => SharedProviderRelayUsageCompleteness.Partial,
            _ => SharedProviderRelayUsageCompleteness.Unavailable
        };
        return completeness == SharedProviderRelayUsageCompleteness.Unavailable
            ? SharedProviderRelayUsage.Unavailable
            : new SharedProviderRelayUsage(
                input.Value,
                output.Value,
                imageCount: null,
                completeness);
    }

    private static bool TryGetUsage(JsonElement root, out JsonElement usage)
    {
        if (root.TryGetProperty("usage", out usage) && usage.ValueKind == JsonValueKind.Object)
        {
            return true;
        }

        return root.TryGetProperty("response", out var response) &&
            response.ValueKind == JsonValueKind.Object &&
            response.TryGetProperty("usage", out usage) &&
            usage.ValueKind == JsonValueKind.Object;
    }

    private static TokenCount ReadTokenCount(JsonElement usage, string propertyName)
    {
        if (!usage.TryGetProperty(propertyName, out var value))
        {
            return new TokenCount(Value: null, IsInvalid: false);
        }

        return value.ValueKind == JsonValueKind.Number &&
            value.TryGetInt64(out var count) &&
            count >= 0
                ? new TokenCount(count, IsInvalid: false)
                : new TokenCount(Value: null, IsInvalid: true);
    }

    private readonly record struct TokenCount(long? Value, bool IsInvalid);
}

internal static class SharedProviderRelayRetryAfterParser
{
    public static TimeSpan? Parse(string? value, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            value != value.Trim() ||
            value.Any(char.IsControl))
        {
            return null;
        }

        if (long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) &&
            seconds >= 0)
        {
            return TimeSpan.FromSeconds(Math.Min(
                seconds,
                (long)SharedProviderFailure.MaximumRetryAfter.TotalSeconds));
        }

        if (!DateTimeOffset.TryParseExact(
                value,
                "r",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var retryAt))
        {
            return null;
        }

        var delay = retryAt - now;
        if (delay <= TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        return delay > SharedProviderFailure.MaximumRetryAfter
            ? SharedProviderFailure.MaximumRetryAfter
            : delay;
    }
}
