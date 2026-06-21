using CanDoItAll.Processes.Abstractions;

namespace CanDoItAll.Processes.Runtime;

public sealed record ProcessManagerDecision(
    ProcessManagerDecisionId DecisionId,
    ProcessRunId RootRunId,
    ProcessRunId RunId,
    ProcessIncidentId? IncidentId,
    ProcessManagerDecisionKind Kind,
    ProcessManagerDecisionStatus Status,
    ProcessManagerIdempotencyKey IdempotencyKey,
    RuntimeEventId DecisionEventId,
    ProcessRecoveryPolicyDenial PolicyDenial,
    string PayloadHash,
    DateTimeOffset OccurredAtUtc);
