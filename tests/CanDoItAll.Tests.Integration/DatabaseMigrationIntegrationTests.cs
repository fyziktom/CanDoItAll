using CanDoItAll.Infrastructure.Configuration;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Factory;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class MigrationBootstrapIntegrationTests
{
    [Fact]
    public async Task Bootstrap_migrates_a_new_managed_sqlite_database()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("integration-sqlite-migrations");
        await using var provider = DatabaseProfileControlPlaneIntegrationHost.BuildServiceProvider(testEnvironment);

        var bootstrapper = provider.GetRequiredService<IAppDatabaseBootstrapper>();
        var runtimeAccessor = provider.GetRequiredService<IDatabaseProfileRuntimeAccessor>();
        var dbContextFactory = provider.GetRequiredService<IDbContextFactory<AppDbContext>>();

        var profile = runtimeAccessor.ResolveCurrentProfile();
        await bootstrapper.EnsureCurrentProfileReadyAsync();

        Assert.True(File.Exists(profile.Profile.Sqlite!.DatabasePath));

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        Assert.Contains(
            await dbContext.Database.GetAppliedMigrationsAsync(),
            migrationId => migrationId.Contains("InitialCreate", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Legacy_sqlite_database_is_baselined_and_preserves_existing_data()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("integration-legacy-sqlite-migration");
        var legacyWorkspaceRoot = Path.Combine(testEnvironment.RootPath, ".artifacts", "workspace");
        var legacyDatabasePath = Path.Combine(legacyWorkspaceRoot, "candoitall.db");
        await CreateLegacyDatabaseAsync(legacyDatabasePath);

        await using var provider = DatabaseProfileControlPlaneIntegrationHost.BuildServiceProvider(testEnvironment);
        var runtimeAccessor = provider.GetRequiredService<IDatabaseProfileRuntimeAccessor>();
        var bootstrapper = provider.GetRequiredService<IAppDatabaseBootstrapper>();
        var dbContextFactory = provider.GetRequiredService<IDbContextFactory<AppDbContext>>();

        var resolvedProfile = runtimeAccessor.ResolveCurrentProfile();
        Assert.Equal(DatabaseProfileResolutionSource.LegacyDiscovery, resolvedProfile.ResolutionSource);
        Assert.Equal(legacyDatabasePath, resolvedProfile.Profile.Sqlite!.DatabasePath);

        await bootstrapper.EnsureCurrentProfileReadyAsync();

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var project = await dbContext.Set<Project>().SingleAsync();

        Assert.Equal("Legacy project", project.Name);
        Assert.Contains(
            await dbContext.Database.GetAppliedMigrationsAsync(),
            migrationId => migrationId.Contains("InitialCreate", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Bootstrap_clears_a_stale_sqlite_migration_lock_before_running_migrations()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("integration-stale-sqlite-migration-lock");
        await using var provider = DatabaseProfileControlPlaneIntegrationHost.BuildServiceProvider(testEnvironment);

        var bootstrapper = provider.GetRequiredService<IAppDatabaseBootstrapper>();
        var dbContextFactory = provider.GetRequiredService<IDbContextFactory<AppDbContext>>();

        await bootstrapper.EnsureCurrentProfileReadyAsync();

        await using (var dbContext = await dbContextFactory.CreateDbContextAsync())
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                """
                INSERT OR REPLACE INTO "__EFMigrationsLock" ("Id", "Timestamp")
                VALUES (1, {0});
                """,
                (DateTimeOffset.UtcNow - TimeSpan.FromMinutes(10)).ToString("O"));
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await bootstrapper.EnsureCurrentProfileReadyAsync(cts.Token);

        await using (var dbContext = await dbContextFactory.CreateDbContextAsync())
        {
            var lockRowCount = await CountRowsAsync(dbContext, "__EFMigrationsLock");
            Assert.Equal(0, lockRowCount);
        }
    }

    private static async Task CreateLegacyDatabaseAsync(string databasePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);

        AppDbContextModelRegistry.ConfigureAssemblies(TestApplicationBootstrap.ModuleAssemblies);

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        AppDbContextOptionsConfigurator.Configure(
            optionsBuilder,
            new DatabaseOptions
            {
                Provider = "Sqlite",
                ConnectionString = $"Data Source={databasePath}"
            },
            Path.GetDirectoryName(databasePath)!);

        await using var dbContext = new AppDbContext(optionsBuilder.Options);
        await dbContext.Database.EnsureCreatedAsync();
        await WorkspaceSchemaInitializer.EnsureAsync(dbContext);
        await ProjectsSchemaInitializer.EnsureAsync(dbContext);
        await PromptFactorySchemaInitializer.EnsureAsync(dbContext);
        await ProjectWorkbenchSchemaInitializer.EnsureAsync(dbContext);
        await ProjectStructureAgentSchemaInitializer.EnsureAsync(dbContext);

        dbContext.Set<Project>().Add(new Project
        {
            Name = "Legacy project",
            Slug = "legacy-project",
            Description = "Database created before migrations",
            Objective = "Preserve existing project data",
            Status = ProjectStatus.Active,
            CurrentPhase = "Migration",
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });

        await dbContext.SaveChangesAsync();
    }

    private static async Task<long> CountRowsAsync(AppDbContext dbContext, string tableName)
    {
        var connection = dbContext.Database.GetDbConnection();
        var shouldCloseConnection = connection.State != System.Data.ConnectionState.Open;
        if (shouldCloseConnection)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"""SELECT COUNT(*) FROM "{tableName.Replace("\"", "\"\"", StringComparison.Ordinal)}";""";
            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt64(result);
        }
        finally
        {
            if (shouldCloseConnection)
            {
                await connection.CloseAsync();
            }
        }
    }
}
