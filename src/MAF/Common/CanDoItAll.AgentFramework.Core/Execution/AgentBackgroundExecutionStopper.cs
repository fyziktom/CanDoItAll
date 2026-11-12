using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public sealed class AgentBackgroundExecutionStopper(ISandboxWorkspaceExecutionRunStore reader,
    ISandboxWorkspaceExecutionRunMutationStore writer, ISandboxWorkspaceExecutionRunLeaseStore leases,
    IAgentExecutionCancellationRegistry cancellation, TimeProvider clock) {
    public Task<AgentParentStopOutcome> StopAsync(Guid executionRunId, AgentBackgroundParentStop parent,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(parent);
        return new AgentParentStopProtocol(reader, writer, leases, cancellation, clock).StopAsync(executionRunId,
            parent.RequireOriginal, parent.RequireCurrentAsync, Observe, static run => run.UpdatedAtUtc, cancellationToken);
    }

    private static AgentParentStopOutcome? Observe(ExecutionRunRecord run) {
        var journal = run.ToolAdmission!;
        if (journal.Support == AgentToolAdmissionSupport.RequestScopedInput) {
            return AgentParentStopOutcome.UnresolvedEffects;
        }
        var proposals = journal.Batches.SelectMany(batch => batch.Proposals).ToArray();
        if (journal.HasUnresolvedProviderDispatch || proposals.Any(proposal =>
                proposal.State is AgentToolProposalState.Executing or AgentToolProposalState.ReconciliationRequired ||
                proposal.State != AgentToolProposalState.Prepared && proposal.EffectState == AgentToolEffectState.Unknown)) {
            return AgentParentStopOutcome.UnresolvedEffects;
        }
        var dispatches = journal.ProviderDispatches.IsDefault ? [] : journal.ProviderDispatches;
        if (journal.SchemaVersion == AgentToolJournalRecord.BackgroundSchemaVersion &&
                journal.ActiveDispatchLeaseId is not null && journal.Segments.IsEmpty ||
                journal.Segments.Any(segment => segment.CheckpointKind == AgentToolCheckpointKind.ProviderRestart &&
                    segment.ProviderEffectScope != AgentToolProviderEffectScope.LocalProposalsOnly &&
                    !dispatches.Any(dispatch => dispatch.SegmentId == segment.Id))) {
            return AgentParentStopOutcome.UnresolvedEffects;
        }
        if (run.State is ExecutionState.Completed or ExecutionState.Failed) {
            if (journal.HasUnresolvedEffects) {
                return AgentParentStopOutcome.UnresolvedEffects;
            }
            if (run.CompletedAtUtc is not { } completed || completed < run.CreatedAtUtc || run.PendingApprovals.Count != 0 ||
                    (run.Outcome, run.State) is not ((RunOutcome.Succeeded, ExecutionState.Completed) or
                     (RunOutcome.Failed, ExecutionState.Failed) or (RunOutcome.Cancelled, ExecutionState.Completed or ExecutionState.Failed))) {
                throw new InvalidDataException("The original terminal Agent outcome is inconsistent with its closed background journal.");
            }
            return AgentParentStopOutcome.AlreadyClosed;
        }
        if (run.Outcome is not null || run.CompletedAtUtc is not null) {
            throw new InvalidDataException("An active background execution cannot carry a terminal outcome.");
        }
        if (proposals.Any(proposal => proposal.EffectState == AgentToolEffectState.Committed)) {
            return AgentParentStopOutcome.CompletionPending;
        }
        return null;
    }
}
