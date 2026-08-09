using CanDoItAll.FileTools.Integration;
using CanDoItAll.Infrastructure.Storage;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Unit;

public sealed class StoragePlacementServiceTests
{
    [Fact]
    public async Task PlaceAsync_uses_the_routed_file_system_storage_and_returns_a_preview_route()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), $"storage-placement-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspaceRoot);

        try
        {
            var storage = new StorageCatalogRecord
            {
                Id = Guid.NewGuid(),
                Name = "Workspace files",
                ProviderKind = StorageProviderKind.FileSystem,
                EndpointOrRoot = workspaceRoot,
                CapabilityMask = StorageCapability.Read |
                                 StorageCapability.Write |
                                 StorageCapability.Download |
                                 StorageCapability.InlinePreview |
                                 StorageCapability.OpenLocally,
                HealthStatus = StorageHealthStatus.Healthy,
                IsEnabled = true
            };
            StorageCatalogHostBindingPolicy.BindCurrent(storage, workspaceRoot, DateTimeOffset.UtcNow);
            var revisions = new ProcessLocalFileCatalogRevisionService();
            var sut = new RevisionPublishingStoragePlacementService(new StoragePlacementService(
                new TestStorageCatalogService(storage),
                new TestStorageRoutingService(storage.Id, storage.Name, storage.ProviderKind, storage.CapabilityMask),
                new TestStorageDriverRegistry(new FileSystemStorageDriver(
                    new FileSystemStoragePathPolicy(new TestWorkspacePathResolver(workspaceRoot)))),
                NullLogger<StoragePlacementService>.Instance),
                revisions);

            var result = await sut.PlaceAsync(
                new StoragePlacementRequest(
                    "alpha.txt",
                    "text/plain",
                    "alpha"u8.ToArray(),
                    StorageUsagePurpose.ProjectAsset,
                    StorageContentKind.Text,
                    RelativePathHint: "managed-files/project-media/files/alpha.txt",
                    PreviewRequired: true));

            Assert.Equal(storage.Id, result.Storage.Id);
            Assert.Equal("managed-files/project-media/files/alpha.txt", result.RelativePath);
            Assert.StartsWith("/storage/objects/preview?ref=", result.Route, StringComparison.Ordinal);
            Assert.True(File.Exists(result.Location));
            Assert.Equal("alpha", await File.ReadAllTextAsync(result.Location));
            Assert.Equal(
                new FileCatalogRevision(1, 0),
                revisions.Get(CreateProjectScope(), storage.Id));
        }
        finally
        {
            Directory.Delete(workspaceRoot, recursive: true);
        }
    }

    [Fact]
    public async Task PlaceAsync_rejects_explicit_storage_overrides_that_cannot_preview_required_content()
    {
        var storage = new StorageCatalogRecord
        {
            Id = Guid.NewGuid(),
            Name = "Download-only FTP",
            ProviderKind = StorageProviderKind.Ftp,
            EndpointOrRoot = "ftp://ftp.example.test",
            CapabilityMask = StorageCapability.Read | StorageCapability.Write | StorageCapability.Download,
            HealthStatus = StorageHealthStatus.Healthy,
            IsEnabled = true
        };
        var revisions = new ProcessLocalFileCatalogRevisionService();
        var sut = new RevisionPublishingStoragePlacementService(new StoragePlacementService(
            new TestStorageCatalogService(storage),
            new TestStorageRoutingService(storage.Id, storage.Name, storage.ProviderKind, storage.CapabilityMask),
            new TestStorageDriverRegistry(new TestStorageDriver(storage.ProviderKind, storage.CapabilityMask)),
            NullLogger<StoragePlacementService>.Instance),
            revisions);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.PlaceAsync(
            new StoragePlacementRequest(
                "proof.pdf",
                "application/pdf",
                "%PDF-1.4"u8.ToArray(),
                StorageUsagePurpose.Evidence,
                StorageContentKind.Pdf,
                PreviewRequired: true,
                PreferredStorageId: storage.Id)));

        Assert.Contains("required capabilities", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(new FileCatalogRevision(0, 0), revisions.Get(CreateProjectScope(), storage.Id));
    }

    private static FileToolsSemanticScope CreateProjectScope()
        => new(
            FileToolsSemanticScopeKind.Project,
            new FileToolsSemanticScopeId("placement-project"),
            "Placement project");

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
            => Task.FromResult<IReadOnlyList<StorageRoutingRule>>([]);

        public Task<StorageRoutingRule> SaveRuleAsync(StorageRoutingRule rule, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class TestStorageRoutingService(
        Guid storageId,
        string storageName,
        StorageProviderKind providerKind,
        StorageCapability capabilityMask) : IStorageRoutingService
    {
        public Task<StorageRecommendation> RecommendAsync(StorageSelectionContext context, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new StorageRecommendation(
                new StorageRecommendationCandidate(
                    storageId,
                    storageName,
                    providerKind,
                    capabilityMask,
                    StorageHealthStatus.Healthy,
                    false,
                    "Unit-test route."),
                [],
                "Unit-test route.",
                []));
        }
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
        {
            var reference = new StorageObjectReference(
                storage.Id,
                storage.ProviderKind,
                StorageLocatorKind.RemotePath,
                request.RelativePathHint ?? request.FileName,
                request.FileName,
                request.ContentType,
                request.Content.LongLength);
            return Task.FromResult(new StorageWriteResult(
                reference,
                new StorageAccessDescriptor(
                    string.Empty,
                    "/storage/objects/download?ref=test",
                    null,
                    false,
                    true,
                    false,
                    request.FileName,
                    request.ContentType,
                    request.Content.LongLength,
                    string.Empty)));
        }

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
