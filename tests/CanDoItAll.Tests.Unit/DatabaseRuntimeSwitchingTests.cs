using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Unit;

public sealed class DatabaseDriverTests
{
    [Fact]
    public async Task Sqlite_driver_create_empty_database_creates_the_database_file()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("runtime-sqlite-driver");
        await using var provider = DatabaseProfileControlPlaneTestHost.BuildServiceProvider(
            testEnvironment,
            includeDatabaseOverride: true);

        var profileService = provider.GetRequiredService<IDatabaseProfileService>();
        var runtimeAccessor = provider.GetRequiredService<IDatabaseProfileRuntimeAccessor>();
        var driverRegistry = provider.GetRequiredService<IDatabaseDriverRegistry>();

        var saveResult = await profileService.SaveAsync(new DatabaseProfileEditorModel
        {
            DisplayName = "Managed sqlite driver target",
            ProviderKind = DatabaseProviderKind.Sqlite,
            SourceKind = DatabaseProfileSourceKind.ManagedSqlite
        });

        Assert.True(saveResult.IsSuccess);

        var resolvedProfile = runtimeAccessor.ResolveProfile(saveResult.Value);
        var databasePath = resolvedProfile.Profile.Sqlite!.DatabasePath;
        if (File.Exists(databasePath))
        {
            File.Delete(databasePath);
        }

        await driverRegistry.Resolve(DatabaseProviderKind.Sqlite).CreateEmptyAsync(resolvedProfile);

        Assert.True(File.Exists(databasePath));
    }
}

public sealed class DatabaseSwitchCoordinatorTests
{
    [Fact]
    public async Task SwitchAsync_returns_failure_when_RuntimeOverride_is_active()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("runtime-override-switch");
        await using var provider = DatabaseProfileControlPlaneTestHost.BuildServiceProvider(
            testEnvironment,
            includeDatabaseOverride: true);

        var profileService = provider.GetRequiredService<IDatabaseProfileService>();
        var switchCoordinator = provider.GetRequiredService<IDatabaseSwitchCoordinator>();

        var saveResult = await profileService.SaveAsync(new DatabaseProfileEditorModel
        {
            DisplayName = "Switch target",
            ProviderKind = DatabaseProviderKind.Sqlite,
            SourceKind = DatabaseProfileSourceKind.ManagedSqlite
        });

        Assert.True(saveResult.IsSuccess);

        var switchResult = await switchCoordinator.SwitchAsync(saveResult.Value);

        Assert.True(switchResult.IsFailure);
        Assert.Contains(
            switchResult.Errors,
            error => error.Message.Contains("Runtime override", StringComparison.OrdinalIgnoreCase));
    }
}

public sealed class AppDbContextRuntimeSwitchTests
{
    [Fact]
    public async Task CreateDbContextAsync_uses_the_new_active_profile_after_a_switch()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("runtime-context-switch");
        await using var provider = DatabaseProfileControlPlaneTestHost.BuildServiceProvider(
            testEnvironment,
            includeDatabaseOverride: false);

        var runtimeAccessor = provider.GetRequiredService<IDatabaseProfileRuntimeAccessor>();
        var profileService = provider.GetRequiredService<IDatabaseProfileService>();
        var switchCoordinator = provider.GetRequiredService<IDatabaseSwitchCoordinator>();
        var runtimeState = provider.GetRequiredService<IDatabaseRuntimeState>();
        var bootstrapper = provider.GetRequiredService<IAppDatabaseBootstrapper>();
        var dbContextFactory = provider.GetRequiredService<IDbContextFactory<AppDbContext>>();

        var initialProfile = runtimeAccessor.ResolveCurrentProfile();
        await bootstrapper.EnsureCurrentProfileReadyAsync();

        var saveResult = await profileService.SaveAsync(new DatabaseProfileEditorModel
        {
            DisplayName = "Managed sqlite switch target",
            ProviderKind = DatabaseProviderKind.Sqlite,
            SourceKind = DatabaseProfileSourceKind.ManagedSqlite
        });

        Assert.True(saveResult.IsSuccess);

        var targetProfile = runtimeAccessor.ResolveProfile(saveResult.Value);
        var switchResult = await switchCoordinator.SwitchAsync(saveResult.Value);

        Assert.True(switchResult.IsSuccess);
        Assert.NotEqual(initialProfile.Profile.Id, targetProfile.Profile.Id);

        await using var switchedContext = await dbContextFactory.CreateDbContextAsync();
        Assert.Equal(targetProfile.ConnectionString, switchedContext.Database.GetConnectionString());
        Assert.NotEqual(initialProfile.ConnectionString, switchedContext.Database.GetConnectionString());

        var runtimeSnapshot = runtimeState.GetSnapshot();
        Assert.Equal(targetProfile.Profile.Id, runtimeSnapshot.ActiveProfileId);
        Assert.Equal(switchResult.Value!.Generation, runtimeSnapshot.Generation);
    }
}

public sealed class SqliteMigrationLockRecoveryTests
{
    [Fact]
    public async Task EnsureCurrentProfileReadyAsync_clears_a_stale_sqlite_migration_lock()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("runtime-stale-sqlite-migration-lock");
        await using var provider = DatabaseProfileControlPlaneTestHost.BuildServiceProvider(
            testEnvironment,
            includeDatabaseOverride: false);

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
