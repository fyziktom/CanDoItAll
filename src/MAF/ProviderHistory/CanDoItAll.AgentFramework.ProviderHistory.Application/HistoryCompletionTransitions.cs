namespace CanDoItAll.AgentFramework.ProviderHistory;

public static class HistoryCompletionTransitions {
    public static bool ShouldApply(HistoryAttemptCompletion? existing, HistoryAttemptCompletion incoming) {
        if (incoming.Outcome == HistoryOutcome.Started) {
            throw new ArgumentException("Completion requires a terminal outcome.", nameof(incoming));
        }
        if (existing is null || existing with { ResponseOriginalBytes = null } == incoming with { ResponseOriginalBytes = null }) {
            return existing is null;
        }
        if (incoming.Outcome is HistoryOutcome.Cancelled or HistoryOutcome.TimedOut or HistoryOutcome.Interrupted &&
            existing.Outcome is HistoryOutcome.Succeeded or HistoryOutcome.Failed &&
            incoming.Usage.State <= existing.Usage.State) {
            return false;
        }
        if (existing.Outcome is HistoryOutcome.Cancelled or HistoryOutcome.TimedOut or HistoryOutcome.Interrupted &&
            incoming.Outcome is HistoryOutcome.Succeeded or HistoryOutcome.Failed &&
            incoming.Usage.State >= existing.Usage.State && PreservesEvidence(existing, incoming)) {
            return true;
        }
        throw new ProviderHistoryException(HistoryFailure.Conflict, "Conflicting terminal evidence was reported for one provider attempt.");
    }

    private static bool PreservesEvidence(HistoryAttemptCompletion previous, HistoryAttemptCompletion current) =>
        AtLeast(previous.Usage.InputTokens, current.Usage.InputTokens) &&
        AtLeast(previous.Usage.OutputTokens, current.Usage.OutputTokens) &&
        AtLeast(previous.Usage.CachedInputTokens, current.Usage.CachedInputTokens) &&
        AtLeast(previous.Usage.CacheWriteTokens, current.Usage.CacheWriteTokens) &&
        AtLeast(previous.Usage.ReasoningTokens, current.Usage.ReasoningTokens) &&
        AtLeast(previous.Usage.ImageCount, current.Usage.ImageCount) &&
        (previous.Price.Amount is null || current.Price.Amount is not null);

    private static bool AtLeast(long? previous, long? current) => previous is null || current >= previous;
}
