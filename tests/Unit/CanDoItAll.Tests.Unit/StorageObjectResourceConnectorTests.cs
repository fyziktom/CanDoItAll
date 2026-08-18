using CanDoItAll.FileTools.FileInteraction;
using CanDoItAll.FileTools.Integration;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Search;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Resources;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

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
    public async Task General_resource_save_rejects_governed_storage_object_connector()
    {
        Guid storageId = Guid.NewGuid();
        var plugin = new StorageObjectResourceConnectorPlugin();
        var service = new ResourcesService(
            new ThrowingDbContextFactory(),
            new FixedClock(),
            new NullActivityStream(),
            new NullSearchIndex(),
            new ResourceConnectorPluginRegistry([plugin]));
        var model = new ResourceEditorModel
        {
            ProjectId = Guid.NewGuid(),
            Name = "Bypass attempt",
            ConnectorPluginKey = StorageObjectResourceConnectorPlugin.PluginKey,
            ConfigJson = StorageObjectResourceConnectorPlugin.Serialize(new StorageObjectResourceConfig(
                ResourceFileSourceKey.ForStorage(storageId).Value,
                storageId,
                StorageProviderKind.FileSystem,
                StorageLocatorKind.RelativePath,
                "file.txt",
                "file.txt",
                "text/plain",
                1))
        };

        Result<Guid> result = await service.SaveAsync(model);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, error => error.Message.Contains("authorized Resources browse promotion", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Writer_persists_curated_configuration_and_is_idempotent_per_project_object()
    {
        await using ResourcePersistenceFixture fixture = await ResourcePersistenceFixture.CreateAsync();
        Guid storageId = Guid.NewGuid();
        var config = new StorageObjectResourceConfig(
            ResourceFileSourceKey.ForStorage(storageId).Value,
            storageId,
            StorageProviderKind.FileSystem,
            StorageLocatorKind.RelativePath,
            "folder/file.txt",
            "file.txt",
            "text/plain",
            42);
        var writer = new StorageObjectResourceWriter(fixture.Factory, new FixedClock());
        var request = new StorageObjectResourceWriteRequest(
            fixture.ProjectId,
            "Stored file",
            ResourceSensitivity.Sensitive,
            config);

        StorageObjectResourceWriteResult created = await writer.SaveAsync(request);
        StorageObjectResourceWriteResult repeated = await writer.SaveAsync(request);

        Assert.True(created.Created);
        Assert.False(repeated.Created);
        Assert.Equal(created.ResourceId, repeated.ResourceId);
        await using AppDbContext dbContext = fixture.Factory.CreateDbContext();
        ProjectResource resource = Assert.Single(await dbContext.Set<ProjectResource>().AsNoTracking().ToListAsync());
        Assert.Equal(StorageObjectResourceConnectorPlugin.PluginKey, resource.ConnectorPluginKey);
        Assert.Equal(StorageObjectResourceConnectorPlugin.SchemaVersion, resource.ConfigSchemaVersion);
        Assert.Equal(ResourceValidationStatus.Valid, resource.ValidationStatus);
        Assert.Equal(ResourceSensitivity.Sensitive, resource.Sensitivity);
        Assert.Equal(StorageObjectResourceConnectorPlugin.Serialize(config), resource.ConfigJson);
        Assert.DoesNotContain("folder/file.txt", resource.LocationOrIdentifier, StringComparison.Ordinal);
        Assert.DoesNotContain("handle", resource.ConfigJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("route", resource.ConfigJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("metadata", resource.ConfigJson, StringComparison.OrdinalIgnoreCase);
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
            fixture.Factory,
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
            fixture.Factory,
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
                ResourceStorageSourceScopeKey.BuildFingerprint(storage)),
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
        private ResourcePersistenceFixture(TestDbContextFactory factory, Guid projectId)
        {
            Factory = factory;
            ProjectId = projectId;
        }

        public TestDbContextFactory Factory { get; }

        public Guid ProjectId { get; }

        public static async Task<ResourcePersistenceFixture> CreateAsync()
        {
            AppDbContextModelRegistry.ConfigureAssemblies(
                [typeof(Project).Assembly, typeof(ProjectResource).Assembly]);
            var options = AppDbContextTestOptionsBuilder.Create()
                .UseInMemoryDatabase($"storage-object-resource-{Guid.NewGuid():N}")
                .Options;
            var factory = new TestDbContextFactory(options);
            Guid projectId = Guid.NewGuid();
            await using AppDbContext dbContext = factory.CreateDbContext();
            dbContext.Set<Project>().Add(new Project { Id = projectId, Name = "Target project" });
            await dbContext.SaveChangesAsync();
            return new ResourcePersistenceFixture(factory, projectId);
        }

        public async Task<Guid> AddResourceAsync(StorageObjectResourceConfig config)
        {
            Guid resourceId = Guid.NewGuid();
            await using AppDbContext dbContext = Factory.CreateDbContext();
            dbContext.Set<ProjectResource>().Add(new ProjectResource
            {
                Id = resourceId,
                ProjectId = ProjectId,
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

    private sealed class TestDbContextFactory(DbContextOptions<AppDbContext> options) : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() => new(options);

        public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(CreateDbContext());
    }

    private sealed class ThrowingDbContextFactory : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() => throw new InvalidOperationException("DB must not be reached.");

        public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("DB must not be reached.");
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset GetUtcNow() => DateTimeOffset.Parse("2026-07-13T00:00:00Z");
    }

    private sealed class NullSearchIndex : ISearchIndexService
    {
        public Task UpsertAsync(SearchDocumentInput input, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task DeleteAsync(string sourceType, string sourceKey, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<SearchResult>> SearchAsync(
            string query,
            int take = 12,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<SearchResult>>([]);
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
