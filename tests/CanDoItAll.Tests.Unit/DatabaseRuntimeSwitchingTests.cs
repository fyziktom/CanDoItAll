using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using static CanDoItAll.Tests.Unit.DatabaseRuntimeSwitchingTestProfiles;

namespace CanDoItAll.Tests.Unit;

public sealed class DatabaseDriverTests
{
    [Fact]
    public async Task Resolve_throws_when_sqlite_driver_is_requested()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("runtime-sqlite-driver");
        await using var provider = DatabaseProfileControlPlaneTestHost.BuildServiceProvider(
            testEnvironment,
            includeDatabaseOverride: true);

        var driverRegistry = provider.GetRequiredService<IDatabaseDriverRegistry>();

        var ex = Assert.Throws<InvalidOperationException>(() => driverRegistry.Resolve(DatabaseProviderKind.Sqlite));

        Assert.Contains("No database driver is registered", ex.Message, StringComparison.Ordinal);
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

        var saveResult = await profileService.SaveAsync(CreatePostgreSqlEditorForDatabase(
            "Switch target",
            "runtime_override_switch",
            Path.Combine(testEnvironment.RootPath, "switch-target-workspace")));

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
        var dbContextFactory = provider.GetRequiredService<IDbContextFactory<AppDbContext>>();

        var initialProfile = runtimeAccessor.ResolveCurrentProfile();
        await using var targetDatabase = PostgresTestDatabaseLease.Create("runtime-context-switch");
        var saveResult = await profileService.SaveAsync(CreatePostgreSqlEditor(
            "PostgreSQL switch target",
            targetDatabase.ConnectionString,
            Path.Combine(testEnvironment.RootPath, "switch-target-workspace")));

        Assert.True(saveResult.IsSuccess);

        var targetProfile = runtimeAccessor.ResolveProfile(saveResult.Value);
        var switchResult = await switchCoordinator.SwitchAsync(saveResult.Value);

        Assert.True(switchResult.IsSuccess, string.Join("; ", switchResult.Errors.Select(error => error.Message)));
        Assert.NotEqual(initialProfile.Profile.Id, targetProfile.Profile.Id);

        await using var switchedContext = await dbContextFactory.CreateDbContextAsync();
        Assert.Equal(targetProfile.ConnectionString, switchedContext.Database.GetConnectionString());
        Assert.NotEqual(initialProfile.ConnectionString, switchedContext.Database.GetConnectionString());

        var runtimeSnapshot = runtimeState.GetSnapshot();
        Assert.Equal(targetProfile.Profile.Id, runtimeSnapshot.ActiveProfileId);
        Assert.Equal(switchResult.Value!.Generation, runtimeSnapshot.Generation);
    }
}

public sealed class UnsupportedLegacySqliteProfileTests
{
    [Fact]
    public async Task SaveAsync_rejects_sqlite_profiles()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("runtime-sqlite-profile-rejected");
        await using var provider = DatabaseProfileControlPlaneTestHost.BuildServiceProvider(
            testEnvironment,
            includeDatabaseOverride: true);

        var profileService = provider.GetRequiredService<IDatabaseProfileService>();

        var saveResult = await profileService.SaveAsync(new DatabaseProfileEditorModel
        {
            DisplayName = "Legacy SQLite",
            ProviderKind = DatabaseProviderKind.Sqlite,
            SourceKind = DatabaseProfileSourceKind.ManagedSqlite
        });

        Assert.True(saveResult.IsFailure);
        Assert.Contains(saveResult.Errors, error => error.Message.Contains("SQLite database profiles are no longer supported", StringComparison.Ordinal));
    }
}

internal static class DatabaseRuntimeSwitchingTestProfiles
{
    public static DatabaseProfileEditorModel CreatePostgreSqlEditorForDatabase(
        string displayName,
        string databaseName,
        string workspaceRoot)
    {
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = "127.0.0.1",
            Port = 5432,
            Database = databaseName,
            Username = "postgres",
            Password = "postgres"
        };

        return CreatePostgreSqlEditor(displayName, builder.ConnectionString, workspaceRoot);
    }

    public static DatabaseProfileEditorModel CreatePostgreSqlEditor(
        string displayName,
        string connectionString,
        string workspaceRoot)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        return new DatabaseProfileEditorModel
        {
            DisplayName = displayName,
            ProviderKind = DatabaseProviderKind.PostgreSql,
            SourceKind = DatabaseProfileSourceKind.PostgresConnection,
            PostgresHost = builder.Host ?? "127.0.0.1",
            PostgresPort = builder.Port,
            PostgresDatabaseName = builder.Database ?? "candoitall",
            PostgresUsername = builder.Username ?? "postgres",
            PostgresPassword = builder.Password ?? string.Empty,
            PostgresAdminDatabaseName = builder.Database,
            WorkspaceRoot = workspaceRoot
        };
    }
}
