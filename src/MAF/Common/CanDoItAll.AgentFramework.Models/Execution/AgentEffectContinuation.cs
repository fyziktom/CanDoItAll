using System.ComponentModel;
using System.Collections.Immutable;
using System.Text.Json;

namespace CanDoItAll.AgentFramework.Models;

[Description("Effect knowledge: 0 ReceiptConfirmed, 1 KnownCommitted, 2 KnownCommittedDetailUnavailable, 3 KnownCommittedDetailDenied, 4 ReceiptNotObserved, 5 ReaderUnavailable. Missing receipt detail never authorizes retry.")]
public enum AgentEffectReceiptKnowledge {
    ReceiptConfirmed,
    KnownCommitted,
    KnownCommittedDetailUnavailable,
    KnownCommittedDetailDenied,
    ReceiptNotObserved,
    ReaderUnavailable
}

[Description("Slice stop: 0 ExhaustedSnapshot, 1 LookupLimit, 2 CooperativeBudget, 3 Cancelled, 4 ReaderFailure, 5 CheckpointConflict, 6 CheckpointFailure, 7 EvidenceCapacity, 8 SnapshotRead. SnapshotRead performs no reconciliation or checkpoint publication.")]
public enum AgentEffectReconciliationStop {
    ExhaustedSnapshot,
    LookupLimit,
    CooperativeBudget,
    Cancelled,
    ReaderFailure,
    CheckpointConflict,
    CheckpointFailure,
    EvidenceCapacity,
    SnapshotRead
}

[Description("Checkpoint persistence: 0 Confirmed, 1 NotAttempted, 2 Unconfirmed. Confirmation describes checkpoint persistence, not permission to dispatch effects.")]
public enum AgentEffectCheckpointPersistence { Confirmed, NotAttempted, Unconfirmed }

[Description("Counts from one validated journal snapshot; unexamined effects have unknown outcomes.")]
public sealed record AgentEffectSnapshotCounts(
    [property: Description("Total saved proposals in this snapshot.")] int TotalProposals,
    [property: Description("Proposals whose commitment is recorded.")] int KnownCommitted,
    [property: Description("Original owner effects eligible for receipt reconciliation.")] int EligibleOwnerEffects,
    [property: Description("Prepared effects that have not been dispatched.")] int PreparedEffects,
    [property: Description("Actual pending approval decisions.")] int PendingApprovals,
    [property: Description("Unresolved proposals outside the owner-receipt candidate set.")] int OtherUnresolvedProposals,
    [property: Description("Provider dispatches whose output has not been recovered.")] int UncertainProviderDispatches);

[Description("Bounded knowledge about one original effect, without owner receipt contents.")]
public sealed record AgentEffectReceiptSummary(
    [property: Description("Exact original immutable proposal binding.")] AgentToolApprovalBinding Binding,
    [property: Description("Observed effect knowledge: 0 ReceiptConfirmed, 1 KnownCommitted, 2 KnownCommittedDetailUnavailable, 3 KnownCommittedDetailDenied, 4 ReceiptNotObserved, 5 ReaderUnavailable. Unavailable detail never authorizes retry.")] AgentEffectReceiptKnowledge Knowledge,
    [property: Description("Whether this confirmation was acknowledged in the execution checkpoint.")] bool Persisted);
[Description("Current-read authorized bounded receipt-reconciliation progress. It contains no provider output or permission to retry an effect.")]
public sealed record AgentEffectContinuation {
    public const int MaximumObservations = 4;
    public const int MaximumUtf8Bytes = 16 * 1024;

    public AgentEffectContinuation(Guid executionRunId, Guid? chatSessionId, long runRevision, long journalRevision,
        AgentToolContinuationRequirement requirement, AgentEffectSnapshotCounts counts, int examinedThisSlice,
        int unexaminedEligibleInSnapshot, AgentEffectReconciliationStop stop, AgentEffectCheckpointPersistence persistence,
        ImmutableArray<AgentEffectReceiptSummary> observations) {
        ArgumentOutOfRangeException.ThrowIfEqual(executionRunId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfLessThan(runRevision, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(journalRevision, 1);
        ArgumentNullException.ThrowIfNull(counts);
        if (chatSessionId == Guid.Empty || !Enum.IsDefined(requirement) || !Enum.IsDefined(stop) || !Enum.IsDefined(persistence) ||
            observations.IsDefault || observations.Length > MaximumObservations || examinedThisSlice is < 0 or > MaximumObservations ||
            unexaminedEligibleInSnapshot < 0 || unexaminedEligibleInSnapshot > counts.TotalProposals ||
            counts.TotalProposals is < 0 or > AgentToolJournalRecord.MaximumBatches * AgentToolJournalRecord.MaximumCallsPerBatch ||
            counts.KnownCommitted < 0 || counts.KnownCommitted > counts.TotalProposals ||
            counts.EligibleOwnerEffects < 0 || counts.EligibleOwnerEffects > counts.TotalProposals ||
            counts.PreparedEffects < 0 || counts.PreparedEffects > counts.TotalProposals ||
            counts.PendingApprovals < 0 || counts.PendingApprovals > counts.TotalProposals ||
            counts.OtherUnresolvedProposals < 0 || counts.OtherUnresolvedProposals > counts.TotalProposals ||
            counts.UncertainProviderDispatches is < 0 or > AgentToolJournalRecord.MaximumBatches) {
            throw new ArgumentException("The effect continuation contains invalid bounded progress or snapshot counts.");
        }
        var intents = new HashSet<AgentToolBusinessIntentId>();
        foreach (var observation in observations) {
            if (observation?.Binding is not { } binding || binding.BatchId.Value == Guid.Empty || binding.IntentId.Value == Guid.Empty ||
                binding.SemanticVersion < 1 || string.IsNullOrWhiteSpace(binding.Digest.Value) || !intents.Add(binding.IntentId) ||
                !Enum.IsDefined(observation.Knowledge) || observation.Persisted &&
                    observation.Knowledge is AgentEffectReceiptKnowledge.ReceiptNotObserved or AgentEffectReceiptKnowledge.ReaderUnavailable) {
                throw new ArgumentException("The continuation observations require distinct exact original proposal bindings.", nameof(observations));
            }
        }
        ExecutionRunId = executionRunId;
        ChatSessionId = chatSessionId;
        RunRevision = runRevision;
        JournalRevision = journalRevision;
        Requirement = requirement;
        Counts = counts;
        ExaminedThisSlice = examinedThisSlice;
        UnexaminedEligibleInSnapshot = unexaminedEligibleInSnapshot;
        Stop = stop;
        Persistence = persistence;
        Observations = observations;
        if (JsonSerializer.SerializeToUtf8Bytes(this).Length > MaximumUtf8Bytes) {
            throw new ArgumentException("The sanitized continuation exceeds its serialized response bound.");
        }
    }

    [Description("Original execution run identity.")]
    public Guid ExecutionRunId { get; }
    [Description("Original chat identity, or null for a background run.")]
    public Guid? ChatSessionId { get; }
    [Description("Revision of the observed or acknowledged original run snapshot.")]
    public long RunRevision { get; }
    [Description("Revision of the observed or acknowledged original journal snapshot.")]
    public long JournalRevision { get; }
    [Description("Remaining continuation requirement: 0 None, 1 ProviderInputRequired, 2 EffectReconciliationRequired. None does not authorize prepared-effect dispatch.")]
    public AgentToolContinuationRequirement Requirement { get; }
    [Description("Aggregate counts for the bounded snapshot interpretation.")]
    public AgentEffectSnapshotCounts Counts { get; }
    [Description("Owner candidates inspected in this slice, at most four.")]
    public int ExaminedThisSlice { get; }
    [Description("Eligible candidates not inspected by this slice; their outcomes remain unknown.")]
    public int UnexaminedEligibleInSnapshot { get; }
    [Description("Why this slice ended: 0 ExhaustedSnapshot, 1 LookupLimit, 2 CooperativeBudget, 3 Cancelled, 4 ReaderFailure, 5 CheckpointConflict, 6 CheckpointFailure, 7 EvidenceCapacity, 8 SnapshotRead.")]
    public AgentEffectReconciliationStop Stop { get; }
    [Description("Checkpoint publication: 0 Confirmed, 1 NotAttempted, 2 Unconfirmed. SnapshotRead confirms only the observed saved snapshot.")]
    public AgentEffectCheckpointPersistence Persistence { get; }
    [Description("At most four sanitized original-effect observations, without receipt contents.")]
    public ImmutableArray<AgentEffectReceiptSummary> Observations { get; }
    [Description("Whether original prepared effects remain; no dispatch permission is implied.")]
    public bool HasPreparedEffects => Counts.PreparedEffects != 0;
    [Description("Whether this snapshot contains unexamined owner candidates.")]
    public bool HasMoreCandidates => UnexaminedEligibleInSnapshot != 0;
}
