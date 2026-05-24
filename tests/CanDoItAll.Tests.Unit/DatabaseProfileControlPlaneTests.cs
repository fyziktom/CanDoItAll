using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using static CanDoItAll.Tests.Unit.DatabaseRuntimeSwitchingTestProfiles;

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

        await using (var persistedProvider = DatabaseProfileControlPlaneTestHost.BuildServiceProvider(testEnvironment, includeDatabaseOverride: true))
        {
            var service = persistedProvider.GetRequiredService<IDatabaseProfileService>();
            var saveResult = await service.SaveAsync(CreatePostgreSqlEditorForDatabase(
                "Persisted PostgreSQL",
                "persisted_postgres",
                Path.Combine(testEnvironment.RootPath, "persisted", "workspace")));

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
        Assert.Equal(DatabaseProviderKind.PostgreSql, summaries[0].ProviderKind);
    }

    [Fact]
    public async Task ResolveCurrentProfile_rejects_sqlite_database_override()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("control-plane-sqlite-override-rejected");

        var overrideWorkspaceRoot = Path.Combine(testEnvironment.RootPath, "wrong-override-workspace");
        var ex = Assert.Throws<InvalidOperationException>(() => DatabaseProfileControlPlaneTestHost.BuildServiceProvider(
                testEnvironment,
                includeDatabaseOverride: true,
                additionalValues: new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Database:Provider"] = "Sqlite",
                    ["Database:ConnectionString"] = "Data Source=C:\\legacy\\candoitall.db",
                    ["Storage:WorkspaceRoot"] = overrideWorkspaceRoot
                }));

        Assert.Contains("sqlite", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no longer supported", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ResolveCurrentProfile_uses_fingerprint_scoped_workspace_for_postgres_override_without_storage_override()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("control-plane-postgres-override-workspace");

        await using var firstProvider = DatabaseProfileControlPlaneTestHost.BuildServiceProvider(
            testEnvironment,
            includeDatabaseOverride: false,
            additionalValues: new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["Database:Provider"] = "PostgreSql",
                ["Database:ConnectionString"] = "Host=localhost;Port=5432;Database=candoitall-processes-1;Username=candoitall;Password=first-secret"
            });
        await using var secondProvider = DatabaseProfileControlPlaneTestHost.BuildServiceProvider(
            testEnvironment,
            includeDatabaseOverride: false,
            additionalValues: new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["Database:Provider"] = "PostgreSql",
                ["Database:ConnectionString"] = "Host=localhost;Port=5432;Database=candoitall-processes-2;Username=candoitall;Password=second-secret"
            });

        var firstProfile = firstProvider.GetRequiredService<IActiveDatabaseProfileResolver>().ResolveCurrentProfile();
        var secondProfile = secondProvider.GetRequiredService<IActiveDatabaseProfileResolver>().ResolveCurrentProfile();
        var expectedRootPrefix = Path.Combine(testEnvironment.RootPath, ".artifacts", "workspace", "runtime-overrides") +
                                 Path.DirectorySeparatorChar;

        Assert.Equal(DatabaseProfileResolutionSource.ExplicitOverride, firstProfile.ResolutionSource);
        Assert.Equal(DatabaseProviderKind.PostgreSql, firstProfile.Profile.ProviderKind);
        Assert.StartsWith(expectedRootPrefix, firstProfile.Profile.Storage.WorkspaceRoot, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith(expectedRootPrefix, secondProfile.Profile.Storage.WorkspaceRoot, StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual(firstProfile.Profile.Storage.WorkspaceRoot, secondProfile.Profile.Storage.WorkspaceRoot);
        Assert.DoesNotContain("first-secret", firstProfile.Profile.Storage.WorkspaceRoot, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("second-secret", secondProfile.Profile.Storage.WorkspaceRoot, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StartupConnectionResolver_uses_persisted_active_postgres_profile_without_database_override()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("control-plane-startup-postgres");

        Guid postgresProfileId;
        await using (var provider = DatabaseProfileControlPlaneTestHost.BuildServiceProvider(
            testEnvironment,
            includeDatabaseOverride: false))
        {
            var service = provider.GetRequiredService<IDatabaseProfileService>();
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
                WorkspaceRoot = Path.Combine(testEnvironment.RootPath, "profiles", "postgres-workspace")
            });

            Assert.True(saveResult.IsSuccess);
            postgresProfileId = saveResult.Value;
            Assert.True((await service.ActivateAsync(postgresProfileId)).IsSuccess);
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["ControlPlane:RootPath"] = testEnvironment.ControlPlaneRootPath
            })
            .Build();

        var resolution = DatabaseProfileStartupConnectionResolver.TryResolve(
            configuration,
            testEnvironment.RootPath);

        Assert.NotNull(resolution);
        Assert.Equal(DatabaseProfileResolutionSource.PersistedActiveProfile, resolution!.ResolutionSource);
        Assert.Equal(postgresProfileId, resolution.ProfileId);
        Assert.Equal(DatabaseProviderKind.PostgreSql, resolution.ProviderKind);
        Assert.Contains("Password=super-secret", resolution.ConnectionString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StartupConnectionResolver_prefers_explicit_database_override_over_control_plane_profile()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("control-plane-startup-override");

        await using (var provider = DatabaseProfileControlPlaneTestHost.BuildServiceProvider(
            testEnvironment,
            includeDatabaseOverride: false))
        {
            var service = provider.GetRequiredService<IDatabaseProfileService>();
            var saveResult = await service.SaveAsync(CreatePostgreSqlEditorForDatabase(
                "Managed PostgreSQL profile",
                "startup_control_plane",
                Path.Combine(testEnvironment.RootPath, "startup-postgres-workspace")));

            Assert.True(saveResult.IsSuccess);
            Assert.True((await service.ActivateAsync(saveResult.Value)).IsSuccess);
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["ControlPlane:RootPath"] = testEnvironment.ControlPlaneRootPath,
                ["Database:Provider"] = "InMemory",
                ["Database:ConnectionString"] = "startup-override"
            })
            .Build();

        var resolution = DatabaseProfileStartupConnectionResolver.TryResolve(
            configuration,
            testEnvironment.RootPath);

        Assert.NotNull(resolution);
        Assert.Equal(DatabaseProfileResolutionSource.ExplicitOverride, resolution!.ResolutionSource);
        Assert.Null(resolution.ProfileId);
        Assert.Equal(DatabaseProviderKind.InMemory, resolution.ProviderKind);
        Assert.Equal("startup-override", resolution.ConnectionString);
    }
}

public sealed class SnapshotBackedProfileCatalogTests
{
    [Fact]
    public async Task SaveAsync_rejects_snapshot_cache_profiles()
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

        Assert.True(saveResult.IsFailure);
        Assert.Contains(saveResult.Errors, error => error.Message.Contains("SQLite database profiles are no longer supported", StringComparison.Ordinal));
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
