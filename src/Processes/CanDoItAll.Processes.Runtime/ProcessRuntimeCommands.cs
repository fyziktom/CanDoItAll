using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Drivers.Abstractions;

namespace CanDoItAll.Processes.Runtime;

public sealed record RuntimeCommandContext(
    RuntimeCommandId CommandId,
    ProcessEventActor Actor,
    ProcessCorrelationId CorrelationId,
    DateTimeOffset OccurredAtUtc)
{
    public void Validate()
    {
        if (OccurredAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Runtime command timestamps must be UTC.", nameof(OccurredAtUtc));
        }
    }
}

public sealed record CreateDispatchClaimCommand(
    DispatchWorkItem WorkItem,
    DispatcherOwnerId OwnerId,
    DispatchClaimToken ClaimToken,
    DateTimeOffset LeaseExpiresAtUtc);

public sealed record RenewDispatchClaimCommand(
    ProcessStepInstanceId StepInstanceId,
    DispatcherOwnerId OwnerId,
    DispatchClaimToken ClaimToken,
    DateTimeOffset LeaseExpiresAtUtc);

public sealed record ReleaseDispatchClaimCommand(
    ProcessStepInstanceId StepInstanceId,
    DispatcherOwnerId OwnerId,
    DispatchClaimToken ClaimToken);

public sealed record DeferDispatchClaimCommand(
    ProcessStepInstanceId StepInstanceId,
    DispatcherOwnerId OwnerId,
    DispatchClaimToken ClaimToken,
    ProcessRunId? DeferredRunId);

public sealed record SubmitStrategyResultCommand(
    ProcessStepInstanceId StepInstanceId,
    DispatcherOwnerId OwnerId,
    DispatchClaimToken ClaimToken,
    StrategyResultIdempotencyKey IdempotencyKey,
    StrategyResultEnvelope Result);

public sealed record ExpireDispatchClaimsCommand(DateTimeOffset NowUtc);

public sealed record ReclaimDispatchClaimCommand(
    ProcessStepInstanceId StepInstanceId,
    DispatcherOwnerId OwnerId,
    DispatchClaimToken NewClaimToken,
    DateTimeOffset LeaseExpiresAtUtc);

public sealed record ProcessRuntimeBlockedRecoveryAuthorization(
    DateTimeOffset ExpectedStateUpdatedAtUtc,
    ProcessRuntimeStatus ExpectedStateStatus,
    ProcessStepInstanceId SourceBlockedStepInstanceId,
    StrategyResultIdempotencyKey SourceResultIdempotencyKey,
    string DiagnosticFingerprint,
    ProcessRecoveryRouteKind RecoveryRouteKind,
    ProcessStepInstanceId? ResponsibleTargetStepInstanceId,
    ProcessRuntimeBlockedRecoveryPhase Phase)
{
    public ProcessRunId? RelatedChildRunId { get; init; }

    public DateTimeOffset? ExpectedRelatedChildUpdatedAtUtc { get; init; }

    public ProcessRuntimeChildLineageEvidence? ExpectedChildLineageEvidence { get; init; }
}

public sealed record RequestStepReworkCommand(
    ProcessStepInstanceId StepInstanceId,
    string Reason)
{
    public ProcessRuntimeBlockedRecoveryAuthorization? BlockedRecoveryAuthorization { get; init; }
}
