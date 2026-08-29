using CanDoItAll.AgentFramework.ProviderHistory;
using Microsoft.Extensions.AI;

namespace CanDoItAll.AgentFramework.Maf;

internal static class ProviderHistoryChatContext {
    private const string ContextKey = "CanDoItAll.ProviderHistory.Invocation";

    public static ChatOptions WithContext(ChatOptions? options, HistoryInvocationContext context) {
        var clone = options?.Clone() ?? new ChatOptions();
        clone.AdditionalProperties = options?.AdditionalProperties is { } properties ? new(properties) : [];
        clone.AdditionalProperties[ContextKey] = context;
        return clone;
    }

    public static ChatOptions Ensure(ChatOptions? options) =>
        Read(options) is not null ? options! : WithContext(options, HistoryInvocationContext.Create(HistoryWorkload.Agent));

    public static HistoryInvocationContext? Read(ChatOptions? options) =>
        options?.AdditionalProperties?.TryGetValue(ContextKey, out var value) == true && value is HistoryInvocationContext context
            ? context : null;

    public static ChatOptions? ForTransport(ChatOptions? options) {
        if (options?.AdditionalProperties?.ContainsKey(ContextKey) != true) {
            return options;
        }
        var clone = options.Clone();
        clone.AdditionalProperties = new(options.AdditionalProperties);
        clone.AdditionalProperties.Remove(ContextKey);
        return clone;
    }
}
