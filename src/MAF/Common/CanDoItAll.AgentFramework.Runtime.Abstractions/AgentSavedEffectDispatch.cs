using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Runtime.Abstractions;

public enum AgentSavedEffectDispatchSupport {
    Unsupported,
    OriginalPreparedOwnerEffect
}

public sealed record AgentSavedEffectDispatchRequest {
    public AgentSavedEffectDispatchRequest(AgentToolSessionReference session, AgentToolApprovalBinding binding) {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(binding);
        if (session.BackgroundSource is not null || binding.BatchId.Value == Guid.Empty || binding.IntentId.Value == Guid.Empty ||
            binding.SemanticVersion < 1 || string.IsNullOrWhiteSpace(binding.Digest.Value)) {
            throw new ArgumentException("Saved dispatch requires one exact interactive-session proposal binding.");
        }
        Session = session;
        Binding = binding;
    }

    public AgentToolSessionReference Session { get; }
    public AgentToolApprovalBinding Binding { get; }
}

public sealed record AgentSavedEffectDispatchResult {
    public AgentSavedEffectDispatchResult(AgentToolApprovalBinding binding, AgentToolProposalState state,
        AgentToolEffectState effectState, long runRevision, long journalRevision) {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentOutOfRangeException.ThrowIfLessThan(runRevision, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(journalRevision, 1);
        if (binding.BatchId.Value == Guid.Empty || binding.IntentId.Value == Guid.Empty || binding.SemanticVersion < 1 ||
            string.IsNullOrWhiteSpace(binding.Digest.Value) || !Enum.IsDefined(effectState) ||
            state is not (AgentToolProposalState.Completed or AgentToolProposalState.Rejected or AgentToolProposalState.OwnerReceiptConfirmed) ||
            state == AgentToolProposalState.Completed && effectState == AgentToolEffectState.Unknown ||
            state == AgentToolProposalState.OwnerReceiptConfirmed && effectState != AgentToolEffectState.Committed ||
            state == AgentToolProposalState.Rejected && effectState == AgentToolEffectState.Committed) {
            throw new ArgumentException("A saved dispatch result requires exact acknowledged terminal proposal knowledge.");
        }
        Binding = binding;
        State = state;
        EffectState = effectState;
        RunRevision = runRevision;
        JournalRevision = journalRevision;
    }

    public AgentToolApprovalBinding Binding { get; }
    public AgentToolProposalState State { get; }
    public AgentToolEffectState EffectState { get; }
    public long RunRevision { get; }
    public long JournalRevision { get; }
}
