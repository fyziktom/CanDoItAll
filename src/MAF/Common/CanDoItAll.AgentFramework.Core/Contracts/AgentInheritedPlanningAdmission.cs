using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public sealed record AgentInheritedPlanningBinding(AgentToolProtocolEnvelope OriginalTarget, AgentPlanningInheritedOrigin Origin);

public sealed class AgentInheritedPlanningAdmission(AgentToolAdmissionJournal journal) {
    public async Task<AgentToolJournalRecord> CreateAsync(ExecutionRunRecord run, string originalInput, AgentPlanningOwner owner,
        Func<AgentToolSessionAdmission, AgentInheritedPlanningBinding> bind, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(bind);
        if (!Enum.IsDefined(owner)) {
            throw new ArgumentOutOfRangeException(nameof(owner));
        }
        var admitted = await journal.CreateForNewBackgroundRunAsync(run, originalInput, cancellationToken: cancellationToken);
        var binding = bind(admitted.Session) ?? throw new InvalidOperationException("The inherited planning admission returned no original binding.");
        var result = admitted with {
            SchemaVersion = AgentToolJournalRecord.InheritedPlanningSchemaVersion,
            Support = AgentToolAdmissionSupport.EffectJournalOnly,
            PlanningDecision = AgentPlanningDecisionTransitions.CreateInherited(admitted.Session.Reference, binding.OriginalTarget,
                owner, binding.Origin, journal.Clock.GetUtcNow())
        };
        result.Validate();
        return result;
    }
}
