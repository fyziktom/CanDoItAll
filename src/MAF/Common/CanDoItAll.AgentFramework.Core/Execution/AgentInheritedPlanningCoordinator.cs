using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public sealed class AgentInheritedPlanningCoordinator(ISandboxWorkspaceExecutionRunStore runs, AgentToolAdmissionJournal journal,
    IAgentPlanningStepRuntime runtime, IEnumerable<IAgentInheritedPlanningParticipant> participants) {
    private readonly Dictionary<AgentPlanningOwner, IAgentInheritedPlanningParticipant> participants = participants.ToDictionary(item => item.Owner);
    private readonly AgentPlanningDecisionOwner owner = new(journal);

    public Task<AgentPlanningExecutionResult> RunAsync(AgentDefinition agent, ProviderProfile provider,
        AgentPlanningDecisionExpectation expected, CancellationToken cancellationToken = default)
        => RunWithinLifecycleAsync(agent, provider, expected, (execution, token) => execution.RunAsync(token), cancellationToken);

    internal AgentToolSemanticDigest CaptureRuntimeContract(AgentPlanningOwner selection, AgentDefinition agent, ProviderProfile provider)
        => runtime.CaptureRuntimeContract(agent, provider, RequireParticipant(selection));

    internal async Task RequireCurrentReadAsync(ExecutionRunRecord original, CancellationToken cancellationToken) {
        if (original.ToolAdmission is not { SchemaVersion: AgentToolJournalRecord.InheritedPlanningSchemaVersion,
                PlanningDecision.InheritedOrigin: not null } admission || !await journal.CanReadBackgroundResultAsync(original, cancellationToken)) {
            throw new AgentToolReceiptAccessDeniedException();
        }
        await RequireParticipant(admission.PlanningDecision!.Owner).RequireCurrentReadAsync(admission.PlanningDecision, cancellationToken);
    }

    private IAgentInheritedPlanningParticipant RequireParticipant(AgentPlanningOwner selection)
        => participants.TryGetValue(selection, out var participant) ? participant
            : throw new AgentToolAdmissionException("planning.unsupported", "The inherited planning participant is not registered.");

    public async Task<T> RunWithinLifecycleAsync<T>(AgentDefinition agent, ProviderProfile provider,
        AgentPlanningDecisionExpectation expected, Func<AgentInheritedPlanningExecution, CancellationToken, Task<T>> lifecycle,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(lifecycle);
        var original = await runs.GetExecutionRunAsync(expected.ExecutionRunId, cancellationToken);
        if (original?.ToolAdmission is not { SchemaVersion: AgentToolJournalRecord.InheritedPlanningSchemaVersion,
                PlanningDecision.InheritedOrigin: not null } admitted || AgentPlanningDecisionExpectation.From(original) != expected) {
            throw new AgentToolAdmissionException("planning.inherited-original-changed", "The original inherited execution or expected revision changed.");
        }
        var decision = admitted.PlanningDecision!;
        var participant = RequireParticipant(decision.Owner);
        await RequireCurrentReadAsync(original, cancellationToken);
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
        var loop = new AgentPlanningExecutionLoop(owner, runtime);
        return await AgentPlanningExecutionLoop.WithLeaseAsync(lease, work => lifecycle(new(lease,
            token => loop.RunBoundAsync(agent, provider, work, participant, contract,
                (current, currentToken) => owner.InheritBasisAsync(current, participant.ReadOriginalBasisAsync, currentToken), token)),
            cancellationToken));
    }
}
