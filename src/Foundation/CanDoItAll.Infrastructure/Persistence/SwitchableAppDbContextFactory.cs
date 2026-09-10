using CanDoItAll.Infrastructure.Configuration;
using CanDoItAll.Infrastructure.ControlPlane;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Infrastructure;
using System.Reflection;

namespace CanDoItAll.Infrastructure.Persistence;

public static class AppDbContextMigrationsAssemblyNames
{
    public const string PostgreSql = "CanDoItAll.Migrations.PostgreSql";
}

public interface IProfileAppDbContextFactory
{
    Task<AppDbContext> CreateDbContextForProfileAsync(
        ResolvedDatabaseProfile profile,
        CancellationToken cancellationToken = default);
}

public static class AppDbContextOptionsConfigurator
{
    public static DbContextOptions<AppDbContext> CreateOptions(ResolvedDatabaseProfile profile)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        Configure(optionsBuilder, profile);
        return optionsBuilder.Options;
    }

    public static void Configure(DbContextOptionsBuilder optionsBuilder, ResolvedDatabaseProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ConfigureModelCacheKey(optionsBuilder);

        switch (profile.Profile.ProviderKind)
        {
            case DatabaseProviderKind.InMemory:
                optionsBuilder.UseInMemoryDatabase(profile.ConnectionString);
                return;

            case DatabaseProviderKind.PostgreSql:
                optionsBuilder.UseNpgsql(
                    profile.ConnectionString,
                    builder => builder.MigrationsAssembly(AppDbContextMigrationsAssemblyNames.PostgreSql));
                return;

            default:
                throw new InvalidOperationException($"Unsupported provider '{profile.Profile.ProviderKind}'.");
        }
    }

    public static void Configure(DbContextOptionsBuilder optionsBuilder, DatabaseOptions databaseOptions, string contentRootPath)
    {
        ArgumentNullException.ThrowIfNull(databaseOptions);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentRootPath);
        ConfigureModelCacheKey(optionsBuilder);

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

            optionsBuilder.UseNpgsql(
                connectionString,
                builder => builder.MigrationsAssembly(AppDbContextMigrationsAssemblyNames.PostgreSql));
            return;
        }

        throw new InvalidOperationException($"Unsupported database provider '{databaseOptions.Provider}'.");
    }

    public static void ConfigureModelCacheKey(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.ReplaceService<IModelCacheKeyFactory, AppDbContextModelCacheKeyFactory>();
    }
}

public sealed class ProfileAppDbContextFactory : IProfileAppDbContextFactory
{
    public Task<AppDbContext> CreateDbContextForProfileAsync(
        ResolvedDatabaseProfile profile,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return Task.FromResult(new AppDbContext(AppDbContextOptionsConfigurator.CreateOptions(profile)));
    }
}

public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext> {
    private const string CompositionAssemblyName = "CanDoItAll.Composition";
    private const string ModuleAssembliesTypeName = "CanDoItAll.Composition.ModuleAssemblies";
    private const string ModuleAssembliesFieldName = "All";

    public AppDbContext CreateDbContext(string[] args) {
        ConfigureModuleAssemblies();

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        var databaseOptions = BuildDatabaseOptions();
        AppDbContextOptionsConfigurator.Configure(optionsBuilder, databaseOptions, Directory.GetCurrentDirectory());
        return new AppDbContext(optionsBuilder.Options);
    }

    private static DatabaseOptions BuildDatabaseOptions() {
        return new DatabaseOptions {
            Provider = Environment.GetEnvironmentVariable("CANDOITALL_DATABASE_PROVIDER") ?? "PostgreSql",
            ConnectionString = Environment.GetEnvironmentVariable("CANDOITALL_DATABASE_CONNECTION")
        };
    }

    private static void ConfigureModuleAssemblies() {
        var compositionAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(assembly => string.Equals(assembly.GetName().Name, CompositionAssemblyName, StringComparison.Ordinal))
            ?? LoadCompositionAssembly();
        ConfigureModuleAssemblies(compositionAssembly);
    }

    internal static void ConfigureModuleAssemblies(Assembly compositionAssembly) {
        ArgumentNullException.ThrowIfNull(compositionAssembly);
        var catalog = compositionAssembly.GetType(ModuleAssembliesTypeName, throwOnError: false, ignoreCase: false)
            ?? throw new InvalidOperationException(
                $"The complete design-time model requires type '{ModuleAssembliesTypeName}' in assembly '{compositionAssembly.FullName}'.");
        var field = catalog.GetField(ModuleAssembliesFieldName, BindingFlags.Public | BindingFlags.Static);
        if (field?.GetValue(null) is not Assembly[] { Length: > 0 } moduleAssemblies || moduleAssemblies.Any(assembly => assembly is null)) {
            throw new InvalidOperationException(
                $"The complete design-time model requires '{ModuleAssembliesTypeName}.{ModuleAssembliesFieldName}' to contain a non-empty Assembly[] without null entries.");
        }
        AppDbContextModelRegistry.ConfigureAssemblies(moduleAssemblies);
    }

    private static Assembly LoadCompositionAssembly() {
        try {
            return Assembly.Load(CompositionAssemblyName);
        } catch (Exception exception) when (exception is FileNotFoundException or FileLoadException or BadImageFormatException) {
            throw new InvalidOperationException(
                $"The complete design-time model requires assembly '{CompositionAssemblyName}'. Run EF with the CanDoItAll.Web startup project and CanDoItAll.Migrations.PostgreSql migrations project, and resolve missing or incompatible dependencies.",
                exception);
        }
    }
}
