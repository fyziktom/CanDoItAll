using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.UI.Chat;

public enum AgentChatReceiptReadState { Observed, Denied, Unavailable }

public sealed record AgentChatEffectRecovery(AgentEffectContinuation Latest, int KnownCommittedAtLeast,
    AgentChatReceiptReadState ReadState) {
    public bool LeaseReleaseUnconfirmed { get; init; }
    public bool ActivityFinalizationFailed { get; init; }
    public const string ReleaseUnconfirmedMessage = "Release of the original run lease is unconfirmed. Further recovery is blocked pending operational inspection; reading the snapshot cannot confirm release.";
    public const string ActivityFinalizationFailedMessage = "Recovery activity could not be finalized. Further continuation is blocked pending operational inspection; reading the snapshot cannot confirm activity cleanup.";
    public string ReleaseStatus => ReleaseUnconfirmedMessage;

    public static AgentChatEffectRecovery Observe(AgentChatEffectRecovery? previous, AgentEffectContinuation current) {
        ArgumentNullException.ThrowIfNull(current);
        if (previous is not null && (previous.Latest.ExecutionRunId != current.ExecutionRunId ||
                previous.Latest.ChatSessionId != current.ChatSessionId)) {
            throw new InvalidOperationException("Receipt observations belong to a different run or thread.");
        }
        var committed = current.Counts.KnownCommitted + current.Observations.Count(item => !item.Persisted &&
            item.Knowledge is AgentEffectReceiptKnowledge.ReceiptConfirmed or AgentEffectReceiptKnowledge.KnownCommitted or
                AgentEffectReceiptKnowledge.KnownCommittedDetailUnavailable or AgentEffectReceiptKnowledge.KnownCommittedDetailDenied);
        return new(current, Math.Max(previous?.KnownCommittedAtLeast ?? 0, committed), AgentChatReceiptReadState.Observed) {
            LeaseReleaseUnconfirmed = previous?.LeaseReleaseUnconfirmed == true,
            ActivityFinalizationFailed = previous?.ActivityFinalizationFailed == true
        };
    }

    public string Status => ReadState switch {
        AgentChatReceiptReadState.Denied => "Current access to receipt details was denied. Previously observed commitment remains known.",
        AgentChatReceiptReadState.Unavailable => "Current receipt access could not be verified. Previously observed commitment remains known.",
        _ when Latest.Persistence != AgentEffectCheckpointPersistence.Confirmed =>
            "Receipt observations were obtained, but the execution checkpoint is not confirmed. Keep the original effect identities.",
        _ when Latest.Stop == AgentEffectReconciliationStop.SnapshotRead =>
            "The current execution snapshot was read. No receipt lookup or continuation was performed by that read.",
        _ => "The run is awaiting explicit continuation. No assistant response was produced by receipt recovery."
    };
}
