using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Modules.AgentFramework;

public static class AgentFrameworkUiServiceCollectionExtensions
{
    public static IServiceCollection AddAgentFrameworkUi(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddCascadingAuthenticationState();
        services.TryAddScoped<IAgentsWorkspaceQuery, AgentsWorkspaceQuery>();
        services.TryAddScoped<IAgentCatalogOperations, AgentCatalogOperations>();
        services.TryAddScoped<IAgentCapabilitiesReads, AgentCapabilitiesReads>();
        services.TryAddScoped<IAgentEditorReads, AgentEditorReads>();
        services.TryAddScoped<IProviderProfilesReads, ProviderProfilesReads>();
        services.TryAddScoped<IProviderEditorCommands, ProviderEditorCommands>();
        services.TryAddScoped<ProviderEditorRecovery>();
        services.TryAddScoped<SharedProviderRecovery>();
        services.TryAddScoped<IAgentEditorCommands, AgentEditorCommands>();
        services.TryAddScoped<IAgentEditorAccessQuery, AgentEditorAccessQuery>();
        services.TryAddScoped<IBoundAgentResourceQuery, BoundAgentResourceQuery>();
        services.TryAddScoped<
            IWorkflowExternalResponsePageActorContextProvider,
            WorkflowExternalResponsePageActorContextProvider>();
        return services;
    }
}
