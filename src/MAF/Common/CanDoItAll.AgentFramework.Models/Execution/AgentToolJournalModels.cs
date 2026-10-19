using System.Collections.Immutable;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace CanDoItAll.AgentFramework.Models;

public sealed record AgentToolProtocolEnvelope {
    public const int MaximumUtf8Bytes = 8 * 1024 * 1024;

    public AgentToolProtocolEnvelope(string format, int version, string payloadJson, AgentToolSemanticDigest digest) {
        ArgumentException.ThrowIfNullOrWhiteSpace(format);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(format.Length, 128);
        ArgumentOutOfRangeException.ThrowIfLessThan(version, 1);
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadJson);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(Encoding.UTF8.GetByteCount(payloadJson), MaximumUtf8Bytes);
        if (ComputeDigest(payloadJson) != digest) {
            throw new ArgumentException("The saved tool protocol envelope does not match its digest.", nameof(digest));
        }

        Format = format;
        Version = version;
        PayloadJson = payloadJson;
        Digest = digest;
    }

    public string Format { get; }
    public int Version { get; }
    public string PayloadJson { get; }
    public AgentToolSemanticDigest Digest { get; }

    public static AgentToolProtocolEnvelope Create(string format, int version, string payloadJson)
        => new(format, version, payloadJson, ComputeDigest(payloadJson));

    public static AgentToolSemanticDigest ComputeDigest(string value)
        => new(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant());

    public override string ToString() => $"{Format} v{Version} {Digest.Value}";
}

public enum AgentToolAdmissionSupport {
    Recoverable,
    RequestScopedInput,
    EffectJournalOnly
}

public enum AgentToolCheckpointKind {
    ProviderRestart,
    EffectsOnly
}

[Description("Continuation requirement: 0 None, 1 ProviderInputRequired, 2 EffectReconciliationRequired. This classification grants no authority to dispatch prepared effects.")]
public enum AgentToolContinuationRequirement {
    None,
    ProviderInputRequired,
    EffectReconciliationRequired
}

public sealed record AgentToolOriginalInput(Guid MessageId, string Content);

public enum AgentToolProposalState {
    Prepared,
    Executing,
    Completed,
    Rejected,
    ReconciliationRequired,
    Cancelled,
    OwnerReceiptConfirmed
}

public sealed record AgentToolOwnerReceipt(AgentToolCommittedEffect Effect, AgentToolProtocolEnvelope Receipt);

/// <summary>
/// How the cancellation of an agent execution run resolved one admitted tool call, as a JSON integer: 0 NotDispatched
/// (never started), 1 NoExternalMutation (a read that changed nothing), 2 ReceiptCommitted (the owner's receipt shows
/// the change was made), 3 CancelledUnreconciled (started, and its effect could not be confirmed).
/// </summary>
public enum AgentToolCancellationDisposition {
    NotDispatched,
    NoExternalMutation,
    ReceiptCommitted,
    CancelledUnreconciled
}

/// <summary>
/// Why a cancellation disposition applies to a tool call, as a JSON integer: 0 NeverDispatched, 1 ReceiptFound,
/// 2 ReceiptNotObserved (the owner reported no receipt), 3 NoReceiptProtocol (the tool offers no receipt lookup),
/// 4 CurrentAccessDenied (the receipt lookup was refused by current access), 5 ReadOnlyInvocation.
/// </summary>
public enum AgentToolCancellationReason {
    NeverDispatched,
    ReceiptFound,
    ReceiptNotObserved,
    NoReceiptProtocol,
    CurrentAccessDenied,
    ReadOnlyInvocation
}

public sealed record AgentToolCancellationResolution(
    AgentToolCancellationDisposition Disposition,
    AgentToolCancellationReason Reason,
    AgentToolCommittedEffect? CommittedEffect = null,
    AgentToolProtocolEnvelope? Receipt = null);

public sealed record AgentToolCancellationOutcome(
    AgentToolBusinessIntentId IntentId,
    string ToolName,
    AgentToolEffectState EffectState,
    AgentToolCancellationResolution? Cancellation);

public sealed record AgentToolRunCancellationReconciliation(
    Guid ExecutionRunId,
    Guid ChatSessionId,
    IReadOnlyList<AgentToolCancellationOutcome> Outcomes,
    IReadOnlyList<AgentToolProviderDispatchOutcome>? ProviderDispatches = null) {
    public bool HasUnknownEffects => Outcomes.Any(outcome => outcome.EffectState == AgentToolEffectState.Unknown) ||
        ProviderDispatches?.Any(dispatch => dispatch.State != AgentToolProviderDispatchState.ResponseAdmitted) == true;
}

[Description("Immutable identity and semantic binding of one original saved tool proposal; it is not approval or dispatch authority.")]
public sealed record AgentToolApprovalBinding(
    [property: Description("Original saved tool batch identity.")] AgentToolBatchId BatchId,
    [property: Description("Original business intent identity within the saved batch.")] AgentToolBusinessIntentId IntentId,
    [property: Description("Positive version of the proposal's semantic interpretation.")] int SemanticVersion,
    [property: Description("Digest binding the exact original proposal semantics.")] AgentToolSemanticDigest Digest);

public sealed record AgentToolReceiptReconciliationCursor(
    AgentToolBatchId BatchId,
    AgentToolBusinessIntentId IntentId,
    int BatchOrdinal,
    int ProposalOrdinal);

public sealed record AgentToolPreparedCall(
    string CallId,
    AgentToolPreparedPayload Payload,
    bool RequiresApproval,
    AgentToolProviderCallBinding? ProviderCall = null);

public sealed record AgentToolProposalRecord(
    AgentToolBusinessIntentId IntentId,
    int Ordinal,
    string CallId,
    AgentToolPreparedPayload Payload,
    bool RequiresApproval,
    AgentToolProposalState State,
    ExecutionApprovalStatus ApprovalStatus,
    AgentToolSemanticDigest? ApprovedDigest,
    string? ApprovalId = null,
    Guid? DispatchClaimId = null,
    AgentToolProtocolEnvelope? Result = null,
    AgentToolEffectState EffectState = AgentToolEffectState.Unknown,
    AgentToolCancellationResolution? Cancellation = null,
    AgentToolProviderCallBinding? ProviderCall = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    AgentToolProtocolEnvelope? DisclosureEvidence = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    AgentToolOwnerReceipt? OwnerReceipt = null);

public sealed record AgentToolBatchRecord(
    AgentToolBatchId Id,
    int Ordinal,
    AgentToolSemanticDigest RequestDigest,
    AgentToolProtocolEnvelope Response,
    ImmutableArray<AgentToolProposalRecord> Proposals,
    AgentToolProviderDispatchId? ProviderDispatchId = null);

public sealed record AgentToolInvocationSegment(
    Guid Id,
    int Ordinal,
    int FirstBatchOrdinal,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    AgentToolProtocolEnvelope? RestartCheckpoint,
    Guid? ContinuesSegmentId = null,
    AgentToolProtocolEnvelope? ApprovalCheckpoint = null,
    ImmutableArray<PendingToolApprovalRecord> PendingApprovals = default,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    AgentToolCheckpointKind CheckpointKind = AgentToolCheckpointKind.ProviderRestart,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    Guid? OriginDispatchLeaseId = null);

public sealed record AgentToolJournalRecord(
    int SchemaVersion,
    long Revision,
    AgentToolSessionAdmission Session,
    ImmutableArray<AgentToolInvocationSegment> Segments,
    ImmutableArray<AgentToolBatchRecord> Batches,
    Guid? ActiveDispatchLeaseId = null,
    AgentToolAdmissionSupport Support = AgentToolAdmissionSupport.Recoverable,
    AgentToolOriginalInput? OriginalInput = null,
    AgentToolAdmittedRuntimeContext? RuntimeContext = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] AgentToolBackgroundInput? BackgroundInput = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    ImmutableArray<AgentToolProviderDispatchRecord> ProviderDispatches = default,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    AgentToolContinuationRequirement ToolContinuationRequirement = AgentToolContinuationRequirement.None,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    AgentToolReceiptReconciliationCursor? ReceiptReconciliationCursor = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    AgentPlanningDecisionRecord? PlanningDecision = null) {
    public const int CurrentSchemaVersion = 1;
    public const int BackgroundSchemaVersion = 2;
    public const int ProviderDispatchSchemaVersion = 3;
    public const int TypedContextSchemaVersion = 4;
    public const int EffectJournalSchemaVersion = 5;
    public const int PlanningSchemaVersion = 6;
    public const int MaximumBatches = 512;
    public const int MaximumCallsPerBatch = 64;
    public const int MaximumSegments = 32;
    public const int MaximumUtf8Bytes = 24 * 1024 * 1024;
    private static readonly ConditionalWeakTable<AgentToolJournalRecord, object> ValidatedSnapshots = new();

    [JsonIgnore]
    public string? RecoveryInputContent => OriginalInput?.Content ?? BackgroundInput?.Content;

    public bool HasUnresolvedEffects => HasUnresolvedProviderDispatch || Batches.Any(batch => batch.Proposals.Any(proposal =>
        proposal.State is AgentToolProposalState.Prepared or AgentToolProposalState.Executing or AgentToolProposalState.ReconciliationRequired));

    [JsonIgnore]
    public bool HasUnresolvedProviderDispatch => !ProviderDispatches.IsDefault &&
        ProviderDispatches.Any(dispatch => dispatch.State != AgentToolProviderDispatchState.ResponseAdmitted);

    public void Validate() => ValidatedSnapshots.GetValue(this, static journal => {
        journal.ValidateCore();
        return new object();
    });

    private void ValidateCore() {
        var effectJournalSchema = SchemaVersion is EffectJournalSchemaVersion or PlanningSchemaVersion;
        if (SchemaVersion is not (CurrentSchemaVersion or BackgroundSchemaVersion or ProviderDispatchSchemaVersion or TypedContextSchemaVersion or EffectJournalSchemaVersion or PlanningSchemaVersion) ||
            Revision < 1 || !Enum.IsDefined(Support) || !Enum.IsDefined(ToolContinuationRequirement) ||
            Support == AgentToolAdmissionSupport.EffectJournalOnly && !effectJournalSchema ||
            (SchemaVersion == PlanningSchemaVersion) != (PlanningDecision is not null) ||
            SchemaVersion == PlanningSchemaVersion && Support != AgentToolAdmissionSupport.EffectJournalOnly ||
            ToolContinuationRequirement != AgentToolContinuationRequirement.None &&
                (!effectJournalSchema || Support != AgentToolAdmissionSupport.EffectJournalOnly) ||
            Batches.IsDefault || Segments.IsDefault || Batches.Length > MaximumBatches || Segments.Length > MaximumSegments ||
            Support == AgentToolAdmissionSupport.RequestScopedInput && (Batches.Length != 0 || Segments.Length != 0) ||
            Batches.Length != 0 && Segments.Length == 0) {
            throw new InvalidDataException("The tool admission journal version, revision or batch count is unsupported.");
        }

        ArgumentNullException.ThrowIfNull(Session);
        ArgumentNullException.ThrowIfNull(Session.Reference);
        ArgumentNullException.ThrowIfNull(Session.Profile);
        if (Session.Reference.BackgroundSource is null ? SchemaVersion is not (CurrentSchemaVersion or ProviderDispatchSchemaVersion or TypedContextSchemaVersion or EffectJournalSchemaVersion or PlanningSchemaVersion) || BackgroundInput is not null :
                SchemaVersion is not (BackgroundSchemaVersion or ProviderDispatchSchemaVersion or EffectJournalSchemaVersion) || Session.Purpose != AgentRuntimeContextPurpose.GovernedProcessAutomation || OriginalInput is not null ||
                BackgroundInput is null || string.IsNullOrWhiteSpace(BackgroundInput.Content) ||
                Encoding.UTF8.GetByteCount(BackgroundInput.Content) > AgentToolProtocolEnvelope.MaximumUtf8Bytes) {
            throw new InvalidDataException("The admitted tool input does not match its interactive or background source.");
        }
        if (OriginalInput is { } input && (input.MessageId == Guid.Empty || string.IsNullOrWhiteSpace(input.Content) ||
                Encoding.UTF8.GetByteCount(input.Content) > AgentToolProtocolEnvelope.MaximumUtf8Bytes)) {
            throw new InvalidDataException("The admitted original input or typed context attachments cannot be recovered safely.");
        }

        var hasAttachments = RuntimeContext is { Attachments.IsDefaultOrEmpty: false };
        if (!effectJournalSchema &&
                ((SchemaVersion == TypedContextSchemaVersion) != hasAttachments ||
                    SchemaVersion == TypedContextSchemaVersion && Support != AgentToolAdmissionSupport.Recoverable) ||
            effectJournalSchema && hasAttachments &&
                (Support != AgentToolAdmissionSupport.Recoverable || Session.Reference.BackgroundSource is not null)) {
            throw new InvalidDataException("Saved typed context requires its supported recoverable journal version.");
        }
        RuntimeContext?.Validate();
        AgentToolProviderJournalValidation.Validate(this);
        if (ReceiptReconciliationCursor is { } cursor &&
            (!effectJournalSchema || Support != AgentToolAdmissionSupport.EffectJournalOnly ||
                cursor.BatchOrdinal < 0 || cursor.BatchOrdinal >= Batches.Length ||
                Batches[cursor.BatchOrdinal].Id != cursor.BatchId || cursor.ProposalOrdinal < 0 ||
                cursor.ProposalOrdinal >= Batches[cursor.BatchOrdinal].Proposals.Length ||
                Batches[cursor.BatchOrdinal].Proposals[cursor.ProposalOrdinal].IntentId != cursor.IntentId)) {
            throw new InvalidDataException("The receipt reconciliation cursor must identify an original effects-only proposal.");
        }

        var bytes = 0L;
        var segmentIds = new HashSet<Guid>();
        for (var index = 0; index < Segments.Length; index++) {
            var segment = Segments[index];
            if (segment.Id == Guid.Empty || !segmentIds.Add(segment.Id) || segment.Ordinal != index ||
                segment.FirstBatchOrdinal < 0 || segment.FirstBatchOrdinal > Batches.Length ||
                index == 0 && (segment.FirstBatchOrdinal != 0 || segment.ContinuesSegmentId is not null) ||
                index > 0 && (segment.ContinuesSegmentId != Segments[index - 1].Id ||
                    segment.FirstBatchOrdinal < Segments[index - 1].FirstBatchOrdinal) ||
                segment.PendingApprovals.IsDefault || segment.PendingApprovals.Length > MaximumCallsPerBatch ||
                !Enum.IsDefined(segment.CheckpointKind)) {
                throw new InvalidDataException("The SDK invocation checkpoint sequence is invalid.");
            }

            if (segment.CheckpointKind == AgentToolCheckpointKind.EffectsOnly) {
                if (!effectJournalSchema || Support != AgentToolAdmissionSupport.EffectJournalOnly ||
                    segment.RestartCheckpoint is not null || segment.ApprovalCheckpoint is not null ||
                    segment.OriginDispatchLeaseId is null || segment.OriginDispatchLeaseId == Guid.Empty) {
                    throw new InvalidDataException("An effects-only segment requires its original live lease and cannot contain provider checkpoints.");
                }
                ValidateEffectApprovals(segment, index);
            } else if (Support == AgentToolAdmissionSupport.EffectJournalOnly || segment.RestartCheckpoint is null ||
                segment.OriginDispatchLeaseId is not null ||
                (segment.ApprovalCheckpoint is null) != (segment.PendingApprovals.Length == 0)) {
                throw new InvalidDataException("A provider-restart segment requires its original checkpoint and approval pairing.");
            }

            bytes += segment.RestartCheckpoint is null ? 0L : Encoding.UTF8.GetByteCount(segment.RestartCheckpoint.PayloadJson);
            bytes += segment.ApprovalCheckpoint is null ? 0L : Encoding.UTF8.GetByteCount(segment.ApprovalCheckpoint.PayloadJson);
        }
        var batchIds = new HashSet<Guid>();
        var intentIds = new HashSet<Guid>();
        for (var index = 0; index < Batches.Length; index++) {
            var batch = Batches[index];
            if (batch.Id.Value == Guid.Empty || !batchIds.Add(batch.Id.Value) || batch.Ordinal != index ||
                batch.Proposals.IsDefault || batch.Proposals.Length > MaximumCallsPerBatch) {
                throw new InvalidDataException("The tool admission batch identity, order or size is invalid.");
            }

            bytes += Encoding.UTF8.GetByteCount(batch.Response.PayloadJson);
            var callIds = new HashSet<string>(StringComparer.Ordinal);
            for (var ordinal = 0; ordinal < batch.Proposals.Length; ordinal++) {
                var proposal = batch.Proposals[ordinal];
                if (proposal.IntentId.Value == Guid.Empty || !intentIds.Add(proposal.IntentId.Value) ||
                    proposal.Ordinal != ordinal || string.IsNullOrWhiteSpace(proposal.CallId) ||
                    proposal.CallId.Length > 512 || !callIds.Add(proposal.CallId) ||
                    !Enum.IsDefined(proposal.State) || !Enum.IsDefined(proposal.ApprovalStatus) ||
                    proposal.ApprovalStatus == ExecutionApprovalStatus.Approved && proposal.ApprovedDigest != proposal.Payload.Digest ||
                    proposal.State == AgentToolProposalState.Executing && proposal.DispatchClaimId is null ||
                    proposal.State == AgentToolProposalState.Completed && proposal.Result is null) {
                    throw new InvalidDataException("The admitted tool proposal or exact approval binding is invalid.");
                }

                if (proposal.DisclosureEvidence is not null && (proposal.Result is null || proposal.DispatchClaimId is null ||
                        proposal.State is AgentToolProposalState.Prepared or AgentToolProposalState.Rejected)) {
                    throw new InvalidDataException("Disclosure evidence must accompany the original dispatched result checkpoint.");
                }

                if ((proposal.State == AgentToolProposalState.OwnerReceiptConfirmed) != (proposal.OwnerReceipt is not null) ||
                    proposal.OwnerReceipt is { } ownerReceipt &&
                        (!effectJournalSchema ||
                            proposal.ApprovalStatus != ExecutionApprovalStatus.Approved || proposal.ApprovedDigest != proposal.Payload.Digest ||
                            proposal.DispatchClaimId is null || proposal.DispatchClaimId == Guid.Empty ||
                            proposal.Payload.Effect != AgentToolProposalEffect.Mutation ||
                            proposal.Payload.Recovery != AgentToolProposalRecovery.OwnerReceipt || proposal.ProviderCall is not null ||
                            proposal.EffectState != AgentToolEffectState.Committed || proposal.Cancellation is not null ||
                            ownerReceipt.Effect is null || ownerReceipt.Receipt is null ||
                            string.IsNullOrWhiteSpace(ownerReceipt.Effect.SourceKind) || ownerReceipt.Effect.SourceKind != ownerReceipt.Effect.SourceKind.Trim() ||
                            string.IsNullOrWhiteSpace(ownerReceipt.Effect.SourceId) || ownerReceipt.Effect.SourceId != ownerReceipt.Effect.SourceId.Trim())) {
                    throw new InvalidDataException("Owner receipt confirmation requires an approved dispatched effect and complete owner evidence.");
                }

                if ((proposal.State == AgentToolProposalState.Cancelled) != (proposal.Cancellation is not null) ||
                    proposal.Cancellation is { } resolution && (!Enum.IsDefined(resolution.Disposition) || !Enum.IsDefined(resolution.Reason) ||
                        (resolution.Disposition == AgentToolCancellationDisposition.ReceiptCommitted) !=
                            (resolution.CommittedEffect is not null && resolution.Receipt is not null) ||
                        resolution.Disposition != AgentToolCancellationDisposition.ReceiptCommitted &&
                            (resolution.CommittedEffect is not null || resolution.Receipt is not null))) {
                    throw new InvalidDataException("The cancelled tool proposal has invalid reconciliation evidence.");
                }

                if (proposal.Cancellation is { } cancellation && !(cancellation.Disposition switch {
                    AgentToolCancellationDisposition.NotDispatched => proposal.DispatchClaimId is null &&
                        proposal.EffectState == (proposal.Payload.Effect == AgentToolProposalEffect.Mutation ? AgentToolEffectState.NotCommitted : AgentToolEffectState.None),
                    AgentToolCancellationDisposition.NoExternalMutation => proposal.Payload.Effect == AgentToolProposalEffect.Read &&
                        proposal.EffectState == AgentToolEffectState.None,
                    AgentToolCancellationDisposition.ReceiptCommitted => proposal.DispatchClaimId is not null &&
                        proposal.Payload.Recovery == AgentToolProposalRecovery.OwnerReceipt && proposal.EffectState == AgentToolEffectState.Committed,
                    AgentToolCancellationDisposition.CancelledUnreconciled => proposal.DispatchClaimId is not null &&
                        proposal.EffectState == AgentToolEffectState.Unknown,
                    _ => false
                })) {
                    throw new InvalidDataException("Cancellation evidence does not match the original dispatch and effect state.");
                }

                bytes += Encoding.UTF8.GetByteCount(proposal.Payload.ArgumentsJson);
                if (effectJournalSchema && proposal.Payload.SourcePreparation is { } source) {
                    bytes += Encoding.UTF8.GetByteCount(source.PayloadJson);
                }
                bytes += proposal.Result is null ? 0L : Encoding.UTF8.GetByteCount(proposal.Result.PayloadJson);
                bytes += proposal.DisclosureEvidence is { } evidence ? Encoding.UTF8.GetByteCount(evidence.PayloadJson) : 0L;
                bytes += proposal.Cancellation?.Receipt is { } receipt ? Encoding.UTF8.GetByteCount(receipt.PayloadJson) : 0L;
                if (proposal.OwnerReceipt is { } owner) {
                    bytes += Encoding.UTF8.GetByteCount(owner.Receipt.PayloadJson);
                    bytes += Encoding.UTF8.GetByteCount(owner.Effect.SourceKind);
                    bytes += Encoding.UTF8.GetByteCount(owner.Effect.SourceId);
                }
            }
        }

        PlanningDecision?.ValidateJournal(this);
        if (bytes > MaximumUtf8Bytes || AgentToolJournalJsonSize.Count(this) > MaximumUtf8Bytes) {
            throw new InvalidDataException("The tool admission journal exceeds its supported serialized payload bound.");
        }
    }

    private void ValidateEffectApprovals(AgentToolInvocationSegment segment, int index) {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var intents = new HashSet<AgentToolBusinessIntentId>();
        foreach (var approval in segment.PendingApprovals) {
            var binding = approval.ToolAdmission;
            var batch = binding is null ? null : Batches.FirstOrDefault(batch => batch.Id == binding.BatchId);
            var proposal = batch?.Proposals.FirstOrDefault(proposal => proposal.IntentId == binding!.IntentId);
            if (binding is null || batch is null || proposal is null || string.IsNullOrWhiteSpace(approval.ApprovalId) ||
                !ids.Add(approval.ApprovalId) || !intents.Add(binding.IntentId) ||
                batch.Ordinal < segment.FirstBatchOrdinal ||
                index + 1 < Segments.Length && batch.Ordinal >= Segments[index + 1].FirstBatchOrdinal ||
                !proposal.RequiresApproval || proposal.ApprovalId != approval.ApprovalId ||
                proposal.CallId != approval.CallId || proposal.Payload.ToolName != approval.ToolName ||
                proposal.Payload.ArgumentsJson != approval.ArgumentsJson ||
                proposal.Payload.SemanticVersion != binding.SemanticVersion || proposal.Payload.Digest != binding.Digest) {
                throw new InvalidDataException("An effects-only approval must bind its exact saved proposal.");
            }
        }
    }

    public override string ToString()
        => $"Tool admission v{SchemaVersion} revision {Revision}, {Batches.Length} batch(es)";
}
