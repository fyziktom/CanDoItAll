using CanDoItAll.Composition;
using CanDoItAll.Infrastructure.Configuration;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CanDoItAll.Migrations.Sqlite;

public sealed class SqliteAppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        AppDbContextOptionsConfigurator.Configure(
            optionsBuilder,
            new DatabaseOptions
            {
                Provider = "Sqlite",
                ConnectionString = "Data Source=candoitall-migrations.db"
            },
            Directory.GetCurrentDirectory());

        return new AppDbContext(optionsBuilder.Options);
    }
}
