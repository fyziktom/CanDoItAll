using CanDoItAll.Infrastructure.Storage;

namespace CanDoItAll.Tests.Unit;

public sealed class StorageAccessServiceTests
{
    [Fact]
    public async Task DescribeAsync_enables_local_open_for_workspace_file_system_storage()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), $"storage-access-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspaceRoot);

        try
        {
            var storage = new StorageCatalogRecord
            {
                Id = Guid.NewGuid(),
                Name = "Workspace files",
                ProviderKind = StorageProviderKind.FileSystem,
                EndpointOrRoot = Path.Combine(workspaceRoot, "managed-files"),
                CapabilityMask = StorageCapability.Read |
                                 StorageCapability.Download |
                                 StorageCapability.InlinePreview |
                                 StorageCapability.OpenLocally
            };
            StorageCatalogHostBindingPolicy.BindCurrent(
                storage,
                storage.EndpointOrRoot,
                DateTimeOffset.UtcNow);
            var sut = new StorageAccessService(
                new TestStorageCatalogService(storage),
                new TestStorageDriverRegistry(new TestStorageDriver(
                    StorageProviderKind.FileSystem,
                    storage.CapabilityMask)),
                new FileSystemStoragePathPolicy(new TestWorkspacePathResolver(workspaceRoot)));

            var descriptor = await sut.DescribeAsync(new StorageObjectReference(
                storage.Id,
                StorageProviderKind.FileSystem,
                StorageLocatorKind.RelativePath,
                "managed-files/proof/alpha.txt",
                "alpha.txt",
                "text/plain",
                13));

            Assert.True(descriptor.SupportsInlinePreview);
            Assert.True(descriptor.SupportsDownload);
            Assert.True(descriptor.SupportsOpenLocally);
            Assert.StartsWith("/storage/objects/preview?ref=", descriptor.PreviewUrl, StringComparison.Ordinal);
            Assert.StartsWith("/storage/objects/download?ref=", descriptor.DownloadUrl, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(workspaceRoot, recursive: true);
        }
    }

    [Fact]
    public async Task DescribeAsync_keeps_ftp_objects_download_only()
    {
        var storage = new StorageCatalogRecord
        {
            Id = Guid.NewGuid(),
            Name = "FTP mirror",
            ProviderKind = StorageProviderKind.Ftp,
            EndpointOrRoot = "ftp://ftp.example.test",
            CapabilityMask = StorageCapability.Read | StorageCapability.Download | StorageCapability.BatchTransfer
        };
        var sut = new StorageAccessService(
            new TestStorageCatalogService(storage),
            new TestStorageDriverRegistry(new TestStorageDriver(
                StorageProviderKind.Ftp,
                storage.CapabilityMask)),
            new FileSystemStoragePathPolicy(new TestWorkspacePathResolver(Path.GetTempPath())));

        var descriptor = await sut.DescribeAsync(new StorageObjectReference(
            storage.Id,
            StorageProviderKind.Ftp,
            StorageLocatorKind.RemotePath,
            "exports/release.zip",
            "release.zip",
            "application/zip",
            512));

        Assert.False(descriptor.SupportsInlinePreview);
        Assert.True(descriptor.SupportsDownload);
        Assert.False(descriptor.SupportsOpenLocally);
        Assert.Equal(string.Empty, descriptor.PreviewUrl);
        Assert.StartsWith("/storage/objects/download?ref=", descriptor.DownloadUrl, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DescribeAsync_uses_absolute_route_as_the_direct_ipfs_url()
    {
        var sut = new StorageAccessService(
            new TestStorageCatalogService(),
            new TestStorageDriverRegistry(),
            new FileSystemStoragePathPolicy(new TestWorkspacePathResolver(Path.GetTempPath())));
        var reference = new StorageObjectReference(
            null,
            StorageProviderKind.Ipfs,
            StorageLocatorKind.ContentAddress,
            "bafy-test",
            "proof.txt",
            "text/plain",
            9,
            "https://gateway.example.test/ipfs/bafy-test");

        var descriptor = await sut.DescribeAsync(reference);

        Assert.True(descriptor.SupportsInlinePreview);
        Assert.True(descriptor.SupportsDownload);
        Assert.False(descriptor.SupportsOpenLocally);
        Assert.Equal("https://gateway.example.test/ipfs/bafy-test", descriptor.DirectUrl);
    }

    private sealed class TestStorageCatalogService(params StorageCatalogRecord[] storages) : IStorageCatalogService
    {
        private readonly Dictionary<Guid, StorageCatalogRecord> storageById = storages.ToDictionary(storage => storage.Id);

        public Task<IReadOnlyList<StorageCatalogRecord>> ListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<StorageCatalogRecord>>(storageById.Values.ToList());

        public Task<StorageCatalogRecord?> GetAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(storageById.TryGetValue(id, out var storage) ? storage : null);

        public Task<StorageCatalogRecord> EnsureBootstrapFileSystemStorageAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<StorageCatalogRecord> SaveAsync(StorageCatalogRecord record, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<StorageRoutingRule>> ListRulesAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<StorageRoutingRule> SaveRuleAsync(StorageRoutingRule rule, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class TestStorageDriverRegistry(params IStorageDriver[] drivers) : IStorageDriverRegistry
    {
        private readonly Dictionary<StorageProviderKind, IStorageDriver> driversByKind = drivers.ToDictionary(driver => driver.ProviderKind);

        public IReadOnlyCollection<StorageProviderKind> RegisteredKinds => driversByKind.Keys.ToArray();

        public bool TryResolve(StorageProviderKind providerKind, out IStorageDriver driver)
            => driversByKind.TryGetValue(providerKind, out driver!);

        public IStorageDriver Resolve(StorageProviderKind providerKind)
            => driversByKind[providerKind];
    }

    private sealed class TestStorageDriver(StorageProviderKind providerKind, StorageCapability supportedCapabilities) : IStorageDriver
    {
        public StorageProviderKind ProviderKind => providerKind;

        public StorageCapability SupportedCapabilities => supportedCapabilities;

        public Task<StorageConnectionTestResult> TestConnectionAsync(
            StorageCatalogRecord storage,
            string? secretValue,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<StorageWriteResult> SaveAsync(
            StorageCatalogRecord storage,
            StorageWriteRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<Stream> OpenReadAsync(
            StorageCatalogRecord storage,
            StorageObjectReference reference,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task DeleteAsync(
            StorageCatalogRecord storage,
            StorageObjectReference reference,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class TestWorkspacePathResolver(string workspaceRoot) : IWorkspacePathResolver
    {
        public string ResolveWorkspaceRoot() => workspaceRoot;

        public string ResolveManagedFilesRoot() => Path.Combine(workspaceRoot, "managed-files");

        public string ResolveExportsRoot() => Path.Combine(workspaceRoot, "exports");

        public string ResolveEvidenceRoot() => Path.Combine(workspaceRoot, "evidence");

        public string ResolveManagerArtifactsRoot() => Path.Combine(workspaceRoot, "manager-artifacts");
    }
}
