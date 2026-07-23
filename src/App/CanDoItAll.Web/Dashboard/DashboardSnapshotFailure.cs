namespace CanDoItAll.Web.Dashboard;

public enum DashboardSnapshotSource
{
    Coordinator,
    Projects,
    Workflows,
    Processes,
    AgentUsage
}

public sealed class DashboardSnapshotSourceException : Exception
{
    public DashboardSnapshotSourceException(
        DashboardSnapshotSource snapshotSource,
        Exception innerException)
        : base($"Dashboard snapshot source '{snapshotSource}' failed.", innerException)
    {
        ArgumentNullException.ThrowIfNull(innerException);
        SnapshotSource = snapshotSource;
    }

    public DashboardSnapshotSource SnapshotSource { get; }
}
