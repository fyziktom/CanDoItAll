using Microsoft.Extensions.Logging.Abstractions;
using CanDoItAll.Infrastructure.Storage;

namespace CanDoItAll.Tests.Unit;

public sealed class StorageTransferPipelineTests
{
    [Fact]
    public async Task ExecuteAsync_transfers_files_reports_progress_and_verifies_content()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), $"storage-transfer-{Guid.NewGuid():N}");
        var sourceRoot = Path.Combine(workspaceRoot, "source");
        var targetRoot = Path.Combine(workspaceRoot, "target");
        Directory.CreateDirectory(Path.Combine(sourceRoot, "proof"));
        await File.WriteAllTextAsync(Path.Combine(sourceRoot, "proof", "alpha.txt"), "alpha-transfer");

        try
        {
            var pathResolver = new TestWorkspacePathResolver(workspaceRoot);
            var driver = new FileSystemStorageDriver(new FileSystemStoragePathPolicy(pathResolver));
            var pipeline = new StorageTransferPipeline(
                new TestStorageCatalogService(),
                new TestStorageDriverRegistry(driver),
                new NullStorageSecretResolver(),
                NullLogger<StorageTransferPipeline>.Instance);
            var progressUpdates = new List<StorageTransferProgress>();

            var result = await pipeline.ExecuteAsync(new StorageTransferManifest(
                null,
                null,
                [
                    new StorageTransferItem(
                        "proof/alpha.txt",
                        "imports/alpha.txt",
                        "text/plain",
                        StorageUsagePurpose.ProjectAsset,
                        StorageContentKind.Text)
                ],
                CreateStorage(sourceRoot, canWrite: false),
                CreateStorage(targetRoot, canWrite: true),
                new StorageTransferOptions(
                    VerifyTargetContent: true,
                    ProgressCallback: (progress, _) =>
                    {
                        progressUpdates.Add(progress);
                        return ValueTask.CompletedTask;
                    })));

            Assert.Equal(1, result.TotalCount);
            Assert.Equal(1, result.SuccessCount);
            Assert.Equal(0, result.FailureCount);
            Assert.Single(progressUpdates);
            Assert.Equal(1, progressUpdates[0].CompletedCount);
            Assert.Equal(1, progressUpdates[0].SuccessCount);
            Assert.Equal("alpha-transfer", await File.ReadAllTextAsync(Path.Combine(targetRoot, "imports", "alpha.txt")));
        }
        finally
        {
            Directory.Delete(workspaceRoot, recursive: true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_short_circuits_when_batch_transfer_capability_is_missing()
    {
        var pipeline = new StorageTransferPipeline(
            new TestStorageCatalogService(),
            new TestStorageDriverRegistry(
                new TestStorageDriver(
                    StorageProviderKind.FileSystem,
                    StorageCapability.Read | StorageCapability.BatchTransfer),
                new TestStorageDriver(
                    StorageProviderKind.Ftp,
                    StorageCapability.Write)),
            new NullStorageSecretResolver(),
            NullLogger<StorageTransferPipeline>.Instance);

        var result = await pipeline.ExecuteAsync(new StorageTransferManifest(
            null,
            null,
            [
                new StorageTransferItem(
                    "proof/alpha.txt",
                    "imports/alpha.txt",
                    "text/plain",
                    StorageUsagePurpose.ProjectAsset)
            ],
            new StorageCatalogRecord
            {
                Id = Guid.NewGuid(),
                Name = "Source",
                ProviderKind = StorageProviderKind.FileSystem,
                EndpointOrRoot = "source",
                CapabilityMask = StorageCapability.Read | StorageCapability.BatchTransfer
            },
            new StorageCatalogRecord
            {
                Id = Guid.NewGuid(),
                Name = "Target",
                ProviderKind = StorageProviderKind.Ftp,
                EndpointOrRoot = "target",
                CapabilityMask = StorageCapability.Write
            }));

        Assert.Equal(1, result.TotalCount);
        Assert.Equal(0, result.SuccessCount);
        Assert.Equal(1, result.FailureCount);
        Assert.Contains("not flagged for batch transfer", result.Items[0].Message, StringComparison.OrdinalIgnoreCase);
    }

    private static StorageCatalogRecord CreateStorage(string rootPath, bool canWrite)
    {
        var capabilityMask = StorageCapability.Read |
                             StorageCapability.Download |
                             StorageCapability.BatchTransfer;
        if (canWrite)
        {
            capabilityMask |= StorageCapability.Write |
                              StorageCapability.Delete |
                              StorageCapability.MutableUpdate |
                              StorageCapability.BatchFolderUpload;
        }

        return new StorageCatalogRecord
        {
            Id = Guid.NewGuid(),
            Name = canWrite ? "Target" : "Source",
            ProviderKind = StorageProviderKind.FileSystem,
            EndpointOrRoot = rootPath,
            CapabilityMask = capabilityMask
        };
    }

    private sealed class TestStorageCatalogService : IStorageCatalogService
    {
        public Task<IReadOnlyList<StorageCatalogRecord>> ListAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<StorageCatalogRecord?> GetAsync(Guid id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

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

    private sealed class NullStorageSecretResolver : IStorageSecretResolver
    {
        public Task<string?> ResolveCredentialAsync(Guid? secretId, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(null);
    }
}
