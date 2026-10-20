using System.Text;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Runtime.Abstractions;

public enum AgentSavedEffectDispatchSupport {
    Unsupported,
    OriginalPreparedOwnerEffect
}

public sealed record AgentSavedEffectDispatchRequest {
    public AgentSavedEffectDispatchRequest(AgentToolSessionReference session, AgentToolApprovalBinding binding)
        : this(session, binding, AgentRuntimeContextPurpose.InteractiveChat) { }

    public static AgentSavedEffectDispatchRequest ForInherited(AgentToolSessionReference session, AgentToolApprovalBinding binding)
        => new(session, binding, AgentRuntimeContextPurpose.GovernedProcessAutomation);

    private AgentSavedEffectDispatchRequest(AgentToolSessionReference session, AgentToolApprovalBinding binding,
        AgentRuntimeContextPurpose purpose) {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(binding);
        var originalSession = purpose switch {
            AgentRuntimeContextPurpose.InteractiveChat => session.BackgroundSource is null,
            AgentRuntimeContextPurpose.GovernedProcessAutomation => session.BackgroundSource is not null &&
                session.ChatSessionId == Guid.Empty && session.AuthorityId.Value == Guid.Empty,
            _ => false
        };
        if (!originalSession || binding.BatchId.Value == Guid.Empty || binding.IntentId.Value == Guid.Empty ||
            binding.SemanticVersion < 1 || string.IsNullOrWhiteSpace(binding.Digest.Value)) {
            throw new ArgumentException("Saved dispatch requires an exact proposal binding and its original session kind.");
        }
        Session = session;
        Binding = binding;
    }

    public AgentToolSessionReference Session { get; }
    public AgentToolApprovalBinding Binding { get; }
}

public sealed record AgentSavedEffectDispatchResult {
    public AgentSavedEffectDispatchResult(AgentToolApprovalBinding binding, AgentToolProposalState state,
        AgentToolEffectState effectState, long runRevision, long journalRevision, AgentToolProtocolEnvelope? ownerResult = null) {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentOutOfRangeException.ThrowIfLessThan(runRevision, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(journalRevision, 1);
        if (ownerResult is not null) {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(Encoding.UTF8.GetByteCount(ownerResult.PayloadJson), 65_536);
        }
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
        OwnerResult = ownerResult;
    }

    public AgentToolApprovalBinding Binding { get; }
    public AgentToolProposalState State { get; }
    public AgentToolEffectState EffectState { get; }
    public long RunRevision { get; }
    public long JournalRevision { get; }
    public AgentToolProtocolEnvelope? OwnerResult { get; }
}
