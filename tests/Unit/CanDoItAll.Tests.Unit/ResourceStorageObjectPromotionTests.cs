using CanDoItAll.FileTools.FileBrowser;
using CanDoItAll.FileTools.FileInteraction;
using CanDoItAll.FileTools.Integration;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Resources;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Unit;

public sealed class ResourceStorageObjectPromotionTests
{
    [Fact]
    public async Task Promote_reauthorizes_then_persists_curated_identity_then_publishes_revision()
    {
        var events = new List<string>();
        PromotionFixture fixture = PromotionFixture.Create(events);

        ResourceStorageObjectPromotionResult result = await fixture.Service.PromoteAsync(fixture.Command);

        Assert.True(result.Created);
        Assert.Equal(1, result.Revision.Scope);
        Assert.Equal(["activate", "context", "authorize", "save", "publish", "revoke"], events);
        StorageObjectResourceConfig config = Assert.IsType<StorageObjectResourceConfig>(fixture.Writer.Request?.Config);
        Assert.Equal(fixture.Source.Key.Value, config.SourceKey);
        Assert.Equal(fixture.Storage.Id, config.StorageId);
        Assert.Equal(StorageLocatorKind.RelativePath, config.LocatorKind);
        Assert.Equal("folder/report.md", config.Locator);
        Assert.Equal("report.md", config.DisplayName);
        string persisted = StorageObjectResourceConnectorPlugin.Serialize(config);
        Assert.DoesNotContain("opaque-handle", persisted, StringComparison.Ordinal);
        Assert.DoesNotContain("DisplayPath", persisted, StringComparison.Ordinal);
        Assert.DoesNotContain("Metadata", persisted, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(StorageProviderKind.Ipfs, StorageLocatorKind.ContentAddress, "bafy-promoted")]
    [InlineData(StorageProviderKind.Ipfs, StorageLocatorKind.RemotePath, "mfs/report.md")]
    [InlineData(StorageProviderKind.Ftp, StorageLocatorKind.RemotePath, "reports/report.md")]
    public async Task Promote_preserves_provider_native_stable_locator(
        StorageProviderKind providerKind,
        StorageLocatorKind locatorKind,
        string locator)
    {
        PromotionFixture fixture = PromotionFixture.Create([], providerKind, locatorKind, locator);

        await fixture.Service.PromoteAsync(fixture.Command);

        StorageObjectResourceConfig config = Assert.IsType<StorageObjectResourceConfig>(fixture.Writer.Request?.Config);
        Assert.Equal(providerKind, config.ProviderKind);
        Assert.Equal(locatorKind, config.LocatorKind);
        Assert.Equal(locator, config.Locator);
    }

    [Fact]
    public async Task Promote_persistence_failure_leaves_revision_unchanged_and_revokes_handle()
    {
        var events = new List<string>();
        PromotionFixture fixture = PromotionFixture.Create(events);
        fixture.Writer.Exception = new InvalidOperationException("database unavailable");

        ResourcePromotionException exception = await Assert.ThrowsAsync<ResourcePromotionException>(
            async () => await fixture.Service.PromoteAsync(fixture.Command));

        Assert.Equal(ResourcePromotionFailureCode.PersistenceFailed, exception.Code);
        Assert.Equal(0, fixture.Revisions.PublishCount);
        Assert.Equal(["activate", "context", "authorize", "save", "revoke"], events);
    }

    [Fact]
    public async Task Promote_cancellation_before_persistence_completion_leaves_revision_unchanged()
    {
        var events = new List<string>();
        PromotionFixture fixture = PromotionFixture.Create(events);
        fixture.Writer.Exception = new OperationCanceledException();

        await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await fixture.Service.PromoteAsync(fixture.Command));

        Assert.Equal(0, fixture.Revisions.PublishCount);
        Assert.Equal(["activate", "context", "authorize", "save", "revoke"], events);
    }

    [Fact]
    public async Task Promote_cross_actor_handle_is_rejected_without_persistence_or_revision()
    {
        var events = new List<string>();
        PromotionFixture fixture = PromotionFixture.Create(events);
        fixture.Authorization.Exception = new FileAccessDeniedException(
            FileAccessFailureCode.ContextMismatch,
            "actor mismatch");

        ResourcePromotionException exception = await Assert.ThrowsAsync<ResourcePromotionException>(
            async () => await fixture.Service.PromoteAsync(fixture.Command));

        Assert.Equal(ResourcePromotionFailureCode.Unauthorized, exception.Code);
        Assert.Null(fixture.Writer.Request);
        Assert.Equal(0, fixture.Revisions.PublishCount);
        Assert.Equal(["activate", "context", "authorize", "revoke"], events);
    }

    [Fact]
    public async Task Promote_forged_or_stale_browser_item_is_rejected_before_handle_creation()
    {
        var events = new List<string>();
        PromotionFixture fixture = PromotionFixture.Create(events);
        fixture.Activator.Exception = new FileBrowserProviderException(new FileBrowserError(
            FileBrowserErrorCode.Conflict,
            "stale item"));

        ResourcePromotionException exception = await Assert.ThrowsAsync<ResourcePromotionException>(
            async () => await fixture.Service.PromoteAsync(fixture.Command));

        Assert.Equal(ResourcePromotionFailureCode.SelectionChanged, exception.Code);
        Assert.Null(fixture.Writer.Request);
        Assert.Equal(0, fixture.Revisions.PublishCount);
        Assert.Equal(["activate"], events);
    }

    [Fact]
    public async Task Promote_authorized_object_from_another_storage_is_rejected_and_revoked()
    {
        var events = new List<string>();
        PromotionFixture fixture = PromotionFixture.Create(events);
        fixture.Authorization.Authorized = fixture.Authorization.Authorized! with
        {
            Storage = CreateStorage(Guid.NewGuid())
        };

        ResourcePromotionException exception = await Assert.ThrowsAsync<ResourcePromotionException>(
            async () => await fixture.Service.PromoteAsync(fixture.Command));

        Assert.Equal(ResourcePromotionFailureCode.SelectionChanged, exception.Code);
        Assert.Null(fixture.Writer.Request);
        Assert.Equal(0, fixture.Revisions.PublishCount);
        Assert.Equal(["activate", "context", "authorize", "revoke"], events);
    }

    private static StorageCatalogRecord CreateStorage(
        Guid id,
        StorageProviderKind providerKind = StorageProviderKind.FileSystem)
        => new()
        {
            Id = id,
            Name = "Filesystem",
            ProviderKind = providerKind,
            IsEnabled = true,
            CapabilityMask = StorageCapability.Read
        };

    private sealed class PromotionFixture
    {
        private PromotionFixture(
            ResourceStorageObjectPromotionService service,
            ResourceFileSourceDescriptor source,
            StorageCatalogRecord storage,
            RecordingBrowseItemActivator activator,
            RecordingAuthorizationCoordinator authorization,
            RecordingWriter writer,
            RecordingRevisions revisions,
            ResourceStorageObjectPromotionCommand command)
        {
            Service = service;
            Source = source;
            Storage = storage;
            Activator = activator;
            Authorization = authorization;
            Writer = writer;
            Revisions = revisions;
            Command = command;
        }

        public ResourceStorageObjectPromotionService Service { get; }

        public ResourceFileSourceDescriptor Source { get; }

        public StorageCatalogRecord Storage { get; }

        public RecordingBrowseItemActivator Activator { get; }

        public RecordingAuthorizationCoordinator Authorization { get; }

        public RecordingWriter Writer { get; }

        public RecordingRevisions Revisions { get; }

        public ResourceStorageObjectPromotionCommand Command { get; }

        public static PromotionFixture Create(
            List<string> events,
            StorageProviderKind providerKind = StorageProviderKind.FileSystem,
            StorageLocatorKind locatorKind = StorageLocatorKind.RelativePath,
            string locator = "folder/report.md")
        {
            StorageCatalogRecord storage = CreateStorage(Guid.NewGuid(), providerKind);
            ResourceFileSourceKey sourceKey = ResourceFileSourceKey.ForStorage(storage.Id);
            var scope = new FileToolsSemanticScope(
                FileToolsSemanticScopeKind.ResourceSource,
                ResourceStorageSourceScopeKey.Create(
                    storage.Id,
                    ResourceStorageSourceScopeKey.BuildFingerprint(storage)),
                storage.Name);
            var source = new ResourceFileSourceDescriptor(
                sourceKey,
                providerKind switch
                {
                    StorageProviderKind.FileSystem => ResourceFileSourceClass.FileSystem,
                    StorageProviderKind.Ipfs => ResourceFileSourceClass.Ipfs,
                    StorageProviderKind.Ftp => ResourceFileSourceClass.Ftp,
                    _ => throw new ArgumentOutOfRangeException(nameof(providerKind))
                },
                storage.Name,
                "Filesystem",
                scope,
                storage.Id,
                storage.ProviderKind,
                false,
                StorageHealthStatus.Healthy);
            var itemKey = new FileBrowserItemKey(new FileBrowserSourceId("storage-source"), "item-key");
            var file = new FileReference("authorized", "opaque-handle");
            var request = new FileToolsKnownFileRequest(scope, file, FileToolsKnownFileIntent.ReadOnly);
            var activation = new FileToolsKnownFileActivation(request, "report.md", "text/markdown", 12);
            var activator = new RecordingBrowseItemActivator(events, activation);
            var reference = new StorageObjectReference(
                storage.Id,
                storage.ProviderKind,
                locatorKind,
                locator,
                "report.md",
                "text/markdown",
                12,
                Route: "must-not-persist",
                MetadataJson: "{\"secret\":\"must-not-persist\"}");
            var authorization = new RecordingAuthorizationCoordinator(
                events,
                new AuthorizedStorageFile(storage, reference, scope, FileAccessOperation.View, null));
            var writer = new RecordingWriter(events);
            var revisions = new RecordingRevisions(events);
            var service = new ResourceStorageObjectPromotionService(
                new StaticSourceCatalog(source),
                activator,
                new RecordingContextProvider(events),
                authorization,
                writer,
                revisions,
                revisions,
                NullLogger<ResourceStorageObjectPromotionService>.Instance);
            var command = new ResourceStorageObjectPromotionCommand(
                sourceKey,
                itemKey,
                Guid.NewGuid(),
                "Promoted report");
            return new PromotionFixture(
                service,
                source,
                storage,
                activator,
                authorization,
                writer,
                revisions,
                command);
        }
    }

    private sealed class StaticSourceCatalog(ResourceFileSourceDescriptor source) : IResourceFileSourceCatalog
    {
        public Task<ResourceFileSourceCatalogSnapshot> LoadAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new ResourceFileSourceCatalogSnapshot([source], [], "fingerprint"));

        public Task<ResourceFileSourceDescriptor> ResolveAsync(
            ResourceFileSourceKey key,
            CancellationToken cancellationToken = default)
            => key == source.Key
                ? Task.FromResult(source)
                : Task.FromException<ResourceFileSourceDescriptor>(new InvalidOperationException("missing"));
    }

    private sealed class RecordingBrowseItemActivator(
        List<string> events,
        FileToolsKnownFileActivation activation) : IFileToolsBrowseItemActivator
    {
        public Exception? Exception { get; set; }

        public ValueTask<FileToolsKnownFileActivation> ActivateAsync(
            FileToolsSemanticScope scope,
            FileBrowserItemKey itemKey,
            FileToolsKnownFileIntent intent,
            CancellationToken cancellationToken = default)
        {
            events.Add("activate");
            return Exception is null
                ? ValueTask.FromResult(activation)
                : ValueTask.FromException<FileToolsKnownFileActivation>(Exception);
        }
    }

    private sealed class RecordingContextProvider(List<string> events) : IFileAccessContextProvider
    {
        public ValueTask<FileAccessContext> GetCurrentAsync(CancellationToken cancellationToken = default)
        {
            events.Add("context");
            return ValueTask.FromResult(new FileAccessContext(
                new FileAccessActorId("current-actor"),
                new FileAccessSessionId("session"),
                Guid.NewGuid(),
                1,
                0,
                new FileAccessCorrelationId("correlation")));
        }
    }

    private sealed class RecordingAuthorizationCoordinator(
        List<string> events,
        AuthorizedStorageFile authorized) : IStorageFileAccessAuthorizationCoordinator
    {
        public AuthorizedStorageFile? Authorized { get; set; } = authorized;

        public Exception? Exception { get; set; }

        public ValueTask<FileReference> GrantAsync(
            FileAccessGrantRequest request,
            StorageObjectReference reference,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public ValueTask<AuthorizedStorageFile> ResolveAsync(
            FileReference file,
            FileAccessContext context,
            FileAccessOperation operation,
            CancellationToken cancellationToken = default)
        {
            events.Add("authorize");
            return Exception is null
                ? ValueTask.FromResult(Authorized!)
                : ValueTask.FromException<AuthorizedStorageFile>(Exception);
        }

        public ValueTask RevokeAsync(FileReference file, CancellationToken cancellationToken = default)
        {
            events.Add("revoke");
            return ValueTask.CompletedTask;
        }

        public ValueTask RevokeAllAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class RecordingWriter(List<string> events) : IStorageObjectResourceWriter
    {
        public StorageObjectResourceWriteRequest? Request { get; private set; }

        public Exception? Exception { get; set; }

        public Task<StorageObjectResourceWriteResult> SaveAsync(
            StorageObjectResourceWriteRequest request,
            CancellationToken cancellationToken = default)
        {
            events.Add("save");
            Request = request;
            return Exception is null
                ? Task.FromResult(new StorageObjectResourceWriteResult(Guid.NewGuid(), true))
                : Task.FromException<StorageObjectResourceWriteResult>(Exception);
        }
    }

    private sealed class RecordingRevisions(List<string> events) : IFileCatalogChangeSink, IFileCatalogRevisionReader
    {
        private FileCatalogRevision revision = new(0, 0);

        public int PublishCount { get; private set; }

        public FileCatalogRevision Get(FileToolsSemanticScope scope, Guid storageId) => revision;

        public FileCatalogRevision PublishStorageChanged(Guid storageId)
            => throw new NotSupportedException();

        public FileCatalogRevision PublishScopeChanged(FileToolsSemanticScope scope, Guid storageId)
        {
            events.Add("publish");
            PublishCount++;
            revision = new FileCatalogRevision(revision.Storage, revision.Scope + 1);
            return revision;
        }
    }
}
