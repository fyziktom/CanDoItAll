using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
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
        services.AddOptions<ApiAccessOptions>();
        services.TryAddSingleton<IApiTokenService, ApiTokenService>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<
            IConnectorManifestSource,
            SharedProviderConnectorManifestSource>());
        services.TryAddScoped<ConnectorPluginRegistry>();
        services.TryAddScoped<ISettingsRendererRegistry, SettingsRendererRegistry>();
        services.AddScoped<ConnectorCommandProcessor>();
        services.AddScoped<ConnectorOutboxService>();
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
            IProviderProfileCommitObserver,
            SharedProviderCatalogProfileCommitObserver>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<
            ISecretDeletionReferencePolicy,
            SharedProviderSourceSecretDeletionReferencePolicy>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<
            IProviderProfileDeletionGuard,
            SharedProviderProfileDeletionGuard>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<
            IProviderDatabaseTransferGuard,
            SharedProviderDatabaseTransferGuard>());
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
        services.AddScoped<IDatabaseTransferHandler, WorkspaceDefaultProviderDatabaseTransferHandler>();
        services.AddScoped<IProjectManagementKnowledgeProvider, StaticProjectManagementKnowledgeProvider>();
        services.AddScoped<ProjectManagementKnowledgeService>();
        return services;
    }
}

public static class WorkspaceModuleAssemblyMarker;
