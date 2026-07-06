using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Memory.Application;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Memory.Persistence;

public static class MemoryPersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddGenericMemoryModule(
        this IServiceCollection services,
        Action<MemoryModuleOptions>? configure = null)
    {
        var options = new MemoryModuleOptions();
        configure?.Invoke(options);
        options.Validate();

        AppDbContextModelRegistry.ConfigureAssemblies([typeof(MemoryProviderProfileEntity).Assembly]);

        services.AddSingleton(options);
        services.AddSingleton(options.WorkerOptions);
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IMemoryProviderProfileStore, EfMemoryProviderProfileStore>();
        services.AddScoped<IMemoryOperationLedgerStore, EfMemoryOperationLedgerStore>();
        services.AddScoped<IMemoryFeedbackLedgerStore, EfMemoryFeedbackLedgerStore>();
        services.AddScoped<IMemoryEventLedgerStore, EfMemoryEventLedgerStore>();
        services.AddScoped<IMemorySourceRequestLedgerStore, EfMemorySourceRequestLedgerStore>();
        services.AddScoped<IMemoryRetentionProjectionStore, EfMemoryRetentionProjectionStore>();
        services.AddScoped<IManualSourceSnapshotProvider, ManualMemorySourceSnapshotProvider>();
        services.AddMemorySourceGatewayAdapter<ManualMemorySourceGatewayAdapter>();
        services.AddScoped<ManualMemorySourceIngestionService>();
        services.TryAddScoped<IMemorySourceGateway>(serviceProvider =>
        {
            var adapters = serviceProvider.GetServices<IMemorySourceGatewayAdapter>().ToArray();
            return new MemorySourceGateway(
                adapters,
                adapters
                    .Select(adapter => adapter.Descriptor.SourceKind)
                    .Distinct()
                    .ToArray());
        });
        services.AddScoped<IMemoryRuntimeService, MemoryRuntimeService>();
        services.AddScoped<IMemoryOperationHandler, MemoryOperationHandler>();
        services.AddScoped<IMemoryAsyncOperationWorker, MemoryAsyncOperationWorker>();
        services.AddScoped<IMemoryFeedbackWorker, MemoryFeedbackWorker>();
        services.AddScoped<IMemoryProviderEventWorker, MemoryProviderEventWorker>();
        services.AddScoped<IMemoryRetentionWorker, MemoryRetentionWorker>();

        if (options.EnableDeterministicMockProvider)
        {
            services.AddSingleton<DeterministicMockMemoryProviderDriver>();
            services.AddSingleton<IMemoryProviderDriver>(provider =>
                provider.GetRequiredService<DeterministicMockMemoryProviderDriver>());
        }

        return services;
    }
}

public static class MemoryPersistenceAssemblyMarker;
