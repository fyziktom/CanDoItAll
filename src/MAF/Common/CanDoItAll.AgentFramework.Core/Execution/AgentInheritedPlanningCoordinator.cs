using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public sealed class AgentInheritedPlanningCoordinator(ISandboxWorkspaceExecutionRunStore runs, AgentToolAdmissionJournal journal,
    IAgentPlanningStepRuntime runtime, IEnumerable<IAgentInheritedPlanningParticipant> participants) {
    private readonly IAgentInheritedPlanningParticipant[] participants = participants.ToArray();
    private readonly AgentPlanningDecisionOwner owner = new(journal);

    public Task<AgentPlanningExecutionResult> RunAsync(AgentDefinition agent, ProviderProfile provider,
        AgentPlanningDecisionExpectation expected, CancellationToken cancellationToken = default)
        => RunWithinLifecycleAsync(agent, provider, expected, (execution, token) => execution.RunAsync(token), cancellationToken);

    internal AgentToolSemanticDigest CaptureRuntimeContract(AgentPlanningOwner selection, AgentToolProtocolEnvelope parent,
        AgentDefinition agent, ProviderProfile provider)
        => runtime.CaptureRuntimeContract(agent, provider, RequireParticipant(selection, parent));

    internal async Task RequireCurrentReadAsync(ExecutionRunRecord original, CancellationToken cancellationToken) {
        if (original.ToolAdmission is not { SchemaVersion: AgentToolJournalRecord.InheritedPlanningSchemaVersion,
                PlanningDecision.InheritedOrigin: not null } admission || !await journal.CanReadBackgroundResultAsync(original, cancellationToken)) {
            throw new AgentToolReceiptAccessDeniedException();
        }
        await RequireParticipant(admission.PlanningDecision!.Owner, admission.PlanningDecision.InheritedOrigin!.ParentBinding).RequireCurrentReadAsync(admission.PlanningDecision, cancellationToken);
    }

    internal Task<AgentInheritedPlanningBinding> BindInitialAsync(AgentPlanningOwner selection,
        AgentInheritedPlanningBindingContext context, AgentToolProtocolEnvelope parent, AgentToolProtocolEnvelope sourceEvidence,
        CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        if (RequireParticipant(selection, parent) is not IAgentInheritedPlanningInitialBinder binder) {
            throw new AgentToolAdmissionException("planning.initial-binding-unsupported",
                "The registered inherited participant cannot bind an initial execution.");
        }
        return binder.BindInitialAsync(context, parent, sourceEvidence, cancellationToken);
    }

    private IAgentInheritedPlanningParticipant RequireParticipant(AgentPlanningOwner selection, AgentToolProtocolEnvelope parent) {
        var matches = participants.Where(participant => participant.Owner == selection && participant.SupportsParent(parent))
            .Take(2).ToArray();
        return matches.Length switch {
            1 => matches[0],
            0 => throw new AgentToolAdmissionException("planning.unsupported", "No inherited planning participant supports the original parent."),
            _ => throw new AgentToolAdmissionException("planning.ambiguous-participant", "Multiple inherited planning participants support the original parent.")
        };
    }

    public Task<T> RunWithinLifecycleAsync<T>(AgentDefinition agent, ProviderProfile provider,
        AgentPlanningDecisionExpectation expected, Func<AgentInheritedPlanningExecution, CancellationToken, Task<T>> lifecycle,
        CancellationToken cancellationToken = default)
        => RunCoreAsync(expected, (_, _) => Task.FromResult((agent, provider)), lifecycle, (agent, provider), cancellationToken);

    internal Task<T> RunPreparingWithinLifecycleAsync<T>(AgentPlanningDecisionExpectation expected,
        Func<ExecutionRunRecord, CancellationToken, Task<(AgentDefinition Agent, ProviderProfile Provider)>> prepare,
        Func<AgentInheritedPlanningExecution, CancellationToken, Task<T>> lifecycle, CancellationToken cancellationToken)
        => RunCoreAsync(expected, prepare, lifecycle, null, cancellationToken);

    private async Task<T> RunCoreAsync<T>(AgentPlanningDecisionExpectation expected,
        Func<ExecutionRunRecord, CancellationToken, Task<(AgentDefinition Agent, ProviderProfile Provider)>> prepare,
        Func<AgentInheritedPlanningExecution, CancellationToken, Task<T>> lifecycle,
        (AgentDefinition Agent, ProviderProfile Provider)? prepared, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(prepare);
        ArgumentNullException.ThrowIfNull(lifecycle);
        var original = await runs.GetExecutionRunAsync(expected.ExecutionRunId, cancellationToken);
        if (original?.ToolAdmission is not { SchemaVersion: AgentToolJournalRecord.InheritedPlanningSchemaVersion,
                PlanningDecision.InheritedOrigin: not null } admitted || AgentPlanningDecisionExpectation.From(original) != expected) {
            throw new AgentToolAdmissionException("planning.inherited-original-changed", "The original inherited execution or expected revision changed.");
        }
        var decision = admitted.PlanningDecision!;
        var participant = RequireParticipant(decision.Owner, decision.InheritedOrigin!.ParentBinding);
        await RequireCurrentReadAsync(original, cancellationToken);
        var contract = AgentPlanningRunMetadata.Read(original);
        if (prepared is { } ready) {
            RequireContract(ready.Agent, ready.Provider, participant, contract);
        }
        var lease = await owner.StartInheritedAsync(expected, async (observation, token) => {
            if (observation.Session != admitted.Session || observation.Planning?.Decision.DecisionId != decision.DecisionId) {
                return false;
            }
            await participant.RequireCurrentReadAsync(observation.Planning.Decision, token);
            return true;
        }, cancellationToken);
        var loop = new AgentPlanningExecutionLoop(owner, runtime);
        return await AgentPlanningExecutionLoop.WithLeaseAsync(lease, async work => {
            using var deadline = new CancellationTokenSource(Timeout.InfiniteTimeSpan, journal.Clock);
            using var bounded = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
            (AgentDefinition Agent, ProviderProfile Provider) selected;
            try {
                deadline.CancelAfter(Remaining(work));
                bounded.Token.ThrowIfCancellationRequested();
                selected = await prepare(work.OriginalRun, bounded.Token);
                RequireContract(selected.Agent, selected.Provider, participant, contract);
                _ = Remaining(work);
                bounded.Token.ThrowIfCancellationRequested();
            } catch (Exception primary) {
                try {
                    using var settlement = new CancellationTokenSource(TimeSpan.FromSeconds(5), journal.Clock);
                    await owner.SettleTerminalAsync(work, cancellationToken.IsCancellationRequested
                        ? AgentPlanningGenerationState.Cancelled : AgentPlanningGenerationState.Failed, settlement.Token);
                } catch (Exception cleanup) {
                    throw new AggregateException(primary, cleanup);
                }
                throw;
            }
            return await lifecycle(new(lease,
                token => loop.RunBoundAsync(selected.Agent, selected.Provider, work, participant, contract,
                    (current, currentToken) => owner.InheritBasisAsync(current, participant.ReadOriginalBasisAsync, currentToken), token)),
                bounded.Token);
        });
    }

    private TimeSpan Remaining(AgentPlanningLiveWork work)
        => AgentPlanningDecisionTransitions.RemainingWork(work.OriginalRun.ToolAdmission!.PlanningDecision!,
            work.ObserveElapsed(journal.Clock, journal.Clock.GetTimestamp()), journal.Clock.GetUtcNow());

    private void RequireContract(AgentDefinition agent, ProviderProfile provider, IAgentInheritedPlanningParticipant participant,
        AgentToolSemanticDigest expected) {
        if (runtime.CaptureRuntimeContract(agent, provider, participant) != expected) {
            throw new AgentToolAdmissionException("planning.runtime-contract-changed", "The originally admitted planning provider or contract changed.");
        }
    }
}
