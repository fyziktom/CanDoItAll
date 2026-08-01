using CanDoItAll.Memory.SourceGateway;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Memory.Application;

public static class MemoryApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddMemoryApplication(
        this IServiceCollection services,
        MemoryAsyncWorkerOptions workerOptions)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(workerOptions);
        workerOptions.Validate();

        services.TryAddSingleton(workerOptions);
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.TryAddScoped<IManualSourceSnapshotProvider, ManualMemorySourceSnapshotProvider>();
        services.AddMemorySourceGatewayAdapter<ManualMemorySourceGatewayAdapter>();
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
        services.TryAddScoped<MemoryOperationCoordinator>();
        services.TryAddScoped<MemoryQueryOperationService>();
        services.TryAddScoped<MemorySourceCaptureOperationService>();
        services.TryAddScoped<MemoryFeedbackOperationService>();
        services.TryAddScoped<MemoryStatusOperationService>();
        services.TryAddScoped<MemoryEventOperationService>();
        services.TryAddScoped<IMemoryOperationAccessAuthorizer, ExactMemoryOperationAccessAuthorizer>();
        services.TryAddScoped<IMemoryOperationHandler>(serviceProvider =>
            new MemoryOperationHandler(
                serviceProvider.GetRequiredService<MemoryQueryOperationService>(),
                serviceProvider.GetRequiredService<MemorySourceCaptureOperationService>(),
                serviceProvider.GetRequiredService<MemoryFeedbackOperationService>(),
                serviceProvider.GetRequiredService<MemoryStatusOperationService>(),
                serviceProvider.GetRequiredService<MemoryEventOperationService>()));
        services.TryAddScoped<IMemoryRuntimeService, MemoryRuntimeService>();
        services.TryAddScoped<ManualMemorySourceIngestionService>();
        services.TryAddScoped<IMemoryAsyncOperationWorker, MemoryAsyncOperationWorker>();
        services.TryAddScoped<IMemoryFeedbackWorker, MemoryFeedbackWorker>();
        services.TryAddScoped<MemoryProviderEventInboxProcessor>();
        services.TryAddScoped<MemoryProviderEventOutboxProcessor>();
        services.TryAddScoped<IMemoryProviderEventWorker, MemoryProviderEventWorker>();
        services.TryAddScoped<IMemoryRetentionWorker, MemoryRetentionWorker>();

        return services;
    }
}
