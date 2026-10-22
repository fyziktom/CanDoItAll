using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public sealed class AgentInheritedPlanningStopper(ISandboxWorkspaceExecutionRunStore reader,
    ISandboxWorkspaceExecutionRunMutationStore writer, ISandboxWorkspaceExecutionRunLeaseStore leases,
    IAgentExecutionCancellationRegistry cancellation, TimeProvider clock) {
    public Task<AgentParentStopOutcome> StopAsync(Guid executionRunId, AgentInheritedParentStop parent,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(parent);
        return new AgentParentStopProtocol(reader, writer, leases, cancellation, clock).StopAsync(executionRunId,
            parent.RequireOriginal, parent.RequireCurrentAsync, Observe,
            static run => run.ToolAdmission!.PlanningDecision!.Work.LastObservedAtUtc, cancellationToken);
    }

    private static AgentParentStopOutcome? Observe(ExecutionRunRecord run) {
        var journal = run.ToolAdmission!;
        var decision = journal.PlanningDecision!;
        if (decision.Work.Open is not null) {
            return AgentParentStopOutcome.OpenWork;
        }
        var proposals = journal.Batches.SelectMany(batch => batch.Proposals).ToArray();
        if (journal.HasUnresolvedProviderDispatch || proposals.Any(proposal =>
                proposal.State is AgentToolProposalState.Executing or AgentToolProposalState.ReconciliationRequired ||
                proposal.State != AgentToolProposalState.Prepared && proposal.EffectState == AgentToolEffectState.Unknown)) {
            return AgentParentStopOutcome.UnresolvedEffects;
        }
        var expected = decision.Current.State switch {
            AgentPlanningGenerationState.Completed => RunOutcome.Succeeded,
            AgentPlanningGenerationState.Failed => RunOutcome.Failed,
            AgentPlanningGenerationState.Cancelled => RunOutcome.Cancelled,
            _ => (RunOutcome?)null
        };
        if (expected is { } terminal) {
            if (journal.HasUnresolvedEffects) {
                return AgentParentStopOutcome.UnresolvedEffects;
            }
            if (run.Outcome != terminal || run.CompletedAtUtc is not { } completed || completed < decision.Work.LastObservedAtUtc ||
                    run.PendingApprovals.Count != 0 || (terminal, run.State) is not
                    ((RunOutcome.Succeeded, ExecutionState.Completed) or (RunOutcome.Failed, ExecutionState.Failed) or
                     (RunOutcome.Cancelled, ExecutionState.Completed or ExecutionState.Failed))) {
                throw new InvalidDataException("The original terminal Agent outcome is inconsistent with its closed planning journal.");
            }
            return AgentParentStopOutcome.AlreadyClosed;
        }
        if (proposals.Any(proposal => proposal.EffectState == AgentToolEffectState.Committed)) {
            return AgentParentStopOutcome.CompletionPending;
        }
        if (decision.Current.State is not (AgentPlanningGenerationState.Active or AgentPlanningGenerationState.AwaitingApproval or
                AgentPlanningGenerationState.PausedBasisChanged)) {
            throw new InvalidDataException("The original planning generation has no supported parent-stop transition.");
        }
        return null;
    }
}
