using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Core;

namespace CanDoItAll.Processes.Projections;

public sealed record ProcessProjectionLag(
    long LatestKnownGlobalSequence,
    long LastProcessedGlobalSequence,
    int BacklogEventCount);

public sealed record ProcessProjectionFreshness(
    DateTimeOffset ObservedAtUtc,
    long SourceGlobalSequence,
    ProcessProjectionLag Lag);

public enum ProcessProjectedRunStatus
{
    Unknown,
    Active,
    NeedsAttention,
    Completed,
    Failed,
    Cancelled
}

public enum ProcessProjectedSensitivity
{
    Normal,
    Restricted
}

public enum ProcessRuntimeOperatorActionKind
{
    RequestRework,
    CancelRun
}

public sealed record ProcessLiveRunEventProjection(
    RuntimeEventId EventId,
    long GlobalSequence,
    ProcessRunId RootRunId,
    ProcessRunId RunId,
    string EventType,
    DateTimeOffset OccurredAtUtc,
    ProcessProjectedSensitivity Sensitivity,
    string Summary,
    string? RestrictedDiagnosticReference)
{
    public IReadOnlyList<ProcessRuntimeDiagnosticProjection> Diagnostics { get; init; } = [];
}

public sealed record ProcessLiveProcessSnapshot(
    ProcessRunId RootRunId,
    ProcessRunId RunId,
    ProcessProjectedRunStatus Status,
    bool IsActive,
    DateTimeOffset FirstEventAtUtc,
    DateTimeOffset LastEventAtUtc,
    ProcessProjectionFreshness Freshness,
    IReadOnlyList<ProcessLiveRunEventProjection> RecentEvents,
    IReadOnlyList<ProcessIncidentProjection> Incidents)
{
    public Guid? ProjectId { get; init; }

    public string ProjectName { get; init; } = string.Empty;

    public string ProcessName { get; init; } = string.Empty;

    public bool IsSubprocess { get; init; }

    public IReadOnlyList<ProcessRuntimeOperatorActionProjection> OperatorActions { get; init; } = [];

    public IReadOnlyList<ProcessRuntimeChildRunWaitProjection> WaitingOnChildRuns { get; init; } = [];

    public ProcessRuntimeCurrentStepProjection? CurrentStep { get; init; }

    public IReadOnlyList<ProcessRuntimeDiagnosticProjection> Diagnostics { get; init; } = [];

    public int ExecutableStepCount { get; init; }

    public int CompletedStepCount { get; init; }

    public int TerminalStepCount { get; init; }

    public string ProgressLabel { get; init; } = string.Empty;
}

public sealed record ProcessRuntimeCurrentStepProjection(
    Guid RunId,
    Guid StepInstanceId,
    string StepKey,
    string StepStatus,
    string RoleKey,
    string RoleDisplayName,
    string ExecutorDisplayName,
    int AttemptNumber,
    bool IsWorking,
    bool IsLeaseExpired,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? ClaimedAtUtc,
    DateTimeOffset? LeaseExpiresAtUtc,
    string Summary)
{
    public IReadOnlyList<ProcessRuntimeDiagnosticProjection> Diagnostics { get; init; } = [];

    public IReadOnlyList<ProcessRuntimeArtifactLineageProjection> ProducedArtifacts { get; init; } = [];
}

public sealed record ProcessRuntimeDiagnosticProjection(
    Guid RunId,
    Guid StepInstanceId,
    string StepKey,
    string StrategyId,
    string ResultHash,
    string Code,
    string Category,
    string SafeSummary,
    string Sensitivity,
    string RetrySafety,
    string Idempotency,
    string? RestrictedDiagnosticReference)
{
    public ProcessRuntimeOperatorDiagnosticDetailsProjection? OperatorDetails { get; init; }
}

public sealed record ProcessRuntimeOperatorDiagnosticDetailsProjection(
    string GateId,
    string BranchOutcomeKey,
    string RouteTargetBranchOutcomeKey,
    IReadOnlyList<string> FailedCriteriaIds,
    IReadOnlyList<string> ReceiptRuleIds,
    string NextAction);

public sealed record ProcessRuntimeArtifactLineageProjection(
    Guid SlotId,
    Guid ArtifactId,
    string ContentHash);

public sealed record ProcessRuntimeRecoveryDecisionProjection(
    string FailureCategory,
    string DecisionKind,
    string SourceDiagnosticCode,
    string Policy,
    string SafeReason);

public sealed record ProcessRuntimeResultLineageProjection(
    Guid RunId,
    Guid StepInstanceId,
    string StepKey,
    string StrategyId,
    Guid IdempotencyKey,
    string Outcome,
    string AppliedStepStatus,
    string ResultHash,
    IReadOnlyList<ProcessRuntimeDiagnosticProjection> Diagnostics,
    IReadOnlyList<ProcessRuntimeArtifactLineageProjection> ProducedArtifacts,
    ProcessRuntimeRecoveryDecisionProjection? RecoveryDecision);

public sealed record ProcessRuntimeChildRunWaitProjection(
    Guid ParentRunId,
    Guid ParentStepInstanceId,
    string ParentStepKey,
    string ParentStepStatus,
    Guid ChildRunId,
    string ChildRunStatus,
    string? ChildStepKey,
    string? ChildStepStatus,
    string Summary);

public sealed record ProcessRuntimeOperatorActionProjection(
    Guid RunId,
    Guid StepInstanceId,
    string StepKey,
    string StepStatus,
    string RoleKey,
    string RoleDisplayName,
    string ExecutorDisplayName,
    ProcessRuntimeOperatorActionKind Kind,
    string Label,
    string Summary,
    bool IsEnabled,
    string? DisabledReason)
{
    public string ProblemSummary { get; init; } = string.Empty;

    public string RequiredOperatorDecision { get; init; } = string.Empty;

    public string RecommendedInstruction { get; init; } = string.Empty;

    public bool PrimaryRootCause { get; init; }
}

public sealed record ProcessRunDetailProjection(
    ProcessRunId RootRunId,
    ProcessRunId RunId,
    ProcessProjectedRunStatus Status,
    DateTimeOffset FirstEventAtUtc,
    DateTimeOffset LastEventAtUtc,
    ProcessProjectionFreshness Freshness,
    IReadOnlyList<ProcessLiveRunEventProjection> RecentEvents)
{
    public IReadOnlyList<ProcessRuntimeDiagnosticProjection> Diagnostics { get; init; } = [];

    public IReadOnlyList<ProcessRuntimeResultLineageProjection> ResultLineage { get; init; } = [];
}

public sealed record ProcessTimelineEventProjection(
    RuntimeEventId EventId,
    long GlobalSequence,
    ProcessRunId RootRunId,
    ProcessRunId RunId,
    string EventType,
    DateTimeOffset OccurredAtUtc,
    ProcessProjectedSensitivity Sensitivity,
    string Summary,
    string? RestrictedDiagnosticReference)
{
    public IReadOnlyList<ProcessRuntimeDiagnosticProjection> Diagnostics { get; init; } = [];
}

public sealed record ProcessIncidentProjection(
    string IncidentId,
    ProcessRunId RootRunId,
    ProcessRunId RunId,
    string Classification,
    string Severity,
    string Status,
    string SafeSummary,
    string DiagnosticReference,
    DateTimeOffset RaisedAtUtc);

public sealed record ProcessManagerMessageProjection(
    string MessageId,
    ProcessRunId RootRunId,
    ProcessRunId RunId,
    string Kind,
    string Summary,
    DateTimeOffset CreatedAtUtc,
    ProcessProjectedSensitivity Sensitivity,
    string? RestrictedDiagnosticReference);

public sealed record ProcessRuntimeCanvasNodeProjection(
    string NodeId,
    string Label,
    ProcessProjectedRunStatus Status,
    bool IsActive);

public sealed record ProcessRuntimeCanvasProjection(
    ProcessRunId RootRunId,
    ProcessRunId RunId,
    ProcessProjectionFreshness Freshness,
    IReadOnlyList<ProcessRuntimeCanvasNodeProjection> Nodes);

public sealed record ProcessDefinitionCanvasNodeProjection(
    string NodeId,
    string Label,
    string Kind);

public sealed record ProcessDefinitionCanvasRouteProjection(
    string SourceNodeId,
    string TargetNodeId,
    string OutcomeId);

public sealed record ProcessDefinitionCanvasProjection(
    string DefinitionId,
    ProcessProjectionFreshness Freshness,
    IReadOnlyList<ProcessDefinitionCanvasNodeProjection> Nodes,
    IReadOnlyList<ProcessDefinitionCanvasRouteProjection> Routes);

public sealed record ProcessArtifactSlotProjection(
    ArtifactSlotId SlotId,
    string Status,
    string? ArtifactReference);

public sealed record ProcessArtifactMapProjection(
    ProcessRunId RootRunId,
    ProcessRunId RunId,
    ProcessProjectionFreshness Freshness,
    IReadOnlyList<ProcessArtifactSlotProjection> Slots);
