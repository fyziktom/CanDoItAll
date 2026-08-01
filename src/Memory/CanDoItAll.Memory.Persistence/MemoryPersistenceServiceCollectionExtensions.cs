using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Memory.Application;
using CanDoItAll.Memory.Persistence.Hosting;
using Microsoft.Extensions.DependencyInjection;

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
        services.AddSingleton(options.WorkerHosting);
        services.AddMemoryApplication(options.WorkerOptions);
        services.AddScoped<IMemoryProviderProfileStore, EfMemoryProviderProfileStore>();
        services.AddScoped<IMemoryOperationLedgerStore, EfMemoryOperationLedgerStore>();
        services.AddScoped<IMemoryFeedbackLedgerStore, EfMemoryFeedbackLedgerStore>();
        services.AddScoped<IMemoryEventLedgerStore, EfMemoryEventLedgerStore>();
        services.AddScoped<IMemorySourceRequestLedgerStore, EfMemorySourceRequestLedgerStore>();
        services.AddScoped<IMemoryRetentionProjectionStore, EfMemoryRetentionProjectionStore>();
        services.AddSingleton<MemoryWorkerInMemoryLeaseRegistry>();
        services.AddScoped<IMemoryWorkerLeaseStore, EfMemoryWorkerLeaseStore>();
        if (options.WorkerHosting.Enabled)
        {
            services.AddSingleton(MemoryWorkerLeaseOwnerId.CreateUnique());
            services.AddScoped<IMemoryWorkerLeaseRunner, MemoryWorkerLeaseRunner>();
            services.AddScoped<IMemoryBackgroundWorkerCycle, MemoryBackgroundWorkerCycle>();
            services.AddHostedService<MemoryBackgroundWorkerHostedService>();
        }

        return services;
    }
}

public static class MemoryPersistenceAssemblyMarker;
