using Microsoft.Extensions.DependencyInjection;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workbench;

public static class WorkbenchModuleServiceCollectionExtensions
{
    public static IServiceCollection AddWorkbenchModule(this IServiceCollection services)
    {
        services.AddScoped<WorkbenchStateService>();
        services.AddScoped<ProjectWorkbenchService>();
        services.AddScoped<IProjectStructureLocalFileOpener, ProjectStructureLocalFileOpener>();
        services.AddScoped<IProjectWorkbenchSeedService>(serviceProvider => serviceProvider.GetRequiredService<ProjectWorkbenchService>());
        return services;
    }
}

public static class WorkbenchModuleAssemblyMarker;
