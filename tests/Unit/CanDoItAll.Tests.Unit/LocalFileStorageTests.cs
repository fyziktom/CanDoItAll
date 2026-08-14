using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Unit;

public sealed class LocalFileStorageTests
{
    [Fact]
    public async Task SaveTextAsync_writes_and_reads_inside_the_active_workspace_root()
    {
        var workspaceRoot = TestFileSystem.CreateTemporaryRoot("local-file-store");

        try
        {
            var sut = CreateSut(workspaceRoot);
            var relativePath = Path.Combine("managed-files", "proof", "alpha.txt");

            var fullPath = await sut.SaveTextAsync(relativePath, "alpha");
            var restoredContent = await sut.ReadTextAsync(relativePath);

            Assert.Equal(Path.Combine(workspaceRoot, "managed-files", "proof", "alpha.txt"), fullPath);
            Assert.Equal("alpha", restoredContent);
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(workspaceRoot);
        }
    }

    [Fact]
    public async Task SaveTextAsync_rejects_paths_outside_the_active_workspace_root()
    {
        var workspaceRoot = TestFileSystem.CreateTemporaryRoot("local-file-store");

        try
        {
            var sut = CreateSut(workspaceRoot);

            var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
                sut.SaveTextAsync(Path.Combine("..", "outside.txt"), "alpha"));

            Assert.Contains("logical path", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(workspaceRoot);
        }
    }

    private static LocalFileStore CreateSut(string workspaceRoot)
    {
        var workspacePathResolver = new TestWorkspacePathResolver(workspaceRoot);
        return new LocalFileStore(
            new WorkspacePathAccessGuard(
                workspacePathResolver,
                TestWorkspaceServices.PhysicalPathPolicyFactory),
            new TestStorageCatalogService(workspaceRoot),
            new StorageDriverRegistry([
                new FileSystemStorageDriver(new FileSystemStoragePathPolicy(workspacePathResolver))
            ]));
    }

    private sealed class TestWorkspacePathResolver(string workspaceRoot) : IWorkspacePathResolver
    {
        public string ResolveWorkspaceRoot() => workspaceRoot;

        public string ResolveManagedFilesRoot() => Path.Combine(workspaceRoot, "managed-files");

        public string ResolveExportsRoot() => Path.Combine(workspaceRoot, "exports");

        public string ResolveEvidenceRoot() => Path.Combine(workspaceRoot, "evidence");

        public string ResolveManagerArtifactsRoot() => Path.Combine(workspaceRoot, ".artifacts");
    }

    private sealed class TestStorageCatalogService(string workspaceRoot) : IStorageCatalogService
    {
        private readonly StorageCatalogRecord _storage = CreateStorage(workspaceRoot);

        private static StorageCatalogRecord CreateStorage(string workspaceRoot)
        {
            var storage = new StorageCatalogRecord
            {
                EndpointOrRoot = workspaceRoot,
                IsSystemDefault = true,
                ProviderKind = StorageProviderKind.FileSystem,
                CapabilityMask =
                    StorageCapability.Read |
                    StorageCapability.Write |
                    StorageCapability.Delete |
                    StorageCapability.Download |
                    StorageCapability.InlinePreview |
                    StorageCapability.OpenLocally |
                    StorageCapability.MutableUpdate |
                    StorageCapability.BatchFolderUpload |
                    StorageCapability.BatchTransfer |
                    StorageCapability.ConnectionTest
            };
            StorageCatalogHostBindingPolicy.BindCurrent(storage, workspaceRoot, DateTimeOffset.UtcNow);
            return storage;
        }

        public Task<IReadOnlyList<StorageCatalogRecord>> ListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<StorageCatalogRecord>>([_storage]);

        public Task<StorageCatalogRecord?> GetAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(_storage.Id == id ? _storage : null);

        public Task<StorageCatalogRecord> EnsureBootstrapFileSystemStorageAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_storage);

        public Task<StorageCatalogRecord> SaveAsync(StorageCatalogRecord record, CancellationToken cancellationToken = default)
            => Task.FromResult(record);

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<StorageRoutingRule>> ListRulesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<StorageRoutingRule>>([]);

        public Task<StorageRoutingRule> SaveRuleAsync(StorageRoutingRule rule, CancellationToken cancellationToken = default)
            => Task.FromResult(rule);
    }
}
