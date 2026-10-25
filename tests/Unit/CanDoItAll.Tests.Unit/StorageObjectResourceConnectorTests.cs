using CanDoItAll.Tests.Support;
using CanDoItAll.FileTools.FileInteraction;
using CanDoItAll.FileTools.Integration;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Resources;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace CanDoItAll.Tests.Unit.Storage;

public sealed class StorageObjectResourceConnectorTests
{
    [Theory]
    [InlineData(StorageProviderKind.FileSystem, StorageLocatorKind.RelativePath)]
    [InlineData(StorageProviderKind.Ipfs, StorageLocatorKind.ContentAddress)]
    [InlineData(StorageProviderKind.Ipfs, StorageLocatorKind.RemotePath)]
    [InlineData(StorageProviderKind.Ftp, StorageLocatorKind.RemotePath)]
    public void Config_round_trips_supported_stable_occurrences(
        StorageProviderKind providerKind,
        StorageLocatorKind locatorKind)
    {
        Guid storageId = Guid.NewGuid();
        var config = new StorageObjectResourceConfig(
            ResourceFileSourceKey.ForStorage(storageId).Value,
            storageId,
            providerKind,
            locatorKind,
            locatorKind == StorageLocatorKind.ContentAddress ? "bafy-example" : "folder/file.txt",
            "file.txt",
            "text/plain",
            42);

        string json = StorageObjectResourceConnectorPlugin.Serialize(config);
        StorageObjectResourceConfig restored = StorageObjectResourceConnectorPlugin.Deserialize(json);
        string location = StorageObjectResourceConnectorPlugin.BuildStableLocation(restored);

        Assert.Equal(config, restored);
        Assert.StartsWith($"storage-object:{storageId:N}:{providerKind}:{locatorKind}:", location, StringComparison.Ordinal);
        Assert.DoesNotContain(config.Locator, location, StringComparison.Ordinal);
    }

    [Fact]
    public void Stored_config_with_uppercase_source_digits_validates_without_being_rewritten()
    {
        Guid storageId = Guid.NewGuid();
        string storedSourceKey = $"storage:{storageId.ToString("N").ToUpperInvariant()}";
        var config = new StorageObjectResourceConfig(
            storedSourceKey,
            storageId,
            StorageProviderKind.FileSystem,
            StorageLocatorKind.RelativePath,
            "folder/file.txt",
            "file.txt",
            "text/plain",
            42);

        string json = StorageObjectResourceConnectorPlugin.Serialize(config);
        StorageObjectResourceConfig restored = StorageObjectResourceConnectorPlugin.Deserialize(json);

        Assert.Equal(storedSourceKey, restored.SourceKey);
        Assert.True(ResourceFileSourceKey.TryParse(restored.SourceKey, out ResourceFileSourceKey key));
        Assert.Equal(ResourceFileSourceKey.ForStorage(storageId), key);
    }

    [Fact]
    public void Config_rejects_incompatible_provider_and_locator()
    {
        Guid storageId = Guid.NewGuid();
        var config = new StorageObjectResourceConfig(
            ResourceFileSourceKey.ForStorage(storageId).Value,
            storageId,
            StorageProviderKind.Ftp,
            StorageLocatorKind.ContentAddress,
            "bafy-invalid",
            "file.txt",
            "text/plain",
            null);

        Assert.Throws<ArgumentException>(() => StorageObjectResourceConnectorPlugin.Serialize(config));
    }

    [Fact]
    public void Config_rejects_unmapped_authority_fields()
    {
        Guid storageId = Guid.NewGuid();
        string json = $$"""
                        {
                          "sourceKey": "storage:{{storageId:N}}",
                          "storageId": "{{storageId}}",
                          "providerKind": "FileSystem",
                          "locatorKind": "RelativePath",
                          "locator": "file.txt",
                          "displayName": "file.txt",
                          "contentType": "text/plain",
                          "contentLength": 1,
                          "handle": "unsigned-authority"
                        }
                        """;

        Assert.Throws<InvalidOperationException>(() => StorageObjectResourceConnectorPlugin.Deserialize(json));
    }

    [Fact]
    public void General_resource_save_rejects_governed_storage_object_connector_before_persistence() {
        var profile = Guid.NewGuid();
        var project = new ProjectWriteAdmission(profile, Guid.NewGuid(), Guid.NewGuid());
        var original = new ResourceMetadataExpectation.Absent(new(profile, new(Guid.NewGuid())), Guid.NewGuid());
        var model = new ResourceEditorModel {
            Name = "Bypass attempt",
            ConnectorPluginKey = StorageObjectResourceConnectorPlugin.PluginKey
        };
        var preparation = new ResourceMetadataPreparation(new ResourceConnectorPluginRegistry([new StorageObjectResourceConnectorPlugin()]));
        var refused = Assert.Throws<ResourceMetadataRefusedException>(() => preparation.Prepare(new ReplaceResourceMetadataCommand(original, project, ResourceMetadataValues.Capture(model))));
        Assert.Equal(ResourceMetadataRejectionReason.GovernedResource, refused.Reason);
        Assert.Contains("authorized Resources browse promotion", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reopen_resolves_current_source_and_stable_occurrence_then_releases_session()
    {
        await using ResourcePersistenceFixture fixture = await ResourcePersistenceFixture.CreateAsync();
        Guid storageId = Guid.NewGuid();
        ResourceFileSourceKey sourceKey = ResourceFileSourceKey.ForStorage(storageId);
        var config = new StorageObjectResourceConfig(
            sourceKey.Value,
            storageId,
            StorageProviderKind.FileSystem,
            StorageLocatorKind.RelativePath,
            "folder/file.txt",
            "file.txt",
            "text/plain",
            3);
        Guid resourceId = await fixture.AddResourceAsync(config);
        ResourceFileSourceDescriptor source = CreateSource(sourceKey, storageId);
        var activator = new RecordingKnownFileActivator(source.Scope);
        var sessions = new RecordingKnownFileSessionFactory();
        var releaser = new RecordingKnownFileReleaser();
        var service = new ResourceStorageObjectInteractionService(
            fixture.OwnerFactory,
            new StaticSourceCatalog(source),
            activator,
            sessions,
            releaser);

        await using ResourceStorageObjectInteraction interaction = await service.OpenAsync(resourceId);
        await using FileContentLease content = await interaction.Session.ContentSource.OpenReadAsync(
            new FileContentReadRequest(interaction.Session.File));

        Assert.Equal("file.txt", interaction.Request.FileName);
        Assert.Equal("folder/file.txt", activator.Occurrence?.OccurrenceId);
        Assert.Equal(storageId, activator.Occurrence?.StorageId);
        Assert.Equal(3, content.Length);
        Assert.Equal(1, sessions.CreateCount);
        Assert.Equal(0, releaser.ReleaseCount);
        await interaction.DisposeAsync();
        Assert.Equal(1, releaser.ReleaseCount);
    }

    [Fact]
    public async Task Reopen_missing_current_source_never_activates_persisted_occurrence()
    {
        await using ResourcePersistenceFixture fixture = await ResourcePersistenceFixture.CreateAsync();
        Guid storageId = Guid.NewGuid();
        ResourceFileSourceKey sourceKey = ResourceFileSourceKey.ForStorage(storageId);
        Guid resourceId = await fixture.AddResourceAsync(new StorageObjectResourceConfig(
            sourceKey.Value,
            storageId,
            StorageProviderKind.FileSystem,
            StorageLocatorKind.RelativePath,
            "folder/file.txt",
            "file.txt",
            "text/plain",
            3));
        ResourceFileSourceDescriptor source = CreateSource(sourceKey, storageId);
        var activator = new RecordingKnownFileActivator(source.Scope);
        var service = new ResourceStorageObjectInteractionService(
            fixture.OwnerFactory,
            new MissingSourceCatalog(),
            activator,
            new RecordingKnownFileSessionFactory(),
            new RecordingKnownFileReleaser());

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await service.OpenAsync(resourceId));

        Assert.Null(activator.Occurrence);
    }

    private static ResourceFileSourceDescriptor CreateSource(ResourceFileSourceKey sourceKey, Guid storageId)
    {
        var storage = new StorageCatalogRecord
        {
            Id = storageId,
            Name = "Filesystem",
            ProviderKind = StorageProviderKind.FileSystem,
            IsEnabled = true,
            CapabilityMask = StorageCapability.Read
        };
        var scope = new FileToolsSemanticScope(
            FileToolsSemanticScopeKind.ResourceSource,
            ResourceStorageSourceScopeKey.Create(
                storageId,
                ResourceStorageSourceScopeKey.BuildFingerprint(storage.ToSnapshot())),
            storage.Name);
        return new ResourceFileSourceDescriptor(
            sourceKey,
            ResourceFileSourceClass.FileSystem,
            storage.Name,
            "Filesystem",
            scope,
            storageId,
            storage.ProviderKind,
            false,
            StorageHealthStatus.Healthy);
    }

    private sealed class ResourcePersistenceFixture : IAsyncDisposable
    {
        private ResourcePersistenceFixture(TestDbContextFactory factory, IDbContextFactory<ResourcesDbContext> ownerFactory, ProjectWriteAdmissionService admissions, CoordinatedDatabaseTransaction coordinator, Guid projectId)
        {
            Factory = factory;
            OwnerFactory = ownerFactory;
            Admissions = admissions;
            Coordinator = coordinator;
            ProjectId = projectId;
        }

        public TestDbContextFactory Factory { get; }

        public IDbContextFactory<ResourcesDbContext> OwnerFactory { get; }

        public ProjectWriteAdmissionService Admissions { get; }

        public CoordinatedDatabaseTransaction Coordinator { get; }

        public Guid ProjectId { get; }

        public static async Task<ResourcePersistenceFixture> CreateAsync()
        {
            AppDbContextModelRegistry.ConfigureAssemblies(
                [typeof(Project).Assembly, typeof(ProjectResource).Assembly]);
            var databaseName = $"storage-object-resource-{Guid.NewGuid():N}";
            var databaseRoot = new InMemoryDatabaseRoot();
            var ownerFactory = new PooledDbContextFactory<ResourcesDbContext>(
                new DbContextOptionsBuilder<ResourcesDbContext>()
                    .UseInMemoryDatabase(databaseName, databaseRoot).Options);
            var options = AppDbContextTestOptionsBuilder.Create()
                .UseInMemoryDatabase(databaseName, databaseRoot)
                .Options;
            var factory = new TestDbContextFactory(options);
            Guid projectId = Guid.NewGuid();
            await using AppDbContext dbContext = factory.CreateDbContext();
            dbContext.Set<Project>().Add(new Project { Id = projectId, Name = "Target project" });
            await dbContext.SaveChangesAsync();
            var projectOptions = new DbContextOptionsBuilder<ProjectsDbContext>()
                .UseInMemoryDatabase(databaseName, databaseRoot).Options;
            var projectProfile = new ResolvedDatabaseProfile(new() { ProviderKind = DatabaseProviderKind.InMemory },
                DatabaseProfileResolutionSource.ExplicitOverride, databaseName);
            var coordinator = CoordinatedDatabaseTransaction.ForProfile(projectProfile);
            var admissions = new ProjectWriteAdmissionService(new PooledDbContextFactory<ProjectsDbContext>(projectOptions),
                projectOptions, coordinator, new CanonicalDatabase(projectProfile), DatabaseTransferTestSupport.UnconfiguredActivation());
            return new ResourcePersistenceFixture(factory, ownerFactory, admissions, coordinator, projectId);
        }

        public async Task<Guid> AddResourceAsync(StorageObjectResourceConfig config)
        {
            Guid resourceId = Guid.NewGuid();
            await using AppDbContext dbContext = Factory.CreateDbContext();
            dbContext.Set<ProjectResource>().Add(new ProjectResource
            {
                Id = resourceId,
                ProjectId = ProjectId,
                ProjectLifetimeId = (await Admissions.CaptureAsync(ProjectId))!.LifetimeId,
                Name = config.DisplayName,
                ConnectorPluginKey = StorageObjectResourceConnectorPlugin.PluginKey,
                ConfigSchemaVersion = StorageObjectResourceConnectorPlugin.SchemaVersion,
                LocationOrIdentifier = StorageObjectResourceConnectorPlugin.BuildStableLocation(config),
                ConfigJson = StorageObjectResourceConnectorPlugin.Serialize(config)
            });
            await dbContext.SaveChangesAsync();
            return resourceId;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class CanonicalDatabase(ResolvedDatabaseProfile profile) : ICanonicalRuntimeDatabase {
        public ResolvedDatabaseProfile Profile { get; } = profile;
        public long Generation => 1;
    }

    private sealed class TestDbContextFactory(DbContextOptions<AppDbContext> options) : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() => new(options);

        public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(CreateDbContext());
    }

    private sealed class StaticSourceCatalog(ResourceFileSourceDescriptor source) : IResourceFileSourceCatalog
    {
        public Task<ResourceFileSourceCatalogSnapshot> LoadAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new ResourceFileSourceCatalogSnapshot([source], [], "fingerprint"));

        public Task<ResourceFileSourceDescriptor> ResolveAsync(
            ResourceFileSourceKey key,
            CancellationToken cancellationToken = default)
            => Task.FromResult(source);
    }

    private sealed class MissingSourceCatalog : IResourceFileSourceCatalog
    {
        public Task<ResourceFileSourceCatalogSnapshot> LoadAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ResourceFileSourceDescriptor> ResolveAsync(
            ResourceFileSourceKey key,
            CancellationToken cancellationToken = default)
            => Task.FromException<ResourceFileSourceDescriptor>(new InvalidOperationException("source missing"));
    }

    private sealed class RecordingKnownFileActivator(FileToolsSemanticScope scope) : IFileToolsKnownFileActivator
    {
        public FileToolsKnownFileOccurrence? Occurrence { get; private set; }

        public ValueTask<FileToolsKnownFileActivation> ActivateAsync(
            FileToolsSemanticScope requestedScope,
            FileToolsKnownFileOccurrence occurrence,
            FileToolsKnownFileIntent intent,
            CancellationToken cancellationToken = default)
        {
            Assert.Equal(scope.Id, requestedScope.Id);
            Occurrence = occurrence;
            var file = new FileReference("authorized", "current-handle");
            var request = new FileToolsKnownFileRequest(requestedScope, file, intent);
            return ValueTask.FromResult(new FileToolsKnownFileActivation(
                request,
                occurrence.FileName,
                occurrence.MediaType,
                occurrence.Size));
        }
    }

    private sealed class RecordingKnownFileSessionFactory : IFileToolsKnownFileSessionFactory
    {
        public int CreateCount { get; private set; }

        public ValueTask<FileToolsKnownFileSession> CreateAsync(
            FileToolsKnownFileRequest request,
            CancellationToken cancellationToken = default)
        {
            CreateCount++;
            return ValueTask.FromResult(new FileToolsKnownFileSession(
                request.File,
                new StaticContentSource(),
                request.Intent));
        }
    }

    private sealed class RecordingKnownFileReleaser : IFileToolsKnownFileSessionReleaser
    {
        public int ReleaseCount { get; private set; }

        public ValueTask ReleaseAsync(FileReference file, CancellationToken cancellationToken = default)
        {
            ReleaseCount++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class StaticContentSource : IFileContentSource
    {
        public ValueTask<FileContentLease> OpenReadAsync(
            FileContentReadRequest request,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(new FileContentLease(
                new MemoryStream([1, 2, 3]),
                "text/plain",
                3));
    }
}
