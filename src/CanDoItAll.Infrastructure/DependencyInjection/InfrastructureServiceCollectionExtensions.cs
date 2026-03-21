using System.Reflection;
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

        services.AddDataProtection()
            .SetApplicationName("CanDoItAll");

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IActivityStream, NullActivityStream>();
        services.AddSingleton<ISecretRedactor, SecretRedactor>();
        services.AddSingleton<IWorkspacePathResolver, WorkspacePathResolver>();
        services.AddScoped<IFileStore, LocalFileStore>();
        services.AddScoped<IManagedArtifactStore, ManagedArtifactStore>();
        services.AddSingleton<IBackgroundJobQueue, InMemoryBackgroundJobQueue>();
        services.AddSingleton<IRuntimeReadinessService, RuntimeReadinessService>();
        services.AddScoped<IBackgroundJobTracker, BackgroundJobTracker>();
        services.AddScoped<ISearchIndexService, SearchIndexService>();

        services.AddDbContextFactory<AppDbContext>((serviceProvider, options) =>
        {
            var databaseOptions = serviceProvider
                .GetRequiredService<Microsoft.Extensions.Options.IOptions<DatabaseOptions>>()
                .Value;

            ConfigureDb(options, environment, databaseOptions);
        });

        services.AddHealthChecks()
            .AddCheck<RuntimeReadinessHealthCheck>("runtime-readiness");

        return services;
    }

    private static void ConfigureDb(DbContextOptionsBuilder optionsBuilder, IHostEnvironment environment, DatabaseOptions databaseOptions)
    {
        var provider = databaseOptions.Provider.Trim().ToLowerInvariant();
        if (provider is "inmemory" or "memory")
        {
            var databaseName = string.IsNullOrWhiteSpace(databaseOptions.ConnectionString)
                ? "candoitall"
                : databaseOptions.ConnectionString.Trim();

            optionsBuilder.UseInMemoryDatabase(databaseName);
            return;
        }

        if (provider is "postgres" or "postgresql")
        {
            var connectionString = string.IsNullOrWhiteSpace(databaseOptions.ConnectionString)
                ? "Host=localhost;Database=candoitall;Username=postgres;Password=postgres"
                : databaseOptions.ConnectionString;

            optionsBuilder.UseNpgsql(connectionString, builder => builder.MigrationsAssembly("CanDoItAll.Web"));
            return;
        }

        var connection = string.IsNullOrWhiteSpace(databaseOptions.ConnectionString)
            ? $"Data Source={Path.Combine(environment.ContentRootPath, ".artifacts", "workspace", "candoitall.db")}"
            : databaseOptions.ConnectionString;

        var filePath = connection.Replace("Data Source=", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        optionsBuilder.UseSqlite(connection, builder => builder.MigrationsAssembly("CanDoItAll.Web"));
    }
}
