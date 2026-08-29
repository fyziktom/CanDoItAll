using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using CanDoItAll.Infrastructure.ControlPlane;

namespace CanDoItAll.AgentFramework.ProviderHistory.Persistence;

public static class ProviderHistoryPersistenceServiceCollectionExtensions {
    public static IServiceCollection AddProviderHistoryPersistence(this IServiceCollection services) {
        services.AddSingleton<IProviderHistoryPartition, HistoryPartitionStore>();
        services.AddSingleton<HistoryHostLeaseStore>();
        services.AddSingleton<HistoryTextProtector>();
        services.AddSingleton<HistoryDetailStore>();
        services.AddSingleton<IProviderHistoryCapture, HistoryCaptureStore>();
        services.AddSingleton<IProviderHistoryRecorder, HistoryInvocationRecorder>();
        services.AddSingleton<HistoryRecoveryStore>();
        services.TryAddScoped<IProviderHistoryAccess, UnavailableProviderHistoryAccess>();
        services.AddSingleton<HistoryReadConcurrency>();
        services.AddScoped<HistoryAuthorizedOperation>();
        services.AddSingleton<HistoryCoverageReader>();
        services.AddSingleton<IHistoryReadStore, HistoryReadStore>();
        services.AddSingleton<IHistoryCursorProtector, HistoryCursorProtector>();
        services.AddScoped<IProviderRequestHistory, ProviderRequestHistoryService>();
        services.AddScoped<IProviderHistoryPolicyService, HistoryPolicyStore>();
        services.AddSingleton<HistoryRetentionStore>();
        services.AddScoped<IDatabaseTransferHandler, HistoryDatabaseTransferHandler>();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<HistoryOutboxWriter>();
        services.AddSingleton<HistoryOutboxProcessor>();
        services.AddSingleton<HistoryProjectionWriter>();
        services.AddSingleton<HistorySourceMaintenanceRunner>();
        services.AddHostedService<HistoryMaintenanceHostedService>();
        return services;
    }
}
