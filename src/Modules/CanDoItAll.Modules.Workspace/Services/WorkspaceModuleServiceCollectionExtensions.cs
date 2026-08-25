using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Security;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.SharedProviders.Abstractions;

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
        services.TryAddEnumerable(ServiceDescriptor.Singleton<
            IConnectorManifestSource,
            SharedProviderConnectorManifestSource>());
        services.TryAddScoped<ConnectorPluginRegistry>();
        services.TryAddScoped<ISettingsRendererRegistry, SettingsRendererRegistry>();
        services.AddScoped<ConnectorCommandProcessor>();
        services.AddScoped<ConnectorOutboxService>();
        services.AddScoped<ProviderExecutionService>();
        services.AddScoped<WorkspaceService>();
        services.TryAddScoped<SharedProviderServiceIdentityStore>();
        services.TryAddScoped<SharedProviderPublicationStore>();
        services.TryAddScoped<SharedProviderPublicationEligibilityPolicy>();
        services.TryAddScoped<SharedProviderPublicationApplicationService>();
        services.TryAddSingleton<SharedProviderCatalogCache>();
        services.TryAddScoped<SharedProviderCatalogQueryService>();
        services.TryAddScoped<ISharedProviderCatalogQueryService>(serviceProvider =>
            serviceProvider.GetRequiredService<SharedProviderCatalogQueryService>());
        services.TryAddScoped<ISharedProviderRoutingResolver>(serviceProvider =>
            serviceProvider.GetRequiredService<SharedProviderCatalogQueryService>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<
            ISharedProviderPublicationCommitObserver,
            SharedProviderCatalogPublicationCommitObserver>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<
            IWorkspaceProviderProfileCommitObserver,
            SharedProviderCatalogProfileCommitObserver>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<
            ISecretDeletionReferencePolicy,
            WorkspaceProviderSecretDeletionReferencePolicy>());
        services.TryAddScoped<SharedProviderSourceService>();
        services.TryAddScoped<SharedProviderReconciliationCoordinator>();
        services.TryAddScoped<SharedProviderSourceSyncService>();
        services.TryAddScoped<SharedProviderInvocationAuditService>();
        services.TryAddScoped<SharedProviderInvocationRecoveryService>();
        services.TryAddSingleton<SharedProviderInvocationRecoverySchedule>(_ =>
            SharedProviderInvocationRecoverySchedule.Default);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<
            IHostedService,
            SharedProviderInvocationRecoveryWorker>());
        services.TryAddScoped<
            ISharedProviderImageExecutionTargetResolver,
            SharedProviderImageExecutionTargetResolver>();
        services.TryAddScoped<
            ISharedProviderRelayApplicationService,
            SharedProviderRelayApplicationService>();
        services.TryAddScoped<IStorageCatalogSelectionSource, WorkspaceStorageCatalogSelectionSource>();
        services.AddScoped<DatabaseProfileWorkspaceService>();
        services.AddScoped<IDatabaseTransferHandler, AiProvidersDatabaseTransferHandler>();
        services.AddScoped<IProjectManagementKnowledgeProvider, StaticProjectManagementKnowledgeProvider>();
        services.AddScoped<ProjectManagementKnowledgeService>();
        return services;
    }
}

public static class WorkspaceModuleAssemblyMarker;
