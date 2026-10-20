using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Runtime.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Modules.Workbench;

internal static class ProjectTaskPlanProcessOutcome {
    internal static (ProjectTaskPlanProcessTarget Target, ProcessDecisionChildResult Outcome) Read(
        ExecutionRunRecord run, ProcessDecisionChildMaterialBinding original) {
        original.Validate();
        var journal = run.ToolAdmission ?? throw Invalid();
        journal.Validate();
        if (journal.SchemaVersion != AgentToolJournalRecord.InheritedPlanningSchemaVersion || journal.PlanningDecision is not { } decision) {
            throw Invalid();
        }
        var target = ProjectTaskPlanProcessCodec.Target(decision);
        if (ProjectTaskPlanProcessCodec.Parent(decision.InheritedOrigin!) != original ||
            run.Id != original.Child.Address.ChildId.Value || run.Id != journal.Session.Reference.ExecutionRunId ||
            run.AgentId != journal.Session.AgentId || run.ChatSessionId is not null ||
            run.CreatedAtUtc != target.Source.Evidence.ExecutionCreatedAtUtc ||
            run.SourceKind != decision.InheritedOrigin!.Source.SourceKind || run.SourceId != decision.InheritedOrigin.Source.SourceId) {
            throw Invalid();
        }
        return (target, Classify(run, journal, decision));
    }

    private static ProcessDecisionChildResult Classify(ExecutionRunRecord run, AgentToolJournalRecord journal, AgentPlanningDecisionRecord decision) {
        var proposals = journal.Batches.SelectMany(batch => batch.Proposals).ToArray();
        if (proposals.Any(proposal => proposal.Payload.ToolName != ProjectStructureToolPolicy.ProjectTaskPlanPublish)) {
            throw Invalid();
        }
        if (decision.Work.Open is not null) {
            return new ProcessDecisionChildResult.Pending(ProcessDecisionChildPendingReason.ActiveWork);
        }
        if (journal.HasUnresolvedProviderDispatch || proposals.Any(proposal =>
                proposal.State is AgentToolProposalState.Executing or AgentToolProposalState.ReconciliationRequired ||
                proposal.State != AgentToolProposalState.Prepared && proposal.EffectState == AgentToolEffectState.Unknown)) {
            return new ProcessDecisionChildResult.Pending(ProcessDecisionChildPendingReason.UnresolvedEffects);
        }
        if (decision.Current.State == AgentPlanningGenerationState.AwaitingApproval) {
            return new ProcessDecisionChildResult.Pending(ProcessDecisionChildPendingReason.ApprovalRequired);
        }
        if (decision.Current.State == AgentPlanningGenerationState.PausedBasisChanged) {
            return new ProcessDecisionChildResult.Pending(ProcessDecisionChildPendingReason.BasisChanged);
        }
        if (journal.HasUnresolvedEffects) {
            return new ProcessDecisionChildResult.Pending(ProcessDecisionChildPendingReason.UnresolvedEffects);
        }
        var terminal = decision.Current.State switch {
            AgentPlanningGenerationState.Completed => ProcessDecisionChildTerminalStatus.Succeeded,
            AgentPlanningGenerationState.Failed => ProcessDecisionChildTerminalStatus.Failed,
            AgentPlanningGenerationState.Cancelled => ProcessDecisionChildTerminalStatus.Cancelled,
            _ => (ProcessDecisionChildTerminalStatus?)null
        };
        if (terminal is null) {
            return new ProcessDecisionChildResult.Pending(ProcessDecisionChildPendingReason.ExecutionIncomplete);
        }
        var expectedOutcome = terminal switch {
            ProcessDecisionChildTerminalStatus.Succeeded => RunOutcome.Succeeded,
            ProcessDecisionChildTerminalStatus.Failed => RunOutcome.Failed,
            _ => RunOutcome.Cancelled
        };
        if (run.Outcome != expectedOutcome || run.State != (expectedOutcome == RunOutcome.Failed ? ExecutionState.Failed : ExecutionState.Completed) ||
            run.CompletedAtUtc is not { } completed || completed < decision.Work.LastObservedAtUtc || run.PendingApprovals.Count != 0 ||
            proposals.Any(proposal => proposal.EffectState is not (AgentToolEffectState.Committed or AgentToolEffectState.NotCommitted))) {
            throw Invalid();
        }
        var committed = proposals.Where(proposal => proposal.EffectState == AgentToolEffectState.Committed).ToArray();
        if (committed.Length > 1) {
            throw Invalid();
        }
        ProcessDecisionEffectReceiptId? receipt = committed.Length == 0 ? null : ReadReceipt(run, journal, decision, committed[0]);
        return new ProcessDecisionChildResult.Closed(terminal.Value,
            new(decision.Work.Revision, receipt is null ? ProcessDecisionChildOutcome.NoEffect : ProcessDecisionChildOutcome.Accepted, receipt),
            new(decision.Work.Revision, new(decision.Work.ClosedWorkTicks, decision.AutomaticReplans)));
    }

    private static ProcessDecisionEffectReceiptId ReadReceipt(ExecutionRunRecord run, AgentToolJournalRecord journal,
        AgentPlanningDecisionRecord decision, AgentToolProposalRecord proposal) {
        if (proposal.State is not (AgentToolProposalState.Completed or AgentToolProposalState.OwnerReceiptConfirmed or AgentToolProposalState.Cancelled) ||
            proposal.DispatchClaimId is null || proposal.ApprovalStatus != ExecutionApprovalStatus.Approved || proposal.ApprovedDigest != proposal.Payload.Digest) {
            throw Invalid();
        }
        var batch = journal.Batches.Single(item => item.Proposals.Any(saved => saved.IntentId == proposal.IntentId));
        AgentToolApprovalBinding binding = new(batch.Id, proposal.IntentId, proposal.Payload.SemanticVersion, proposal.Payload.Digest);
        var command = ProjectTaskPlanProcessCodec.Resolve(decision, journal.Session, proposal.Payload, binding).Command;
        var owner = proposal.OwnerReceipt ?? (proposal.Cancellation is { Disposition: AgentToolCancellationDisposition.ReceiptCommitted } cancelled
            ? new AgentToolOwnerReceipt(cancelled.CommittedEffect!, cancelled.Receipt!) : null);
        if (owner is not null && (owner.Effect != new AgentToolCommittedEffect(ProjectTaskPlanAgentCodec.EffectSourceKind, command.Operation.Id.ToString("D")) ||
            ProjectTaskPlanAgentResult.Classify(command, new(binding, AgentToolProposalState.OwnerReceiptConfirmed,
                AgentToolEffectState.Committed, run.Revision, journal.Revision, owner.Receipt)).Outcome != AgentPlanningPublicationOutcome.Committed)) {
            throw Invalid();
        }
        return new(command.Operation.Id);
    }

    private static InvalidDataException Invalid() => new("The original Process task-plan outcome has inconsistent child, work or effect evidence.");
}
