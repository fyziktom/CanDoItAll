using Microsoft.Extensions.DependencyInjection;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workbench;

public static class WorkbenchModuleServiceCollectionExtensions
{
    public static IServiceCollection AddWorkbenchModule(this IServiceCollection services)
    {
        services.AddScoped<WorkbenchStateService>();
        services.AddScoped<ProjectWorkbenchService>();
        services.AddScoped<ProjectStructureLeaseService>();
        services.AddScoped<ProjectStructureAnalyticsService>();
        services.AddScoped<ProjectStructureChecklistService>();
        services.AddScoped<ProjectStructureImportService>();
        services.AddScoped<ProjectStructureAgentService>();
        services.AddScoped<IProjectGanttPreviewService, ProjectGanttPreviewService>();
        services.AddScoped<IProjectStructureLocalFileOpener, ProjectStructureLocalFileOpener>();
        services.AddScoped<IProjectStructureRuntimeLauncher, ProjectStructureRuntimeLauncher>();
        services.AddScoped<IProjectWorkbenchSeedService>(serviceProvider => serviceProvider.GetRequiredService<ProjectWorkbenchService>());
        return services;
    }
}

public static class WorkbenchModuleAssemblyMarker;


