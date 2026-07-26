using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Builder;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Application;

public enum ProcessBlockedRunRecoveryActionKind
{
    None,
    CurrentStepRework,
    UpstreamStepRework
}

public enum ProcessBlockedRunRecoveryPolicy
{
    None,
    SafeIdempotentRework,
    MissingOutputRework,
    MissingInputProducerRework,
    RestoredInputConsumerRework,
    CompletedChildConsumerRework,
    AgentTransientNoSideEffectsRework
}

public enum ProcessBlockedRunRecoveryOutcome
{
    NotBlocked,
    Recovered,
    RequiresAttention
}

public sealed record ProcessBlockedRunRecoveryCommand(
    ProcessRunId RunId,
    ProcessStepInstanceId BlockedStepInstanceId,
    ProcessStepInstanceId TargetStepInstanceId,
    ProcessBlockedRunRecoveryActionKind ActionKind,
    ProcessBlockedRunRecoveryPolicy Policy,
    StrategyResultIdempotencyKey SourceResultIdempotencyKey,
    string DiagnosticFingerprint,
    ProcessRecoveryRouteKind RecoveryRouteKind,
    ProcessStepInstanceId? ResponsibleStepInstanceId,
    ProcessRuntimeBlockedRecoveryPhase Phase,
    DateTimeOffset ExpectedStateUpdatedAtUtc)
{
    public ProcessRunId? RelatedChildRunId { get; init; }

    public DateTimeOffset? ExpectedRelatedChildUpdatedAtUtc { get; init; }

    public ProcessRuntimeChildLineageEvidence? ExpectedChildLineageEvidence { get; init; }
}

public sealed record ProcessBlockedRunRecoveryCommandResult(
    bool Succeeded,
    ProcessRuntimeStatus Status,
    IReadOnlyList<string> Diagnostics);

public sealed record ProcessBlockedRunRecoveryResult(
    ProcessRunId RunId,
    ProcessBlockedRunRecoveryOutcome Outcome,
    ProcessBlockedRunRecoveryActionKind ActionKind,
    ProcessStepInstanceId? TargetStepInstanceId,
    ProcessBlockedRunRecoveryPolicy Policy,
    ProcessRuntimeStatus Status,
    IReadOnlyList<string> Diagnostics);

public interface IProcessBlockedRunRecoveryCommandExecutor
{
    Task<ProcessBlockedRunRecoveryCommandResult> ExecuteAsync(
        ProcessBlockedRunRecoveryCommand command,
        string requestedBy,
        CancellationToken cancellationToken = default);
}

public interface IProcessBlockedRunRecoveryPolicyCatalog
{
    ProcessBlockedRunRecoveryPolicy Resolve(
        ProcessRuntimeStateSnapshot state,
        ProcessInstancePlan plan,
        ProcessRuntimeStepState blockedStep,
        ProcessRuntimeStepAssignment targetAssignment,
        StrategyResultReceipt receipt,
        ProcessRecoveryDecisionReceipt decision);
}

public interface IProcessBlockedRunRecoveryCoordinator
{
    Task<ProcessBlockedRunRecoveryResult> TryRecoverAsync(
        ProcessRunId runId,
        string requestedBy,
        CancellationToken cancellationToken = default);
}
