using System.Text.Json;
using CanDoItAll.AgentFramework.ProviderHistory;

namespace CanDoItAll.AgentFramework.Providers;

internal static class ProviderObservedUsage {
    public static HistoryUsage ChatCompletions(JsonElement usage) => Read(
        Number(usage, "prompt_tokens"), Number(usage, "completion_tokens"),
        Detail(usage, "prompt_tokens_details", "cached_tokens"),
        Detail(usage, "completion_tokens_details", "reasoning_tokens"));

    public static HistoryUsage Responses(JsonElement usage) => Read(
        Number(usage, "input_tokens"), Number(usage, "output_tokens"),
        Detail(usage, "input_tokens_details", "cached_tokens"),
        Detail(usage, "output_tokens_details", "reasoning_tokens"));

    public static HistoryUsage Ollama(JsonElement root) => Read(
        Number(root, "prompt_eval_count"), Number(root, "eval_count"), null, null);

    private static HistoryUsage Read(long? input, long? output, long? cached, long? reasoning) {
        if (cached > input || reasoning > output) {
            return new(HistoryUsageState.Unavailable);
        }
        return new(input.HasValue && output.HasValue ? HistoryUsageState.Complete :
            input.HasValue || output.HasValue ? HistoryUsageState.Partial : HistoryUsageState.Unavailable,
            input, output, cached, ReasoningTokens: reasoning);
    }

    private static long? Detail(JsonElement value, string container, string name) =>
        value.ValueKind == JsonValueKind.Object && value.TryGetProperty(container, out var details) ? Number(details, name) : null;

    private static long? Number(JsonElement value, string name) =>
        value.ValueKind == JsonValueKind.Object && value.TryGetProperty(name, out var item) &&
        item.ValueKind == JsonValueKind.Number && item.TryGetInt64(out var result) && result >= 0 ? result : null;
}
