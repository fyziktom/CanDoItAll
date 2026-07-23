using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CanDoItAll.Web.Dashboard;

public interface IDashboardSnapshotLoadRunner
{
    Task<DashboardSnapshotData> LoadAsync();
}

public sealed class DashboardSnapshotLoadRunner(
    IServiceScopeFactory scopeFactory,
    IHostApplicationLifetime applicationLifetime) : IDashboardSnapshotLoadRunner
{
    public async Task<DashboardSnapshotData> LoadAsync()
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var loader = scope.ServiceProvider.GetRequiredService<IDashboardSnapshotLoader>();
        return await loader.LoadAsync(applicationLifetime.ApplicationStopping).ConfigureAwait(false);
    }
}
