using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Tests.Unit;

[Collection(AppDbContextModelRegistryTestCollectionNames.Name)]
public sealed class StorageCatalogServiceTests
{
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
