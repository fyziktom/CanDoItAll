namespace CanDoItAll.Modules.AgentFramework.ProviderManagement;

internal static class SharedProviderTerminalEvidence {
    public static bool IgnoreCancellation(SharedProviderInvocationRecord current, SharedProviderInvocationCompletion incoming) =>
        !current.FinalizationRecovered && current.Outcome is SharedProviderInvocationOutcome.Succeeded or SharedProviderInvocationOutcome.Failed &&
        incoming.Outcome == SharedProviderInvocationOutcome.Cancelled && incoming.UsageCompleteness <= current.UsageCompleteness;

    public static bool CanReconcile(SharedProviderInvocationRecord current, SharedProviderInvocationCompletion incoming) =>
        (current.FinalizationRecovered || current.Outcome == SharedProviderInvocationOutcome.Cancelled) &&
        (incoming.Outcome is SharedProviderInvocationOutcome.Succeeded or SharedProviderInvocationOutcome.Failed ||
            incoming.Outcome == SharedProviderInvocationOutcome.Cancelled && incoming.UsageCompleteness > current.UsageCompleteness) &&
        incoming.UsageCompleteness >= current.UsageCompleteness &&
        Preserves(current.InputTokenCount, incoming.InputTokenCount) && Preserves(current.OutputTokenCount, incoming.OutputTokenCount) &&
        Preserves(current.CachedInputTokenCount, incoming.CachedInputTokenCount) &&
        Preserves(current.CacheWriteTokenCount, incoming.CacheWriteTokenCount) &&
        Preserves(current.ReasoningTokenCount, incoming.ReasoningTokenCount) &&
        (current.ImageCount is null || incoming.ImageCount >= current.ImageCount) &&
        (current.Price is null || incoming.Price is not null);

    private static bool Preserves(long? current, long? incoming) => current is null || incoming >= current;
}
