using CanDoItAll.Composition;
using CanDoItAll.Infrastructure.Configuration;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CanDoItAll.Migrations.PostgreSql;

public sealed class PostgreSqlAppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        AppDbContextOptionsConfigurator.Configure(
            optionsBuilder,
            new DatabaseOptions
            {
                Provider = "PostgreSql",
                ConnectionString = "Host=127.0.0.1;Database=candoitall_migrations;Username=postgres;Password=postgres"
            },
            Directory.GetCurrentDirectory());

        return new AppDbContext(optionsBuilder.Options);
    }
}
