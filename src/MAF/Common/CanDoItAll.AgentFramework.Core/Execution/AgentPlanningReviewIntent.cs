using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.AgentFramework.Core;

internal sealed class AgentPlanningReviewIntent {
    internal const string SourceKind = "inherited-planning-review-v1";
    private readonly AgentInheritedPlanningReviewRequest request;

    internal AgentPlanningReviewIntent(AgentInheritedPlanningReviewRequest request, ExecutionRunRecord original,
        AgentToolSemanticDigest ownerIntent) {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerIntent.Value);
        this.request = request;
        var admission = original.ToolAdmission!;
        Digest = AgentToolProtocolEnvelope.ComputeDigest(JsonSerializer.Serialize(new {
            Version = 1,
            Request = request,
            OwnerIntent = ownerIntent,
            admission.PlanningDecision!.InheritedOrigin,
            Target = new {
                admission.PlanningDecision.OriginalTarget.Format,
                admission.PlanningDecision.OriginalTarget.Version,
                admission.PlanningDecision.OriginalTarget.Digest
            },
            admission.Session,
            original.AgentId,
            original.ChatSessionId,
            original.CreatedAtUtc,
            original.SourceKind,
            original.SourceId,
            original.ProcessRunId,
            original.ProcessStepId,
            MetadataDigest = AgentToolProtocolEnvelope.ComputeDigest(original.MetadataJson)
        }));
    }

    internal AgentToolSemanticDigest Digest { get; }
    private ExecutionApprovalStatus Status => request.Choice == AgentPlanningReviewChoice.Approve
        ? ExecutionApprovalStatus.Approved : ExecutionApprovalStatus.Rejected;

    internal bool IsReplay(ExecutionRunDetail detail) {
        var approval = detail.Approvals.SingleOrDefault(item => item.ApprovalId == request.ApprovalId);
        if (approval is null || approval.Status == ExecutionApprovalStatus.Pending) {
            return false;
        }
        var proposal = RequirePublication(detail.Run);
        if (approval.ExecutionRunId != detail.Run.Id || approval.ToolAdmission != request.Binding ||
            approval.DecisionSourceKind != SourceKind || approval.DecisionSourceId != Digest.Value ||
            approval.Status != Status || approval.DecidedAtUtc is null || proposal.ApprovalStatus != Status ||
            approval.CallId != proposal.CallId || approval.ToolName != proposal.Payload.ToolName ||
            detail.Run.PendingApprovals.Any(item => item.ApprovalId == request.ApprovalId)) {
            throw Changed();
        }
        if (Status == ExecutionApprovalStatus.Approved) {
            if (proposal.ApprovedDigest != request.Binding.Digest) {
                throw Changed();
            }
        } else if (proposal is not {
            State: AgentToolProposalState.Cancelled, EffectState: AgentToolEffectState.NotCommitted,
            DispatchClaimId: null, Result: null, OwnerReceipt: null,
            Cancellation: { Disposition: AgentToolCancellationDisposition.NotDispatched,
                Reason: AgentToolCancellationReason.NeverDispatched }
        } || detail.Run.Outcome != RunOutcome.Cancelled || detail.Run.ToolAdmission!.PlanningDecision!.Work.Open is not null) {
            throw Changed();
        }
        return true;
    }

    internal void RequirePending(ExecutionRunDetail detail) {
        var run = detail.Run;
        var proposal = RequirePublication(run);
        var pending = run.PendingApprovals.SingleOrDefault();
        var approval = detail.Approvals.SingleOrDefault(item => item.ApprovalId == request.ApprovalId);
        if (AgentPlanningDecisionExpectation.From(run) != request.Expected || run.Outcome is not null ||
            run.ToolAdmission!.PlanningDecision is not { Current.State: AgentPlanningGenerationState.AwaitingApproval, Work.Open: null } ||
            pending?.ApprovalId != request.ApprovalId || pending.ToolAdmission != request.Binding ||
            approval is not null && (approval is not { Status: ExecutionApprovalStatus.Pending, DecidedAtUtc: null } ||
                approval.ExecutionRunId != run.Id || approval.ToolAdmission != request.Binding) ||
            proposal is not {
                State: AgentToolProposalState.Prepared, ApprovalStatus: ExecutionApprovalStatus.Pending,
                DispatchClaimId: null, ApprovedDigest: null, Result: null, OwnerReceipt: null, ProviderCall: null,
                Payload: { Effect: AgentToolProposalEffect.Mutation, Recovery: AgentToolProposalRecovery.OwnerReceipt,
                    SourcePreparation: not null }
            }) {
            throw Changed();
        }
    }

    private AgentToolProposalRecord RequirePublication(ExecutionRunRecord run) {
        var admission = run.ToolAdmission;
        if (run.Id != request.Expected.ExecutionRunId || admission?.SchemaVersion != AgentToolJournalRecord.InheritedPlanningSchemaVersion ||
            admission.PlanningDecision is not { InheritedOrigin: not null } decision ||
            decision.DecisionId != request.Expected.DecisionId || decision.CurrentGeneration != request.Expected.Generation ||
            decision.Current.Steps.LastOrDefault()?.Publications.SingleOrDefault()?.Proposal != request.Binding) {
            throw Changed();
        }
        return AgentToolJournalTransitions.RequireProposal(admission, request.Binding);
    }

    private static AgentToolAdmissionException Changed()
        => new("planning.review-intent-changed", "The original approval, actor, action or displayed planning expectation changed.");
}
