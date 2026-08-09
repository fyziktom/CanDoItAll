using System.Reflection;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.BackgroundJobs;
using CanDoItAll.Infrastructure.Configuration;
using CanDoItAll.Infrastructure.FileSystem;
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
        services.TryAddSingleton(TimeProvider.System);

        services
            .AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection("Database"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services
            .AddOptions<PostgreSqlStartupReadinessOptions>()
            .Bind(configuration.GetSection(PostgreSqlStartupReadinessOptions.SectionName))
            .Validate(
                options => options.Timeout >= TimeSpan.FromSeconds(1) &&
                    options.Timeout <= TimeSpan.FromHours(1),
                "PostgreSQL startup readiness timeout must be between one second and one hour.")
            .Validate(
                options => options.InitialRetryDelay >= TimeSpan.FromMilliseconds(100) &&
                    options.InitialRetryDelay <= TimeSpan.FromSeconds(30),
                "PostgreSQL startup readiness initial retry delay must be between 100 milliseconds and 30 seconds.")
            .Validate(
                options => options.MaximumRetryDelay >= options.InitialRetryDelay &&
                    options.MaximumRetryDelay <= TimeSpan.FromMinutes(1),
                "PostgreSQL startup readiness maximum retry delay must be at least the initial delay and no more than one minute.")
            .Validate(
                options => options.MaximumRetryDelay <= options.Timeout,
                "PostgreSQL startup readiness maximum retry delay cannot exceed the timeout.")
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

        services
            .AddOptions<DataProtectionKeyProtectionOptions>()
            .Bind(configuration.GetSection(DataProtectionKeyProtectionOptions.SectionName))
            .ValidateOnStart();

        services
            .AddOptions<CurrencyOptions>()
            .Bind(configuration.GetSection(CurrencyOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.TryAddSingleton<CurrencyDisplayState>();
        services.TryAddSingleton<ICurrencyFormatter, CurrencyFormatter>();

        var configuredControlPlaneOptions =
            configuration.GetSection("ControlPlane").Get<ControlPlaneOptions>() ?? new ControlPlaneOptions();
        var dataProtectionKeysPath =
            ControlPlanePathDefaults.ResolveDataProtectionKeysPath(environment, configuredControlPlaneOptions);
        var bootstrapFileWriter = new DurableFileWriter(new PhysicalFileSystemPathPolicyFactory());
        DataProtectionKeyRingProtection.Configure(
            services.AddDataProtection(),
            configuration,
            environment,
            dataProtectionKeysPath,
            bootstrapFileWriter);

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IActivityStream, NullActivityStream>();
        services.AddSingleton<ISecretRedactor, SecretRedactor>();
        services.AddSingleton<IControlPlanePathResolver, ControlPlanePathResolver>();
        services.AddSingleton<IFileApplicationPreferenceService, FileApplicationPreferenceService>();
        services.AddSingleton<IControlPlaneSecretProtector, ControlPlaneSecretProtector>();
        services.AddSingleton<DatabaseProfileControlPlaneService>();
        services.AddSingleton<IDatabaseProfileService>(serviceProvider => serviceProvider.GetRequiredService<DatabaseProfileControlPlaneService>());
        services.AddSingleton<IControlPlaneSecretContinuityVerifier>(serviceProvider =>
            serviceProvider.GetRequiredService<DatabaseProfileControlPlaneService>());
        services.AddScoped<IDatabaseTransferService, DatabaseTransferService>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<
            IProjectTransferTargetStateParticipant,
            InfrastructureProjectTransferTargetStateParticipant>());
        services.AddSingleton<IDatabaseSwitchNotificationService, DatabaseSwitchNotificationService>();
        services.AddSingleton<IDatabaseRuntimeState, DatabaseRuntimeState>();
        services.AddSingleton<ICanonicalRuntimeDatabase, CanonicalRuntimeDatabase>();
        services.AddSingleton<CanonicalDatabaseProfileRuntimeAccessor>();
        services.AddSingleton<IActiveDatabaseProfileResolver>(serviceProvider => serviceProvider.GetRequiredService<CanonicalDatabaseProfileRuntimeAccessor>());
        services.AddSingleton<IDatabaseProfileRuntimeAccessor>(serviceProvider => serviceProvider.GetRequiredService<CanonicalDatabaseProfileRuntimeAccessor>());
        services.AddSingleton<IDatabaseDriver, InMemoryDatabaseDriver>();
        services.AddSingleton<PostgreSqlStartupReadinessPolicy>();
        services.AddSingleton<IDatabaseDriver, PostgreSqlDatabaseDriver>();
        services.AddSingleton<IDatabaseDriverRegistry, DatabaseDriverRegistry>();
        services.AddSingleton<ILegacyCognitiveMemoryExportServiceFactory, LegacyCognitiveMemoryExportServiceFactory>();
        services.AddPooledDbContextFactory<AppDbContext>((serviceProvider, optionsBuilder) =>
        {
            var canonicalRuntimeDatabase = serviceProvider.GetRequiredService<ICanonicalRuntimeDatabase>();
            AppDbContextOptionsConfigurator.Configure(optionsBuilder, canonicalRuntimeDatabase.Profile);
        });
        services.AddSingleton<IProfileAppDbContextFactory, ProfileAppDbContextFactory>();
        services.AddSingleton<IWorkspacePathResolver, WorkspacePathResolver>();
        services.AddSingleton<IWorkspacePathAccessGuard, WorkspacePathAccessGuard>();
        services.AddSingleton<IPhysicalFileSystemPathPolicyFactory, PhysicalFileSystemPathPolicyFactory>();
        services.AddSingleton<DurableFileWriter>();
        services.AddSingleton<IExternalTargetPathRegistryFactory, ExternalTargetPathRegistryFactory>();
        services.AddScoped<IExternalTargetPathRegistry, ExternalTargetPathRegistry>();
        services.AddSingleton<StorageCatalogService>();
        services.AddSingleton<IStorageCatalogService>(provider =>
            provider.GetRequiredService<StorageCatalogService>());
        services.AddSingleton<IStorageCatalogPathMigrationService>(provider =>
            provider.GetRequiredService<StorageCatalogService>());
        services.AddSingleton<FileSystemStoragePathPolicy>();
        services.AddHttpClient<IIpfsStorageTransport, IpfsHttpStorageTransport>();
        services.AddSingleton<IFtpStorageTransport, FtpWebStorageTransport>();
        services.AddSingleton<IStorageDriver, FileSystemStorageDriver>();
        services.AddSingleton<IStorageDriver, IpfsStorageDriver>();
        services.AddSingleton<IStorageDriver, FtpStorageDriver>();
        services.AddSingleton<IStorageDriverRegistry, StorageDriverRegistry>();
        services.AddSingleton<IStorageBrowseDriver, FileSystemStorageBrowseDriver>();
        services.AddSingleton<IStorageBrowseDriver, IpfsStorageBrowseDriver>();
        services.AddSingleton<IStorageBrowseDriver, FtpStorageBrowseDriver>();
        services.AddSingleton<IStorageBrowseDriverRegistry, StorageBrowseDriverRegistry>();
        services.AddSingleton<IStorageRoutingService, DefaultStorageRoutingService>();
        services.AddSingleton<IStorageConnectionTestService, StorageConnectionTestService>();
        services.AddSingleton<IStorageAccessService, StorageAccessService>();
        services.AddSingleton<StoragePlacementService>();
        services.AddSingleton<IStoragePlacementService>(provider =>
            provider.GetRequiredService<StoragePlacementService>());
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
