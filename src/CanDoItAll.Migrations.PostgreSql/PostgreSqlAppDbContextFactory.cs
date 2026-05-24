using CanDoItAll.Composition;
using CanDoItAll.Infrastructure.Configuration;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CanDoItAll.Migrations.PostgreSql;

public sealed class PostgreSqlAppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    private const string ConnectionStringEnvironmentVariable = "CANDOITALL_MIGRATIONS_POSTGRES_CONNECTION";
    private const string DefaultConnectionString = "Host=127.0.0.1;Database=candoitall_migrations;Username=postgres;Password=postgres";

    public AppDbContext CreateDbContext(string[] args)
    {
        AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);

        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = DefaultConnectionString;
        }

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        AppDbContextOptionsConfigurator.Configure(
            optionsBuilder,
            new DatabaseOptions
            {
                Provider = "PostgreSql",
                ConnectionString = connectionString
            },
            Directory.GetCurrentDirectory());

        return new AppDbContext(optionsBuilder.Options);
    }
}
