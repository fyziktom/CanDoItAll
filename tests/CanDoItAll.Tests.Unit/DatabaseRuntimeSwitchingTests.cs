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
