namespace CanDoItAll.Application.UI.Dashboard;

public enum DashboardActivityTab {
    Workflows,
    Processes
}

public sealed record DashboardPresentation(
    DashboardSnapshotRead? Snapshot,
    DateTimeOffset NowUtc,
    DateTimeOffset? NextAutomaticRefreshAtUtc,
    string? ErrorMessage,
    bool IsInitialLoading,
    bool IsRefreshing,
    DashboardActivityTab Activity);
