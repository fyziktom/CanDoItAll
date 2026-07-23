using System.Collections.Immutable;

namespace CanDoItAll.Web.Dashboard;

public enum DashboardActivityMode
{
    Active,
    RecentFallback
}

public enum DashboardStatusTone
{
    Neutral,
    Info,
    Success,
    Warning,
    Danger
}

public sealed record DashboardDisplayStatus(
    string Label,
    DashboardStatusTone Tone);

public sealed record DashboardProjectItem(
    Guid ProjectId,
    string Name,
    string CurrentPhase,
    DashboardDisplayStatus Status,
    DateTimeOffset UpdatedAtUtc);

public sealed record DashboardWorkflowRunItem(
    Guid WorkflowId,
    Guid RunId,
    string WorkflowName,
    string Summary,
    DashboardDisplayStatus Status,
    DateTimeOffset UpdatedAtUtc);

public sealed record DashboardProcessRunItem(
    Guid RunId,
    string ProcessName,
    string ProjectName,
    DashboardDisplayStatus Status,
    DateTimeOffset UpdatedAtUtc,
    bool ProjectionIsBehind);

public sealed record DashboardUsageTotals(
    long ObservedTokens,
    decimal KnownCostUsd,
    int UnknownUsageObservationCount,
    DateTimeOffset UpdatedAtUtc);

public sealed record DashboardSnapshotData(
    ImmutableArray<DashboardProjectItem> Projects,
    DashboardActivityMode WorkflowMode,
    ImmutableArray<DashboardWorkflowRunItem> Workflows,
    DashboardActivityMode ProcessMode,
    ImmutableArray<DashboardProcessRunItem> Processes,
    DashboardUsageTotals Usage);

public sealed record DashboardSnapshot(
    DashboardSnapshotData Data,
    DateTimeOffset CapturedAtUtc);

public enum DashboardSnapshotState
{
    Current,
    StaleAfterRefreshFailure
}

public sealed record DashboardSnapshotRead(
    DashboardSnapshot Snapshot,
    DateTimeOffset NextAutomaticRefreshAtUtc,
    DashboardSnapshotState State,
    DateTimeOffset? LastRefreshFailureAtUtc)
{
    public bool HasRefreshFailure => State == DashboardSnapshotState.StaleAfterRefreshFailure;
}
