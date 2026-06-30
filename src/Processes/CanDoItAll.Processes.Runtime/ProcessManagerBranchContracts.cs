using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Core;

namespace CanDoItAll.Processes.Runtime;

public sealed record ProcessBranchDecisionRequest(
    ProcessBranchDecisionRequestId RequestId,
    ProcessRunId RootRunId,
    ProcessRunId RunId,
    ProcessStepInstanceId StepInstanceId,
    ProcessStepDefinitionId StepDefinitionId,
    BranchFamilyId FamilyId,
    IReadOnlyList<BranchOutcomeDefinition> Outcomes,
    IReadOnlyList<string> EvidenceKeys,
    ProcessManagerIdempotencyKey IdempotencyKey,
    ProcessCorrelationId CorrelationId,
    RuntimeEventId CausationEventId,
    string PayloadHash);

public sealed record ProcessBranchDecisionCommand(
    ProcessBranchDecisionRequest Request,
    BranchOutcomeId SelectedOutcomeId,
    decimal Confidence,
    ProcessDiagnosticReference? RationaleReference,
    DateTimeOffset OccurredAtUtc);

public sealed record ProcessBranchDecision(
    ProcessBranchDecisionId DecisionId,
    ProcessBranchDecisionRequestId RequestId,
    ProcessRunId RootRunId,
    ProcessRunId RunId,
    BranchFamilyId FamilyId,
    BranchOutcomeId SelectedOutcomeId,
    ProcessRouteTarget RouteTarget,
    ProcessBranchDecisionStatus Status,
    ProcessManagerIdempotencyKey IdempotencyKey,
    ProcessLoopFingerprintId? LoopFingerprintId,
    int? RemainingLoopBudget,
    decimal Confidence,
    RuntimeEventId DecisionEventId,
    DateTimeOffset CreatedAtUtc);

public sealed record ProcessBranchRouteHandoff(
    ProcessBranchDecisionId DecisionId,
    ProcessRouteTarget RouteTarget,
    RuntimeEventId DecisionEventId);
