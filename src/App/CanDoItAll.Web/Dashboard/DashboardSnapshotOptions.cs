namespace CanDoItAll.Web.Dashboard;

public sealed class DashboardSnapshotOptions
{
    public static readonly TimeSpan DefaultRefreshInterval = TimeSpan.FromMinutes(5);

    public TimeSpan RefreshInterval { get; set; } = DefaultRefreshInterval;
}

public enum DashboardSnapshotRefreshMode
{
    UseCached,
    Force
}
