using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
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
    public async Task ResolveCurrentProfile_rejects_retired_provider_database_override()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("control-plane-retired-provider-override-rejected");

        var overrideWorkspaceRoot = Path.Combine(testEnvironment.RootPath, "wrong-override-workspace");
        var ex = Assert.Throws<InvalidOperationException>(() => DatabaseProfileControlPlaneTestHost.BuildServiceProvider(
                testEnvironment,
                includeDatabaseOverride: true,
                additionalValues: new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Database:Provider"] = LegacyCatalogTestData.RetiredProviderName(),
                    ["Database:ConnectionString"] = "Data Source=C:\\legacy\\candoitall.db",
                    ["Storage:WorkspaceRoot"] = overrideWorkspaceRoot
                }));

        Assert.Contains("Unsupported database provider", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(LegacyCatalogTestData.RetiredProviderName(), ex.Message, StringComparison.OrdinalIgnoreCase);
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

public sealed class LegacyDatabaseProfileCatalogQuarantineTests
{
    [Fact]
    public async Task ResolveCurrentProfile_quarantines_retired_only_catalog_and_creates_default_profile()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("control-plane-legacy-only-quarantine");
        var retiredProfileId = Guid.NewGuid();
        await LegacyCatalogTestData.WriteCatalogAsync(
            testEnvironment,
            [LegacyCatalogTestData.CreateRetiredProfileJson(retiredProfileId, "Retired local profile")],
            retiredProfileId);

        await using var provider = DatabaseProfileControlPlaneTestHost.BuildServiceProvider(
            testEnvironment,
            includeDatabaseOverride: false);

        var service = provider.GetRequiredService<IDatabaseProfileService>();
        var resolver = provider.GetRequiredService<IActiveDatabaseProfileResolver>();

        var resolved = resolver.ResolveCurrentProfile();
        var summaries = await service.ListAsync();
        var catalogJson = await File.ReadAllTextAsync(LegacyCatalogTestData.CatalogPath(testEnvironment));
        var quarantineJson = await File.ReadAllTextAsync(Assert.Single(Directory.GetFiles(LegacyCatalogTestData.QuarantinePath(testEnvironment), "*.json")));

        Assert.Equal(DatabaseProfileResolutionSource.AutoProvisionedPostgreSql, resolved.ResolutionSource);
        Assert.Equal(DatabaseProviderKind.PostgreSql, resolved.Profile.ProviderKind);
        var summary = Assert.Single(summaries);
        Assert.True(summary.IsActive);
        Assert.Equal(DatabaseProviderKind.PostgreSql, summary.ProviderKind);
        Assert.DoesNotContain(LegacyCatalogTestData.RetiredProviderName(), catalogJson, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(LegacyCatalogTestData.RetiredProviderName(), quarantineJson, StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual(retiredProfileId, await LegacyCatalogTestData.ReadActiveProfileIdAsync(testEnvironment));
    }

    [Fact]
    public async Task ResolveCurrentProfile_quarantines_retired_entries_and_retains_postgresql_entries()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("control-plane-mixed-quarantine");
        var retiredProfileId = Guid.NewGuid();
        var postgresProfileId = Guid.NewGuid();
        await LegacyCatalogTestData.WriteCatalogAsync(
            testEnvironment,
            [
                LegacyCatalogTestData.CreateRetiredProfileJson(retiredProfileId, "Retired local profile"),
                LegacyCatalogTestData.CreatePostgreSqlProfileJson(postgresProfileId, "Retained PostgreSQL")
            ],
            retiredProfileId);

        await using var provider = DatabaseProfileControlPlaneTestHost.BuildServiceProvider(
            testEnvironment,
            includeDatabaseOverride: false);

        var service = provider.GetRequiredService<IDatabaseProfileService>();
        var resolved = provider.GetRequiredService<IActiveDatabaseProfileResolver>().ResolveCurrentProfile();
        var summaries = await service.ListAsync();
        var catalogJson = await File.ReadAllTextAsync(LegacyCatalogTestData.CatalogPath(testEnvironment));

        Assert.Equal(DatabaseProfileResolutionSource.PersistedCatalogFallback, resolved.ResolutionSource);
        Assert.Equal(postgresProfileId, resolved.Profile.Id);
        var summary = Assert.Single(summaries);
        Assert.Equal(postgresProfileId, summary.Id);
        Assert.True(summary.IsActive);
        Assert.DoesNotContain(LegacyCatalogTestData.RetiredProviderName(), catalogJson, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(postgresProfileId, await LegacyCatalogTestData.ReadActiveProfileIdAsync(testEnvironment));
        Assert.Single(Directory.GetFiles(LegacyCatalogTestData.QuarantinePath(testEnvironment), "*.json"));
    }

    [Fact]
    public async Task ResolveCurrentProfile_retains_postgresql_entry_with_null_legacy_sqlite_metadata()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("control-plane-postgres-null-sqlite");
        var postgresProfileId = Guid.NewGuid();
        await LegacyCatalogTestData.WriteCatalogAsync(
            testEnvironment,
            [LegacyCatalogTestData.CreatePostgreSqlProfileJson(postgresProfileId, "Retained PostgreSQL", includeNullSqliteMetadata: true)],
            postgresProfileId);

        await using var provider = DatabaseProfileControlPlaneTestHost.BuildServiceProvider(
            testEnvironment,
            includeDatabaseOverride: false);

        var resolved = provider.GetRequiredService<IActiveDatabaseProfileResolver>().ResolveCurrentProfile();
        var service = provider.GetRequiredService<IDatabaseProfileService>();
        var summaries = await service.ListAsync();

        Assert.Equal(DatabaseProfileResolutionSource.PersistedActiveProfile, resolved.ResolutionSource);
        Assert.Equal(postgresProfileId, resolved.Profile.Id);
        var summary = Assert.Single(summaries);
        Assert.Equal(postgresProfileId, summary.Id);
        Assert.False(Directory.Exists(LegacyCatalogTestData.QuarantinePath(testEnvironment)));
    }

    [Fact]
    public async Task ListAsync_leaves_valid_postgresql_catalog_without_quarantine()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("control-plane-valid-catalog");
        await using var provider = DatabaseProfileControlPlaneTestHost.BuildServiceProvider(
            testEnvironment,
            includeDatabaseOverride: false);

        var service = provider.GetRequiredService<IDatabaseProfileService>();
        var saveResult = await service.SaveAsync(CreatePostgreSqlEditorForDatabase(
            "Valid PostgreSQL",
            "valid_catalog",
            Path.Combine(testEnvironment.RootPath, "valid-workspace")));

        Assert.True(saveResult.IsSuccess);
        var summaries = await service.ListAsync();

        var summary = Assert.Single(summaries);
        Assert.Equal(saveResult.Value, summary.Id);
        Assert.False(Directory.Exists(LegacyCatalogTestData.QuarantinePath(testEnvironment)));
    }
}

internal static class LegacyCatalogTestData
{
    public static string RetiredProviderName() => "Sqlite";

    public static string CatalogPath(CanDoItAllTestEnvironment testEnvironment)
        => Path.Combine(testEnvironment.ControlPlaneRootPath, "database-profiles", "catalog.json");

    public static string QuarantinePath(CanDoItAllTestEnvironment testEnvironment)
        => Path.Combine(testEnvironment.ControlPlaneRootPath, "database-profiles", "quarantine");

    public static async Task WriteCatalogAsync(
        CanDoItAllTestEnvironment testEnvironment,
        IReadOnlyList<string> profileJson,
        Guid? activeProfileId)
    {
        var profileRoot = Path.Combine(testEnvironment.ControlPlaneRootPath, "database-profiles");
        Directory.CreateDirectory(profileRoot);
        await File.WriteAllTextAsync(
            Path.Combine(profileRoot, "catalog.json"),
            $$"""
            {
              "schemaVersion": 1,
              "profiles": [
                {{string.Join($",{Environment.NewLine}", profileJson)}}
              ]
            }
            """);

        await File.WriteAllTextAsync(
            Path.Combine(profileRoot, "active-profile.json"),
            $$"""
            {
              "activeProfileId": {{JsonSerializer.Serialize(activeProfileId)}},
              "lastPromptShownAtUtc": null,
              "lastSwitchGeneration": 0
            }
            """);
    }

    public static string CreateRetiredProfileJson(Guid profileId, string displayName)
    {
        var providerName = RetiredProviderName();
        var sourceName = "Managed" + providerName;
        var connectionPropertyName = providerName.ToLowerInvariant();
        return $$"""
            {
              "id": "{{profileId}}",
              "displayName": {{JsonSerializer.Serialize(displayName)}},
              "providerKind": "{{providerName}}",
              "sourceKind": "{{sourceName}}",
              "{{connectionPropertyName}}": {
                "databasePath": "C:\\legacy\\candoitall.db"
              },
              "storage": {
                "mode": "ManagedPerProfile",
                "workspaceRoot": "C:\\legacy"
              },
              "runtime": {
                "fingerprint": "legacy:local",
                "lockedByRuntimeOverride": false
              },
              "audit": {
                "createdUtc": "2026-01-01T00:00:00+00:00",
                "lastUsedUtc": "2026-01-02T00:00:00+00:00",
                "lastSuccessfulOpenUtc": "2026-01-03T00:00:00+00:00"
              }
            }
            """;
    }

    public static string CreatePostgreSqlProfileJson(
        Guid profileId,
        string displayName,
        bool includeNullSqliteMetadata = false)
    {
        var nullSqliteMetadata = includeNullSqliteMetadata
            ? """
              "sqlite": null,
            """
            : string.Empty;

        return $$"""
            {
              "id": "{{profileId}}",
              "displayName": {{JsonSerializer.Serialize(displayName)}},
              "providerKind": "PostgreSql",
              "sourceKind": "PostgresConnection",
              {{nullSqliteMetadata}}
              "postgreSql": {
                "host": "localhost",
                "port": 5432,
                "databaseName": "candoitall",
                "username": "postgres",
                "encryptedPassword": null,
                "adminDatabaseName": null,
                "trustServerCertificate": false
              },
              "storage": {
                "mode": "ExternalWorkspaceRoot",
                "workspaceRoot": "C:\\postgres-workspace"
              },
              "runtime": {
                "fingerprint": "postgres:localhost:5432:candoitall:postgres",
                "lockedByRuntimeOverride": false
              },
              "audit": {
                "createdUtc": "2026-01-01T00:00:00+00:00",
                "lastUsedUtc": "2026-01-04T00:00:00+00:00",
                "lastSuccessfulOpenUtc": "2026-01-05T00:00:00+00:00"
              }
            }
            """;
    }

    public static async Task<Guid?> ReadActiveProfileIdAsync(CanDoItAllTestEnvironment testEnvironment)
    {
        await using var stream = File.OpenRead(Path.Combine(
            testEnvironment.ControlPlaneRootPath,
            "database-profiles",
            "active-profile.json"));
        using var document = await JsonDocument.ParseAsync(stream);
        return document.RootElement.TryGetProperty("activeProfileId", out var activeProfileId) &&
            activeProfileId.ValueKind == JsonValueKind.String &&
            Guid.TryParse(activeProfileId.GetString(), out var parsed)
                ? parsed
                : null;
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
