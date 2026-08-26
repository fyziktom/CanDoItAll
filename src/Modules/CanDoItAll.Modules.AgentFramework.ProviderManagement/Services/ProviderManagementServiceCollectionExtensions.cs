using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Providers;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Security;
using CanDoItAll.SharedProviders.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace CanDoItAll.Modules.AgentFramework.ProviderManagement;

public static class ProviderManagementServiceCollectionExtensions
{
    public static IServiceCollection AddAgentFrameworkProviderManagement(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddHttpClient();
        services.AddScoped<IProviderAdministrationConnector, OpenAiProviderAdministrationConnector>();
        services.AddScoped<IProviderAdministrationConnector, ScenarioHarnessProviderAdministrationConnector>();
        services.AddScoped<IProviderAdministrationConnector, ProcessMockProviderAdministrationConnector>();
        services.AddScoped<IProviderAdministrationConnector, ComfyUiProviderAdministrationConnector>();
        services.AddScoped<IProviderAdministrationConnector, OllamaProviderAdministrationConnector>();
        services.AddScoped<IProviderAdministrationConnector, OllamaRemoteProviderAdministrationConnector>();
        services.AddScoped<ProviderAdministrationConnectorCatalog>();
        services.AddScoped<IProviderManifestCatalog>(serviceProvider =>
            serviceProvider.GetRequiredService<ProviderAdministrationConnectorCatalog>());
        services.AddScoped<IConnectorManifestSource>(
            serviceProvider => serviceProvider.GetRequiredService<ProviderAdministrationConnectorCatalog>());
        services.AddScoped<ProviderAdministrationService>();
        services.AddScoped<IProviderAdministrationService>(serviceProvider =>
            serviceProvider.GetRequiredService<ProviderAdministrationService>());
        services.AddScoped<
            IProviderRuntimeAdministrationService,
            ProviderRuntimeAdministrationService>();
        services.AddScoped<ProviderProfileMapper>();
        services.AddSingleton<CanonicalProviderRuntimeProfileSnapshotService>();
        services.AddSingleton<IProviderRuntimeProfileSource>(serviceProvider =>
            serviceProvider.GetRequiredService<CanonicalProviderRuntimeProfileSnapshotService>());
        services.AddSingleton<IProviderRuntimeProfileSnapshotSource>(serviceProvider =>
            serviceProvider.GetRequiredService<CanonicalProviderRuntimeProfileSnapshotService>());
        services.AddSingleton<IProviderRuntimeProfileSnapshotInitializer>(serviceProvider =>
            serviceProvider.GetRequiredService<CanonicalProviderRuntimeProfileSnapshotService>());
        services.AddSingleton<IProviderRuntimeProfileSnapshotUpdater>(serviceProvider =>
            serviceProvider.GetRequiredService<CanonicalProviderRuntimeProfileSnapshotService>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<
            IProviderProfileCommitObserver,
            AgentFrameworkProviderRuntimeSnapshotCommitObserver>());
        services.AddScoped<DatabaseProviderProfileRegistry>();
        services.AddScoped<IProviderProfileRegistry>(serviceProvider =>
            serviceProvider.GetRequiredService<DatabaseProviderProfileRegistry>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<
            ISecretDeletionReferencePolicy,
            ProviderSecretDeletionReferencePolicy>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<
            IConnectorManifestSource,
            SharedProviderConnectorManifestSource>());
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
        services.TryAddScoped<ISharedProviderManagementService, SharedProviderManagementService>();
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
        services.AddScoped<IDatabaseTransferHandler, AiProvidersDatabaseTransferHandler>();
        return services;
    }
}
