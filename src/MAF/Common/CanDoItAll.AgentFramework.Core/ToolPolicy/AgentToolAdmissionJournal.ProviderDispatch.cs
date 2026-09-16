using System.Collections.Immutable;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public sealed partial class AgentToolAdmissionJournal {
    public async Task<AgentToolProviderDispatchRecord> BeginProviderDispatchAsync(AgentToolRunLease lease,
        Guid segmentId, AgentToolSemanticDigest requestDigest, AgentToolSemanticDigest contractDigest,
        IReadOnlyList<AgentToolApprovalBinding> approvedCalls, CancellationToken cancellationToken) {
        AgentToolProviderDispatchRecord? selected = null;
        await MutateAsync(lease, true, journal => {
            RequireRecoverable(journal);
            if (journal.Segments.LastOrDefault()?.Id != segmentId || journal.HasUnresolvedProviderDispatch) {
                throw ProviderUncertain();
            }

            var allowed = approvedCalls.ToHashSet();
            if (allowed.Count != approvedCalls.Count) {
                throw Failure("A provider request cannot repeat an approved native proposal.");
            }

            var segment = journal.Segments[^1];
            var previous = journal.Segments.SingleOrDefault(item => item.Id == segment.ContinuesSegmentId);
            foreach (var binding in approvedCalls) {
                var proposal = AgentToolJournalTransitions.RequireProposal(journal, binding);
                if (proposal.ProviderCall is null || proposal.State != AgentToolProposalState.Prepared ||
                    proposal.ApprovalStatus != ExecutionApprovalStatus.Approved || proposal.ApprovedDigest != binding.Digest ||
                    previous is null || !previous.PendingApprovals.Any(approval => approval.ToolAdmission == binding)) {
                    throw Failure("The provider request does not bind an exact approved native continuation.");
                }
            }

            foreach (var batch in journal.Batches) {
                foreach (var proposal in batch.Proposals.Where(proposal => proposal.State is
                    AgentToolProposalState.Prepared or AgentToolProposalState.Executing or AgentToolProposalState.ReconciliationRequired)) {
                    if (!allowed.Contains(Binding(batch.Id, proposal))) {
                        throw Failure("Drain or reconcile the earlier serial batch before dispatching a provider request.");
                    }
                }
            }

            var dispatches = journal.ProviderDispatches.IsDefault ? [] : journal.ProviderDispatches;
            var dispatch = new AgentToolProviderDispatchRecord(new(Guid.NewGuid()), dispatches.Length, segmentId,
                requestDigest, contractDigest, approvedCalls.ToImmutableArray(), AgentToolProviderDispatchState.Started);
            var batches = journal.Batches;
            foreach (var binding in approvedCalls) {
                var batch = batches.Single(batch => batch.Id == binding.BatchId);
                var proposal = batch.Proposals.Single(proposal => proposal.IntentId == binding.IntentId);
                batches = batches.SetItem(batch.Ordinal, batch with { Proposals = batch.Proposals.SetItem(proposal.Ordinal,
                    proposal with { State = AgentToolProposalState.Executing, DispatchClaimId = dispatch.Id.Value }) });
            }

            selected = dispatch;
            return journal with {
                SchemaVersion = journal.SchemaVersion == AgentToolJournalRecord.TypedContextSchemaVersion
                    ? AgentToolJournalRecord.TypedContextSchemaVersion : AgentToolJournalRecord.ProviderDispatchSchemaVersion,
                Revision = checked(journal.Revision + 1), Batches = batches, ProviderDispatches = dispatches.Add(dispatch)
            };
        }, cancellationToken);
        return selected ?? throw Failure("The provider request did not acquire its durable dispatch identity.");
    }

    public Task<AgentToolJournalRecord> CompleteProviderDispatchAsync(AgentToolRunLease lease,
        AgentToolProviderDispatchId dispatchId, AgentToolSemanticDigest requestDigest,
        AgentToolProtocolEnvelope response, IReadOnlyList<AgentToolPreparedCall> calls, CancellationToken cancellationToken)
        => MutateAsync(lease, true, journal => {
            var dispatch = journal.ProviderDispatches.IsDefault ? null :
                journal.ProviderDispatches.SingleOrDefault(item => item.Id == dispatchId);
            if (dispatch is null || dispatch.State != AgentToolProviderDispatchState.Started ||
                dispatch.RequestDigest != requestDigest || journal.Segments[^1].Id != dispatch.SegmentId) {
                throw Failure("The provider response does not own the current durable request.");
            }

            var batch = CreateBatch(journal, requestDigest, response, calls, dispatchId);
            var batches = journal.Batches;
            foreach (var binding in dispatch.ApprovedCalls) {
                var previous = batches.Single(item => item.Id == binding.BatchId);
                var proposal = previous.Proposals.Single(item => item.IntentId == binding.IntentId);
                if (proposal.State != AgentToolProposalState.Executing || proposal.DispatchClaimId != dispatchId.Value) {
                    throw Failure("The native approval lost its provider dispatch before response admission.");
                }

                batches = batches.SetItem(previous.Ordinal, previous with { Proposals = previous.Proposals.SetItem(proposal.Ordinal,
                    proposal with { State = AgentToolProposalState.Completed, Result = response, EffectState = AgentToolEffectState.Unknown }) });
            }

            return journal with {
                Revision = checked(journal.Revision + 1), Batches = batches.Add(batch),
                ProviderDispatches = journal.ProviderDispatches.SetItem(dispatch.Ordinal, dispatch with {
                    State = AgentToolProviderDispatchState.ResponseAdmitted, ResponseBatchId = batch.Id
                })
            };
        }, cancellationToken, backgroundCheck: BackgroundCheck.RecordOutcome);

    private static AgentToolBatchRecord CreateBatch(AgentToolJournalRecord journal, AgentToolSemanticDigest requestDigest,
        AgentToolProtocolEnvelope response, IReadOnlyList<AgentToolPreparedCall> calls,
        AgentToolProviderDispatchId? providerDispatchId = null) {
        var proposals = calls.Select((call, ordinal) => new AgentToolProposalRecord(
            new(Guid.NewGuid()), ordinal, call.CallId, call.Payload, call.RequiresApproval,
            AgentToolProposalState.Prepared,
            call.RequiresApproval ? ExecutionApprovalStatus.Pending : ExecutionApprovalStatus.Approved,
            call.RequiresApproval ? null : call.Payload.Digest, ProviderCall: call.ProviderCall)).ToImmutableArray();
        return new(new(Guid.NewGuid()), journal.Batches.Length, requestDigest, response, proposals, providerDispatchId);
    }

    private static AgentToolAdmissionException ProviderUncertain()
        => new("tool-admission.reconciliation-required",
            "A provider request may already have executed a hosted tool. Its response was not durably admitted; automatic provider redispatch is prohibited.");
}
