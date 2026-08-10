using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Tests.Unit;

[Collection(AppDbContextModelRegistryTestCollectionNames.Name)]
[Trait("Category", "UnixPortabilityCore")]
public sealed class StorageCatalogServiceTests
{
    [Fact]
    public void Bootstrap_authority_rejects_ambiguous_current_host_roots()
    {
        string workspaceRoot = TestFileSystem.CreateTemporaryRoot("storage-bootstrap-authority");
        try
        {
            StorageCatalogRecord first = CreateBoundBootstrapStorage(workspaceRoot, "First");
            StorageCatalogRecord second = CreateBoundBootstrapStorage(workspaceRoot, "Second");

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                StorageBootstrapCatalogPolicy.ResolveAuthoritativeFileSystemStorage(
                    [first, second],
                    workspaceRoot));

            Assert.Contains("multiple", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(workspaceRoot);
        }
    }

    [Fact]
    public async Task ListAsync_bootstraps_the_workspace_file_system_storage_once()
    {
        var workspaceRoot = TestFileSystem.CreateTemporaryRoot("storage-catalog");

        try
        {
            var databaseName = $"storage-catalog-{Guid.NewGuid():N}";
            var sut = CreateSut(databaseName, workspaceRoot);

            var firstResult = await sut.ListAsync();
            var secondResult = await sut.ListAsync();
            var rules = await sut.ListRulesAsync();

            var bootstrapStorage = Assert.Single(firstResult, item => item.IsSystemDefault);
            Assert.Single(secondResult, item => item.IsSystemDefault);
            Assert.Equal(workspaceRoot, bootstrapStorage.EndpointOrRoot);
            Assert.Equal(StorageProviderKind.FileSystem, bootstrapStorage.ProviderKind);
            Assert.Equal(HostBoundPathRecord.CurrentFormatVersion, bootstrapStorage.RootBindingFormatVersion);
            Assert.Equal(HostBoundPathState.Active, bootstrapStorage.RootPathState);
            Assert.NotEmpty(bootstrapStorage.RootHostBindingId);
            Assert.Single(rules, item => item.PreferredStorageId == bootstrapStorage.Id);
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(workspaceRoot);
        }
    }

    [Fact]
    public async Task DeleteAsync_does_not_remove_the_system_default_storage()
    {
        var workspaceRoot = TestFileSystem.CreateTemporaryRoot("storage-catalog");

        try
        {
            var databaseName = $"storage-catalog-{Guid.NewGuid():N}";
            var sut = CreateSut(databaseName, workspaceRoot);
            var bootstrapStorage = await sut.EnsureBootstrapFileSystemStorageAsync();

            await sut.DeleteAsync(bootstrapStorage.Id);

            var storages = await sut.ListAsync();
            Assert.Single(storages, item => item.Id == bootstrapStorage.Id);
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(workspaceRoot);
        }
    }

    [Fact]
    public async Task Bootstrap_does_not_repurpose_a_non_filesystem_system_default()
    {
        string workspaceRoot = TestFileSystem.CreateTemporaryRoot("storage-catalog-protected-default");
        try
        {
            DbContextOptions<AppDbContext> options = AppDbContextTestOptionsBuilder.Create()
                .UseInMemoryDatabase($"storage-catalog-protected-default-{Guid.NewGuid():N}")
                .Options;
            Guid legacyDefaultId = Guid.NewGuid();
            await using (var seed = new AppDbContext(options))
            {
                seed.Add(new StorageCatalogRecord
                {
                    Id = legacyDefaultId,
                    Name = "Legacy remote default",
                    ProviderKind = StorageProviderKind.Ftp,
                    IsSystemDefault = true,
                    IsEnabled = true,
                    EndpointOrRoot = "ftp://storage.example.test/archive",
                    ConfigJson = "{\"mode\":\"legacy\"}",
                    CreatedAtUtc = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero),
                    UpdatedAtUtc = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero)
                });
                await seed.SaveChangesAsync();
            }

            var sut = new StorageCatalogService(
                new TestDbContextFactory(options),
                new TestWorkspacePathResolver(workspaceRoot),
                new TestClock(new DateTimeOffset(2026, 8, 9, 12, 0, 0, TimeSpan.Zero)));

            InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                sut.EnsureBootstrapFileSystemStorageAsync());

            Assert.Contains("left unchanged", exception.Message, StringComparison.OrdinalIgnoreCase);
            await using var assertContext = new AppDbContext(options);
            StorageCatalogRecord preserved = Assert.Single(
                await assertContext.Set<StorageCatalogRecord>().AsNoTracking().ToListAsync());
            Assert.Equal(legacyDefaultId, preserved.Id);
            Assert.Equal("Legacy remote default", preserved.Name);
            Assert.Equal(StorageProviderKind.Ftp, preserved.ProviderKind);
            Assert.Equal("ftp://storage.example.test/archive", preserved.EndpointOrRoot);
            Assert.Equal("{\"mode\":\"legacy\"}", preserved.ConfigJson);
            Assert.Empty(await assertContext.Set<StorageRoutingRule>().AsNoTracking().ToListAsync());
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(workspaceRoot);
        }
    }

    [Fact]
    public async Task Bootstrap_rejects_ambiguous_system_defaults_without_mutating_them()
    {
        string workspaceRoot = TestFileSystem.CreateTemporaryRoot("storage-catalog-ambiguous-defaults");
        try
        {
            DbContextOptions<AppDbContext> options = AppDbContextTestOptionsBuilder.Create()
                .UseInMemoryDatabase($"storage-catalog-ambiguous-defaults-{Guid.NewGuid():N}")
                .Options;
            StorageCatalogRecord first = CreateBoundBootstrapStorage(workspaceRoot, "First default");
            StorageCatalogRecord second = CreateBoundBootstrapStorage(workspaceRoot, "Second default");
            await using (var seed = new AppDbContext(options))
            {
                seed.AddRange(first, second);
                await seed.SaveChangesAsync();
            }

            var sut = new StorageCatalogService(
                new TestDbContextFactory(options),
                new TestWorkspacePathResolver(workspaceRoot),
                new TestClock(new DateTimeOffset(2026, 8, 9, 12, 0, 0, TimeSpan.Zero)));

            InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                sut.EnsureBootstrapFileSystemStorageAsync());

            Assert.Contains("multiple", exception.Message, StringComparison.OrdinalIgnoreCase);
            await using var assertContext = new AppDbContext(options);
            List<StorageCatalogRecord> preserved = await assertContext.Set<StorageCatalogRecord>()
                .AsNoTracking()
                .OrderBy(item => item.Name)
                .ToListAsync();
            Assert.Equal(["First default", "Second default"], preserved.Select(item => item.Name));
            Assert.All(preserved, item => Assert.True(item.IsSystemDefault));
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(workspaceRoot);
        }
    }

    [Fact]
    public async Task Legacy_bootstrap_requires_explicit_current_workspace_rebind()
    {
        string workspaceRoot = TestFileSystem.CreateTemporaryRoot("storage-catalog-bootstrap-rebind");
        try
        {
            DbContextOptions<AppDbContext> options = AppDbContextTestOptionsBuilder.Create()
                .UseInMemoryDatabase($"storage-catalog-bootstrap-rebind-{Guid.NewGuid():N}")
                .Options;
            Guid storageId = Guid.NewGuid();
            await using (var seed = new AppDbContext(options))
            {
                seed.Add(new StorageCatalogRecord
                {
                    Id = storageId,
                    Name = "Workspace file system",
                    ProviderKind = StorageProviderKind.FileSystem,
                    IsSystemDefault = true,
                    IsEnabled = true,
                    EndpointOrRoot = workspaceRoot
                });
                await seed.SaveChangesAsync();
            }

            var sut = new StorageCatalogService(
                new TestDbContextFactory(options),
                new TestWorkspacePathResolver(workspaceRoot),
                new TestClock(new DateTimeOffset(2026, 8, 9, 12, 0, 0, TimeSpan.Zero)));

            InvalidOperationException unresolved = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                sut.EnsureBootstrapFileSystemStorageAsync());
            Assert.Contains("not authoritative", unresolved.Message, StringComparison.OrdinalIgnoreCase);

            StorageCatalogRecord rebound = await sut.RebindRootAsync(storageId, workspaceRoot);
            StorageCatalogRecord bootstrap = await sut.EnsureBootstrapFileSystemStorageAsync();

            Assert.Equal(HostBoundPathState.Active, rebound.RootPathState);
            Assert.Equal(storageId, bootstrap.Id);
            Assert.Equal(Path.GetFullPath(workspaceRoot), bootstrap.EndpointOrRoot);
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(workspaceRoot);
        }
    }

    [Fact]
    [Trait("RequiresHostDocker", "true")]
    public async Task EnsureBootstrapFileSystemStorageAsync_recovers_from_concurrent_first_use()
    {
        AppDbContextModelRegistry.ConfigureAssemblies(TestApplicationBootstrap.ModuleAssemblies);
        var workspaceRoot = TestFileSystem.CreateTemporaryRoot("storage-catalog");
        await using var database = PostgresTestDatabaseLease.Create("storage-catalog-concurrent");

        try
        {
            var options = database.CreateAppDbContextOptions();
            await using (var dbContext = new AppDbContext(options))
            {
                await dbContext.Database.EnsureCreatedAsync();
            }

            var factory = new TestDbContextFactory(options);
            var resolver = new TestWorkspacePathResolver(workspaceRoot);
            var clock = new TestClock(new DateTimeOffset(2026, 4, 1, 12, 0, 0, TimeSpan.Zero));
            var tasks = Enumerable.Range(0, 12)
                .Select(_ => Task.Run(async () =>
                {
                    var service = new StorageCatalogService(factory, resolver, clock);
                    return await service.EnsureBootstrapFileSystemStorageAsync();
                }))
                .ToArray();

            var results = await Task.WhenAll(tasks);

            await using var assertContext = new AppDbContext(options);
            var storages = await assertContext.Set<StorageCatalogRecord>().ToListAsync();
            var rules = await assertContext.Set<StorageRoutingRule>().ToListAsync();
            var bootstrapStorage = Assert.Single(storages, item => item.IsSystemDefault);
            Assert.All(results, result => Assert.Equal(bootstrapStorage.Id, result.Id));
            Assert.Single(rules, item => item.PreferredStorageId == bootstrapStorage.Id);
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(workspaceRoot);
        }
    }

    [Fact]
    public async Task SaveAsync_InvalidBrowseCacheConfiguration_DoesNotPersistStorage()
    {
        var workspaceRoot = TestFileSystem.CreateTemporaryRoot("storage-catalog");

        try
        {
            string databaseName = $"storage-catalog-{Guid.NewGuid():N}";
            StorageCatalogService sut = CreateSut(databaseName, workspaceRoot);
            var record = new StorageCatalogRecord
            {
                Name = "Invalid cache",
                ProviderKind = StorageProviderKind.Ftp,
                ConfigJson = """
                    {
                      "browseCache": {
                        "enabled": true,
                        "mode": "disabled"
                      }
                    }
                    """
            };

            StorageBrowseException exception = await Assert.ThrowsAsync<StorageBrowseException>(() =>
                sut.SaveAsync(record));
            IReadOnlyList<StorageCatalogRecord> persisted = await sut.ListAsync();

            Assert.Equal(StorageBrowseErrorCode.InvalidConfiguration, exception.Error.Code);
            Assert.DoesNotContain(persisted, item => item.Id == record.Id);
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(workspaceRoot);
        }
    }

    [Fact]
    public async Task Root_binding_migration_dry_run_execute_and_rollback_are_transactional_and_redacted()
    {
        string workspaceRoot = TestFileSystem.CreateTemporaryRoot("storage-catalog-migration-workspace");
        string legacyRoot = TestFileSystem.CreateTemporaryRoot("storage-catalog-migration-legacy");
        try
        {
            string databaseName = $"storage-catalog-migration-{Guid.NewGuid():N}";
            DbContextOptions<AppDbContext> options = AppDbContextTestOptionsBuilder.Create()
                .UseInMemoryDatabase(databaseName)
                .Options;
            Guid storageId = Guid.NewGuid();
            await using (var seed = new AppDbContext(options))
            {
                seed.Add(new StorageCatalogRecord
                {
                    Id = storageId,
                    Name = "Legacy filesystem",
                    ProviderKind = StorageProviderKind.FileSystem,
                    EndpointOrRoot = legacyRoot,
                    CreatedAtUtc = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero),
                    UpdatedAtUtc = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero)
                });
                await seed.SaveChangesAsync();
            }

            var sut = new StorageCatalogService(
                new TestDbContextFactory(options),
                new TestWorkspacePathResolver(workspaceRoot),
                new TestClock(new DateTimeOffset(2026, 8, 9, 12, 0, 0, TimeSpan.Zero)));

            StorageCatalogPathMigrationReport dryRun = await sut.DryRunAsync();
            StorageCatalogPathMigrationReport committed = await sut.ExecuteAsync();
            string reportJson = System.Text.Json.JsonSerializer.Serialize(committed);
            await using var committedContext = new AppDbContext(options);
            StorageCatalogRecord migrated = await committedContext.Set<StorageCatalogRecord>()
                .SingleAsync(item => item.Id == storageId);
            string migrationRoot = Path.Combine(
                workspaceRoot,
                ".candoitall",
                "migrations",
                "storage-catalog-host-binding-v1");

            Assert.True(dryRun.IsDryRun);
            Assert.Equal(StorageCatalogPathMigrationState.Discovered, dryRun.State);
            Assert.Equal(StorageCatalogPathMigrationState.PointerCommitted, committed.State);
            Assert.Equal(HostBoundPathState.NeedsRebind, migrated.RootPathState);
            Assert.Empty(migrated.RootHostBindingId);
            Assert.False(migrated.IsEnabled);
            Assert.DoesNotContain(legacyRoot, reportJson, StringComparison.Ordinal);
            Assert.True(File.Exists(Path.Combine(migrationRoot, "storage-catalog.v1.backup.json")));
            Assert.True(File.Exists(Path.Combine(migrationRoot, "storage-catalog.v1.backup.json.integrity.json")));
            Assert.True(File.Exists(Path.Combine(migrationRoot, "storage-catalog.v2.staged.json")));
            Assert.True(File.Exists(Path.Combine(migrationRoot, "commit.json")));
            await committedContext.DisposeAsync();

            File.Delete(Path.Combine(migrationRoot, "commit.json"));
            StorageCatalogPathMigrationReport repaired = await sut.ExecuteAsync();
            Assert.Equal(StorageCatalogPathMigrationState.PointerCommitted, repaired.State);
            Assert.True(File.Exists(Path.Combine(migrationRoot, "commit.json")));

            File.Delete(Path.Combine(migrationRoot, "commit.json"));
            StorageCatalogPathMigrationReport rolledBack = await sut.RollbackAsync();
            await using var rolledBackContext = new AppDbContext(options);
            StorageCatalogRecord restored = await rolledBackContext.Set<StorageCatalogRecord>()
                .SingleAsync(item => item.Id == storageId);

            Assert.Equal(StorageCatalogPathMigrationState.RolledBack, rolledBack.State);
            Assert.Equal(legacyRoot, restored.EndpointOrRoot);
            Assert.Equal(0, restored.RootBindingFormatVersion);
            Assert.True(File.Exists(Path.Combine(migrationRoot, "rollback.commit.json")));
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(workspaceRoot);
            TestFileSystem.DeleteDirectoryWithRetry(legacyRoot);
        }
    }

    [Fact]
    public async Task Root_binding_rollback_rejects_a_modified_backup()
    {
        string workspaceRoot = TestFileSystem.CreateTemporaryRoot("storage-catalog-checksum-workspace");
        string legacyRoot = TestFileSystem.CreateTemporaryRoot("storage-catalog-checksum-legacy");
        try
        {
            DbContextOptions<AppDbContext> options = AppDbContextTestOptionsBuilder.Create()
                .UseInMemoryDatabase($"storage-catalog-checksum-{Guid.NewGuid():N}")
                .Options;
            await using (var seed = new AppDbContext(options))
            {
                seed.Add(new StorageCatalogRecord
                {
                    Id = Guid.NewGuid(),
                    Name = "Legacy filesystem",
                    ProviderKind = StorageProviderKind.FileSystem,
                    EndpointOrRoot = legacyRoot
                });
                await seed.SaveChangesAsync();
            }

            var sut = new StorageCatalogService(
                new TestDbContextFactory(options),
                new TestWorkspacePathResolver(workspaceRoot),
                new TestClock(new DateTimeOffset(2026, 8, 9, 12, 0, 0, TimeSpan.Zero)));
            await sut.ExecuteAsync();
            string backupPath = Path.Combine(
                workspaceRoot,
                ".candoitall",
                "migrations",
                "storage-catalog-host-binding-v1",
                "storage-catalog.v1.backup.json");
            File.Delete(Path.Combine(Path.GetDirectoryName(backupPath)!, "commit.json"));
            await File.AppendAllTextAsync(backupPath, Environment.NewLine);

            InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => sut.RollbackAsync());

            Assert.Contains("checksum", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(legacyRoot, exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(workspaceRoot);
            TestFileSystem.DeleteDirectoryWithRetry(legacyRoot);
        }
    }

    [Fact]
    public async Task Foreign_storage_root_is_disabled_without_reinterpreting_its_path()
    {
        string workspaceRoot = TestFileSystem.CreateTemporaryRoot("storage-catalog-foreign-workspace");
        try
        {
            DbContextOptions<AppDbContext> options = AppDbContextTestOptionsBuilder.Create()
                .UseInMemoryDatabase($"storage-catalog-foreign-{Guid.NewGuid():N}")
                .Options;
            string foreignRoot = OperatingSystem.IsWindows()
                ? "/foreign/storage/root"
                : @"C:\foreign\storage\root";
            Guid storageId = Guid.NewGuid();
            await using (var seed = new AppDbContext(options))
            {
                seed.Add(new StorageCatalogRecord
                {
                    Id = storageId,
                    Name = "Foreign filesystem",
                    ProviderKind = StorageProviderKind.FileSystem,
                    EndpointOrRoot = foreignRoot,
                    IsEnabled = true
                });
                await seed.SaveChangesAsync();
            }

            var sut = new StorageCatalogService(
                new TestDbContextFactory(options),
                new TestWorkspacePathResolver(workspaceRoot),
                new TestClock(new DateTimeOffset(2026, 8, 9, 12, 0, 0, TimeSpan.Zero)));
            await sut.ExecuteAsync();
            await using var assertContext = new AppDbContext(options);
            StorageCatalogRecord migrated = await assertContext.Set<StorageCatalogRecord>()
                .SingleAsync(item => item.Id == storageId);

            bool resolved = StorageCatalogHostBindingPolicy.TryResolve(
                migrated,
                workspaceRoot,
                out _,
                out string diagnostic);

            Assert.False(migrated.IsEnabled);
            Assert.Equal(StorageHealthStatus.Unavailable, migrated.HealthStatus);
            Assert.Equal(HostBoundPathState.NeedsRebind, migrated.RootPathState);
            Assert.False(resolved);
            Assert.Contains("rebind", diagnostic, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(foreignRoot, diagnostic, StringComparison.Ordinal);
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(workspaceRoot);
        }
    }

    private static StorageCatalogService CreateSut(string databaseName, string workspaceRoot)
    {
        var options = AppDbContextTestOptionsBuilder.Create()
            .UseInMemoryDatabase(databaseName)
            .Options;

        return new StorageCatalogService(
            new TestDbContextFactory(options),
            new TestWorkspacePathResolver(workspaceRoot),
            new TestClock(new DateTimeOffset(2026, 4, 1, 12, 0, 0, TimeSpan.Zero)));
    }

    private static StorageCatalogRecord CreateBoundBootstrapStorage(string workspaceRoot, string name)
    {
        var storage = new StorageCatalogRecord
        {
            Name = name,
            ProviderKind = StorageProviderKind.FileSystem,
            IsSystemDefault = true,
            IsEnabled = true,
            EndpointOrRoot = workspaceRoot
        };
        StorageCatalogHostBindingPolicy.BindCurrent(storage, workspaceRoot, DateTimeOffset.UtcNow);
        return storage;
    }

    private sealed class TestDbContextFactory(DbContextOptions<AppDbContext> options) : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext()
            => new(options);

        public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new AppDbContext(options));
    }

    private sealed class TestWorkspacePathResolver(string workspaceRoot) : IWorkspacePathResolver
    {
        public string ResolveWorkspaceRoot() => workspaceRoot;

        public string ResolveManagedFilesRoot() => Path.Combine(workspaceRoot, "managed-files");

        public string ResolveExportsRoot() => Path.Combine(workspaceRoot, "exports");

        public string ResolveEvidenceRoot() => Path.Combine(workspaceRoot, "evidence");

        public string ResolveManagerArtifactsRoot() => Path.Combine(workspaceRoot, ".artifacts");
    }

    private sealed class TestClock(DateTimeOffset currentUtc) : IClock
    {
        public DateTimeOffset GetUtcNow() => currentUtc;
    }
}
