using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Workspace.ApiAccess;

namespace CanDoItAll.Modules.Workspace;

public static class WorkspaceModuleServiceCollectionExtensions
{
    public static IServiceCollection AddWorkspaceModule(this IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Scoped<
            IProjectTransferTargetStateParticipant,
            WorkspaceProjectTransferTargetStateParticipant>());
        services.AddHttpClient();
        services.AddOptions<ApiAccessOptions>();
        services.TryAddSingleton<IApiTokenService, ApiTokenService>();
        services.AddScoped<IProviderAdapter, OpenAiProviderAdapter>();
        services.AddScoped<IProviderAdapter, ScenarioHarnessProviderAdapter>();
        services.AddScoped<IProviderAdapter, ProcessMockProviderAdapter>();
        services.AddScoped<IProviderAdapter, ComfyUiProviderAdapter>();
        services.AddScoped<IProviderAdapter, OllamaProviderAdapter>();
        services.AddScoped<IProviderAdapter, OllamaRemoteProviderAdapter>();
        services.AddScoped<ProviderRegistry>();
        services.TryAddScoped<LegacyProviderRuntimeGateway>();
        services.TryAddScoped<IProviderRuntimeGateway>(serviceProvider => serviceProvider.GetRequiredService<LegacyProviderRuntimeGateway>());
        services.AddScoped<IConnectorManifestSource>(serviceProvider => serviceProvider.GetRequiredService<ProviderRegistry>());
        services.TryAddScoped<ConnectorPluginRegistry>();
        services.TryAddScoped<ISettingsRendererRegistry, SettingsRendererRegistry>();
        services.AddScoped<ConnectorCommandProcessor>();
        services.AddScoped<ConnectorOutboxService>();
        services.AddScoped<ProviderExecutionService>();
        services.AddScoped<WorkspaceService>();
        services.AddScoped<DatabaseProfileWorkspaceService>();
        services.AddScoped<IDatabaseTransferHandler, AiProvidersDatabaseTransferHandler>();
        services.AddScoped<IProjectManagementKnowledgeProvider, StaticProjectManagementKnowledgeProvider>();
        services.AddScoped<ProjectManagementKnowledgeService>();
        return services;
    }
}

public static class WorkspaceModuleAssemblyMarker;

