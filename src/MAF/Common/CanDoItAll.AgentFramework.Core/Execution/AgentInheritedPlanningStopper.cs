using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public enum AgentInheritedPlanningStopOutcome {
    Stopped, AlreadyClosed, Signalled, LeaseBusy, MissingJournal, OpenWork, UnresolvedEffects, CompletionPending
}

public sealed class AgentInheritedPlanningStopper(ISandboxWorkspaceExecutionRunStore reader,
    ISandboxWorkspaceExecutionRunMutationStore writer, ISandboxWorkspaceExecutionRunLeaseStore leases,
    IAgentExecutionCancellationRegistry cancellation, TimeProvider clock) {
    public async Task<AgentInheritedPlanningStopOutcome> StopAsync(Guid executionRunId, AgentInheritedParentStop parent,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(parent);
        if (executionRunId == Guid.Empty) {
            throw new ArgumentException("Parent stopping requires the original execution identity.", nameof(executionRunId));
        }
        cancellationToken.ThrowIfCancellationRequested();
        if (await reader.GetExecutionRunDetailAsync(executionRunId, cancellationToken).ConfigureAwait(false) is not { } original) {
            return AgentInheritedPlanningStopOutcome.MissingJournal;
        }
        await parent.RequireCurrentAsync(original.Run, cancellationToken).ConfigureAwait(false);
        if (cancellation.RequestCancellation(executionRunId)) {
            return AgentInheritedPlanningStopOutcome.Signalled;
        }
        using var acquisition = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        acquisition.CancelAfter(TimeSpan.FromMilliseconds(250));
        IAsyncDisposable handle;
        try {
            handle = await leases.AcquireToolDispatchLeaseAsync(executionRunId, acquisition.Token).ConfigureAwait(false);
        } catch (OperationCanceledException) when (acquisition.IsCancellationRequested && !cancellationToken.IsCancellationRequested) {
            return AgentInheritedPlanningStopOutcome.LeaseBusy;
        }
        await using var held = handle;
        var observed = (await reader.GetExecutionRunDetailAsync(executionRunId, cancellationToken).ConfigureAwait(false))?.Run
            ?? throw new InvalidDataException("The original execution disappeared while acquiring its stop lease.");
        await parent.RequireCurrentAsync(observed, cancellationToken).ConfigureAwait(false);
        if (Observe(observed) is { } pending) {
            return pending;
        }
        var outcome = AgentInheritedPlanningStopOutcome.Stopped;
        await writer.UpdateExecutionRunDetailAsync(executionRunId, current => {
            parent.RequireOriginal(current.Run);
            observed = current.Run;
            if (Observe(current.Run) is { } changed) {
                outcome = changed;
                return current;
            }
            var now = clock.GetUtcNow();
            if (now < current.Run.UpdatedAtUtc || now < current.Run.ToolAdmission!.PlanningDecision!.Work.LastObservedAtUtc) {
                throw new InvalidDataException("Parent cancellation cannot precede the original execution's observed work.");
            }
            return AgentSavedEffectCancellation.ApplyParentStop(current, now);
        }, token => parent.RequireCurrentAsync(observed, token), cancellationToken).ConfigureAwait(false);
        return outcome;
    }

    private static AgentInheritedPlanningStopOutcome? Observe(ExecutionRunRecord run) {
        var journal = run.ToolAdmission!;
        var decision = journal.PlanningDecision!;
        if (decision.Work.Open is not null) {
            return AgentInheritedPlanningStopOutcome.OpenWork;
        }
        var proposals = journal.Batches.SelectMany(batch => batch.Proposals).ToArray();
        if (journal.HasUnresolvedProviderDispatch || proposals.Any(proposal =>
                proposal.State is AgentToolProposalState.Executing or AgentToolProposalState.ReconciliationRequired ||
                proposal.State != AgentToolProposalState.Prepared && proposal.EffectState == AgentToolEffectState.Unknown)) {
            return AgentInheritedPlanningStopOutcome.UnresolvedEffects;
        }
        var expected = decision.Current.State switch {
            AgentPlanningGenerationState.Completed => RunOutcome.Succeeded,
            AgentPlanningGenerationState.Failed => RunOutcome.Failed,
            AgentPlanningGenerationState.Cancelled => RunOutcome.Cancelled,
            _ => (RunOutcome?)null
        };
        if (expected is { } terminal) {
            if (journal.HasUnresolvedEffects) {
                return AgentInheritedPlanningStopOutcome.UnresolvedEffects;
            }
            if (run.Outcome != terminal || run.CompletedAtUtc is not { } completed || completed < decision.Work.LastObservedAtUtc ||
                    run.PendingApprovals.Count != 0 || (terminal, run.State) is not
                    ((RunOutcome.Succeeded, ExecutionState.Completed) or (RunOutcome.Failed, ExecutionState.Failed) or
                     (RunOutcome.Cancelled, ExecutionState.Completed or ExecutionState.Failed))) {
                throw new InvalidDataException("The original terminal Agent outcome is inconsistent with its closed planning journal.");
            }
            return AgentInheritedPlanningStopOutcome.AlreadyClosed;
        }
        if (proposals.Any(proposal => proposal.EffectState == AgentToolEffectState.Committed)) {
            return AgentInheritedPlanningStopOutcome.CompletionPending;
        }
        if (decision.Current.State is not (AgentPlanningGenerationState.Active or AgentPlanningGenerationState.AwaitingApproval or
                AgentPlanningGenerationState.PausedBasisChanged)) {
            throw new InvalidDataException("The original planning generation has no supported parent-stop transition.");
        }
        return null;
    }
}
