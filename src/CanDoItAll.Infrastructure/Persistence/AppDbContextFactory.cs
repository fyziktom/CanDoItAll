using CanDoItAll.Infrastructure.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CanDoItAll.Infrastructure.Persistence;

/* codex-capsule
kind: factory
name: AppDbContextFactory
summary: Creates AppDbContext instances for design-time migration operations.
owns: sqlite-default, postgres-fallback
deps: DatabaseOptions
risks: wrong-content-root, missing-connection-string
tests: integration:AppDbContextFactoryTests
inputs: environment variables, database options
outputs: configured AppDbContext
*/
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        var databaseOptions = BuildDatabaseOptions();

        if (string.Equals(databaseOptions.Provider, "postgres", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(databaseOptions.Provider, "postgresql", StringComparison.OrdinalIgnoreCase))
        {
            var connectionString = string.IsNullOrWhiteSpace(databaseOptions.ConnectionString)
                ? "Host=localhost;Database=candoitall;Username=postgres;Password=postgres"
                : databaseOptions.ConnectionString;

            optionsBuilder.UseNpgsql(connectionString, builder => builder.MigrationsAssembly("CanDoItAll.Web"));
        }
        else
        {
            var basePath = Directory.GetCurrentDirectory();
            var databasePath = Path.Combine(basePath, ".artifacts", "workspace", "candoitall.db");
            Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);

            var connectionString = string.IsNullOrWhiteSpace(databaseOptions.ConnectionString)
                ? $"Data Source={databasePath}"
                : databaseOptions.ConnectionString;

            optionsBuilder.UseSqlite(connectionString, builder => builder.MigrationsAssembly("CanDoItAll.Web"));
        }

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
