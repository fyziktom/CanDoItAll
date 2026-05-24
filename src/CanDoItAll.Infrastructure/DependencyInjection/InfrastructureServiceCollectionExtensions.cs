using System.Reflection;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.BackgroundJobs;
using CanDoItAll.Infrastructure.Configuration;
using CanDoItAll.Infrastructure.Logging;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Readiness;
using CanDoItAll.Infrastructure.Search;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace CanDoItAll.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddCanDoItAllInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment,
        IReadOnlyList<Assembly> moduleAssemblies)
    {
        AppDbContextModelRegistry.ConfigureAssemblies(moduleAssemblies);
        services.TryAddSingleton(environment);

        services
            .AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection("Database"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services
            .AddOptions<StorageOptions>()
            .Bind(configuration.GetSection("Storage"))
            .ValidateOnStart();

        services
            .AddOptions<WorkbenchOptions>()
            .Bind(configuration.GetSection("Workbench"))
            .ValidateOnStart();

        services
            .AddOptions<DevelopmentManagerOptions>()
            .Bind(configuration.GetSection("DevelopmentManager"))
            .ValidateOnStart();

        services
            .AddOptions<ControlPlaneOptions>()
            .Bind(configuration.GetSection("ControlPlane"))
            .ValidateOnStart();

        var configuredControlPlaneOptions =
            configuration.GetSection("ControlPlane").Get<ControlPlaneOptions>() ?? new ControlPlaneOptions();
        var dataProtectionKeysPath =
            ControlPlanePathDefaults.ResolveControlPlaneRootPath(environment, configuredControlPlaneOptions);

        services.AddDataProtection()
            .SetApplicationName("CanDoItAll")
            .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(dataProtectionKeysPath, "dataprotection-keys")));

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IActivityStream, NullActivityStream>();
        services.AddSingleton<ISecretRedactor, SecretRedactor>();
        services.AddSingleton<IControlPlanePathResolver, ControlPlanePathResolver>();
        services.AddSingleton<IControlPlaneSecretProtector, ControlPlaneSecretProtector>();
        services.AddSingleton<DatabaseProfileControlPlaneService>();
        services.AddSingleton<IDatabaseProfileService>(serviceProvider => serviceProvider.GetRequiredService<DatabaseProfileControlPlaneService>());
        services.AddScoped<IDatabaseTransferService, DatabaseTransferService>();
        services.AddSingleton<IDatabaseSwitchNotificationService, DatabaseSwitchNotificationService>();
        services.AddSingleton<IDatabaseRuntimeState, DatabaseRuntimeState>();
        services.AddSingleton<ICanonicalRuntimeDatabase, CanonicalRuntimeDatabase>();
        services.AddSingleton<CanonicalDatabaseProfileRuntimeAccessor>();
        services.AddSingleton<IActiveDatabaseProfileResolver>(serviceProvider => serviceProvider.GetRequiredService<CanonicalDatabaseProfileRuntimeAccessor>());
        services.AddSingleton<IDatabaseProfileRuntimeAccessor>(serviceProvider => serviceProvider.GetRequiredService<CanonicalDatabaseProfileRuntimeAccessor>());
        services.AddSingleton<IDatabaseDriver, InMemoryDatabaseDriver>();
        services.AddSingleton<IDatabaseDriver, PostgreSqlDatabaseDriver>();
        services.AddSingleton<IDatabaseDriverRegistry, DatabaseDriverRegistry>();
        services.AddPooledDbContextFactory<AppDbContext>((serviceProvider, optionsBuilder) =>
        {
            var canonicalRuntimeDatabase = serviceProvider.GetRequiredService<ICanonicalRuntimeDatabase>();
            AppDbContextOptionsConfigurator.Configure(optionsBuilder, canonicalRuntimeDatabase.Profile);
        });
        services.AddSingleton<IProfileAppDbContextFactory, ProfileAppDbContextFactory>();
        services.AddSingleton<IWorkspacePathResolver, WorkspacePathResolver>();
        services.AddSingleton<IWorkspacePathAccessGuard, WorkspacePathAccessGuard>();
        services.AddSingleton<IStorageCatalogService, StorageCatalogService>();
        services.AddSingleton<IStorageDriver, FileSystemStorageDriver>();
        services.AddSingleton<IStorageDriver, IpfsStorageDriver>();
        services.AddSingleton<IStorageDriver, FtpStorageDriver>();
        services.AddSingleton<IStorageDriverRegistry, StorageDriverRegistry>();
        services.AddSingleton<IStorageRoutingService, DefaultStorageRoutingService>();
        services.AddSingleton<IStorageConnectionTestService, StorageConnectionTestService>();
        services.AddSingleton<IStorageAccessService, StorageAccessService>();
        services.AddSingleton<IStoragePlacementService, StoragePlacementService>();
        services.AddSingleton<IStorageTransferPipeline, StorageTransferPipeline>();
        services.AddScoped<IFileStore, LocalFileStore>();
        services.AddScoped<IManagedArtifactStore, ManagedArtifactStore>();
        services.AddSingleton<IBackgroundJobQueue, InMemoryBackgroundJobQueue>();
        services.AddSingleton<IRuntimeReadinessService, RuntimeReadinessService>();
        services.AddScoped<IBackgroundJobTracker, BackgroundJobTracker>();
        services.AddScoped<ISearchIndexService, SearchIndexService>();

        services.AddHealthChecks()
            .AddCheck<RuntimeReadinessHealthCheck>("runtime-readiness");

        return services;
    }
}
