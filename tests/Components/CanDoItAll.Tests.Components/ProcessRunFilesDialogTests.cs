using Bunit;
using CanDoItAll.FileTools.FileBrowser;
using CanDoItAll.FileTools.FileInteraction;
using CanDoItAll.FileTools.Integration;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Processes.Application;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class ProcessRunFilesDialogTests
{
    [Fact]
    public void Refresh_re_resolves_scope_and_re_enumerates_mutable_run_files()
    {
        using var context = new TestContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        Guid runId = Guid.NewGuid();
        var files = new MutableFileSet(["before.txt"]);
        var scopeProvider = new RecordingScopeProvider(runId);
        var sessionFactory = new MutableBrowseSessionFactory(files);
        RegisterServices(context, scopeProvider, sessionFactory);

        var cut = context.RenderComponent<ProcessRunFilesDialog>(parameters => parameters
            .Add(component => component.IsOpen, true)
            .Add(component => component.RunId, runId));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("before.txt", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("Always current", cut.Markup, StringComparison.Ordinal);
            Assert.NotNull(cut.Find("[data-testid='process-run-files-refresh']"));
        });
        files.Names = ["after.txt"];

        cut.Find("[data-testid='process-run-files-refresh']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("after.txt", cut.Markup, StringComparison.Ordinal);
            Assert.DoesNotContain("before.txt", cut.Markup, StringComparison.Ordinal);
            Assert.Equal(2, scopeProvider.ResolveCalls);
            Assert.Equal(2, sessionFactory.CreateCalls);
        });
    }

    [Fact]
    public void Forbidden_scope_renders_explicit_error_and_retry_re_resolves()
    {
        using var context = new TestContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        var scopeProvider = new ThrowingScopeProvider();
        RegisterServices(context, scopeProvider, new MutableBrowseSessionFactory(new MutableFileSet([])));

        var cut = context.RenderComponent<ProcessRunFilesDialog>(parameters => parameters
            .Add(component => component.IsOpen, true)
            .Add(component => component.RunId, Guid.NewGuid()));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("The process-run root is not authorized.", cut.Markup, StringComparison.Ordinal);
            Assert.NotNull(cut.Find("[data-testid='process-run-files-retry']"));
        });
        cut.Find("[data-testid='process-run-files-retry']").Click();

        cut.WaitForAssertion(() => Assert.Equal(2, scopeProvider.ResolveCalls));
    }

    private static void RegisterServices(
        TestContext context,
        IProcessRunFileScopeProvider scopeProvider,
        IFileToolsBrowseSessionFactory sessionFactory)
    {
        context.Services.AddLogging();
        context.Services.AddSingleton(scopeProvider);
        context.Services.AddSingleton(sessionFactory);
        context.Services.AddSingleton<IFileToolsBrowseItemActivator, ThrowingBrowseItemActivator>();
        context.Services.AddSingleton<IFileToolsKnownFileSessionFactory, ThrowingKnownFileSessionFactory>();
        context.Services.AddSingleton<IFileToolsKnownFileSessionReleaser, NoopKnownFileSessionReleaser>();
        context.Services.AddSingleton<IFileToolsBrowseItemActionService, UnavailableFileToolsBrowseItemActionService>();
        context.Services.AddSingleton<ProcessRunFilesCoordinator>();
    }

    private sealed class MutableFileSet(IReadOnlyList<string> names)
    {
        public IReadOnlyList<string> Names { get; set; } = names;
    }

    private sealed class RecordingScopeProvider(Guid runId) : IProcessRunFileScopeProvider
    {
        private readonly FileToolsSemanticScope scope = new(
            FileToolsSemanticScopeKind.ProcessRun,
            new FileToolsSemanticScopeId($"run:v1:{runId:N}:{new string('a', 64)}"),
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
                $"fingerprint-{ResolveCalls}"));
        }
    }

    private sealed class ThrowingScopeProvider : IProcessRunFileScopeProvider
    {
        public int ResolveCalls { get; private set; }

        public ValueTask<ProcessRunFileScopeSet> ResolveAsync(
            Guid runId,
            CancellationToken cancellationToken = default)
        {
            ResolveCalls++;
            return ValueTask.FromException<ProcessRunFileScopeSet>(new FileBrowserProviderException(
                new FileBrowserError(FileBrowserErrorCode.Forbidden, "The process-run root is not authorized.")));
        }
    }

    private sealed class MutableBrowseSessionFactory(MutableFileSet files) : IFileToolsBrowseSessionFactory
    {
        public int CreateCalls { get; private set; }

        public ValueTask<FileToolsBrowseSession> CreateAsync(
            FileToolsSemanticScope scope,
            CancellationToken cancellationToken = default)
        {
            CreateCalls++;
            return ValueTask.FromResult(new FileToolsBrowseSession(
                scope,
                [new MutableFileBrowserProvider(files)],
                new FileBrowserSortDescriptor(
                    FileBrowserSortField.ProviderNative,
                    FileBrowserSortDirection.Ascending,
                    FoldersFirst: false),
                new FileToolsBrowseSessionRevision($"revision-{CreateCalls}")));
        }
    }

    private sealed class MutableFileBrowserProvider(MutableFileSet files) : IFileBrowserProvider
    {
        private readonly FileBrowserItem root = new(
            new FileBrowserItemKey(new FileBrowserSourceId("process-run-files"), "root", "current"),
            parentKey: null,
            "Run artifacts",
            FileBrowserItemKind.Container,
            FileBrowserItemCategory.Folder,
            childState: FileBrowserChildState.HasChildren,
            capabilities: FileBrowserItemCapabilities.Select | FileBrowserItemCapabilities.Navigate);

        public FileBrowserSourceDescriptor Descriptor { get; } = new(
            new FileBrowserSourceId("process-run-files"),
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
            FileBrowserItem[] items = files.Names
                .Select(name => new FileBrowserItem(
                    new FileBrowserItemKey(Descriptor.Id, name, "current"),
                    root.Key,
                    name,
                    FileBrowserItemKind.File,
                    FileBrowserItemCategory.Document,
                    childState: FileBrowserChildState.Empty,
                    mediaType: "text/plain",
                    capabilities: FileBrowserItemCapabilities.Select | FileBrowserItemCapabilities.Open))
                .ToArray();
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
