namespace CanDoItAll.Web.Dashboard;

public sealed class DashboardSnapshotService(DashboardSnapshotCache cache)
{
    public Task<DashboardSnapshotRead> GetAsync(
        DashboardSnapshotRefreshMode refreshMode = DashboardSnapshotRefreshMode.UseCached,
        CancellationToken cancellationToken = default)
    {
        return cache.GetAsync(refreshMode, cancellationToken);
    }
}
