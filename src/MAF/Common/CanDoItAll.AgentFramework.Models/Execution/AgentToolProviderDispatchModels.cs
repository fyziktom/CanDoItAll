using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace CanDoItAll.AgentFramework.Models;

public readonly record struct AgentToolProviderDispatchId {
    [JsonConstructor]
    public AgentToolProviderDispatchId(Guid value) {
        ArgumentOutOfRangeException.ThrowIfEqual(value, Guid.Empty);
        Value = value;
    }

    public Guid Value { get; }
}

public enum AgentToolProviderDispatchState {
    Started,
    ResponseAdmitted,
    CancelledUnreconciled
}

public sealed record AgentToolProviderCallBinding(
    string RuntimeToolName,
    AgentToolSemanticDigest ContractDigest,
    AgentToolProtocolEnvelope ProtocolCall);

public sealed record AgentToolProviderDispatchRecord(
    AgentToolProviderDispatchId Id,
    int Ordinal,
    Guid SegmentId,
    AgentToolSemanticDigest RequestDigest,
    AgentToolSemanticDigest ContractDigest,
    ImmutableArray<AgentToolApprovalBinding> ApprovedCalls,
    AgentToolProviderDispatchState State,
    AgentToolBatchId? ResponseBatchId = null);

public sealed record AgentToolProviderDispatchOutcome(
    AgentToolProviderDispatchId Id,
    AgentToolProviderDispatchState State);

internal static class AgentToolProviderJournalValidation {
    internal static void Validate(AgentToolJournalRecord journal) {
        var dispatches = journal.ProviderDispatches.IsDefault ? [] : journal.ProviderDispatches;
        if ((journal.SchemaVersion == AgentToolJournalRecord.ProviderDispatchSchemaVersion) != (dispatches.Length != 0) ||
            dispatches.Length > AgentToolJournalRecord.MaximumBatches) {
            throw new InvalidDataException("The provider-dispatch journal version or count is unsupported.");
        }

        var ids = new HashSet<AgentToolProviderDispatchId>();
        var approvedIntents = new HashSet<AgentToolBusinessIntentId>();
        for (var index = 0; index < dispatches.Length; index++) {
            var dispatch = dispatches[index];
            if (dispatch.Id.Value == Guid.Empty || !ids.Add(dispatch.Id) || dispatch.Ordinal != index ||
                !journal.Segments.Any(segment => segment.Id == dispatch.SegmentId) ||
                !Enum.IsDefined(dispatch.State) || dispatch.ApprovedCalls.IsDefault ||
                dispatch.ApprovedCalls.Length > AgentToolJournalRecord.MaximumCallsPerBatch ||
                (dispatch.State == AgentToolProviderDispatchState.ResponseAdmitted) != (dispatch.ResponseBatchId is not null) ||
                dispatch.State != AgentToolProviderDispatchState.ResponseAdmitted && index != dispatches.Length - 1) {
                throw new InvalidDataException("The provider dispatch identity, ordering or response binding is invalid.");
            }

            if (dispatch.ResponseBatchId is { } responseId && !journal.Batches.Any(batch =>
                    batch.Id == responseId && batch.ProviderDispatchId == dispatch.Id && batch.RequestDigest == dispatch.RequestDigest)) {
                throw new InvalidDataException("The provider dispatch has no atomically admitted response batch.");
            }

            foreach (var binding in dispatch.ApprovedCalls) {
                var proposal = journal.Batches.SingleOrDefault(batch => batch.Id == binding.BatchId)?.Proposals
                    .SingleOrDefault(proposal => proposal.IntentId == binding.IntentId);
                if (!approvedIntents.Add(binding.IntentId) || proposal?.ProviderCall is null ||
                    proposal.Payload.Digest != binding.Digest || proposal.Payload.SemanticVersion != binding.SemanticVersion ||
                    proposal.ApprovalStatus != ExecutionApprovalStatus.Approved || proposal.ApprovedDigest != binding.Digest ||
                    proposal.DispatchClaimId != dispatch.Id.Value ||
                    dispatch.State == AgentToolProviderDispatchState.ResponseAdmitted && proposal.State != AgentToolProposalState.Completed ||
                    dispatch.State == AgentToolProviderDispatchState.Started && proposal.State != AgentToolProposalState.Executing ||
                    dispatch.State == AgentToolProviderDispatchState.CancelledUnreconciled && proposal.State != AgentToolProposalState.Cancelled) {
                    throw new InvalidDataException("The provider dispatch does not own its exact approved native proposals.");
                }
            }
        }

        foreach (var batch in journal.Batches) {
            if (batch.ProviderDispatchId is { } id && !dispatches.Any(dispatch =>
                    dispatch.Id == id && dispatch.ResponseBatchId == batch.Id)) {
                throw new InvalidDataException("A native response batch has no completed provider dispatch.");
            }

            foreach (var proposal in batch.Proposals.Where(proposal => proposal.ProviderCall is not null)) {
                if (batch.ProviderDispatchId is null || !proposal.RequiresApproval ||
                    string.IsNullOrWhiteSpace(proposal.ProviderCall!.RuntimeToolName) ||
                    proposal.ProviderCall.RuntimeToolName.Length > 512 ||
                    proposal.Payload.Effect != AgentToolProposalEffect.Mutation ||
                    proposal.Payload.Recovery != AgentToolProposalRecovery.ReconcileBeforeRetry ||
                    proposal.DispatchClaimId is not null && !approvedIntents.Contains(proposal.IntentId)) {
                    throw new InvalidDataException("The native approval proposal lacks its original provider-call binding.");
                }
            }
        }
    }
}
