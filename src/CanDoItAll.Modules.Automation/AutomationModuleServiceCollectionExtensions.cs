using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Automation;

public static class AutomationModuleServiceCollectionExtensions
{
    public static IServiceCollection AddAutomationModule(this IServiceCollection services)
    {
        services.AddScoped<AutomationWorkspaceService>();
        services.TryAddScoped<IAutomationSignalProvider, NullAutomationSignalProvider>();
        return services;
    }
}

public static class AutomationModuleAssemblyMarker;


