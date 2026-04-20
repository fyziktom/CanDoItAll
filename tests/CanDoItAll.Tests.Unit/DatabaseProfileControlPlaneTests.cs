using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Unit;

public sealed class ControlPlaneDatabaseProfileCatalogTests
{
    [Fact]
    public async Task SaveAsync_persists_postgres_profile_metadata_without_plaintext_password()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("control-plane-catalog");
        await using var provider = DatabaseProfileControlPlaneTestHost.BuildServiceProvider(
            testEnvironment,
            includeDatabaseOverride: true);

        var service = provider.GetRequiredService<IDatabaseProfileService>();
        var profileWorkspaceRoot = Path.Combine(testEnvironment.RootPath, "profiles", "postgres-workspace");
        var saveResult = await service.SaveAsync(new DatabaseProfileEditorModel
        {
            DisplayName = "Local postgres",
            ProviderKind = DatabaseProviderKind.PostgreSql,
            SourceKind = DatabaseProfileSourceKind.PostgresConnection,
            PostgresHost = "localhost",
            PostgresPort = 5432,
            PostgresDatabaseName = "candoitall",
            PostgresUsername = "postgres",
            PostgresPassword = "super-secret",
            WorkspaceRoot = profileWorkspaceRoot
        });

        Assert.True(saveResult.IsSuccess);

        var summaries = await service.ListAsync();
        var summary = Assert.Single(summaries);
        Assert.Equal("Local postgres", summary.DisplayName);
        Assert.Equal(DatabaseProviderKind.PostgreSql, summary.ProviderKind);

        var editor = await service.GetEditorAsync(saveResult.Value);
        Assert.Equal("super-secret", editor.PostgresPassword);

        var catalogPath = Path.Combine(testEnvironment.ControlPlaneRootPath, "database-profiles", "catalog.json");
        var catalogJson = await File.ReadAllTextAsync(catalogPath);
        Assert.Contains("Local postgres", catalogJson);
        Assert.DoesNotContain("super-secret", catalogJson);
    }
}

public sealed class DataProtectionControlPlaneTests
{
    [Fact]
    public async Task Control_plane_secret_protector_round_trips_across_service_provider_restart()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("control-plane-dataprotection");
        var protectedValue = string.Empty;

        await using (var firstProvider = DatabaseProfileControlPlaneTestHost.BuildServiceProvider(testEnvironment, includeDatabaseOverride: true))
        {
            var protector = firstProvider.GetRequiredService<IControlPlaneSecretProtector>();
            protectedValue = protector.Protect("postgres-password");
        }

        await using (var secondProvider = DatabaseProfileControlPlaneTestHost.BuildServiceProvider(testEnvironment, includeDatabaseOverride: true))
        {
            var protector = secondProvider.GetRequiredService<IControlPlaneSecretProtector>();
            Assert.Equal("postgres-password", protector.Unprotect(protectedValue));
        }

        var keysPath = Path.Combine(testEnvironment.ControlPlaneRootPath, "dataprotection-keys");
        Assert.NotEmpty(Directory.GetFiles(keysPath));
    }
}

public sealed class DatabaseProfileOverrideTests
{
    [Fact]
    public async Task ResolveCurrentProfile_prefers_explicit_override_over_the_persisted_active_profile()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("control-plane-override");

        var persistedDatabasePath = Path.Combine(testEnvironment.RootPath, "persisted", "workspace", "persisted.db");
        Directory.CreateDirectory(Path.GetDirectoryName(persistedDatabasePath)!);

        await using (var persistedProvider = DatabaseProfileControlPlaneTestHost.BuildServiceProvider(testEnvironment, includeDatabaseOverride: true))
        {
            var service = persistedProvider.GetRequiredService<IDatabaseProfileService>();
            var saveResult = await service.SaveAsync(new DatabaseProfileEditorModel
            {
                DisplayName = "Persisted sqlite",
                ProviderKind = DatabaseProviderKind.Sqlite,
                SourceKind = DatabaseProfileSourceKind.ExternalSqliteFile,
                SqliteDatabasePath = persistedDatabasePath,
                WorkspaceRoot = Path.GetDirectoryName(persistedDatabasePath)!
            });

            Assert.True(saveResult.IsSuccess);
            await service.ActivateAsync(saveResult.Value);
        }

        var overrideWorkspaceRoot = Path.Combine(testEnvironment.RootPath, "override-workspace");
        await using var overrideProvider = DatabaseProfileControlPlaneTestHost.BuildServiceProvider(
            testEnvironment,
            includeDatabaseOverride: true,
            additionalValues: new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["Database:Provider"] = "InMemory",
                ["Database:ConnectionString"] = "override-selection",
                ["Storage:WorkspaceRoot"] = overrideWorkspaceRoot
            });

        var resolver = overrideProvider.GetRequiredService<IActiveDatabaseProfileResolver>();
        var workspaceResolver = overrideProvider.GetRequiredService<IWorkspacePathResolver>();
        var controlPlaneService = overrideProvider.GetRequiredService<IDatabaseProfileService>();

        var resolvedProfile = resolver.ResolveCurrentProfile();
        var selection = await controlPlaneService.GetCurrentSelectionAsync();
        var summaries = await controlPlaneService.ListAsync();

        Assert.Equal(DatabaseProfileResolutionSource.ExplicitOverride, resolvedProfile.ResolutionSource);
        Assert.True(resolvedProfile.Profile.Runtime.LockedByRuntimeOverride);
        Assert.Equal(DatabaseProviderKind.InMemory, resolvedProfile.Profile.ProviderKind);
        Assert.Equal(overrideWorkspaceRoot, resolvedProfile.Profile.Storage.WorkspaceRoot);
        Assert.Equal(overrideWorkspaceRoot, workspaceResolver.ResolveWorkspaceRoot());
        Assert.Equal(DatabaseProfileResolutionSource.ExplicitOverride, selection.ResolutionSource);
        Assert.Single(summaries);
        Assert.False(summaries[0].IsActive);
        Assert.Equal(DatabaseProviderKind.Sqlite, summaries[0].ProviderKind);
    }

    [Fact]
    public async Task ResolveCurrentProfile_reuses_the_persisted_sqlite_profile_identity_when_the_override_targets_the_same_database_file()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("control-plane-sqlite-override-match");

        Guid persistedProfileId;
        DatabaseProfileEditorModel persistedEditor;
        await using (var persistedProvider = DatabaseProfileControlPlaneTestHost.BuildServiceProvider(testEnvironment, includeDatabaseOverride: false))
        {
            var service = persistedProvider.GetRequiredService<IDatabaseProfileService>();
            var saveResult = await service.SaveAsync(new DatabaseProfileEditorModel
            {
                DisplayName = "Managed profile for override match",
                ProviderKind = DatabaseProviderKind.Sqlite,
                SourceKind = DatabaseProfileSourceKind.ManagedSqlite
            });

            Assert.True(saveResult.IsSuccess);
            persistedProfileId = saveResult.Value;
            persistedEditor = await service.GetEditorAsync(saveResult.Value);
        }

        var overrideWorkspaceRoot = Path.Combine(testEnvironment.RootPath, "wrong-override-workspace");
        await using var overrideProvider = DatabaseProfileControlPlaneTestHost.BuildServiceProvider(
            testEnvironment,
            includeDatabaseOverride: true,
            additionalValues: new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["Database:Provider"] = "Sqlite",
                ["Database:ConnectionString"] = $"Data Source={persistedEditor.SqliteDatabasePath}",
                ["Storage:WorkspaceRoot"] = overrideWorkspaceRoot
            });

        var resolver = overrideProvider.GetRequiredService<IActiveDatabaseProfileResolver>();
        var workspaceResolver = overrideProvider.GetRequiredService<IWorkspacePathResolver>();
        var controlPlaneService = overrideProvider.GetRequiredService<IDatabaseProfileService>();

        var resolvedProfile = resolver.ResolveCurrentProfile();
        var selection = await controlPlaneService.GetCurrentSelectionAsync();

        Assert.Equal(DatabaseProfileResolutionSource.ExplicitOverride, resolvedProfile.ResolutionSource);
        Assert.True(resolvedProfile.Profile.Runtime.LockedByRuntimeOverride);
        Assert.Equal(persistedProfileId, resolvedProfile.Profile.Id);
        Assert.Equal(DatabaseProfileSourceKind.ManagedSqlite, resolvedProfile.Profile.SourceKind);
        Assert.Equal(persistedEditor.SqliteDatabasePath, resolvedProfile.Profile.Sqlite!.DatabasePath);
        Assert.Equal(persistedEditor.WorkspaceRoot, resolvedProfile.Profile.Storage.WorkspaceRoot);
        Assert.Equal(persistedEditor.WorkspaceRoot, workspaceResolver.ResolveWorkspaceRoot());
        Assert.Equal(persistedProfileId, selection.ActiveProfileId);
        Assert.Equal(DatabaseProfileResolutionSource.ExplicitOverride, selection.ResolutionSource);
    }
}

public sealed class SnapshotBackedProfileCatalogTests
{
    [Fact]
    public async Task SaveAsync_auto_assigns_snapshot_cache_paths_for_snapshot_backed_profiles()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("control-plane-snapshot-cache");
        await using var provider = DatabaseProfileControlPlaneTestHost.BuildServiceProvider(
            testEnvironment,
            includeDatabaseOverride: false);

        var service = provider.GetRequiredService<IDatabaseProfileService>();
        var saveResult = await service.SaveAsync(new DatabaseProfileEditorModel
        {
            DisplayName = "Snapshot cache profile",
            ProviderKind = DatabaseProviderKind.Sqlite,
            SourceKind = DatabaseProfileSourceKind.SnapshotCache,
            OriginProfileId = Guid.NewGuid(),
            OriginSnapshotId = Guid.NewGuid()
        });

        Assert.True(saveResult.IsSuccess);

        var editor = await service.GetEditorAsync(saveResult.Value);
        Assert.False(string.IsNullOrWhiteSpace(editor.SqliteDatabasePath));
        Assert.StartsWith(testEnvironment.ControlPlaneRootPath, editor.SqliteDatabasePath, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith(testEnvironment.ControlPlaneRootPath, editor.WorkspaceRoot, StringComparison.OrdinalIgnoreCase);

        var summary = Assert.Single(await service.ListAsync());
        Assert.StartsWith("sqlite:snapshot:", summary.Fingerprint, StringComparison.OrdinalIgnoreCase);
    }
}

internal static class DatabaseProfileControlPlaneTestHost
{
    public static ServiceProvider BuildServiceProvider(
        CanDoItAllTestEnvironment testEnvironment,
        bool includeDatabaseOverride,
        IReadOnlyDictionary<string, string?>? additionalValues = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(CreateConfigurationValues(testEnvironment, includeDatabaseOverride, additionalValues))
            .Build();

        var services = new ServiceCollection();
        TestApplicationBootstrap.ConfigureDefaultServices(
            services,
            configuration,
            testEnvironment.CreateHostEnvironment("CanDoItAll.Tests.Unit"));

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
    }

    private static IReadOnlyDictionary<string, string?> CreateConfigurationValues(
        CanDoItAllTestEnvironment testEnvironment,
        bool includeDatabaseOverride,
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

        if (includeDatabaseOverride)
        {
            values["Database:Provider"] = "InMemory";
            values["Database:ConnectionString"] = "unit-control-plane";
            values["Storage:WorkspaceRoot"] = Path.Combine(testEnvironment.RootPath, "workspace");
        }

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
