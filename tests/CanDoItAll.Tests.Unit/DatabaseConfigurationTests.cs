using CanDoItAll.Infrastructure.DependencyInjection;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Unit;

public sealed class DatabaseConfigurationTests
{
    [Fact]
    public async Task AddCanDoItAllInfrastructure_UsesInMemoryProvider_WhenConfigured()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("candoitall-inmemory-tests");
        var profile = testEnvironment.CreateInMemoryProfile("unit", "rpi3-validation");
        var configuration = TestApplicationBootstrap.BuildConfiguration(profile);

        var services = new ServiceCollection();
        TestApplicationBootstrap.ConfigureDefaultServices(
            services,
            configuration,
            testEnvironment.CreateHostEnvironment("CanDoItAll.Tests.Unit"));

        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        await using var scope = provider.CreateAsyncScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();

        Assert.Equal("Microsoft.EntityFrameworkCore.InMemory", dbContext.Database.ProviderName);
    }

    [Fact]
    public void AppDbContextFactory_UsesInMemoryProvider_WhenConfiguredViaEnvironment()
    {
        const string providerVariable = "CANDOITALL_DATABASE_PROVIDER";
        const string connectionVariable = "CANDOITALL_DATABASE_CONNECTION";
        var originalProvider = Environment.GetEnvironmentVariable(providerVariable);
        var originalConnection = Environment.GetEnvironmentVariable(connectionVariable);

        try
        {
            Environment.SetEnvironmentVariable(providerVariable, "InMemory");
            Environment.SetEnvironmentVariable(connectionVariable, "factory-validation");

            using var context = new AppDbContextFactory().CreateDbContext([]);
            Assert.Equal("Microsoft.EntityFrameworkCore.InMemory", context.Database.ProviderName);
        }
        finally
        {
            Environment.SetEnvironmentVariable(providerVariable, originalProvider);
            Environment.SetEnvironmentVariable(connectionVariable, originalConnection);
        }
    }

    [Fact]
    public void AppDbContextFactory_UsesSqliteMigrationsAssembly_WhenConfiguredViaEnvironment()
    {
        const string providerVariable = "CANDOITALL_DATABASE_PROVIDER";
        const string connectionVariable = "CANDOITALL_DATABASE_CONNECTION";
        var originalProvider = Environment.GetEnvironmentVariable(providerVariable);
        var originalConnection = Environment.GetEnvironmentVariable(connectionVariable);

        try
        {
            Environment.SetEnvironmentVariable(providerVariable, "Sqlite");
            Environment.SetEnvironmentVariable(connectionVariable, "Data Source=:memory:");

            using var context = new AppDbContextFactory().CreateDbContext([]);
            Assert.Equal(
                "CanDoItAll.Migrations.Sqlite",
                GetRelationalOptions(context).MigrationsAssembly);
        }
        finally
        {
            Environment.SetEnvironmentVariable(providerVariable, originalProvider);
            Environment.SetEnvironmentVariable(connectionVariable, originalConnection);
        }
    }

    [Fact]
    public void AppDbContextFactory_UsesPostgreSqlMigrationsAssembly_WhenConfiguredViaEnvironment()
    {
        const string providerVariable = "CANDOITALL_DATABASE_PROVIDER";
        const string connectionVariable = "CANDOITALL_DATABASE_CONNECTION";
        var originalProvider = Environment.GetEnvironmentVariable(providerVariable);
        var originalConnection = Environment.GetEnvironmentVariable(connectionVariable);

        try
        {
            Environment.SetEnvironmentVariable(providerVariable, "PostgreSql");
            Environment.SetEnvironmentVariable(
                connectionVariable,
                "Host=127.0.0.1;Database=candoitall;Username=postgres;Password=postgres");

            using var context = new AppDbContextFactory().CreateDbContext([]);
            Assert.Equal(
                "CanDoItAll.Migrations.PostgreSql",
                GetRelationalOptions(context).MigrationsAssembly);
        }
        finally
        {
            Environment.SetEnvironmentVariable(providerVariable, originalProvider);
            Environment.SetEnvironmentVariable(connectionVariable, originalConnection);
        }
    }

    private static RelationalOptionsExtension GetRelationalOptions(AppDbContext context)
    {
        var dbContextOptions = context.GetService<IDbContextOptions>();
        return Assert.Single(dbContextOptions.Extensions.OfType<RelationalOptionsExtension>());
    }
}
