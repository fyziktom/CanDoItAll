using CanDoItAll.Processes.Abstractions;

namespace CanDoItAll.Processes.Runtime;

public sealed record ProcessRecoveryEvaluationRequest(
    ProcessRecoveryRequestId RecoveryRequestId,
    ProcessIncidentId IncidentId,
    ProcessRecoveryActionKind RequestedAction,
    ProcessManagerIdempotencyKey IdempotencyKey,
    ProcessLoopFingerprintId LoopFingerprintId,
    int MaximumAttempts,
    bool ApprovalGranted,
    bool StrategyAllowsRepeat,
    DateTimeOffset OccurredAtUtc,
    string PayloadHash);

public sealed record ProcessRecoveryPolicyContext(
    ProcessIncident Incident,
    ProcessRecoveryEvaluationRequest Request);

public sealed record ProcessRecoveryPolicyResult(
    ProcessRecoveryPolicyDecision Decision,
    ProcessRecoveryPolicyDenial Denial,
    ProcessEscalationOwnerId? EscalationOwner);

public sealed record ProcessRecoveryRequest(
    ProcessRecoveryRequestId RecoveryRequestId,
    ProcessIncidentId IncidentId,
    ProcessRunId RootRunId,
    ProcessRunId RunId,
    ProcessRecoveryActionKind RequestedAction,
    ProcessRecoveryRequestStatus Status,
    ProcessManagerIdempotencyKey IdempotencyKey,
    ProcessLoopFingerprintId LoopFingerprintId,
    int ConsumedAttempt,
    int MaximumAttempts,
    ProcessRecoveryPolicyDenial PolicyDenial,
    string PayloadHash,
    DateTimeOffset CreatedAtUtc,
    RuntimeEventId DecisionEventId);

public sealed record ProcessRecoveryDispatchHandoff(
    ProcessRecoveryRequestId RecoveryRequestId,
    ProcessIncidentId IncidentId,
    ProcessRunId RunId,
    ProcessRecoveryActionKind RequestedAction,
    RuntimeEventId DecisionEventId);

public sealed record ProcessLoopBudgetConsumption(
    ProcessRunId RootRunId,
    ProcessLoopFingerprintId FingerprintId,
    int MaximumRepeats,
    ProcessManagerIdempotencyKey IdempotencyKey,
    DateTimeOffset OccurredAtUtc);

public sealed record ProcessLoopBudgetConsumptionResult(
    ProcessLoopBudgetOutcome Outcome,
    ProcessLoopFingerprintId FingerprintId,
    int ConsumedCount,
    int MaximumRepeats)
{
    public int Remaining => Math.Max(0, MaximumRepeats - ConsumedCount);
}
