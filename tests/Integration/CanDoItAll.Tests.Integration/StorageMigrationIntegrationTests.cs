using System.Text.Json;
using CanDoItAll.Composition;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Integration.Persistence;

[Trait("Category", "UnixPortabilityCore")]
public sealed class StorageMigrationIntegrationTests
{
    [Fact]
    [Trait("Category", "StorageMigration")]
    [Trait("RequiresHostDocker", "true")]
    public async Task Storage_catalog_migration_is_transactional_restartable_and_reversible_on_postgresql()
    {
        AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);
        string workspaceRoot = TestFileSystem.CreateTemporaryRoot("storage-migration-integration-workspace");
        string legacyRoot = TestFileSystem.CreateTemporaryRoot("storage-migration-integration-legacy");
        await using PostgresTestDatabaseLease database = PostgresTestDatabaseLease.Create("storagepathmigration");

        try
        {
            DbContextOptions<AppDbContext> options = database.CreateAppDbContextOptions();
            Guid storageId = Guid.NewGuid();
            await using (var seed = new AppDbContext(options))
            {
                await seed.Database.EnsureCreatedAsync();
                seed.Add(new StorageCatalogRecord
                {
                    Id = storageId,
                    Name = "Legacy filesystem",
                    ProviderKind = StorageProviderKind.FileSystem,
                    EndpointOrRoot = legacyRoot,
                    IsEnabled = true
                });
                await seed.SaveChangesAsync();
            }

            StorageCatalogService firstService = CreateStorageCatalogService(options, workspaceRoot);
            StorageCatalogPathMigrationReport committed = await firstService.ExecuteAsync();
            string migrationRoot = Path.Combine(
                workspaceRoot,
                ".candoitall",
                "migrations",
                "storage-catalog-host-binding-v1");
            File.Delete(Path.Combine(migrationRoot, "commit.json"));

            StorageCatalogService restartedService = CreateStorageCatalogService(options, workspaceRoot);
            StorageCatalogPathMigrationReport repaired = await restartedService.ExecuteAsync();
            await using (var committedContext = new AppDbContext(options))
            {
                StorageCatalogRecord migrated = await committedContext.Set<StorageCatalogRecord>()
                    .SingleAsync(item => item.Id == storageId);
                Assert.Equal(HostBoundPathState.NeedsRebind, migrated.RootPathState);
                Assert.Equal(HostBoundPathRecord.CurrentFormatVersion, migrated.RootBindingFormatVersion);
                Assert.False(migrated.IsEnabled);
            }

            Assert.Equal(StorageCatalogPathMigrationState.PointerCommitted, committed.State);
            Assert.Equal(StorageCatalogPathMigrationState.PointerCommitted, repaired.State);
            Assert.True(File.Exists(Path.Combine(migrationRoot, "commit.json")));

            StorageCatalogRecord rebound = await restartedService.RebindRootAsync(storageId, legacyRoot);
            Assert.Equal(HostBoundPathState.Active, rebound.RootPathState);
            Assert.NotEmpty(rebound.RootHostBindingId);

            StorageCatalogPathMigrationReport rolledBack = await restartedService.RollbackAsync();
            await using var rolledBackContext = new AppDbContext(options);
            StorageCatalogRecord restored = await rolledBackContext.Set<StorageCatalogRecord>()
                .SingleAsync(item => item.Id == storageId);
            Assert.Equal(StorageCatalogPathMigrationState.RolledBack, rolledBack.State);
            Assert.Equal(legacyRoot, restored.EndpointOrRoot);
            Assert.Equal(0, restored.RootBindingFormatVersion);
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(workspaceRoot);
            TestFileSystem.DeleteDirectoryWithRetry(legacyRoot);
        }
    }

    [Fact]
    [Trait("Category", "StorageMigration")]
    public async Task Preferred_application_migration_survives_restart_and_supports_rollback()
    {
        string rootPath = Path.Combine(
            Path.GetTempPath(),
            nameof(StorageMigrationIntegrationTests),
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(rootPath);

        try
        {
            string executablePath = Path.Combine(rootPath, "document-viewer");
            await File.WriteAllTextAsync(executablePath, string.Empty);
            string settingsPath = Path.Combine(rootPath, "file-application-preferences.json");
            await File.WriteAllTextAsync(
                settingsPath,
                $$"""
                {
                  "schemaVersion": 1,
                  "applications": [
                    {
                      "extension": ".docx",
                      "executablePath": {{JsonSerializer.Serialize(executablePath)}}
                    }
                  ]
                }
                """);

            FileApplicationPreference migrated = Assert.Single(await CreateService(rootPath).ListAsync());
            FileApplicationPreference? restarted = CreateService(rootPath).ResolveForFile("proposal.docx");

            Assert.True(migrated.RequiresRebind);
            Assert.Null(restarted);

            await CreateService(rootPath).SaveAsync(new FileApplicationPreference(
                new FileApplicationExtension(".docx"),
                executablePath));
            FileApplicationPreference? rebound = CreateService(rootPath).ResolveForFile("proposal.docx");
            Assert.NotNull(rebound);
            Assert.Equal(Path.GetFullPath(executablePath), rebound.ExecutablePath);
            Assert.True(await CreateService(rootPath).RollbackPathMigrationAsync());
            using (JsonDocument rolledBack = JsonDocument.Parse(await File.ReadAllTextAsync(settingsPath)))
            {
                Assert.Equal(1, rolledBack.RootElement.GetProperty("schemaVersion").GetInt32());
            }

            FileApplicationPreference remigrated = Assert.Single(await CreateService(rootPath).ListAsync());
            Assert.True(remigrated.RequiresRebind);
            using JsonDocument current = JsonDocument.Parse(await File.ReadAllTextAsync(settingsPath));
            Assert.Equal(2, current.RootElement.GetProperty("schemaVersion").GetInt32());
        }
        finally
        {
            if (Directory.Exists(rootPath))
            {
                Directory.Delete(rootPath, recursive: true);
            }
        }
    }

    [Fact]
    [Trait("Category", "StorageMigration")]
    [Trait("RequiresHostDocker", "true")]
    public async Task Bootstrap_preserves_a_remote_system_default_on_postgresql()
    {
        AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);
        string workspaceRoot = TestFileSystem.CreateTemporaryRoot("storage-bootstrap-remote-default");
        await using PostgresTestDatabaseLease database = PostgresTestDatabaseLease.Create("storageremotedefault");

        try
        {
            DbContextOptions<AppDbContext> options = database.CreateAppDbContextOptions();
            Guid storageId = Guid.NewGuid();
            await using (var seed = new AppDbContext(options))
            {
                await seed.Database.EnsureCreatedAsync();
                seed.Add(new StorageCatalogRecord
                {
                    Id = storageId,
                    Name = "Remote default",
                    ProviderKind = StorageProviderKind.Ipfs,
                    IsSystemDefault = true,
                    IsEnabled = true,
                    EndpointOrRoot = "https://ipfs.example.test",
                    ConfigJson = "{\"gatewayBaseUrl\":\"https://ipfs.example.test\"}",
                    CredentialSecretId = Guid.NewGuid()
                });
                await seed.SaveChangesAsync();
            }

            StorageCatalogService service = CreateStorageCatalogService(options, workspaceRoot);

            InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.EnsureBootstrapFileSystemStorageAsync());

            Assert.Contains("left unchanged", exception.Message, StringComparison.OrdinalIgnoreCase);
            await using var assertContext = new AppDbContext(options);
            StorageCatalogRecord preserved = await assertContext.Set<StorageCatalogRecord>()
                .AsNoTracking()
                .SingleAsync();
            Assert.Equal(storageId, preserved.Id);
            Assert.Equal("Remote default", preserved.Name);
            Assert.Equal(StorageProviderKind.Ipfs, preserved.ProviderKind);
            Assert.Equal("https://ipfs.example.test", preserved.EndpointOrRoot);
            Assert.Equal("{\"gatewayBaseUrl\":\"https://ipfs.example.test\"}", preserved.ConfigJson);
            Assert.NotNull(preserved.CredentialSecretId);
            Assert.Empty(await assertContext.Set<StorageRoutingRule>().AsNoTracking().ToListAsync());
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(workspaceRoot);
        }
    }

    private static FileApplicationPreferenceService CreateService(string rootPath)
        => new(
            new StaticControlPlanePathResolver(rootPath),
            new DurableFileWriter(new PhysicalFileSystemPathPolicyFactory()),
            NullLogger<FileApplicationPreferenceService>.Instance);

    private static StorageCatalogService CreateStorageCatalogService(
        DbContextOptions<AppDbContext> options,
        string workspaceRoot)
    {
        return new StorageCatalogService(
            new TestDbContextFactory(options),
            new StaticWorkspacePathResolver(workspaceRoot),
            new TestClock(new DateTimeOffset(2026, 8, 9, 12, 0, 0, TimeSpan.Zero)));
    }

    private sealed class StaticControlPlanePathResolver(string rootPath) : IControlPlanePathResolver
    {
        public string ResolveRootPath() => rootPath;

        public string ResolveDatabaseProfilesRootPath() => Path.Combine(rootPath, "database-profiles");

        public string ResolveCatalogFilePath() => Path.Combine(ResolveDatabaseProfilesRootPath(), "catalog.json");

        public string ResolveActiveProfileStateFilePath()
            => Path.Combine(ResolveDatabaseProfilesRootPath(), "active-profile.json");

        public string ResolveFileApplicationPreferencesFilePath()
            => Path.Combine(rootPath, "file-application-preferences.json");

        public string ResolveDataProtectionKeysPath() => Path.Combine(rootPath, "dataprotection-keys");

        public string ResolveStateRootPath() => Path.Combine(rootPath, "state");

        public string ResolveLogsRootPath() => Path.Combine(rootPath, "logs");

        public string ResolveRuntimeTemporaryRootPath() => Path.Combine(rootPath, "runtime");
    }

    private sealed class StaticWorkspacePathResolver(string workspaceRoot) : IWorkspacePathResolver
    {
        public string ResolveWorkspaceRoot() => workspaceRoot;

        public string ResolveManagedFilesRoot() => Path.Combine(workspaceRoot, "managed-files");

        public string ResolveExportsRoot() => Path.Combine(workspaceRoot, "exports");

        public string ResolveEvidenceRoot() => Path.Combine(workspaceRoot, "evidence");

        public string ResolveManagerArtifactsRoot() => Path.Combine(workspaceRoot, "manager-artifacts");
    }

    private sealed class TestDbContextFactory(DbContextOptions<AppDbContext> options) : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() => new(options);

        public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new AppDbContext(options));
    }

    private sealed class TestClock(DateTimeOffset currentUtc) : IClock
    {
        public DateTimeOffset GetUtcNow() => currentUtc;
    }
}
