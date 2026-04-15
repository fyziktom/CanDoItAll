using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Modules.Workspace;

public static class WorkspaceModuleServiceCollectionExtensions
{
    public static IServiceCollection AddWorkspaceModule(this IServiceCollection services)
    {
        services.AddHttpClient();
        services.AddScoped<IProviderAdapter, OpenAiProviderAdapter>();
        services.AddScoped<IProviderAdapter, ScenarioHarnessProviderAdapter>();
        services.AddScoped<IProviderAdapter, OllamaProviderAdapter>();
        services.AddScoped<IProviderAdapter, OllamaRemoteProviderAdapter>();
        services.AddScoped<ProviderRegistry>();
        services.TryAddScoped<LegacyProviderRuntimeGateway>();
        services.TryAddScoped<IProviderRuntimeGateway>(serviceProvider => serviceProvider.GetRequiredService<LegacyProviderRuntimeGateway>());
        services.AddScoped<IConnectorManifestSource>(serviceProvider => serviceProvider.GetRequiredService<ProviderRegistry>());
        services.TryAddScoped<ConnectorPluginRegistry>();
        services.AddScoped<ConnectorCommandProcessor>();
        services.AddScoped<ConnectorOutboxService>();
        services.AddScoped<ProviderExecutionService>();
        services.AddScoped<WorkspaceService>();
        services.AddScoped<DatabaseProfileWorkspaceService>();
        services.AddScoped<ProjectStructureAgentAdministrationService>();
        services.AddScoped<IProjectManagementKnowledgeProvider, StaticProjectManagementKnowledgeProvider>();
        services.AddScoped<ProjectManagementKnowledgeService>();
        return services;
    }
}

public static class WorkspaceModuleAssemblyMarker;


