using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Modules.AgentFramework.ProviderManagement;

public static class ProviderManagementServiceCollectionExtensions
{
    public static IServiceCollection AddAgentFrameworkProviderManagement(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services;
    }
}
