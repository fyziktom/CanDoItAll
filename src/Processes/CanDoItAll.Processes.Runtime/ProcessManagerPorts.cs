using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Core;

namespace CanDoItAll.Processes.Runtime;

public interface IProcessDiagnosticEvidenceStore
{
    Task<ProcessDiagnosticReference> StoreAsync(
        ProcessRunId runId,
        RuntimeEventId sourceEventId,
        ProcessRestrictedDiagnosticEvidence evidence,
        CancellationToken cancellationToken = default);
}

public interface IProcessIncidentStore
{
    Task<ProcessIncident?> FindByIdempotencyKeyAsync(
        ProcessRunId runId,
        ProcessManagerIdempotencyKey idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<ProcessIncident?> LoadAsync(
        ProcessIncidentId incidentId,
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        ProcessIncident incident,
        CancellationToken cancellationToken = default);

    Task UpdateStatusAsync(
        ProcessIncidentId incidentId,
        ProcessIncidentStatus status,
        RuntimeEventId? resolutionEventId,
        CancellationToken cancellationToken = default);
}

public interface IProcessManagerQueue
{
    Task<ProcessManagerWorkItem?> FindByIdempotencyKeyAsync(
        ProcessRunId runId,
        ProcessManagerIdempotencyKey idempotencyKey,
        CancellationToken cancellationToken = default);

    Task EnqueueAsync(
        ProcessManagerWorkItem item,
        CancellationToken cancellationToken = default);
}

public interface IProcessRecoveryPolicy
{
    ValueTask<ProcessRecoveryPolicyResult> EvaluateAsync(
        ProcessRecoveryPolicyContext context,
        CancellationToken cancellationToken = default);
}

public interface IProcessRecoveryRequestStore
{
    Task<ProcessRecoveryRequest?> FindByIdempotencyKeyAsync(
        ProcessRunId runId,
        ProcessManagerIdempotencyKey idempotencyKey,
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        ProcessRecoveryRequest request,
        CancellationToken cancellationToken = default);
}

public interface IProcessBranchDecisionStore
{
    Task<ProcessBranchDecision?> FindByIdempotencyKeyAsync(
        ProcessRunId runId,
        ProcessBranchDecisionRequestId requestId,
        ProcessManagerIdempotencyKey idempotencyKey,
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        ProcessBranchDecision decision,
        CancellationToken cancellationToken = default);
}

public interface IProcessLoopBudgetLedger
{
    Task<ProcessLoopBudgetConsumptionResult> ConsumeAsync(
        ProcessLoopBudgetConsumption consumption,
        CancellationToken cancellationToken = default);
}

public interface IProcessSubprocessMessageStore
{
    Task<ProcessSubprocessControlMessage?> FindByIdempotencyKeyAsync(
        ProcessRunId parentRunId,
        ProcessRunId childRunId,
        ProcessManagerIdempotencyKey idempotencyKey,
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        ProcessSubprocessControlMessage message,
        CancellationToken cancellationToken = default);
}

public interface IProcessManagerDecisionStore
{
    Task SaveAsync(
        ProcessManagerDecision decision,
        CancellationToken cancellationToken = default);
}

public interface IProcessManagerStrategy
{
    ValueTask<ProcessManagerStrategyProposal> ProposeAsync(
        ProcessManagerStrategyContext context,
        CancellationToken cancellationToken = default);
}

public sealed record ProcessManagerStrategyContext(
    ProcessManagerWorkItem WorkItem,
    ProcessIncident? Incident,
    IReadOnlyList<ProcessRuntimeEventEnvelope> RecentEvents);

public sealed record ProcessManagerStrategyProposal(
    ProcessManagerDecisionKind Kind,
    ProcessManagerIdempotencyKey IdempotencyKey,
    string PayloadHash);

public sealed record ProcessIncidentHandlingResult(
    ProcessIncident Incident,
    ProcessManagerDecision Decision,
    ProcessRuntimeEventEnvelope DecisionEvent,
    ProcessManagerWorkItem WorkItem,
    bool IsDuplicate,
    IReadOnlyList<ProcessValidationFailure> Diagnostics)
{
    public bool Succeeded => Diagnostics.Count == 0;
}

public sealed record ProcessRecoveryEvaluationResult(
    ProcessRecoveryRequest RecoveryRequest,
    ProcessManagerDecision Decision,
    ProcessRuntimeEventEnvelope DecisionEvent,
    ProcessRecoveryDispatchHandoff? DispatchHandoff,
    bool IsDuplicate,
    IReadOnlyList<ProcessValidationFailure> Diagnostics)
{
    public bool Succeeded => Diagnostics.Count == 0 && DispatchHandoff is not null;
}

public sealed record ProcessBranchDecisionHandlingResult(
    ProcessBranchDecision BranchDecision,
    ProcessManagerDecision Decision,
    ProcessRuntimeEventEnvelope DecisionEvent,
    ProcessBranchRouteHandoff? RouteHandoff,
    bool IsDuplicate,
    IReadOnlyList<ProcessValidationFailure> Diagnostics)
{
    public bool Succeeded => Diagnostics.Count == 0 && BranchDecision.Status == ProcessBranchDecisionStatus.Recorded;
}

public sealed record ProcessSubprocessMessageResult(
    ProcessSubprocessControlMessage Message,
    ProcessManagerDecision Decision,
    ProcessRuntimeEventEnvelope DecisionEvent,
    ProcessManagerWorkItem WorkItem,
    bool IsDuplicate,
    IReadOnlyList<ProcessValidationFailure> Diagnostics)
{
    public bool Succeeded => Diagnostics.Count == 0;
}
