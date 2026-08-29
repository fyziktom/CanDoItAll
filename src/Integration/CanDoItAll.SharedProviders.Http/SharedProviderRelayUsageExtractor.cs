using System.Text.Json;
using CanDoItAll.SharedProviders.Abstractions;

namespace CanDoItAll.SharedProviders.Http;

public static class SharedProviderRelayUsageExtractor {
    public static SharedProviderRelayUsage ExtractBuffered(
        SharedProviderRelayOperation operation,
        ReadOnlySpan<byte> payloadUtf8) {
        if (!Enum.IsDefined(operation)) {
            throw new ArgumentOutOfRangeException(nameof(operation));
        }

        if (payloadUtf8.IsEmpty) {
            return SharedProviderRelayUsage.Unavailable;
        }

        try {
            using var document = JsonDocument.Parse(payloadUtf8.ToArray(), StrictJsonOptions);
            return ExtractFromRoot(operation, document.RootElement);
        }
        catch (JsonException) {
            return SharedProviderRelayUsage.Unavailable;
        }
    }

    public static SharedProviderRelayUsage ExtractServerSentEvents(
        SharedProviderRelayOperation operation,
        IReadOnlyList<SharedProviderRelayStreamFrame> frames) {
        if (!Enum.IsDefined(operation)) {
            throw new ArgumentOutOfRangeException(nameof(operation));
        }

        ArgumentNullException.ThrowIfNull(frames);
        for (var index = frames.Count - 1; index >= 0; index--) {
            var frame = frames[index];
            if (frame is null || frame.IsDone) {
                continue;
            }

            var usage = ExtractBuffered(
                operation,
                System.Text.Encoding.UTF8.GetBytes(frame.Data));
            if (usage.Completeness != SharedProviderRelayUsageCompleteness.Unavailable) {
                return usage;
            }
        }

        return SharedProviderRelayUsage.Unavailable;
    }

    private static JsonDocumentOptions StrictJsonOptions { get; } = new() {
        AllowTrailingCommas = false,
        CommentHandling = JsonCommentHandling.Disallow,
        MaxDepth = 32
    };

    private static SharedProviderRelayUsage ExtractFromRoot(
        SharedProviderRelayOperation operation,
        JsonElement root) {
        if (root.ValueKind != JsonValueKind.Object) {
            return SharedProviderRelayUsage.Unavailable;
        }

        if (operation == SharedProviderRelayOperation.ImageGenerations) {
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

        if (!TryGetUsage(root, out var usage)) {
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
        var cached = ReadNestedTokenCount(usage, inputProperty + "_details", "cached_tokens");
        var reasoning = ReadNestedTokenCount(usage, outputProperty + "_details", "reasoning_tokens");
        var written = ReadTokenCount(usage, "cache_creation_input_tokens");
        if (input.IsInvalid || output.IsInvalid || cached.IsInvalid || written.IsInvalid || reasoning.IsInvalid
            || cached.Value.HasValue && !input.Value.HasValue || written.Value.HasValue && !input.Value.HasValue
            || reasoning.Value.HasValue && !output.Value.HasValue
            || cached.Value > input.Value || written.Value > input.Value - (cached.Value ?? 0)
            || reasoning.Value > output.Value) {
            return SharedProviderRelayUsage.Unavailable;
        }

        var completeness = (input.Value.HasValue, output.Value.HasValue) switch {
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
                completeness,
                cached.Value, written.Value, reasoning.Value);
    }

    private static bool TryGetUsage(JsonElement root, out JsonElement usage) {
        if (root.TryGetProperty("usage", out usage) && usage.ValueKind == JsonValueKind.Object) {
            return true;
        }

        return root.TryGetProperty("response", out var response) &&
            response.ValueKind == JsonValueKind.Object &&
            response.TryGetProperty("usage", out usage) &&
            usage.ValueKind == JsonValueKind.Object;
    }

    private static TokenCount ReadNestedTokenCount(JsonElement usage, string details, string property) {
        if (!usage.TryGetProperty(details, out var value)) {
            return new(null, false);
        }
        return value.ValueKind == JsonValueKind.Object ? ReadTokenCount(value, property) : new(null, true);
    }

    private static TokenCount ReadTokenCount(JsonElement usage, string propertyName) {
        if (!usage.TryGetProperty(propertyName, out var value)) {
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

