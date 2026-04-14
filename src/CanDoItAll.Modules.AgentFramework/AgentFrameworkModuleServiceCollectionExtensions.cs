using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Modules.AgentFramework;

public static class AgentFrameworkModuleServiceCollectionExtensions
{
    public static IServiceCollection AddAgentFrameworkModule(this IServiceCollection services)
    {
        return services;
    }
}

public static class AgentFrameworkModuleAssemblyMarker;
