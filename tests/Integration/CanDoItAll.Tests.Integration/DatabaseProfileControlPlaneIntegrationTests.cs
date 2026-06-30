using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class ControlPlaneDatabaseProfileIntegrationTests
{
    [Fact]
    public async Task ResolveCurrentProfile_auto_provisions_postgresql_profile_with_default_workspace()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("integration-control-plane-managed");
        await using var provider = DatabaseProfileControlPlaneIntegrationHost.BuildServiceProvider(testEnvironment);

        var resolver = provider.GetRequiredService<IActiveDatabaseProfileResolver>();
        var workspaceResolver = provider.GetRequiredService<IWorkspacePathResolver>();
        var profileService = provider.GetRequiredService<IDatabaseProfileService>();

        var resolvedProfile = resolver.ResolveCurrentProfile();
        var selection = await profileService.GetCurrentSelectionAsync();

        Assert.Equal(DatabaseProfileResolutionSource.AutoProvisionedPostgreSql, resolvedProfile.ResolutionSource);
        Assert.Equal(DatabaseProviderKind.PostgreSql, resolvedProfile.Profile.ProviderKind);
        Assert.Equal(DatabaseProfileSourceKind.PostgresConnection, resolvedProfile.Profile.SourceKind);
        Assert.NotNull(resolvedProfile.Profile.PostgreSql);
        Assert.StartsWith(Path.Combine(testEnvironment.RootPath, ".artifacts", "workspace"), resolvedProfile.Profile.Storage.WorkspaceRoot, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(resolvedProfile.Profile.Storage.WorkspaceRoot, workspaceResolver.ResolveWorkspaceRoot());
        Assert.Equal(DatabaseProfileResolutionSource.PersistedActiveProfile, selection.ResolutionSource);

        var summaries = await profileService.ListAsync();
        Assert.Single(summaries);
        Assert.True(summaries[0].IsActive);
    }
}

public sealed class LegacyDatabaseProfileIntegrationTests
{
    [Fact]
    public async Task ResolveCurrentProfile_ignores_legacy_local_workspace_when_the_catalog_is_empty()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("integration-control-plane-legacy");
        var legacyWorkspaceRoot = Path.Combine(testEnvironment.RootPath, ".artifacts", "workspace");
        var legacyDatabasePath = Path.Combine(legacyWorkspaceRoot, "candoitall.db");

        Directory.CreateDirectory(legacyWorkspaceRoot);
        await File.WriteAllTextAsync(legacyDatabasePath, "legacy local database placeholder");

        await using var provider = DatabaseProfileControlPlaneIntegrationHost.BuildServiceProvider(testEnvironment);

        var resolver = provider.GetRequiredService<IActiveDatabaseProfileResolver>();
        var workspaceResolver = provider.GetRequiredService<IWorkspacePathResolver>();
        var profileService = provider.GetRequiredService<IDatabaseProfileService>();

        var resolvedProfile = resolver.ResolveCurrentProfile();
        var selection = await profileService.GetCurrentSelectionAsync();

        Assert.Equal(DatabaseProfileResolutionSource.AutoProvisionedPostgreSql, resolvedProfile.ResolutionSource);
        Assert.Equal(DatabaseProviderKind.PostgreSql, resolvedProfile.Profile.ProviderKind);
        Assert.Equal(DatabaseProfileSourceKind.PostgresConnection, resolvedProfile.Profile.SourceKind);
        Assert.NotNull(resolvedProfile.Profile.PostgreSql);
        Assert.DoesNotContain("candoitall.db", resolvedProfile.ConnectionString, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(resolvedProfile.Profile.Storage.WorkspaceRoot, workspaceResolver.ResolveWorkspaceRoot());
        Assert.Equal(DatabaseProfileResolutionSource.PersistedActiveProfile, selection.ResolutionSource);
        var summaries = await profileService.ListAsync();
        var summary = Assert.Single(summaries);
        Assert.True(summary.IsActive);
        Assert.Equal(DatabaseProviderKind.PostgreSql, summary.ProviderKind);
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
