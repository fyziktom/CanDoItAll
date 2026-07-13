using Bunit;
using CanDoItAll.FileTools.FileBrowser;
using CanDoItAll.FileTools.FileInteraction;
using CanDoItAll.FileTools.Integration;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Resources;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class ResourceFileBrowsePaneTests
{
    [Fact]
    public void Catalog_renders_all_source_classes_including_empty_groups()
    {
        using var context = CreateContext(out ResourceBrowseTestState state);

        var cut = context.RenderComponent<ResourceFileBrowsePane>();

        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(cut.Find("[data-testid='resources-source-group-project']"));
            Assert.NotNull(cut.Find("[data-testid='resources-source-group-filesystem']"));
            Assert.NotNull(cut.Find("[data-testid='resources-source-group-ipfs']"));
            Assert.NotNull(cut.Find("[data-testid='resources-source-group-ftp']"));
            Assert.Contains("No authorized sources in this class.", cut.Markup, StringComparison.Ordinal);
            Assert.Contains(state.Source.DisplayName, cut.Markup, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task Invoked_file_opens_governed_promotion_dialog()
    {
        using var context = CreateContext(out ResourceBrowseTestState state);
        var cut = context.RenderComponent<ResourceFileBrowsePane>();
        cut.WaitForElement($"[data-testid='resources-source-{state.Source.Key.Value}'] button").Click();
        var file = cut.WaitForElement(".ft-file-browser__item-main");

        await file.DoubleClickAsync(new MouseEventArgs());

        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(cut.Find("[data-testid='resources-promotion-dialog']"));
            Assert.Equal("report.md", cut.Find("[data-testid='resources-promotion-name']").GetAttribute("value"));
            Assert.NotNull(cut.Find("[data-testid='resources-promotion-save']"));
            Assert.Equal(0, state.Writer.SaveCount);
        });
    }

    [Fact]
    public async Task Successful_promotion_refreshes_source_and_offers_authorized_reopen()
    {
        using var context = CreateContext(out ResourceBrowseTestState state);
        var cut = context.RenderComponent<ResourceFileBrowsePane>();
        cut.WaitForElement($"[data-testid='resources-source-{state.Source.Key.Value}'] button").Click();
        await cut.WaitForElement(".ft-file-browser__item-main").DoubleClickAsync(new MouseEventArgs());
        cut.WaitForElement("[data-testid='resources-promotion-save']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(1, state.Writer.SaveCount);
            Assert.Equal(1, state.Revisions.PublishCount);
            Assert.Equal(2, state.BrowseSessions.CreateCount);
            Assert.NotNull(cut.Find("[data-testid='resources-promotion-success']"));
            Assert.NotNull(cut.Find("[data-testid='resources-open-stored-object']"));
            Assert.Contains("Source revision is now 1", cut.Markup, StringComparison.Ordinal);
        });
    }

    private static TestContext CreateContext(out ResourceBrowseTestState state)
    {
        var context = new TestContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        state = new ResourceBrowseTestState();
        context.Services.AddSingleton<IResourceFileSourceCatalog>(state.SourceCatalog);
        context.Services.AddSingleton<IFileToolsBrowseSessionFactory>(state.BrowseSessions);
        context.Services.AddSingleton<ResourceFileBrowseCoordinator>();
        context.Services.AddSingleton<IFileToolsBrowseItemActivator>(state.ItemActivator);
        context.Services.AddSingleton<IFileAccessContextProvider>(state.ContextProvider);
        context.Services.AddSingleton<IStorageFileAccessAuthorizationCoordinator>(state.Authorization);
        context.Services.AddSingleton<IStorageObjectResourceWriter>(state.Writer);
        context.Services.AddSingleton<IFileCatalogChangeSink>(state.Revisions);
        context.Services.AddSingleton<IFileCatalogRevisionReader>(state.Revisions);
        context.Services.AddSingleton<ResourceStorageObjectPromotionService>();
        context.Services.AddSingleton<IDbContextFactory<AppDbContext>, ThrowingDbContextFactory>();
        context.Services.AddSingleton<IFileToolsKnownFileActivator, ThrowingKnownFileActivator>();
        context.Services.AddSingleton<IFileToolsKnownFileSessionFactory, ThrowingKnownFileSessionFactory>();
        context.Services.AddSingleton<IFileToolsKnownFileSessionReleaser, NoopKnownFileReleaser>();
        context.Services.AddSingleton<ResourceStorageObjectInteractionService>();
        return context;
    }

    private sealed class ResourceBrowseTestState
    {
        public ResourceBrowseTestState()
        {
            Storage = new StorageCatalogRecord
            {
                Id = Guid.NewGuid(),
                Name = "Local files",
                ProviderKind = StorageProviderKind.FileSystem,
                IsEnabled = true,
                CapabilityMask = StorageCapability.Read,
                HealthStatus = StorageHealthStatus.Healthy
            };
            var scope = new FileToolsSemanticScope(
                FileToolsSemanticScopeKind.ResourceSource,
                ResourceStorageSourceScopeKey.Create(
                    Storage.Id,
                    ResourceStorageSourceScopeKey.BuildFingerprint(Storage)),
                Storage.Name);
            Source = new ResourceFileSourceDescriptor(
                ResourceFileSourceKey.ForStorage(Storage.Id),
                ResourceFileSourceClass.FileSystem,
                Storage.Name,
                "Filesystem · Read enabled · Healthy",
                scope,
                Storage.Id,
                Storage.ProviderKind,
                false,
                Storage.HealthStatus);
            Project = new ResourcePromotionProject(Guid.NewGuid(), "Target project");
            SourceCatalog = new StaticSourceCatalog(Source, Project);
            BrowseSessions = new StaticBrowseSessionFactory();
            var file = new FileReference("authorized", "opaque-handle");
            var request = new FileToolsKnownFileRequest(scope, file, FileToolsKnownFileIntent.ReadOnly);
            ItemActivator = new StaticBrowseItemActivator(new FileToolsKnownFileActivation(
                request,
                "report.md",
                "text/markdown",
                12));
            ContextProvider = new StaticContextProvider();
            Authorization = new StaticAuthorizationCoordinator(new AuthorizedStorageFile(
                Storage,
                new StorageObjectReference(
                    Storage.Id,
                    Storage.ProviderKind,
                    StorageLocatorKind.RelativePath,
                    "folder/report.md",
                    "report.md",
                    "text/markdown",
                    12),
                scope,
                FileAccessOperation.View,
                null));
            Writer = new RecordingWriter();
            Revisions = new RecordingRevisions();
        }

        public StorageCatalogRecord Storage { get; }

        public ResourceFileSourceDescriptor Source { get; }

        public ResourcePromotionProject Project { get; }

        public StaticSourceCatalog SourceCatalog { get; }

        public StaticBrowseSessionFactory BrowseSessions { get; }

        public StaticBrowseItemActivator ItemActivator { get; }

        public StaticContextProvider ContextProvider { get; }

        public StaticAuthorizationCoordinator Authorization { get; }

        public RecordingWriter Writer { get; }

        public RecordingRevisions Revisions { get; }
    }

    private sealed class StaticSourceCatalog(
        ResourceFileSourceDescriptor source,
        ResourcePromotionProject project) : IResourceFileSourceCatalog
    {
        public Task<ResourceFileSourceCatalogSnapshot> LoadAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new ResourceFileSourceCatalogSnapshot([source], [project], "catalog-revision"));

        public Task<ResourceFileSourceDescriptor> ResolveAsync(
            ResourceFileSourceKey key,
            CancellationToken cancellationToken = default)
            => Task.FromResult(source);
    }

    private sealed class StaticBrowseSessionFactory : IFileToolsBrowseSessionFactory
    {
        public int CreateCount { get; private set; }

        public ValueTask<FileToolsBrowseSession> CreateAsync(
            FileToolsSemanticScope scope,
            CancellationToken cancellationToken = default)
        {
            CreateCount++;
            return ValueTask.FromResult(new FileToolsBrowseSession(
                scope,
                [new StaticFileBrowserProvider()],
                new FileBrowserSortDescriptor(
                    FileBrowserSortField.ProviderNative,
                    FileBrowserSortDirection.Ascending,
                    FoldersFirst: false),
                new FileToolsBrowseSessionRevision($"revision-{CreateCount}")));
        }
    }

    private sealed class StaticFileBrowserProvider : IFileBrowserProvider
    {
        private readonly FileBrowserItem root;
        private readonly FileBrowserItem file;

        public StaticFileBrowserProvider()
        {
            Descriptor = new FileBrowserSourceDescriptor(new FileBrowserSourceId("resource-files"), "Resource files");
            root = new FileBrowserItem(
                new FileBrowserItemKey(Descriptor.Id, "root", "r1"),
                null,
                "Root",
                FileBrowserItemKind.Container,
                FileBrowserItemCategory.Folder,
                childState: FileBrowserChildState.HasChildren,
                capabilities: FileBrowserItemCapabilities.Select | FileBrowserItemCapabilities.Navigate);
            file = new FileBrowserItem(
                new FileBrowserItemKey(Descriptor.Id, "report", "r1"),
                root.Key,
                "report.md",
                FileBrowserItemKind.File,
                FileBrowserItemCategory.Document,
                childState: FileBrowserChildState.Empty,
                size: 12,
                mediaType: "text/markdown",
                capabilities: FileBrowserItemCapabilities.Select | FileBrowserItemCapabilities.Open);
        }

        public FileBrowserSourceDescriptor Descriptor { get; }

        public ValueTask<FileBrowserItem> GetRootAsync(
            FileBrowserMetadataRequest metadata,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(root);

        public ValueTask<IReadOnlyList<FileBrowserItem>> GetPathAsync(
            FileBrowserItemKey itemKey,
            FileBrowserMetadataRequest metadata,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult<IReadOnlyList<FileBrowserItem>>([root]);

        public ValueTask<FileBrowserPage> BrowseAsync(
            FileBrowserBrowseRequest request,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(new FileBrowserPage([file], consistencyToken: "r1"));
    }

    private sealed class StaticBrowseItemActivator(FileToolsKnownFileActivation activation) : IFileToolsBrowseItemActivator
    {
        public ValueTask<FileToolsKnownFileActivation> ActivateAsync(
            FileToolsSemanticScope scope,
            FileBrowserItemKey itemKey,
            FileToolsKnownFileIntent intent,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(activation);
    }

    private sealed class StaticContextProvider : IFileAccessContextProvider
    {
        public ValueTask<FileAccessContext> GetCurrentAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult(new FileAccessContext(
                new FileAccessActorId("actor"),
                new FileAccessSessionId("session"),
                Guid.NewGuid(),
                1,
                0,
                new FileAccessCorrelationId("correlation")));
    }

    private sealed class StaticAuthorizationCoordinator(AuthorizedStorageFile authorized)
        : IStorageFileAccessAuthorizationCoordinator
    {
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
            => ValueTask.FromResult(authorized);

        public ValueTask RevokeAsync(FileReference file, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public ValueTask RevokeAllAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class RecordingWriter : IStorageObjectResourceWriter
    {
        public int SaveCount { get; private set; }

        public Task<StorageObjectResourceWriteResult> SaveAsync(
            StorageObjectResourceWriteRequest request,
            CancellationToken cancellationToken = default)
        {
            SaveCount++;
            return Task.FromResult(new StorageObjectResourceWriteResult(Guid.NewGuid(), true));
        }
    }

    private sealed class RecordingRevisions : IFileCatalogChangeSink, IFileCatalogRevisionReader
    {
        public int PublishCount { get; private set; }

        public FileCatalogRevision Get(FileToolsSemanticScope scope, Guid storageId)
            => new(0, PublishCount);

        public FileCatalogRevision PublishStorageChanged(Guid storageId)
            => throw new NotSupportedException();

        public FileCatalogRevision PublishScopeChanged(FileToolsSemanticScope scope, Guid storageId)
        {
            PublishCount++;
            return new FileCatalogRevision(0, PublishCount);
        }
    }

    private sealed class ThrowingDbContextFactory : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() => throw new NotSupportedException();

        public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class ThrowingKnownFileActivator : IFileToolsKnownFileActivator
    {
        public ValueTask<FileToolsKnownFileActivation> ActivateAsync(
            FileToolsSemanticScope scope,
            FileToolsKnownFileOccurrence occurrence,
            FileToolsKnownFileIntent intent,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class ThrowingKnownFileSessionFactory : IFileToolsKnownFileSessionFactory
    {
        public ValueTask<FileToolsKnownFileSession> CreateAsync(
            FileToolsKnownFileRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class NoopKnownFileReleaser : IFileToolsKnownFileSessionReleaser
    {
        public ValueTask ReleaseAsync(FileReference file, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;
    }
}
