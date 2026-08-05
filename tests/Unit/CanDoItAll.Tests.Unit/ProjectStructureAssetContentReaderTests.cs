using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectStructureAssetContentReaderTests
{
    private const string ManagedPath = "managed-files/project-media/images/asset.png";

    [Theory]
    [InlineData(StorageProviderKind.FileSystem)]
    [InlineData(StorageProviderKind.Ftp)]
    [InlineData(StorageProviderKind.Ipfs)]
    public async Task ReadAsync_uses_the_exact_bound_driver_for_supported_providers(
        StorageProviderKind providerKind)
    {
        string workspaceRoot = TestFileSystem.CreateTemporaryRoot("asset-reader-providers");
        try
        {
            byte[] expected = "provider-bound-content"u8.ToArray();
            ProjectManagedStoragePhysicalIdentityPolicy identityPolicy = CreateIdentityPolicy(workspaceRoot);
            StorageCatalogRecord storage = CreateStorage(providerKind, workspaceRoot);
            StorageObjectReference reference = CreateV2Reference(
                providerKind,
                storage,
                identityPolicy,
                expected.LongLength);
            var driver = new RecordingStorageDriver(
                providerKind,
                () => new MemoryStream(expected, writable: false));
            ProjectStructureAssetContentReader reader = CreateReader(
                workspaceRoot,
                new StaticStorageCatalog(storage),
                driver,
                identityPolicy);
            var (node, asset) = CreateAsset(reference);

            byte[] actual = await reader.ReadAsync(node, asset);

            Assert.Equal(expected, actual);
            Assert.Equal(1, driver.OpenCount);
            Assert.Equal(storage.Id, driver.LastStorage?.Id);
            Assert.Equal(reference, driver.LastReference);
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(workspaceRoot);
        }
    }

    [Theory]
    [InlineData(StorageProviderKind.Ftp)]
    [InlineData(StorageProviderKind.Ipfs)]
    public async Task ReadAsync_accepts_explicit_remote_bindings_without_a_media_relative_path(
        StorageProviderKind providerKind)
    {
        string workspaceRoot = TestFileSystem.CreateTemporaryRoot("asset-reader-empty-media-path");
        try
        {
            byte[] expected = "remote-content-without-local-path"u8.ToArray();
            StorageCatalogRecord storage = CreateStorage(providerKind, workspaceRoot);
            var reference = new StorageObjectReference(
                storage.Id,
                providerKind,
                providerKind == StorageProviderKind.Ftp
                    ? StorageLocatorKind.RemotePath
                    : StorageLocatorKind.ContentAddress,
                providerKind == StorageProviderKind.Ftp
                    ? "remote/assets/asset.png"
                    : "bafybeigdyrzt5emptylogicalpath",
                "asset.png",
                "image/png",
                expected.LongLength);
            var driver = new RecordingStorageDriver(
                providerKind,
                () => new MemoryStream(expected, writable: false));
            ProjectStructureAssetContentReader reader = CreateReader(
                workspaceRoot,
                new StaticStorageCatalog(storage),
                driver);
            var (node, asset) = CreateAsset(reference, mediaRelativePath: string.Empty);

            byte[] actual = await reader.ReadAsync(node, asset);

            Assert.Equal(expected, actual);
            Assert.Equal(reference, driver.LastReference);
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(workspaceRoot);
        }
    }

    [Fact]
    public async Task ReadAsync_never_falls_back_to_a_local_file_for_a_remote_reference()
    {
        string workspaceRoot = TestFileSystem.CreateTemporaryRoot("asset-reader-no-local-fallback");
        try
        {
            const string remoteLocator = "shared/asset.png";
            string localPath = Path.Combine(
                workspaceRoot,
                remoteLocator.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);
            await File.WriteAllBytesAsync(localPath, "wrong-local-content"u8.ToArray());

            byte[] expected = "correct-remote-content"u8.ToArray();
            StorageCatalogRecord storage = CreateStorage(StorageProviderKind.Ftp, workspaceRoot);
            var reference = new StorageObjectReference(
                storage.Id,
                StorageProviderKind.Ftp,
                StorageLocatorKind.RemotePath,
                remoteLocator,
                "asset.png",
                "image/png",
                expected.LongLength);
            var driver = new RecordingStorageDriver(
                StorageProviderKind.Ftp,
                () => new MemoryStream(expected, writable: false));
            ProjectStructureAssetContentReader reader = CreateReader(
                workspaceRoot,
                new StaticStorageCatalog(storage),
                driver);
            var (node, asset) = CreateAsset(reference, mediaRelativePath: string.Empty);

            byte[] actual = await reader.ReadAsync(node, asset);

            Assert.Equal(expected, actual);
            Assert.NotEqual(await File.ReadAllBytesAsync(localPath), actual);
            Assert.Equal(1, driver.OpenCount);
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(workspaceRoot);
        }
    }

    [Fact]
    public async Task ReadAsync_accepts_an_unknown_length_stream_at_exactly_the_25_mib_limit()
    {
        const long exactLimit = ProjectStructureAssetUploadLimits.MaximumFileBytes;
        string workspaceRoot = TestFileSystem.CreateTemporaryRoot("asset-reader-exact-limit");
        try
        {
            var stream = new GeneratedReadStream(exactLimit, reportedLength: null);
            var fixture = CreateFileSystemFixture(workspaceRoot, () => stream, contentLength: null);

            byte[] content = await fixture.Reader.ReadAsync(fixture.Node, fixture.Asset);

            Assert.Equal(exactLimit, content.LongLength);
            Assert.True(stream.IsDisposed);
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(workspaceRoot);
        }
    }

    [Fact]
    public async Task ReadAsync_rejects_a_dishonest_stream_that_reports_one_byte_but_exceeds_25_mib()
    {
        const long actualLength = ProjectStructureAssetUploadLimits.MaximumFileBytes + 1;
        string workspaceRoot = TestFileSystem.CreateTemporaryRoot("asset-reader-dishonest-limit");
        try
        {
            var stream = new GeneratedReadStream(actualLength, reportedLength: 1);
            var fixture = CreateFileSystemFixture(workspaceRoot, () => stream, contentLength: 1);

            ProjectStructureAgentException exception = await Assert.ThrowsAsync<ProjectStructureAgentException>(() =>
                fixture.Reader.ReadAsync(fixture.Node, fixture.Asset));

            Assert.Equal(413, exception.StatusCode);
            Assert.Equal("AssetContentTooLarge", exception.ErrorCode);
            Assert.True(stream.IsDisposed);
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(workspaceRoot);
        }
    }

    [Fact]
    public async Task ReadAsync_supports_only_semantically_valid_projected_process_screenshots_for_null_storage_ids()
    {
        string workspaceRoot = TestFileSystem.CreateTemporaryRoot("asset-reader-projected-screenshot");
        try
        {
            byte[] expected = "projected-screenshot"u8.ToArray();
            StorageCatalogRecord bootstrap = CreateStorage(StorageProviderKind.FileSystem, workspaceRoot);
            bootstrap.IsSystemDefault = true;
            var driver = new RecordingStorageDriver(
                StorageProviderKind.FileSystem,
                () => new MemoryStream(expected, writable: false));
            ProjectStructureAssetContentReader reader = CreateReader(
                workspaceRoot,
                new StaticStorageCatalog(bootstrap),
                driver);
            var (projectedNode, projectedAsset) = CreateProjectedScreenshot(expected.LongLength);

            byte[] actual = await reader.ReadAsync(projectedNode, projectedAsset);
            var ordinaryNode = projectedNode with
            {
                Id = $"image:{Guid.NewGuid():N}",
                IsSystemManaged = false
            };
            var ordinaryAsset = projectedAsset with
            {
                NodeId = ordinaryNode.Id
            };
            ProjectStructureAgentException exception = await Assert.ThrowsAsync<ProjectStructureAgentException>(() =>
                reader.ReadAsync(ordinaryNode, ordinaryAsset));

            Assert.Equal(expected, actual);
            Assert.Equal("AssetContentNotFound", exception.ErrorCode);
            Assert.Equal(1, driver.OpenCount);
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(workspaceRoot);
        }
    }

    [Fact]
    public async Task ReadAsync_allows_a_null_storage_id_only_for_the_authoritative_managed_project_media_namespace()
    {
        string workspaceRoot = TestFileSystem.CreateTemporaryRoot("asset-reader-legacy-bootstrap");
        try
        {
            byte[] expected = "legacy-bootstrap-content"u8.ToArray();
            StorageCatalogRecord bootstrap = CreateStorage(StorageProviderKind.FileSystem, workspaceRoot);
            var reference = StorageJson.CreateLegacyManagedFileReference(
                ManagedPath,
                "image/png",
                "asset.png",
                expected.LongLength);
            var driver = new RecordingStorageDriver(
                StorageProviderKind.FileSystem,
                () => new MemoryStream(expected, writable: false));
            ProjectStructureAssetContentReader reader = CreateReader(
                workspaceRoot,
                new StaticStorageCatalog(bootstrap),
                driver);
            var (node, asset) = CreateAsset(reference);

            byte[] actual = await reader.ReadAsync(node, asset);
            var (emptyPathNode, emptyPathAsset) = CreateAsset(
                reference,
                mediaRelativePath: string.Empty);
            ProjectStructureAgentException emptyPath = await Assert.ThrowsAsync<ProjectStructureAgentException>(() =>
                reader.ReadAsync(emptyPathNode, emptyPathAsset));
            var (mismatchedPathNode, mismatchedPathAsset) = CreateAsset(
                reference,
                mediaRelativePath: "managed-files/project-media/images/different.png");
            ProjectStructureAgentException mismatch = await Assert.ThrowsAsync<ProjectStructureAgentException>(() =>
                reader.ReadAsync(mismatchedPathNode, mismatchedPathAsset));

            Assert.Equal(expected, actual);
            Assert.Equal(bootstrap.Id, driver.LastStorage?.Id);
            Assert.Equal("AssetContentNotFound", emptyPath.ErrorCode);
            Assert.Equal("AssetStorageReferenceInvalid", mismatch.ErrorCode);
            Assert.Equal(1, driver.OpenCount);
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(workspaceRoot);
        }
    }

    [Fact]
    public async Task ReadAsync_rejects_malformed_and_missing_storage_references_before_driver_dispatch()
    {
        string workspaceRoot = TestFileSystem.CreateTemporaryRoot("asset-reader-malformed-reference");
        try
        {
            StorageCatalogRecord storage = CreateStorage(StorageProviderKind.FileSystem, workspaceRoot);
            var driver = new RecordingStorageDriver(
                StorageProviderKind.FileSystem,
                () => new MemoryStream());
            ProjectStructureAssetContentReader reader = CreateReader(
                workspaceRoot,
                new StaticStorageCatalog(storage),
                driver);
            var validReference = new StorageObjectReference(
                storage.Id,
                StorageProviderKind.FileSystem,
                StorageLocatorKind.RelativePath,
                ManagedPath);
            var (node, asset) = CreateAsset(validReference);

            ProjectStructureAgentException missing = await Assert.ThrowsAsync<ProjectStructureAgentException>(() =>
                reader.ReadAsync(node with { StorageObjectReferenceJson = string.Empty }, asset));
            ProjectStructureAgentException malformed = await Assert.ThrowsAsync<ProjectStructureAgentException>(() =>
                reader.ReadAsync(node with { StorageObjectReferenceJson = "{not-json" }, asset));

            Assert.Equal("AssetStorageReferenceInvalid", missing.ErrorCode);
            Assert.Equal("AssetStorageReferenceInvalid", malformed.ErrorCode);
            Assert.Equal(0, driver.OpenCount);
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(workspaceRoot);
        }
    }

    [Fact]
    public async Task ReadAsync_rejects_missing_disabled_and_provider_mismatched_catalogs()
    {
        string workspaceRoot = TestFileSystem.CreateTemporaryRoot("asset-reader-catalog-validation");
        try
        {
            Guid storageId = Guid.NewGuid();
            var reference = new StorageObjectReference(
                storageId,
                StorageProviderKind.Ftp,
                StorageLocatorKind.RemotePath,
                ManagedPath);
            var (node, asset) = CreateAsset(reference);
            var driver = new RecordingStorageDriver(StorageProviderKind.Ftp, () => new MemoryStream());

            ProjectStructureAgentException missing = await Assert.ThrowsAsync<ProjectStructureAgentException>(() =>
                CreateReader(workspaceRoot, new StaticStorageCatalog(), driver)
                    .ReadAsync(node, asset));
            StorageCatalogRecord disabledStorage = CreateStorage(StorageProviderKind.Ftp, workspaceRoot);
            disabledStorage.Id = storageId;
            disabledStorage.IsEnabled = false;
            ProjectStructureAgentException disabled = await Assert.ThrowsAsync<ProjectStructureAgentException>(() =>
                CreateReader(workspaceRoot, new StaticStorageCatalog(disabledStorage), driver)
                    .ReadAsync(node, asset));
            StorageCatalogRecord mismatchedStorage = CreateStorage(StorageProviderKind.FileSystem, workspaceRoot);
            mismatchedStorage.Id = storageId;
            ProjectStructureAgentException mismatch = await Assert.ThrowsAsync<ProjectStructureAgentException>(() =>
                CreateReader(workspaceRoot, new StaticStorageCatalog(mismatchedStorage), driver)
                    .ReadAsync(node, asset));

            Assert.Equal("AssetStorageCatalogMissing", missing.ErrorCode);
            Assert.Equal("AssetStorageDisabled", disabled.ErrorCode);
            Assert.Equal("AssetStorageReferenceInvalid", mismatch.ErrorCode);
            Assert.Equal(0, driver.OpenCount);
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(workspaceRoot);
        }
    }

    [Fact]
    public async Task ReadAsync_rejects_missing_and_read_incapable_drivers_without_dispatch()
    {
        string workspaceRoot = TestFileSystem.CreateTemporaryRoot("asset-reader-driver-validation");
        try
        {
            StorageCatalogRecord storage = CreateStorage(StorageProviderKind.Ftp, workspaceRoot);
            var reference = new StorageObjectReference(
                storage.Id,
                StorageProviderKind.Ftp,
                StorageLocatorKind.RemotePath,
                "remote/assets/asset.png",
                "asset.png",
                "image/png",
                3);
            var (node, asset) = CreateAsset(reference, mediaRelativePath: string.Empty);
            ProjectManagedStoragePhysicalIdentityPolicy identityPolicy = CreateIdentityPolicy(workspaceRoot);
            var missingReader = new ProjectStructureAssetContentReader(
                new StaticStorageCatalog(storage),
                new StorageDriverRegistry([]),
                identityPolicy,
                NullLogger<ProjectStructureAssetContentReader>.Instance);
            var incapableDriver = new RecordingStorageDriver(
                StorageProviderKind.Ftp,
                () => new MemoryStream("bad"u8.ToArray(), writable: false),
                StorageCapability.None);
            ProjectStructureAssetContentReader incapableReader = CreateReader(
                workspaceRoot,
                new StaticStorageCatalog(storage),
                incapableDriver,
                identityPolicy);

            ProjectStructureAgentException missing = await Assert.ThrowsAsync<ProjectStructureAgentException>(() =>
                missingReader.ReadAsync(node, asset));
            ProjectStructureAgentException incapable = await Assert.ThrowsAsync<ProjectStructureAgentException>(() =>
                incapableReader.ReadAsync(node, asset));

            Assert.Equal("AssetStorageDriverUnavailable", missing.ErrorCode);
            Assert.Equal("AssetStorageDriverUnavailable", incapable.ErrorCode);
            Assert.Equal(0, incapableDriver.OpenCount);
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(workspaceRoot);
        }
    }

    [Fact]
    public async Task ReadAsync_rejects_a_v2_catalog_retarget_before_driver_dispatch()
    {
        string workspaceRoot = TestFileSystem.CreateTemporaryRoot("asset-reader-retarget");
        try
        {
            ProjectManagedStoragePhysicalIdentityPolicy identityPolicy = CreateIdentityPolicy(workspaceRoot);
            StorageCatalogRecord originalStorage = CreateStorage(StorageProviderKind.Ftp, workspaceRoot);
            StorageObjectReference reference = CreateV2Reference(
                StorageProviderKind.Ftp,
                originalStorage,
                identityPolicy,
                contentLength: 3);
            originalStorage.EndpointOrRoot = "ftp://retargeted.example.test/other-root";
            var driver = new RecordingStorageDriver(
                StorageProviderKind.Ftp,
                () => new MemoryStream("bad"u8.ToArray(), writable: false));
            ProjectStructureAssetContentReader reader = CreateReader(
                workspaceRoot,
                new StaticStorageCatalog(originalStorage),
                driver,
                identityPolicy);
            var (node, asset) = CreateAsset(reference);

            ProjectStructureAgentException exception = await Assert.ThrowsAsync<ProjectStructureAgentException>(() =>
                reader.ReadAsync(node, asset));

            Assert.Equal(409, exception.StatusCode);
            Assert.Equal("AssetStorageBindingChanged", exception.ErrorCode);
            Assert.Equal(0, driver.OpenCount);
            Assert.DoesNotContain("retargeted.example.test", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(workspaceRoot);
        }
    }

    [Fact]
    public async Task ReadAsync_maps_driver_failures_to_a_typed_sanitized_error()
    {
        string workspaceRoot = TestFileSystem.CreateTemporaryRoot("asset-reader-sanitized-failure");
        try
        {
            StorageCatalogRecord storage = CreateStorage(StorageProviderKind.Ftp, workspaceRoot);
            var driver = new RecordingStorageDriver(
                StorageProviderKind.Ftp,
                () => throw new IOException(
                    "ftp://operator:super-secret@storage.example.test/private/asset.png"));
            ProjectStructureAssetContentReader reader = CreateReader(
                workspaceRoot,
                new StaticStorageCatalog(storage),
                driver);
            var reference = new StorageObjectReference(
                storage.Id,
                StorageProviderKind.Ftp,
                StorageLocatorKind.RemotePath,
                ManagedPath);
            var (node, asset) = CreateAsset(reference);

            ProjectStructureAgentException exception = await Assert.ThrowsAsync<ProjectStructureAgentException>(() =>
                reader.ReadAsync(node, asset));

            Assert.Equal(503, exception.StatusCode);
            Assert.Equal("AssetContentUnavailable", exception.ErrorCode);
            Assert.True(exception.IsSafeToExpose);
            Assert.DoesNotContain("super-secret", exception.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("storage.example.test", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(workspaceRoot);
        }
    }

    [Fact]
    public async Task ReadAsync_maps_a_seekable_stream_length_failure_to_a_typed_sanitized_error()
    {
        string workspaceRoot = TestFileSystem.CreateTemporaryRoot("asset-reader-length-failure");
        try
        {
            var stream = new GeneratedReadStream(
                contentLength: 1,
                reportedLength: 1,
                lengthFailure: new ObjectDisposedException("provider-stream", "private/provider/path"));
            var fixture = CreateFileSystemFixture(workspaceRoot, () => stream, contentLength: 1);

            ProjectStructureAgentException exception = await Assert.ThrowsAsync<ProjectStructureAgentException>(() =>
                fixture.Reader.ReadAsync(fixture.Node, fixture.Asset));

            Assert.Equal(503, exception.StatusCode);
            Assert.Equal("AssetContentUnavailable", exception.ErrorCode);
            Assert.True(exception.IsSafeToExpose);
            Assert.DoesNotContain("private/provider/path", exception.Message, StringComparison.Ordinal);
            Assert.True(stream.IsDisposed);
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(workspaceRoot);
        }
    }

    [Fact]
    public async Task ReadAsync_propagates_cancellation_and_disposes_the_provider_stream()
    {
        string workspaceRoot = TestFileSystem.CreateTemporaryRoot("asset-reader-cancellation");
        try
        {
            var stream = new GeneratedReadStream(1024, reportedLength: null);
            var fixture = CreateFileSystemFixture(workspaceRoot, () => stream, contentLength: null);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                fixture.Reader.ReadAsync(fixture.Node, fixture.Asset, cancellation.Token));

            Assert.True(stream.IsDisposed);
        }
        finally
        {
            TestFileSystem.DeleteDirectoryWithRetry(workspaceRoot);
        }
    }

    private static ProjectStructureAssetContentReaderFixture CreateFileSystemFixture(
        string workspaceRoot,
        Func<Stream> streamFactory,
        long? contentLength)
    {
        ProjectManagedStoragePhysicalIdentityPolicy identityPolicy = CreateIdentityPolicy(workspaceRoot);
        StorageCatalogRecord storage = CreateStorage(StorageProviderKind.FileSystem, workspaceRoot);
        StorageObjectReference reference = CreateV2Reference(
            StorageProviderKind.FileSystem,
            storage,
            identityPolicy,
            contentLength);
        var driver = new RecordingStorageDriver(StorageProviderKind.FileSystem, streamFactory);
        ProjectStructureAssetContentReader reader = CreateReader(
            workspaceRoot,
            new StaticStorageCatalog(storage),
            driver,
            identityPolicy);
        var (node, asset) = CreateAsset(reference);
        return new(reader, node, asset);
    }

    private static ProjectStructureAssetContentReader CreateReader(
        string workspaceRoot,
        IStorageCatalogService catalog,
        IStorageDriver driver,
        ProjectManagedStoragePhysicalIdentityPolicy? identityPolicy = null)
        => new(
            catalog,
            new StorageDriverRegistry([driver]),
            identityPolicy ?? CreateIdentityPolicy(workspaceRoot),
            NullLogger<ProjectStructureAssetContentReader>.Instance);

    private static ProjectManagedStoragePhysicalIdentityPolicy CreateIdentityPolicy(string workspaceRoot)
        => new(new FileSystemStoragePathPolicy(new StaticWorkspacePathResolver(workspaceRoot)));

    private static StorageCatalogRecord CreateStorage(
        StorageProviderKind providerKind,
        string workspaceRoot)
        => new()
        {
            Id = Guid.NewGuid(),
            Name = $"{providerKind} test storage",
            ProviderKind = providerKind,
            IsEnabled = true,
            IsSystemDefault = providerKind == StorageProviderKind.FileSystem,
            EndpointOrRoot = providerKind switch
            {
                StorageProviderKind.FileSystem => workspaceRoot,
                StorageProviderKind.Ftp => "ftp://storage.example.test/root",
                StorageProviderKind.Ipfs => "https://ipfs.example.test",
                _ => string.Empty
            },
            CapabilityMask = StorageCapability.Read
        };

    private static StorageObjectReference CreateV2Reference(
        StorageProviderKind providerKind,
        StorageCatalogRecord storage,
        ProjectManagedStoragePhysicalIdentityPolicy identityPolicy,
        long? contentLength)
    {
        var reference = new StorageObjectReference(
            storage.Id,
            providerKind,
            providerKind switch
            {
                StorageProviderKind.FileSystem => StorageLocatorKind.RelativePath,
                StorageProviderKind.Ftp => StorageLocatorKind.RemotePath,
                StorageProviderKind.Ipfs => StorageLocatorKind.ContentAddress,
                _ => throw new ArgumentOutOfRangeException(nameof(providerKind))
            },
            providerKind == StorageProviderKind.Ipfs
                ? "bafybeigdyrzt5examplecid"
                : ManagedPath,
            "asset.png",
            "image/png",
            contentLength);
        return ProjectManagedStorageProvenancePolicy.Stamp(
            reference,
            ManagedPath,
            storage,
            identityPolicy);
    }

    private static (ProjectStructureNode Node, ProjectStructureAssetDescriptor Asset) CreateAsset(
        StorageObjectReference reference,
        string mediaRelativePath = ManagedPath)
    {
        string nodeId = $"image:{Guid.NewGuid():N}";
        ProjectStructureNode node = CreateNode(
            nodeId,
            mediaRelativePath,
            StorageJson.SerializeReference(reference));
        return (node, CreateAssetDescriptor(node));
    }

    private static (ProjectStructureNode Node, ProjectStructureAssetDescriptor Asset) CreateProjectedScreenshot(
        long contentLength)
    {
        Guid runId = Guid.NewGuid();
        string managedArtifactRoot = ProcessLaunchApplicationService.BuildManagedProcessArtifactRoot(
            new ProcessRunId(runId));
        string screenshotPath = $"{managedArtifactRoot}/browser/screenshot.png";
        string nodeId = ProjectStructureProcessNodeKeys.BuildProcessRunScreenshotNodeKey(
            runId,
            screenshotPath);
        StorageObjectReference reference = StorageJson.CreateLegacyManagedFileReference(
            screenshotPath,
            "image/png",
            "screenshot.png",
            contentLength);
        ProjectStructureNode node = CreateNode(
            nodeId,
            screenshotPath,
            StorageJson.SerializeReference(reference)) with
        {
            ArtifactKind = ProjectStructureProcessNodeKeys.ProcessRunScreenshotArtifactKind,
            ArtifactId = runId,
            IsSystemManaged = true
        };
        return (node, CreateAssetDescriptor(node));
    }

    private static ProjectStructureNode CreateNode(
        string nodeId,
        string mediaRelativePath,
        string storageObjectReferenceJson)
        => new(
            Id: nodeId,
            ParentId: null,
            ObjectType: ProjectObjectType.ImageAsset,
            ObjectSubtype: "screenshot",
            Title: "Asset",
            Subtitle: string.Empty,
            Status: string.Empty,
            Notes: string.Empty,
            Route: string.Empty,
            ArtifactKind: string.Empty,
            ArtifactId: null,
            MediaRelativePath: mediaRelativePath,
            MediaContentType: "image/png",
            MediaOriginalFileName: "asset.png",
            X: 0,
            Y: 0,
            VisualProfile: null!,
            Badges: [],
            ProgressMode: string.Empty,
            ProgressPercent: 0,
            MarkerIcon: string.Empty,
            MarkerTone: string.Empty,
            MarkerLabel: string.Empty,
            Markers: [],
            Priority: 0,
            StorageObjectReferenceJson: storageObjectReferenceJson);

    private static ProjectStructureAssetDescriptor CreateAssetDescriptor(ProjectStructureNode node)
        => new(
            Guid.NewGuid(),
            node.Id,
            node.ObjectType,
            node.ObjectSubtype,
            node.Title,
            node.Subtitle,
            node.Route,
            node.MediaRelativePath,
            node.MediaContentType,
            node.MediaOriginalFileName,
            node.MetadataJson,
            true,
            null);

    private sealed record ProjectStructureAssetContentReaderFixture(
        ProjectStructureAssetContentReader Reader,
        ProjectStructureNode Node,
        ProjectStructureAssetDescriptor Asset);

    private sealed class StaticStorageCatalog(params StorageCatalogRecord[] storages)
        : IStorageCatalogService
    {
        private readonly IReadOnlyList<StorageCatalogRecord> items = storages;

        public Task<IReadOnlyList<StorageCatalogRecord>> ListAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult(items);

        public Task<StorageCatalogRecord?> GetAsync(
            Guid id,
            CancellationToken cancellationToken = default)
            => Task.FromResult(items.SingleOrDefault(storage => storage.Id == id));

        public Task<StorageCatalogRecord> EnsureBootstrapFileSystemStorageAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult(items.Single(storage =>
                storage.ProviderKind == StorageProviderKind.FileSystem &&
                storage.IsSystemDefault));

        public Task<StorageCatalogRecord> SaveAsync(
            StorageCatalogRecord record,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<StorageRoutingRule>> ListRulesAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<StorageRoutingRule>>([]);

        public Task<StorageRoutingRule> SaveRuleAsync(
            StorageRoutingRule rule,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class RecordingStorageDriver(
        StorageProviderKind providerKind,
        Func<Stream> streamFactory,
        StorageCapability supportedCapabilities = StorageCapability.Read) : IStorageDriver
    {
        public StorageProviderKind ProviderKind => providerKind;

        public StorageCapability SupportedCapabilities => supportedCapabilities;

        public int OpenCount { get; private set; }

        public StorageCatalogRecord? LastStorage { get; private set; }

        public StorageObjectReference? LastReference { get; private set; }

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
        {
            OpenCount++;
            LastStorage = storage;
            LastReference = reference;
            return Task.FromResult(streamFactory());
        }

        public Task DeleteAsync(
            StorageCatalogRecord storage,
            StorageObjectReference reference,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class StaticWorkspacePathResolver(string workspaceRoot) : IWorkspacePathResolver
    {
        public string ResolveWorkspaceRoot() => workspaceRoot;

        public string ResolveManagedFilesRoot() => Path.Combine(workspaceRoot, "managed-files");

        public string ResolveExportsRoot() => Path.Combine(workspaceRoot, "exports");

        public string ResolveEvidenceRoot() => Path.Combine(workspaceRoot, "evidence");

        public string ResolveManagerArtifactsRoot() => Path.Combine(workspaceRoot, "artifacts");
    }

    private sealed class GeneratedReadStream(
        long contentLength,
        long? reportedLength,
        Exception? lengthFailure = null) : Stream
    {
        private long position;

        public bool IsDisposed { get; private set; }

        public override bool CanRead => true;

        public override bool CanSeek => reportedLength.HasValue;

        public override bool CanWrite => false;

        public override long Length
        {
            get
            {
                if (lengthFailure is not null)
                {
                    throw lengthFailure;
                }

                return reportedLength ?? throw new NotSupportedException();
            }
        }

        public override long Position
        {
            get => position;
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int read = (int)Math.Min(count, contentLength - position);
            if (read <= 0)
            {
                return 0;
            }

            Array.Clear(buffer, offset, read);
            position += read;
            return read;
        }

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int read = (int)Math.Min(buffer.Length, contentLength - position);
            if (read <= 0)
            {
                return ValueTask.FromResult(0);
            }

            buffer.Span[..read].Clear();
            position += read;
            return ValueTask.FromResult(read);
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin)
            => throw new NotSupportedException();

        public override void SetLength(long value)
            => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
            => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            IsDisposed = true;
            base.Dispose(disposing);
        }
    }
}
