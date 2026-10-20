using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public sealed record AgentInheritedPlanningBinding(AgentToolProtocolEnvelope OriginalTarget, AgentPlanningInheritedOrigin Origin);
public sealed record AgentInheritedPlanningBindingContext(ExecutionRunRecord Candidate, AgentToolSessionAdmission Session);

public sealed class AgentInheritedPlanningAdmission(AgentToolAdmissionJournal journal) {
    public async Task<AgentToolJournalRecord> CreateAsync(ExecutionRunRecord run, string originalInput, AgentPlanningOwner owner,
        Func<AgentInheritedPlanningBindingContext, CancellationToken, Task<AgentInheritedPlanningBinding>> bindAsync,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(bindAsync);
        if (!Enum.IsDefined(owner)) {
            throw new ArgumentOutOfRangeException(nameof(owner));
        }
        var admitted = await journal.CreateForNewBackgroundRunAsync(run, originalInput, cancellationToken: cancellationToken);
        var binding = await bindAsync(new(run, admitted.Session), cancellationToken)
            ?? throw new InvalidOperationException("The inherited planning admission returned no original binding.");
        cancellationToken.ThrowIfCancellationRequested();
        var result = admitted with {
            SchemaVersion = AgentToolJournalRecord.InheritedPlanningSchemaVersion,
            Support = AgentToolAdmissionSupport.EffectJournalOnly,
            PlanningDecision = AgentPlanningDecisionTransitions.CreateInherited(admitted.Session.Reference, binding.OriginalTarget,
                owner, binding.Origin, journal.Clock.GetUtcNow())
        };
        result.Validate();
        await journal.RequireBackgroundDispatchAsync(run with { ToolAdmission = result }, cancellationToken);
        return result;
    }
}
