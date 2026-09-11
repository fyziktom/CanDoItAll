using System.Collections.Immutable;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public static class AgentToolJournalTransitions {
    public static void ValidatePersistence(AgentToolJournalRecord? current, AgentToolJournalRecord? proposed) {
        proposed?.Validate();
        if (current is null) {
            return;
        }

        current.Validate();
        if (proposed is null || proposed.SchemaVersion < current.SchemaVersion ||
            proposed.Session != current.Session || proposed.Support != current.Support ||
            proposed.OriginalInput != current.OriginalInput || proposed.BackgroundInput != current.BackgroundInput ||
            proposed.RuntimeContext?.Content != current.RuntimeContext?.Content ||
            proposed.RuntimeContext?.WorkspaceScope != current.RuntimeContext?.WorkspaceScope || proposed.Revision < current.Revision ||
            proposed.Batches.Length < current.Batches.Length || proposed.Segments.Length < current.Segments.Length) {
            throw Conflict("A run update cannot remove or replace its current tool admission journal.");
        }

        if (proposed.Revision == current.Revision && !Equivalent(current, proposed)) {
            throw Conflict("A stale run snapshot cannot change the current tool admission journal.");
        }

        ValidateProviderDispatches(current, proposed);

        for (var index = 0; index < current.Segments.Length; index++) {
            var previous = current.Segments[index];
            var next = proposed.Segments[index];
            if (previous.Id != next.Id || previous.Ordinal != next.Ordinal ||
                previous.FirstBatchOrdinal != next.FirstBatchOrdinal || previous.RestartCheckpoint != next.RestartCheckpoint ||
                previous.ContinuesSegmentId != next.ContinuesSegmentId ||
                previous.ApprovalCheckpoint is not null && (next.ApprovalCheckpoint != previous.ApprovalCheckpoint ||
                    !next.PendingApprovals.SequenceEqual(previous.PendingApprovals))) {
                throw Conflict("A saved SDK invocation or approval checkpoint is immutable.");
            }
        }

        for (var index = 0; index < current.Batches.Length; index++) {
            var previous = current.Batches[index];
            var next = proposed.Batches[index];
            if (previous.Id != next.Id || previous.Ordinal != next.Ordinal || previous.RequestDigest != next.RequestDigest ||
                previous.Response != next.Response || previous.Proposals.Length != next.Proposals.Length ||
                previous.ProviderDispatchId != next.ProviderDispatchId) {
                throw Conflict("An admitted assistant batch cannot be retargeted or reordered.");
            }

            for (var ordinal = 0; ordinal < previous.Proposals.Length; ordinal++) {
                var prior = previous.Proposals[ordinal];
                var target = next.Proposals[ordinal];
                if ((prior.Result is not null || prior.DisclosureEvidence is not null) &&
                    target.DisclosureEvidence != prior.DisclosureEvidence) {
                    throw Conflict("A saved result cannot change or acquire disclosure evidence after its original checkpoint.");
                }

                if (prior.State == AgentToolProposalState.Cancelled &&
                    (target.State != AgentToolProposalState.Cancelled || target.Result != prior.Result ||
                        target.DispatchClaimId != prior.DispatchClaimId ||
                        target.Cancellation != prior.Cancellation && !(prior.Cancellation?.Disposition == AgentToolCancellationDisposition.CancelledUnreconciled &&
                            prior.Payload.Recovery == AgentToolProposalRecovery.OwnerReceipt &&
                            target.Cancellation?.Disposition == AgentToolCancellationDisposition.ReceiptCommitted) ||
                        target.Cancellation == prior.Cancellation && target.EffectState != prior.EffectState)) {
                    throw Conflict("Cancelled proposals cannot be replayed; only a later immutable owner receipt can resolve uncertainty.");
                }

                if (prior.IntentId != target.IntentId || prior.Ordinal != target.Ordinal || prior.CallId != target.CallId ||
                    prior.Payload != target.Payload || prior.RequiresApproval != target.RequiresApproval || prior.ProviderCall != target.ProviderCall ||
                    prior.ApprovalId is not null && target.ApprovalId != prior.ApprovalId ||
                    prior.ApprovalStatus != ExecutionApprovalStatus.Pending &&
                        (target.ApprovalStatus != prior.ApprovalStatus || target.ApprovedDigest != prior.ApprovedDigest) ||
                    prior.State is AgentToolProposalState.Completed or AgentToolProposalState.Rejected &&
                        (target.State != prior.State || target.Result != prior.Result || target.EffectState != prior.EffectState ||
                            target.DispatchClaimId != prior.DispatchClaimId)) {
                    throw Conflict("A saved tool intent, approved payload or terminal result is immutable.");
                }
            }
        }
    }

    public static AgentToolJournalRecord RejectUndispatchedOnCancellation(AgentToolJournalRecord journal) {
        var updated = journal;
        foreach (var batch in journal.Batches) {
            foreach (var proposal in batch.Proposals.Where(proposal => proposal.State == AgentToolProposalState.Prepared)) {
                updated = Replace(updated, new(batch.Id, proposal.IntentId, proposal.Payload.SemanticVersion, proposal.Payload.Digest),
                    current => current with {
                        State = AgentToolProposalState.Cancelled,
                        ApprovalStatus = current.ApprovalStatus == ExecutionApprovalStatus.Pending ? ExecutionApprovalStatus.Rejected : current.ApprovalStatus,
                        EffectState = current.Payload.Effect == AgentToolProposalEffect.Mutation ? AgentToolEffectState.NotCommitted : AgentToolEffectState.None,
                        Cancellation = new(AgentToolCancellationDisposition.NotDispatched, AgentToolCancellationReason.NeverDispatched)
                    });
            }
        }

        return CancelProviderDispatches(updated);
    }

    private static void ValidateProviderDispatches(AgentToolJournalRecord current, AgentToolJournalRecord proposed) {
        var previous = current.ProviderDispatches.IsDefault ? [] : current.ProviderDispatches;
        var next = proposed.ProviderDispatches.IsDefault ? [] : proposed.ProviderDispatches;
        if (next.Length < previous.Length) {
            throw Conflict("A run update cannot remove a provider dispatch or its uncertainty.");
        }

        for (var index = 0; index < previous.Length; index++) {
            var prior = previous[index];
            var target = next[index];
            if (prior.Id != target.Id || prior.Ordinal != target.Ordinal || prior.SegmentId != target.SegmentId ||
                prior.RequestDigest != target.RequestDigest || prior.ContractDigest != target.ContractDigest ||
                !prior.ApprovedCalls.SequenceEqual(target.ApprovedCalls) ||
                prior.State != AgentToolProviderDispatchState.Started &&
                    (target.State != prior.State || target.ResponseBatchId != prior.ResponseBatchId) ||
                target.State == AgentToolProviderDispatchState.Started && target.ResponseBatchId is not null) {
                throw Conflict("The original provider request, approval bindings and completed disposition are immutable.");
            }
        }
    }

    private static AgentToolJournalRecord CancelProviderDispatches(AgentToolJournalRecord journal) {
        if (journal.ProviderDispatches.IsDefault ||
            !journal.ProviderDispatches.Any(dispatch => dispatch.State == AgentToolProviderDispatchState.Started)) {
            return journal;
        }

        var batches = journal.Batches;
        var dispatches = journal.ProviderDispatches;
        foreach (var dispatch in dispatches.Where(dispatch => dispatch.State == AgentToolProviderDispatchState.Started)) {
            foreach (var binding in dispatch.ApprovedCalls) {
                var batch = batches.Single(batch => batch.Id == binding.BatchId);
                var proposal = batch.Proposals.Single(proposal => proposal.IntentId == binding.IntentId);
                batches = batches.SetItem(batch.Ordinal, batch with { Proposals = batch.Proposals.SetItem(proposal.Ordinal,
                    proposal with {
                        State = AgentToolProposalState.Cancelled,
                        EffectState = AgentToolEffectState.Unknown,
                        Cancellation = new(AgentToolCancellationDisposition.CancelledUnreconciled, AgentToolCancellationReason.NoReceiptProtocol)
                    }) });
            }

            dispatches = dispatches.SetItem(dispatch.Ordinal, dispatch with { State = AgentToolProviderDispatchState.CancelledUnreconciled });
        }

        return journal with { Revision = checked(journal.Revision + 1), Batches = batches, ProviderDispatches = dispatches };
    }

    public static AgentToolJournalRecord BindApprovals(AgentToolJournalRecord journal,
        IReadOnlyList<PendingToolApprovalRecord> pending) {
        var updated = journal;
        foreach (var approval in pending) {
            var binding = approval.ToolAdmission ?? throw Conflict("An admitted run requires a durable proposal link for every pending approval.");
            updated = Replace(updated, binding, proposal => {
                if (!string.Equals(proposal.CallId, approval.CallId, StringComparison.Ordinal) ||
                    !string.Equals(proposal.Payload.ToolName, approval.ToolName, StringComparison.Ordinal) ||
                    proposal.ApprovalId is not null && proposal.ApprovalId != approval.ApprovalId ||
                    proposal.ApprovalStatus != ExecutionApprovalStatus.Pending) {
                    throw Conflict("The pending approval does not match the immutable admitted tool proposal.");
                }

                return proposal with { ApprovalId = approval.ApprovalId };
            });
        }

        return updated;
    }

    public static AgentToolJournalRecord ApplyDecisions(AgentToolJournalRecord journal,
        IReadOnlyList<PendingToolApprovalRecord> pending, IReadOnlyList<PendingToolApprovalDecision> decisions,
        bool automatic) {
        var decisionById = decisions.ToDictionary(item => item.ApprovalId, StringComparer.Ordinal);
        if (decisionById.Count != pending.Count) {
            throw Conflict("The decision set does not cover the exact admitted pending proposals.");
        }

        var updated = journal;
        foreach (var approval in pending) {
            var binding = approval.ToolAdmission ?? throw Conflict("An admitted approval is missing its durable proposal link.");
            if (!decisionById.TryGetValue(approval.ApprovalId, out var decision) ||
                decision.ToolAdmission is not null && decision.ToolAdmission != binding) {
                throw Conflict("The decision does not bind the exact admitted intent and semantic digest.");
            }

            updated = Replace(updated, binding, proposal => {
                if (proposal.ApprovalId != approval.ApprovalId || proposal.ApprovalStatus != ExecutionApprovalStatus.Pending ||
                    automatic && proposal.Payload.Effect == AgentToolProposalEffect.SensitiveDisclosure) {
                    throw Conflict("This proposal requires its exact permitted disclosure or mutation approval.");
                }

                return proposal with {
                    ApprovalStatus = decision.Approved ? ExecutionApprovalStatus.Approved : ExecutionApprovalStatus.Rejected,
                    ApprovedDigest = decision.Approved ? proposal.Payload.Digest : null,
                    State = decision.Approved ? proposal.State : AgentToolProposalState.Rejected
                };
            });
        }

        return updated;
    }

    public static AgentToolProposalRecord RequireProposal(AgentToolJournalRecord journal, AgentToolApprovalBinding binding) {
        var batch = journal.Batches.SingleOrDefault(item => item.Id == binding.BatchId)
            ?? throw Conflict("The admitted assistant batch was not found.");
        var proposal = batch.Proposals.SingleOrDefault(item => item.IntentId == binding.IntentId)
            ?? throw Conflict("The admitted business intent was not found.");
        if (proposal.Payload.SemanticVersion != binding.SemanticVersion || proposal.Payload.Digest != binding.Digest) {
            throw Conflict("The admitted semantic payload changed.");
        }

        return proposal;
    }

    public static AgentToolJournalRecord Replace(AgentToolJournalRecord journal, AgentToolApprovalBinding binding,
        Func<AgentToolProposalRecord, AgentToolProposalRecord> replace) {
        var proposal = RequireProposal(journal, binding);
        var batchIndex = journal.Batches.Single(batch => batch.Id == binding.BatchId).Ordinal;
        var batch = journal.Batches[batchIndex];
        var updated = journal with {
            Revision = checked(journal.Revision + 1),
            Batches = journal.Batches.SetItem(batchIndex,
                batch with { Proposals = batch.Proposals.SetItem(proposal.Ordinal, replace(proposal)) })
        };
        ValidatePersistence(journal, updated);
        return updated;
    }

    private static bool Equivalent(AgentToolJournalRecord left, AgentToolJournalRecord right)
        => JsonSerializer.Serialize(left) == JsonSerializer.Serialize(right);

    private static AgentToolAdmissionException Conflict(string message)
        => new("tool-admission.conflict", message);
}
