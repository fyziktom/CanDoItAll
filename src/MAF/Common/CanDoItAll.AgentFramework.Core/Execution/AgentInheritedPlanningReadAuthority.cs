using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

internal sealed class AgentInheritedPlanningReadAuthority(AgentToolAdmissionJournal journal,
    IEnumerable<IAgentInheritedPlanningParticipant> participants) {
    private readonly IAgentInheritedPlanningParticipant[] participants = participants.ToArray();

    internal async Task RequireCurrentAsync(ExecutionRunRecord original, CancellationToken cancellationToken) {
        if (original.ToolAdmission is not { SchemaVersion: AgentToolJournalRecord.InheritedPlanningSchemaVersion,
                PlanningDecision.InheritedOrigin: not null } admission || !await journal.CanReadBackgroundResultAsync(original, cancellationToken)) {
            throw new AgentToolReceiptAccessDeniedException();
        }
        await RequireParticipant(admission.PlanningDecision!.Owner, admission.PlanningDecision.InheritedOrigin!.ParentBinding)
            .RequireCurrentReadAsync(admission.PlanningDecision, cancellationToken);
    }

    internal IAgentInheritedPlanningParticipant RequireParticipant(AgentPlanningOwner selection, AgentToolProtocolEnvelope parent) {
        var matches = participants.Where(participant => participant.Owner == selection && participant.SupportsParent(parent))
            .Take(2).ToArray();
        return matches.Length switch {
            1 => matches[0],
            0 => throw new AgentToolAdmissionException("planning.unsupported", "No inherited planning participant supports the original parent."),
            _ => throw new AgentToolAdmissionException("planning.ambiguous-participant", "Multiple inherited planning participants support the original parent.")
        };
    }
}
