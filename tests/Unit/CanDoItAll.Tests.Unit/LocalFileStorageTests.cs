using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Unit.Storage;

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

        private Task<IReadOnlyList<StorageCatalogRecord>> ReadRecordsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<StorageCatalogRecord>>([_storage]);

        private Task<StorageCatalogRecord?> ReadRecordAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(_storage.Id == id ? _storage : null);

        private Task<StorageCatalogRecord> ReadBootstrapRecordAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_storage);

        private Task<StorageCatalogRecord> SaveRecordAsync(StorageCatalogRecord record, CancellationToken cancellationToken = default)
            => Task.FromResult(record);

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        internal Task<IReadOnlyList<StorageRoutingRule>> ReadRoutingRecordsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<StorageRoutingRule>>([]);

        private Task<StorageRoutingRule> SaveRoutingRecordAsync(StorageRoutingRule rule, CancellationToken cancellationToken = default)
            => Task.FromResult(rule);
        public async Task<IReadOnlyList<StorageCatalogSnapshot>> ListAsync(CancellationToken cancellationToken = default) =>
            (await ReadRecordsAsync(cancellationToken)).Select(StorageCatalogMapping.ToSnapshot).ToArray();

        public async Task<StorageCatalogSnapshot?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
            (await ReadRecordAsync(id, cancellationToken))?.ToSnapshot();

        public async Task<StorageDriverInput?> GetDriverAsync(Guid id, CancellationToken cancellationToken = default) =>
            (await ReadRecordAsync(id, cancellationToken))?.ToDriverInput();

        public async Task<StorageCatalogEditorSnapshot?> GetEditorAsync(Guid id, CancellationToken cancellationToken = default) {
            var row = await ReadRecordAsync(id, cancellationToken);
            return row is null ? null : new(row.ToSnapshot(), StorageJson.ParseProviderConfiguration(row.ConfigJson));
        }

        public async Task<StorageDriverInput> EnsureBootstrapFileSystemStorageAsync(CancellationToken cancellationToken = default) =>
            (await ReadBootstrapRecordAsync(cancellationToken)).ToDriverInput();

        public async Task<StorageCatalogSnapshot> SaveAsync(StorageCatalogSaveRequest request, CancellationToken cancellationToken = default) =>
            (await SaveRecordAsync(StorageCatalogMapping.CreateDraft(request), cancellationToken)).ToSnapshot();

        public async Task<IReadOnlyList<StorageRoutingRuleSnapshot>> ListRulesAsync(CancellationToken cancellationToken = default) =>
            (await ReadRoutingRecordsAsync(cancellationToken)).Select(StorageCatalogMapping.ToSnapshot).ToArray();

        public async Task<StorageRoutingRuleSnapshot> SaveRuleAsync(StorageRoutingRuleSaveRequest request, CancellationToken cancellationToken = default) =>
            (await SaveRoutingRecordAsync(StorageCatalogMapping.CreateDraft(request), cancellationToken)).ToSnapshot();

        public Task ApplyDefaultPurposesAsync(Guid storageId, IReadOnlyCollection<StorageUsagePurpose> defaultPurposes,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

    }
}
