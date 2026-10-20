using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public sealed record AgentInheritedPlanningBinding(AgentToolProtocolEnvelope OriginalTarget, AgentPlanningInheritedOrigin Origin);
public sealed record AgentInheritedPlanningBindingContext(ExecutionRunRecord Candidate, AgentToolSessionAdmission Session);

public sealed class AgentInheritedPlanningAdmission(AgentToolAdmissionJournal journal) {
    public async Task<AgentToolJournalRecord> CreateAsync(ExecutionRunRecord run, string originalInput, AgentPlanningOwner owner,
        AgentToolProtocolEnvelope originalParent,
        Func<AgentInheritedPlanningBindingContext, CancellationToken, Task<AgentInheritedPlanningBinding>> bindAsync,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(bindAsync);
        if (!Enum.IsDefined(owner)) {
            throw new ArgumentOutOfRangeException(nameof(owner));
        }
        var session = await journal.CreateForNewInheritedBackgroundSessionAsync(run, originalInput, originalParent, cancellationToken);
        var binding = await bindAsync(new(run, session), cancellationToken)
            ?? throw new InvalidOperationException("The inherited planning admission returned no original binding.");
        cancellationToken.ThrowIfCancellationRequested();
        if (binding.Origin?.ParentBinding != originalParent) {
            throw new AgentToolAdmissionException("planning.inherited-parent-changed",
                "The inherited binding changed the original parent admitted by its source owner.");
        }
        var result = new AgentToolJournalRecord(AgentToolJournalRecord.InheritedPlanningSchemaVersion, 1, session, [], [],
            Support: AgentToolAdmissionSupport.EffectJournalOnly, BackgroundInput: new(originalInput),
            PlanningDecision: AgentPlanningDecisionTransitions.CreateInherited(session.Reference, binding.OriginalTarget,
                owner, binding.Origin, journal.Clock.GetUtcNow()));
        result.Validate();
        await journal.RequireBackgroundDispatchAsync(run with { ToolAdmission = result }, cancellationToken);
        return result;
    }
}
