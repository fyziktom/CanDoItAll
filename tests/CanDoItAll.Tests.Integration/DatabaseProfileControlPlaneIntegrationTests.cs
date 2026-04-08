using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Tests.Support;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class ControlPlaneDatabaseProfileIntegrationTests
{
    [Fact]
    public async Task ResolveCurrentProfile_auto_provisions_managed_sqlite_profile_under_the_control_plane_root()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("integration-control-plane-managed");
        await using var provider = DatabaseProfileControlPlaneIntegrationHost.BuildServiceProvider(testEnvironment);

        var resolver = provider.GetRequiredService<IActiveDatabaseProfileResolver>();
        var workspaceResolver = provider.GetRequiredService<IWorkspacePathResolver>();
        var profileService = provider.GetRequiredService<IDatabaseProfileService>();
        var bootstrapper = provider.GetRequiredService<IAppDatabaseBootstrapper>();

        var resolvedProfile = resolver.ResolveCurrentProfile();
        var selection = await profileService.GetCurrentSelectionAsync();

        Assert.Equal(DatabaseProfileResolutionSource.AutoProvisionedManagedSqlite, resolvedProfile.ResolutionSource);
        Assert.Equal(DatabaseProviderKind.Sqlite, resolvedProfile.Profile.ProviderKind);
        Assert.Equal(DatabaseProfileSourceKind.ManagedSqlite, resolvedProfile.Profile.SourceKind);
        Assert.StartsWith(testEnvironment.ControlPlaneRootPath, resolvedProfile.Profile.Sqlite!.DatabasePath, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith(testEnvironment.ControlPlaneRootPath, resolvedProfile.Profile.Storage.WorkspaceRoot, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(resolvedProfile.Profile.Storage.WorkspaceRoot, workspaceResolver.ResolveWorkspaceRoot());
        Assert.Equal(DatabaseProfileResolutionSource.PersistedActiveProfile, selection.ResolutionSource);

        await bootstrapper.EnsureCurrentProfileReadyAsync();

        var dbContextFactory = provider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();

        Assert.True(File.Exists(resolvedProfile.Profile.Sqlite.DatabasePath));
        Assert.NotEmpty(await dbContext.Database.GetAppliedMigrationsAsync());
        var summaries = await profileService.ListAsync();
        Assert.Single(summaries);
        Assert.True(summaries[0].IsActive);
    }
}

public sealed class LegacyDatabaseProfileIntegrationTests
{
    [Fact]
    public async Task ResolveCurrentProfile_discovers_the_legacy_workspace_when_the_catalog_is_empty()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("integration-control-plane-legacy");
        var legacyWorkspaceRoot = Path.Combine(testEnvironment.RootPath, ".artifacts", "workspace");
        var legacyDatabasePath = Path.Combine(legacyWorkspaceRoot, "candoitall.db");

        Directory.CreateDirectory(legacyWorkspaceRoot);
        await using (var connection = new SqliteConnection($"Data Source={legacyDatabasePath}"))
        {
            await connection.OpenAsync();
        }

        await using var provider = DatabaseProfileControlPlaneIntegrationHost.BuildServiceProvider(testEnvironment);

        var resolver = provider.GetRequiredService<IActiveDatabaseProfileResolver>();
        var workspaceResolver = provider.GetRequiredService<IWorkspacePathResolver>();
        var profileService = provider.GetRequiredService<IDatabaseProfileService>();
        var bootstrapper = provider.GetRequiredService<IAppDatabaseBootstrapper>();

        var resolvedProfile = resolver.ResolveCurrentProfile();
        var selection = await profileService.GetCurrentSelectionAsync();

        Assert.Equal(DatabaseProfileResolutionSource.LegacyDiscovery, resolvedProfile.ResolutionSource);
        Assert.Equal(DatabaseProfileSourceKind.ExternalSqliteFile, resolvedProfile.Profile.SourceKind);
        Assert.Equal(legacyDatabasePath, resolvedProfile.Profile.Sqlite!.DatabasePath);
        Assert.Equal(legacyWorkspaceRoot, resolvedProfile.Profile.Storage.WorkspaceRoot);
        Assert.Equal(legacyWorkspaceRoot, workspaceResolver.ResolveWorkspaceRoot());
        Assert.Equal(DatabaseProfileResolutionSource.PersistedActiveProfile, selection.ResolutionSource);

        await bootstrapper.EnsureCurrentProfileReadyAsync();

        var dbContextFactory = provider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();

        Assert.NotEmpty(await dbContext.Database.GetAppliedMigrationsAsync());
        var summaries = await profileService.ListAsync();
        var summary = Assert.Single(summaries);
        Assert.True(summary.IsActive);
        Assert.Equal(DatabaseProviderKind.Sqlite, summary.ProviderKind);
    }
}

internal static class DatabaseProfileControlPlaneIntegrationHost
{
    public static ServiceProvider BuildServiceProvider(
        CanDoItAllTestEnvironment testEnvironment,
        IReadOnlyDictionary<string, string?>? additionalValues = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(CreateConfigurationValues(testEnvironment, additionalValues))
            .Build();

        var services = new ServiceCollection();
        TestApplicationBootstrap.ConfigureDefaultServices(
            services,
            configuration,
            testEnvironment.CreateHostEnvironment("CanDoItAll.Tests.Integration"));

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
    }

    private static IReadOnlyDictionary<string, string?> CreateConfigurationValues(
        CanDoItAllTestEnvironment testEnvironment,
        IReadOnlyDictionary<string, string?>? additionalValues)
    {
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["ControlPlane:RootPath"] = testEnvironment.ControlPlaneRootPath,
            ["Storage:ManagedFilesFolder"] = "managed-files",
            ["Storage:ExportsFolder"] = "exports",
            ["Storage:EvidenceFolder"] = "evidence",
            ["Storage:ManagerArtifactsFolder"] = Path.Combine(testEnvironment.RootPath, "manager-artifacts"),
            ["Workbench:MaxWarmTabs"] = "3",
            ["Workbench:SleepAfterMinutes"] = "15",
            ["Workbench:BrowserStorageKey"] = "candoitall.workbench.session",
            ["DevelopmentManager:TuningModeEnabled"] = "true",
            ["DevelopmentManager:ReviewBeforeSend"] = "true",
            ["DevelopmentManager:ManagerBaseUrl"] = "http://127.0.0.1:6407"
        };

        if (additionalValues is not null)
        {
            foreach (var pair in additionalValues)
            {
                values[pair.Key] = pair.Value;
            }
        }

        return values;
    }
}
