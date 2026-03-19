using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Modules.Workbench;

public static class WorkbenchModuleServiceCollectionExtensions
{
    public static IServiceCollection AddWorkbenchModule(this IServiceCollection services)
    {
        services.AddScoped<WorkbenchStateService>();
        services.AddScoped<ProjectWorkbenchService>();
        return services;
    }
}

public static class WorkbenchModuleAssemblyMarker;
