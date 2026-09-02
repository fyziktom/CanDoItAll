using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Modules.AgentFramework;

public static class AgentFrameworkUiServiceCollectionExtensions
{
    public static IServiceCollection AddAgentFrameworkUi(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddCascadingAuthenticationState();
        services.TryAddScoped<
            IWorkflowExternalResponsePageActorContextProvider,
            WorkflowExternalResponsePageActorContextProvider>();
        return services;
    }
}
