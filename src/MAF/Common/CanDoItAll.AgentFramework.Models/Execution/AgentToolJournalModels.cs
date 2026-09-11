using System.Collections.Immutable;
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
    RequestScopedInput
}

public sealed record AgentToolOriginalInput(Guid MessageId, string Content);

public enum AgentToolProposalState {
    Prepared,
    Executing,
    Completed,
    Rejected,
    ReconciliationRequired,
    Cancelled
}

public enum AgentToolCancellationDisposition {
    NotDispatched,
    NoExternalMutation,
    ReceiptCommitted,
    CancelledUnreconciled
}

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

public sealed record AgentToolApprovalBinding(
    AgentToolBatchId BatchId,
    AgentToolBusinessIntentId IntentId,
    int SemanticVersion,
    AgentToolSemanticDigest Digest);

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
    AgentToolProtocolEnvelope? DisclosureEvidence = null);

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
    AgentToolProtocolEnvelope RestartCheckpoint,
    Guid? ContinuesSegmentId = null,
    AgentToolProtocolEnvelope? ApprovalCheckpoint = null,
    ImmutableArray<PendingToolApprovalRecord> PendingApprovals = default);

public sealed record AgentToolAdmittedRuntimeContext(string Content, WorkspaceScopeDescriptor? WorkspaceScope) {
    public AgentRuntimeTransientContext ToTransientContext() => new(Content, WorkspaceScope);
}

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
    ImmutableArray<AgentToolProviderDispatchRecord> ProviderDispatches = default) {
    public const int CurrentSchemaVersion = 1;
    public const int BackgroundSchemaVersion = 2;
    public const int ProviderDispatchSchemaVersion = 3;
    public const int MaximumBatches = 64;
    public const int MaximumCallsPerBatch = 64;
    public const int MaximumSegments = 32;
    public const int MaximumUtf8Bytes = 24 * 1024 * 1024;

    [JsonIgnore]
    public string? RecoveryInputContent => OriginalInput?.Content ?? BackgroundInput?.Content;

    public bool HasUnresolvedEffects => HasUnresolvedProviderDispatch || Batches.Any(batch => batch.Proposals.Any(proposal =>
        proposal.State is AgentToolProposalState.Prepared or AgentToolProposalState.Executing or AgentToolProposalState.ReconciliationRequired));

    [JsonIgnore]
    public bool HasUnresolvedProviderDispatch => !ProviderDispatches.IsDefault &&
        ProviderDispatches.Any(dispatch => dispatch.State != AgentToolProviderDispatchState.ResponseAdmitted);

    public void Validate() {
        if (SchemaVersion is not (CurrentSchemaVersion or BackgroundSchemaVersion or ProviderDispatchSchemaVersion) || Revision < 1 || !Enum.IsDefined(Support) ||
            Batches.IsDefault || Segments.IsDefault || Batches.Length > MaximumBatches || Segments.Length > MaximumSegments ||
            Support != AgentToolAdmissionSupport.Recoverable && (Batches.Length != 0 || Segments.Length != 0) || Batches.Length != 0 && Segments.Length == 0) {
            throw new InvalidDataException("The tool admission journal version, revision or batch count is unsupported.");
        }

        ArgumentNullException.ThrowIfNull(Session);
        ArgumentNullException.ThrowIfNull(Session.Reference);
        ArgumentNullException.ThrowIfNull(Session.Profile);
        if (Session.Reference.BackgroundSource is null ? SchemaVersion is not (CurrentSchemaVersion or ProviderDispatchSchemaVersion) || BackgroundInput is not null :
                SchemaVersion is not (BackgroundSchemaVersion or ProviderDispatchSchemaVersion) || Session.Purpose != AgentRuntimeContextPurpose.GovernedProcessAutomation || OriginalInput is not null ||
                BackgroundInput is null || string.IsNullOrWhiteSpace(BackgroundInput.Content) ||
                Encoding.UTF8.GetByteCount(BackgroundInput.Content) > AgentToolProtocolEnvelope.MaximumUtf8Bytes) {
            throw new InvalidDataException("The admitted tool input does not match its interactive or background source.");
        }
        if (OriginalInput is { } input && (input.MessageId == Guid.Empty || string.IsNullOrWhiteSpace(input.Content) ||
                Encoding.UTF8.GetByteCount(input.Content) > AgentToolProtocolEnvelope.MaximumUtf8Bytes)) {
            throw new InvalidDataException("The admitted original input or typed context attachments cannot be recovered safely.");
        }

        _ = RuntimeContext?.ToTransientContext();
        AgentToolProviderJournalValidation.Validate(this);

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
                (segment.ApprovalCheckpoint is null) != (segment.PendingApprovals.Length == 0)) {
                throw new InvalidDataException("The SDK invocation checkpoint sequence is invalid.");
            }

            bytes += Encoding.UTF8.GetByteCount(segment.RestartCheckpoint.PayloadJson);
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
                bytes += proposal.Result is null ? 0L : Encoding.UTF8.GetByteCount(proposal.Result.PayloadJson);
                bytes += proposal.DisclosureEvidence is { } evidence ? Encoding.UTF8.GetByteCount(evidence.PayloadJson) : 0L;
                bytes += proposal.Cancellation?.Receipt is { } receipt ? Encoding.UTF8.GetByteCount(receipt.PayloadJson) : 0L;
            }
        }

        if (bytes > MaximumUtf8Bytes || System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(this).Length > MaximumUtf8Bytes) {
            throw new InvalidDataException("The tool admission journal exceeds its supported serialized payload bound.");
        }
    }

    public override string ToString()
        => $"Tool admission v{SchemaVersion} revision {Revision}, {Batches.Length} batch(es)";
}
