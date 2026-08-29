using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace CanDoItAll.AgentFramework.Providers;

internal static class ProviderStreamingProtocol
{
    private const int MaximumFrameCharacters = 1_048_576;
    private const int MaximumProviderEvents = 100_000;

    public static async IAsyncEnumerable<ProviderChatStreamingUpdate> ReadOpenAiChatCompletionsAsync(
        Stream stream,
        string fallbackModel,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var model = fallbackModel;
        var finishReason = string.Empty;
        var inputTokens = 0;
        var outputTokens = 0;
        var cachedInputTokens = 0;
        CanDoItAll.AgentFramework.ProviderHistory.HistoryUsage? observedUsage = null;
        var terminalMarkerReceived = false;
        await foreach (var data in ReadSseDataAsync(stream, cancellationToken).ConfigureAwait(false))
        {
            if (string.Equals(data, "[DONE]", StringComparison.Ordinal))
            {
                terminalMarkerReceived = true;
                break;
            }

            using var document = ParseFrame(data, "OpenAI chat completion");
            var root = document.RootElement;
            ThrowOnError(root, "OpenAI chat completion");
            var reportedModel = ProviderDriverJson.ReadString(root, "model");
            if (!string.IsNullOrWhiteSpace(reportedModel))
            {
                model = reportedModel;
            }

            if (root.TryGetProperty("usage", out var usage) && usage.ValueKind == JsonValueKind.Object)
            {
                inputTokens = ProviderDriverJson.ReadInt(usage, "prompt_tokens");
                outputTokens = ProviderDriverJson.ReadInt(usage, "completion_tokens");
                cachedInputTokens = ProviderDriverProtocol.ReadChatCompletionsCachedTokens(usage);
                observedUsage = ProviderObservedUsage.ChatCompletions(usage);
                yield return new ProviderChatUsageObserved(observedUsage);
            }

            if (!root.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var choice in choices.EnumerateArray())
            {
                if (choice.TryGetProperty("index", out var index) && index.TryGetInt32(out var indexValue) && indexValue != 0)
                {
                    continue;
                }

                var reportedFinishReason = ProviderDriverJson.ReadString(choice, "finish_reason");
                if (!string.IsNullOrWhiteSpace(reportedFinishReason))
                {
                    finishReason = reportedFinishReason;
                }

                if (!choice.TryGetProperty("delta", out var delta) || delta.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var text = ProviderDriverJson.ReadString(delta, "content");
                if (!string.IsNullOrEmpty(text))
                {
                    yield return new ProviderChatTextDelta(text);
                }
            }
        }

        if (!terminalMarkerReceived)
        {
            throw new InvalidOperationException("OpenAI chat completion stream ended without its terminal marker.");
        }

        yield return new ProviderChatCompleted(
            model,
            inputTokens,
            outputTokens,
            string.IsNullOrWhiteSpace(finishReason) ? "completed" : finishReason)
        {
            ObservedUsage = observedUsage,
            CachedInputTokens = cachedInputTokens
        };
    }

    public static async IAsyncEnumerable<ProviderChatStreamingUpdate> ReadOpenAiResponsesAsync(
        Stream stream,
        string fallbackModel,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var data in ReadSseDataAsync(stream, cancellationToken).ConfigureAwait(false))
        {
            if (string.Equals(data, "[DONE]", StringComparison.Ordinal))
            {
                break;
            }

            using var document = ParseFrame(data, "OpenAI response");
            var root = document.RootElement;
            ThrowOnError(root, "OpenAI response");
            var type = ProviderDriverJson.ReadString(root, "type");
            if (type is "response.output_text.delta" or "response.refusal.delta")
            {
                var text = ProviderDriverJson.ReadString(root, "delta");
                if (!string.IsNullOrEmpty(text))
                {
                    yield return new ProviderChatTextDelta(text);
                }

                continue;
            }

            if (type is "response.failed" or "response.incomplete" or "error")
            {
                throw new InvalidOperationException("OpenAI response stream reported a terminal failure.");
            }

            if (type != "response.completed")
            {
                continue;
            }

            var response = root.TryGetProperty("response", out var responseElement) &&
                           responseElement.ValueKind == JsonValueKind.Object
                ? responseElement
                : root;
            var model = ProviderDriverJson.ReadString(response, "model");
            var status = ProviderDriverJson.ReadString(response, "status");
            var usage = response.TryGetProperty("usage", out var usageElement) &&
                        usageElement.ValueKind == JsonValueKind.Object
                ? usageElement
                : default;
            yield return new ProviderChatCompleted(
                string.IsNullOrWhiteSpace(model) ? fallbackModel : model,
                usage.ValueKind == JsonValueKind.Object ? ProviderDriverJson.ReadInt(usage, "input_tokens") : 0,
                usage.ValueKind == JsonValueKind.Object ? ProviderDriverJson.ReadInt(usage, "output_tokens") : 0,
                string.IsNullOrWhiteSpace(status) ? "completed" : status)
            {
                ObservedUsage = ProviderObservedUsage.Responses(usage),
                CachedInputTokens = ReadResponsesCachedTokens(usage)
            };
            yield break;
        }

        throw new InvalidOperationException("OpenAI response stream ended without a completed event.");
    }

    public static async IAsyncEnumerable<ProviderChatStreamingUpdate> ReadOllamaAsync(
        Stream stream,
        string fallbackModel,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var data in ReadNdjsonAsync(stream, cancellationToken).ConfigureAwait(false))
        {
            using var document = ParseFrame(data, "Ollama chat completion");
            var root = document.RootElement;
            ThrowOnError(root, "Ollama chat completion");
            if (root.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.Object)
            {
                var text = ProviderDriverJson.ReadString(message, "content");
                if (!string.IsNullOrEmpty(text))
                {
                    yield return new ProviderChatTextDelta(text);
                }
            }

            if (!root.TryGetProperty("done", out var done) || done.ValueKind != JsonValueKind.True)
            {
                continue;
            }

            var model = ProviderDriverJson.ReadString(root, "model");
            var finishReason = ProviderDriverJson.ReadString(root, "done_reason");
            yield return new ProviderChatCompleted(
                string.IsNullOrWhiteSpace(model) ? fallbackModel : model,
                ProviderDriverJson.ReadInt(root, "prompt_eval_count"),
                ProviderDriverJson.ReadInt(root, "eval_count"),
                string.IsNullOrWhiteSpace(finishReason) ? "completed" : finishReason) {
                ObservedUsage = ProviderObservedUsage.Ollama(root)
            };
            yield break;
        }

        throw new InvalidOperationException("Ollama chat completion stream ended without a completed frame.");
    }

    private static async IAsyncEnumerable<string> ReadSseDataAsync(
        Stream stream,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, true, 1024, leaveOpen: true);
        StringBuilder? data = null;
        var eventCount = 0;
        while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            EnsureFrameBound(line.Length);
            if (line.Length == 0)
            {
                if (data is not null)
                {
                    EnsureEventCount(++eventCount);
                    yield return data.ToString().TrimEnd('\n');
                    data = null;
                }

                continue;
            }

            if (!line.StartsWith("data:", StringComparison.Ordinal))
            {
                continue;
            }

            var value = line.AsSpan(5);
            if (!value.IsEmpty && value[0] == ' ')
            {
                value = value[1..];
            }

            data ??= new StringBuilder();
            if (data.Length > 0)
            {
                data.Append('\n');
            }

            data.Append(value);
            EnsureFrameBound(data.Length);
        }

        if (data is not null)
        {
            EnsureEventCount(++eventCount);
            yield return data.ToString().TrimEnd('\n');
        }
    }

    private static async IAsyncEnumerable<string> ReadNdjsonAsync(
        Stream stream,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, true, 1024, leaveOpen: true);
        var eventCount = 0;
        while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            EnsureFrameBound(line.Length);
            EnsureEventCount(++eventCount);
            yield return line;
        }
    }

    private static JsonDocument ParseFrame(string data, string protocol)
    {
        try
        {
            return JsonDocument.Parse(data);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException($"{protocol} stream contained malformed JSON.", exception);
        }
    }

    private static void ThrowOnError(JsonElement root, string protocol)
    {
        if (root.TryGetProperty("error", out var error) && error.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined)
        {
            throw new InvalidOperationException($"{protocol} stream reported a provider error.");
        }
    }

    private static int ReadResponsesCachedTokens(JsonElement usage)
    {
        if (usage.ValueKind != JsonValueKind.Object ||
            !usage.TryGetProperty("input_tokens_details", out var details) ||
            details.ValueKind != JsonValueKind.Object)
        {
            return 0;
        }

        return ProviderDriverJson.ReadInt(details, "cached_tokens");
    }

    private static void EnsureFrameBound(int characterCount)
    {
        if (characterCount > MaximumFrameCharacters)
        {
            throw new InvalidOperationException("Provider stream frame exceeded the configured size limit.");
        }
    }

    private static void EnsureEventCount(int eventCount)
    {
        if (eventCount > MaximumProviderEvents)
        {
            throw new InvalidOperationException("Provider stream exceeded the configured event limit.");
        }
    }
}
