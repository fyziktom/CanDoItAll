using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class MigrationBootstrapIntegrationTests
{
    [Fact]
    public async Task Bootstrap_migrates_a_new_postgresql_database()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("integration-postgres-migrations");
        var activeProfile = testEnvironment.CreatePostgreSqlProfile("migration-primary");
        await using var provider = await TestApplicationBootstrap.BuildServiceProviderAsync(
            activeProfile,
            "CanDoItAll.Tests.Integration",
            TestSchemaBootstrapModules.Full);

        await using var scope = provider.CreateAsyncScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var resolver = scope.ServiceProvider.GetRequiredService<IActiveDatabaseProfileResolver>();

        var profile = resolver.ResolveCurrentProfile();

        Assert.Equal(DatabaseProviderKind.PostgreSql, profile.Profile.ProviderKind);
        Assert.Equal(activeProfile.ConnectionString, profile.ConnectionString);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        Assert.True(await dbContext.Database.CanConnectAsync());
        Assert.Contains(
            await dbContext.Database.GetAppliedMigrationsAsync(),
            migrationId => migrationId.Contains("InitialPostgreSqlBaseline", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Bootstrap_is_idempotent_for_postgresql_profiles()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("integration-postgres-migration-idempotent");
        var activeProfile = testEnvironment.CreatePostgreSqlProfile("migration-idempotent");
        await using var provider = await TestApplicationBootstrap.BuildServiceProviderAsync(
            activeProfile,
            "CanDoItAll.Tests.Integration",
            TestSchemaBootstrapModules.Full);

        await using var scope = provider.CreateAsyncScope();
        var bootstrapper = scope.ServiceProvider.GetRequiredService<IAppDatabaseBootstrapper>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

        await bootstrapper.EnsureCurrentProfileReadyAsync();
        await bootstrapper.EnsureCurrentProfileReadyAsync();

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        Assert.True(await dbContext.Database.CanConnectAsync());
    }

    [Fact]
    public async Task Bootstrap_adopts_existing_postgresql_schema_without_migration_history()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("integration-postgres-existing-schema");
        var activeProfile = testEnvironment.CreatePostgreSqlProfile("migration-existing-schema");

        var services = new ServiceCollection();
        var environment = new TestHostEnvironment(activeProfile.EnvironmentRootPath, "CanDoItAll.Tests.Integration");
        var configuration = TestApplicationBootstrap.BuildConfiguration(activeProfile);
        TestApplicationBootstrap.ConfigureDefaultServices(services, configuration, environment);

        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        await using var scope = provider.CreateAsyncScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using (var existingSchemaContext = await dbContextFactory.CreateDbContextAsync())
        {
            await existingSchemaContext.Database.EnsureCreatedAsync();
            Assert.Empty(await existingSchemaContext.Database.GetAppliedMigrationsAsync());
        }

        var bootstrapper = scope.ServiceProvider.GetRequiredService<IAppDatabaseBootstrapper>();
        await bootstrapper.EnsureCurrentProfileReadyAsync();

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var appliedMigrations = await dbContext.Database.GetAppliedMigrationsAsync();
        Assert.Contains("20260523211921_InitialPostgreSqlBaseline", appliedMigrations);
        Assert.Contains("20260524144716_ProcessStepAutomationDispatchClaims", appliedMigrations);
        Assert.Contains("20260524183000_ProcessClaimHotPathIndexes", appliedMigrations);
        Assert.True(await dbContext.Database.CanConnectAsync());
    }
}
