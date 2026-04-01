using CanDoItAll.Infrastructure.Configuration;
using CanDoItAll.Infrastructure.ControlPlane;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CanDoItAll.Infrastructure.Persistence;

internal static class AppDbContextMigrationsAssemblyNames
{
    public const string Sqlite = "CanDoItAll.Migrations.Sqlite";
    public const string PostgreSql = "CanDoItAll.Migrations.PostgreSql";
}

public interface ISwitchableAppDbContextFactory : IDbContextFactory<AppDbContext>
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

            case DatabaseProviderKind.Sqlite:
                var filePath = profile.ConnectionString
                    .Replace("Data Source=", string.Empty, StringComparison.OrdinalIgnoreCase)
                    .Trim();
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                optionsBuilder.UseSqlite(
                    profile.ConnectionString,
                    builder => builder.MigrationsAssembly(AppDbContextMigrationsAssemblyNames.Sqlite));
                return;

            default:
                throw new InvalidOperationException($"Unsupported provider '{profile.Profile.ProviderKind}'.");
        }
    }

    public static void Configure(DbContextOptionsBuilder optionsBuilder, DatabaseOptions databaseOptions, string contentRootPath)
    {
        ArgumentNullException.ThrowIfNull(databaseOptions);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentRootPath);

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

        var databasePath = Path.Combine(contentRootPath, ".artifacts", "workspace", "candoitall.db");
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);

        var sqliteConnectionString = string.IsNullOrWhiteSpace(databaseOptions.ConnectionString)
            ? $"Data Source={databasePath}"
            : databaseOptions.ConnectionString;

        optionsBuilder.UseSqlite(
            sqliteConnectionString,
            builder => builder.MigrationsAssembly(AppDbContextMigrationsAssemblyNames.Sqlite));
    }
}

public sealed class SwitchableAppDbContextFactory(
    IDatabaseProfileRuntimeAccessor profileAccessor,
    IDatabaseRuntimeState runtimeState) : ISwitchableAppDbContextFactory
{
    public AppDbContext CreateDbContext()
    {
        return CreateDbContextAsync().GetAwaiter().GetResult();
    }

    public async Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
    {
        var lease = await runtimeState.AcquireContextLeaseAsync(cancellationToken);

        try
        {
            var profile = profileAccessor.ResolveCurrentProfile();
            runtimeState.MarkCurrentProfile(profile);
            return new AppDbContext(AppDbContextOptionsConfigurator.CreateOptions(profile), lease);
        }
        catch
        {
            lease.Dispose();
            throw;
        }
    }

    public Task<AppDbContext> CreateDbContextForProfileAsync(
        ResolvedDatabaseProfile profile,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return Task.FromResult(new AppDbContext(AppDbContextOptionsConfigurator.CreateOptions(profile)));
    }
}

public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        var databaseOptions = BuildDatabaseOptions();
        AppDbContextOptionsConfigurator.Configure(optionsBuilder, databaseOptions, Directory.GetCurrentDirectory());
        return new AppDbContext(optionsBuilder.Options);
    }

    private static DatabaseOptions BuildDatabaseOptions()
    {
        return new DatabaseOptions
        {
            Provider = Environment.GetEnvironmentVariable("CANDOITALL_DATABASE_PROVIDER") ?? "Sqlite",
            ConnectionString = Environment.GetEnvironmentVariable("CANDOITALL_DATABASE_CONNECTION")
        };
    }
}
