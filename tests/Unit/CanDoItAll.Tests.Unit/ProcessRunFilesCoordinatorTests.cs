using CanDoItAll.FileTools.FileBrowser;
using CanDoItAll.FileTools.FileInteraction;
using CanDoItAll.FileTools.Integration;
using CanDoItAll.Modules.Processes;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessRunFilesCoordinatorTests
{
    [Fact]
    public async Task Open_and_refresh_re_enumerate_mutable_provider_without_retained_listing()
    {
        Guid runId = Guid.NewGuid();
        var scopeProvider = new RecordingScopeProvider(runId);
        var browseProvider = new MutableBrowseProvider(["before.txt"]);
        var coordinator = new ProcessRunFilesCoordinator(
            scopeProvider,
            new StaticBrowseSessionFactory(browseProvider),
            new ThrowingBrowseItemActivator(),
            new ThrowingKnownFileSessionFactory(),
            new NoopKnownFileSessionReleaser(),
            NullLogger<ProcessRunFilesCoordinator>.Instance);
        await using ProcessRunFileWorkspace workspace = await coordinator.OpenAsync(runId);

        await workspace.Browser.InitializeAsync(browseProvider.Descriptor.Id);
        Assert.Contains(workspace.Browser.Snapshot.Items, item => item.Name == "before.txt");
        browseProvider.Names = ["after.txt"];
        await workspace.Browser.RefreshAsync();

        Assert.Contains(workspace.Browser.Snapshot.Items, item => item.Name == "after.txt");
        Assert.DoesNotContain(workspace.Browser.Snapshot.Items, item => item.Name == "before.txt");
        Assert.Equal(2, browseProvider.BrowseCalls);
        Assert.Equal(1, scopeProvider.ResolveCalls);
    }

    [Fact]
    public async Task Each_open_re_resolves_current_run_scope()
    {
        Guid runId = Guid.NewGuid();
        var scopeProvider = new RecordingScopeProvider(runId);
        var coordinator = new ProcessRunFilesCoordinator(
            scopeProvider,
            new StaticBrowseSessionFactory(new MutableBrowseProvider([])),
            new ThrowingBrowseItemActivator(),
            new ThrowingKnownFileSessionFactory(),
            new NoopKnownFileSessionReleaser(),
            NullLogger<ProcessRunFilesCoordinator>.Instance);

        await using ProcessRunFileWorkspace first = await coordinator.OpenAsync(runId);
        await using ProcessRunFileWorkspace second = await coordinator.OpenAsync(runId);

        Assert.Equal(2, scopeProvider.ResolveCalls);
        Assert.NotEqual(first.Revision, second.Revision);
    }

    private sealed class RecordingScopeProvider(Guid runId) : IProcessRunFileScopeProvider
    {
        private readonly FileToolsSemanticScope scope = new(
            FileToolsSemanticScopeKind.ProcessRun,
            new FileToolsSemanticScopeId($"run:v1:{runId:N}:{new string('b', 64)}"),
            "Run artifacts");

        public int ResolveCalls { get; private set; }

        public ValueTask<ProcessRunFileScopeSet> ResolveAsync(
            Guid requestedRunId,
            CancellationToken cancellationToken = default)
        {
            ResolveCalls++;
            return ValueTask.FromResult(new ProcessRunFileScopeSet(
                runId,
                [scope],
                $"scope-revision-{ResolveCalls}"));
        }
    }

    private sealed class StaticBrowseSessionFactory(MutableBrowseProvider provider) : IFileToolsBrowseSessionFactory
    {
        private int createCalls;

        public ValueTask<FileToolsBrowseSession> CreateAsync(
            FileToolsSemanticScope scope,
            CancellationToken cancellationToken = default)
        {
            createCalls++;
            return ValueTask.FromResult(new FileToolsBrowseSession(
                scope,
                [provider],
                new FileBrowserSortDescriptor(
                    FileBrowserSortField.ProviderNative,
                    FileBrowserSortDirection.Ascending,
                    FoldersFirst: false),
                new FileToolsBrowseSessionRevision($"provider-revision-{createCalls}")));
        }
    }

    private sealed class MutableBrowseProvider(IReadOnlyList<string> names) : IFileBrowserProvider
    {
        private readonly FileBrowserItem root = new(
            new FileBrowserItemKey(new FileBrowserSourceId("run-source"), "root", "current"),
            parentKey: null,
            "Run artifacts",
            FileBrowserItemKind.Container,
            FileBrowserItemCategory.Folder,
            childState: FileBrowserChildState.HasChildren,
            capabilities: FileBrowserItemCapabilities.Select | FileBrowserItemCapabilities.Navigate);

        public IReadOnlyList<string> Names { get; set; } = names;

        public int BrowseCalls { get; private set; }

        public FileBrowserSourceDescriptor Descriptor { get; } = new(
            new FileBrowserSourceId("run-source"),
            "Run artifacts");

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
        {
            BrowseCalls++;
            FileBrowserItem[] items = Names.Select(name => new FileBrowserItem(
                new FileBrowserItemKey(Descriptor.Id, name, "current"),
                root.Key,
                name,
                FileBrowserItemKind.File,
                FileBrowserItemCategory.Document,
                childState: FileBrowserChildState.Empty,
                mediaType: "text/plain",
                capabilities: FileBrowserItemCapabilities.Select | FileBrowserItemCapabilities.Open)).ToArray();
            return ValueTask.FromResult(new FileBrowserPage(items, consistencyToken: "current"));
        }
    }

    private sealed class ThrowingBrowseItemActivator : IFileToolsBrowseItemActivator
    {
        public ValueTask<FileToolsKnownFileActivation> ActivateAsync(
            FileToolsSemanticScope scope,
            FileBrowserItemKey itemKey,
            FileToolsKnownFileIntent intent,
            CancellationToken cancellationToken = default)
            => ValueTask.FromException<FileToolsKnownFileActivation>(new InvalidOperationException("Unexpected activation."));
    }

    private sealed class ThrowingKnownFileSessionFactory : IFileToolsKnownFileSessionFactory
    {
        public ValueTask<FileToolsKnownFileSession> CreateAsync(
            FileToolsKnownFileRequest request,
            CancellationToken cancellationToken = default)
            => ValueTask.FromException<FileToolsKnownFileSession>(new InvalidOperationException("Unexpected session."));
    }

    private sealed class NoopKnownFileSessionReleaser : IFileToolsKnownFileSessionReleaser
    {
        public ValueTask ReleaseAsync(
            FileReference file,
            CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;
    }
}
