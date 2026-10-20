using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public sealed class AgentInheritedPlanningCoordinator(ISandboxWorkspaceExecutionRunStore runs, AgentToolAdmissionJournal journal,
    IAgentPlanningStepRuntime runtime, IEnumerable<IAgentInheritedPlanningParticipant> participants) {
    private readonly Dictionary<AgentPlanningOwner, IAgentInheritedPlanningParticipant> participants = participants.ToDictionary(item => item.Owner);
    private readonly AgentPlanningDecisionOwner owner = new(journal);

    public async Task<AgentPlanningExecutionResult> RunAsync(AgentDefinition agent, ProviderProfile provider,
        AgentPlanningDecisionExpectation expected, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(expected);
        var original = await runs.GetExecutionRunAsync(expected.ExecutionRunId, cancellationToken);
        if (original?.ToolAdmission is not { SchemaVersion: AgentToolJournalRecord.InheritedPlanningSchemaVersion,
                PlanningDecision.InheritedOrigin: not null } admitted || AgentPlanningDecisionExpectation.From(original) != expected) {
            throw new AgentToolAdmissionException("planning.inherited-original-changed", "The original inherited execution or expected revision changed.");
        }
        var decision = admitted.PlanningDecision!;
        if (!participants.TryGetValue(decision.Owner, out var participant)) {
            throw new AgentToolAdmissionException("planning.unsupported", "The inherited planning participant is not registered.");
        }
        if (!await journal.CanReadBackgroundResultAsync(original, cancellationToken)) {
            throw new AgentToolReceiptAccessDeniedException();
        }
        await participant.RequireCurrentReadAsync(decision, cancellationToken);
        var contract = AgentPlanningRunMetadata.Read(original);
        if (runtime.CaptureRuntimeContract(agent, provider, participant) != contract) {
            throw new AgentToolAdmissionException("planning.runtime-contract-changed", "The originally admitted planning provider or contract changed.");
        }
        var lease = await owner.StartInheritedAsync(expected, async (observation, token) => {
            if (observation.Session != admitted.Session || observation.Planning?.Decision.DecisionId != decision.DecisionId) {
                return false;
            }
            await participant.RequireCurrentReadAsync(observation.Planning.Decision, token);
            return true;
        }, cancellationToken);
        return await new AgentPlanningExecutionLoop(owner, runtime).RunAsync(agent, provider, lease, participant, contract,
            (work, token) => owner.InheritBasisAsync(work, participant.ReadOriginalBasisAsync, token), cancellationToken);
    }
}
