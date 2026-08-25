using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Providers;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Modules.AgentFramework.ProviderManagement;

public static class ProviderManagementServiceCollectionExtensions
{
    public static IServiceCollection AddAgentFrameworkProviderManagement(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddHttpClient();
        services.AddScoped<IProviderAdapter, OpenAiProviderAdapter>();
        services.AddScoped<IProviderAdapter, ScenarioHarnessProviderAdapter>();
        services.AddScoped<IProviderAdapter, ProcessMockProviderAdapter>();
        services.AddScoped<IProviderAdapter, ComfyUiProviderAdapter>();
        services.AddScoped<IProviderAdapter, OllamaProviderAdapter>();
        services.AddScoped<IProviderAdapter, OllamaRemoteProviderAdapter>();
        services.AddScoped<ProviderRegistry>();
        services.AddScoped<IProviderManifestCatalog>(serviceProvider =>
            serviceProvider.GetRequiredService<ProviderRegistry>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IConnectorManifestSource>(
            serviceProvider => serviceProvider.GetRequiredService<ProviderRegistry>()));
        services.TryAddScoped<LegacyProviderRuntimeGateway>();
        services.TryAddScoped<IProviderHealthCheckService>(serviceProvider =>
            serviceProvider.GetRequiredService<LegacyProviderRuntimeGateway>());
        services.TryAddScoped<IProviderPromptExecutionService>(serviceProvider =>
            serviceProvider.GetRequiredService<LegacyProviderRuntimeGateway>());
        services.AddScoped<ProviderAdministrationService>();
        services.AddScoped<IProviderAdministrationService>(serviceProvider =>
            serviceProvider.GetRequiredService<ProviderAdministrationService>());
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
        services.AddScoped<IDatabaseTransferHandler, AiProvidersDatabaseTransferHandler>();
        return services;
    }
}
