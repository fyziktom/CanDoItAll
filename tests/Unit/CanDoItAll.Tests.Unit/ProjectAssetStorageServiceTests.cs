using System.Text;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Unit.Projects;

public sealed class ProjectAssetStorageServiceTests
{
    [Fact]
    public async Task Save_typed_json_revalidates_and_canonicalizes_before_storage()
    {
        var placementService = new RecordingStoragePlacementService();
        var service = new ProjectAssetStorageService(
            placementService,
            new ProjectAssetCreationService(),
            CreatePhysicalIdentityPolicy());
        byte[] content = Encoding.UTF8.GetBytes("{\"enabled\":true}");

        SavedMediaDescriptor? saved = await service.SaveAsync(
            Guid.NewGuid(),
            ProjectObjectType.File,
            "json",
            new ProjectObjectMediaPayload(
                "SETTINGS.JSON",
                "application/octet-stream",
                Convert.ToBase64String(content)));

        Assert.NotNull(saved);
        Assert.NotNull(placementService.Request);
        Assert.Equal("SETTINGS.json", placementService.Request.FileName);
        Assert.Equal("application/json", placementService.Request.ContentType);
        Assert.Equal(content, placementService.Request.Content);
        Assert.Equal("SETTINGS.json", saved.OriginalFileName);
        Assert.Equal(MermaidDiagramKind.Unknown, saved.MermaidDiagramKind);
    }

    [Fact]
    public async Task Save_invalid_typed_json_rejects_before_storage_is_called()
    {
        var placementService = new RecordingStoragePlacementService();
        var service = new ProjectAssetStorageService(
            placementService,
            new ProjectAssetCreationService(),
            CreatePhysicalIdentityPolicy());

        InvalidDataException exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => service.SaveAsync(
                Guid.NewGuid(),
                ProjectObjectType.File,
                "json",
                new ProjectObjectMediaPayload(
                    "settings.json",
                    "application/json",
                    Convert.ToBase64String(Encoding.UTF8.GetBytes("{ invalid }")))));

        Assert.IsType<ProjectAssetCreationException>(exception.InnerException);
        Assert.Null(placementService.Request);
    }

    [Fact]
    public async Task Save_mermaid_detects_diagram_kind_from_stored_content()
    {
        var placementService = new RecordingStoragePlacementService();
        var service = new ProjectAssetStorageService(
            placementService,
            new ProjectAssetCreationService(),
            CreatePhysicalIdentityPolicy());
        byte[] content = Encoding.UTF8.GetBytes(
            "sequenceDiagram\n    Alice->>Bob: Hello");

        SavedMediaDescriptor? saved = await service.SaveAsync(
            Guid.NewGuid(),
            ProjectObjectType.File,
            "mermaid",
            new ProjectObjectMediaPayload(
                "interaction.mermaid",
                "text/plain",
                Convert.ToBase64String(content)));

        Assert.NotNull(saved);
        Assert.Equal("interaction.mmd", saved.OriginalFileName);
        Assert.Equal(MermaidDiagramKind.SequenceDiagram, saved.MermaidDiagramKind);
    }

    private sealed class RecordingStoragePlacementService : IStoragePlacementService
    {
        public StoragePlacementRequest? Request { get; private set; }

        public Task<StoragePlacementResult> PlaceAsync(
            StoragePlacementRequest request,
            CancellationToken cancellationToken = default)
        {
            Request = request;
            var storage = new StorageCatalogRecord
            {
                Id = Guid.NewGuid(),
                Name = "Unit-test storage",
                ProviderKind = StorageProviderKind.FileSystem,
                CapabilityMask =
                    StorageCapability.Read |
                    StorageCapability.Write |
                    StorageCapability.Download |
                    StorageCapability.InlinePreview
            };
            StorageCatalogHostBindingPolicy.BindCurrent(
                storage,
                StubWorkspacePathResolver.Root,
                DateTimeOffset.UtcNow);
            var reference = new StorageObjectReference(
                storage.Id,
                storage.ProviderKind,
                StorageLocatorKind.RelativePath,
                request.RelativePathHint ?? request.FileName,
                request.FileName,
                request.ContentType,
                request.Content.LongLength);
            var access = new StorageAccessDescriptor(
                "/storage/objects/preview?ref=test",
                "/storage/objects/download?ref=test",
                null,
                true,
                true,
                false,
                request.FileName,
                request.ContentType,
                request.Content.LongLength,
                string.Empty);
            var recommendation = new StorageRecommendation(
                new StorageRecommendationCandidate(
                    storage.Id,
                    storage.Name,
                    storage.ProviderKind,
                    storage.CapabilityMask,
                    StorageHealthStatus.Healthy,
                    false,
                    "Unit test."),
                [],
                "Unit test.",
                []);

            return Task.FromResult(new StoragePlacementResult(
                storage,
                recommendation,
                new StorageWriteResult(reference, access),
                access.PreviewUrl,
                Path.Combine(Path.GetTempPath(), request.FileName),
                request.RelativePathHint ?? request.FileName));
        }
    }

    private static ProjectManagedStoragePhysicalIdentityPolicy CreatePhysicalIdentityPolicy()
        => new(
            new FileSystemStoragePathPolicy(new StubWorkspacePathResolver()),
            TestWorkspaceServices.PhysicalPathPolicyFactory);

    private sealed class StubWorkspacePathResolver : IWorkspacePathResolver
    {
        internal static readonly string Root = Path.Combine(
            Path.GetTempPath(),
            "candoitall-project-asset-storage-tests");

        public string ResolveWorkspaceRoot() => Root;

        public string ResolveManagedFilesRoot() => Path.Combine(Root, "managed-files");

        public string ResolveExportsRoot() => Path.Combine(Root, "exports");

        public string ResolveEvidenceRoot() => Path.Combine(Root, "evidence");

        public string ResolveManagerArtifactsRoot() => Path.Combine(Root, "manager-artifacts");
    }
}
