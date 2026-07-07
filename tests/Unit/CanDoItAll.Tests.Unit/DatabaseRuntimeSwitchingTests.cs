using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using static CanDoItAll.Tests.Unit.DatabaseRuntimeSwitchingTestProfiles;

namespace CanDoItAll.Tests.Unit;

[Collection(AppDbContextModelRegistryTestCollectionNames.Name)]
public sealed class DatabaseSwitchCoordinatorTests
{
    [Fact]
    public async Task SwitchAsync_returns_failure_when_RuntimeOverride_is_active()
    {
        using var modelRegistryScope = AppDbContextModelRegistry.UseIsolatedAssembliesForTesting();
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

[Collection(AppDbContextModelRegistryTestCollectionNames.Name)]
public sealed class AppDbContextRuntimeSwitchTests
{
    [Fact]
    public async Task CreateDbContextAsync_keeps_canonical_profile_until_restart_after_activation()
    {
        using var modelRegistryScope = AppDbContextModelRegistry.UseIsolatedAssembliesForTesting();
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
        Assert.True(switchResult.Value!.RequiresRestart);
        Assert.False(switchResult.Value.RuntimeChangedInProcess);
        Assert.Equal(initialProfile.Profile.Id, switchResult.Value.RuntimeProfileId);
        Assert.Equal(targetProfile.Profile.Id, switchResult.Value.PendingRestartProfileId);
        Assert.Contains("Restart", switchResult.Value.Message, StringComparison.OrdinalIgnoreCase);

        await using var switchedContext = await dbContextFactory.CreateDbContextAsync();
        Assert.Equal(initialProfile.ConnectionString, switchedContext.Database.GetConnectionString());
        Assert.NotEqual(targetProfile.ConnectionString, switchedContext.Database.GetConnectionString());

        var runtimeSnapshot = runtimeState.GetSnapshot();
        Assert.Equal(initialProfile.Profile.Id, runtimeSnapshot.ActiveProfileId);
        Assert.Equal(switchResult.Value!.Generation, runtimeSnapshot.Generation);

        var persistedSelection = await profileService.GetCurrentSelectionAsync();
        Assert.Equal(targetProfile.Profile.Id, persistedSelection.ActiveProfileId);
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
