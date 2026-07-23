using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Web.Dashboard;

public static class DashboardServiceCollectionExtensions
{
    public static IServiceCollection AddCanDoItAllDashboard(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<DashboardSnapshotOptions>()
            .Validate(
                options => options.RefreshInterval > TimeSpan.Zero,
                "Dashboard snapshot refresh interval must be positive.")
            .ValidateOnStart();
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IDashboardSnapshotLoadRunner, DashboardSnapshotLoadRunner>();
        services.AddSingleton<DashboardSnapshotCache>();
        services.AddScoped<IDashboardSnapshotLoader, DashboardSnapshotLoader>();
        services.AddSingleton<DashboardSnapshotService>();
        return services;
    }
}
