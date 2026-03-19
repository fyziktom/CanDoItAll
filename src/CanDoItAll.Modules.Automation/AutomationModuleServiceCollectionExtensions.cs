using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Modules.Automation;

public static class AutomationModuleServiceCollectionExtensions
{
    public static IServiceCollection AddAutomationModule(this IServiceCollection services)
    {
        services.AddScoped<AutomationWorkspaceService>();
        return services;
    }
}

public static class AutomationModuleAssemblyMarker;
