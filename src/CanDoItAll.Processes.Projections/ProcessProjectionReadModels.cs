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
    RequestRework
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
    string? RestrictedDiagnosticReference);

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
    public IReadOnlyList<ProcessRuntimeOperatorActionProjection> OperatorActions { get; init; } = [];
}

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
    IReadOnlyList<ProcessLiveRunEventProjection> RecentEvents);

public sealed record ProcessTimelineEventProjection(
    RuntimeEventId EventId,
    long GlobalSequence,
    ProcessRunId RootRunId,
    ProcessRunId RunId,
    string EventType,
    DateTimeOffset OccurredAtUtc,
    ProcessProjectedSensitivity Sensitivity,
    string Summary,
    string? RestrictedDiagnosticReference);

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
