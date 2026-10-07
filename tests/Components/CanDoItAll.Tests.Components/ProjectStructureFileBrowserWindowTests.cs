using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.FileTools.FileBrowser;
using CanDoItAll.FileTools.FileBrowser.Components;
using CanDoItAll.FileTools.FileInteraction;
using CanDoItAll.FileTools.FileInteraction.Components;
using CanDoItAll.FileTools.FileInteraction.Markdown;
using CanDoItAll.FileTools.FileInteraction.Spreadsheet;
using CanDoItAll.FileTools.Integration;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.Pages;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.ProjectStructure;

public sealed class ProjectStructureFileBrowserWindowTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_late_collection_activation_releases_its_own_grant_without_touching_the_successor(bool fail) {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        var project = Guid.NewGuid();
        var scope = new FileToolsSemanticScope(FileToolsSemanticScopeKind.Project, new(project.ToString("N")), "Files");
        var factory = new HeldKnownFileSessionFactory();
        var releaser = new RecordingKnownFileSessionReleaser();
        RegisterServices(context, new(project, [scope], new string('c', 64)), new ThrowingNodeFileScopeProvider(),
            new StaticBrowseItemActivator(), factory, sessionReleaser: releaser);
        var request = new ProjectStructureProjectFileCollectionRequest(project, "Same files");
        var cut = context.Render<ProjectStructureFileBrowserWindow>(parameters => parameters
            .Add(component => component.WindowId, "test-files")
            .Add(component => component.Opening, new ProjectStructureFileCollectionOpening(request, _ => Task.CompletedTask))
            .Add(component => component.State, new CanvasWorkbenchWindowState { IsVisible = true }));
        cut.WaitForElement(".ft-file-browser__item-main");
        var original = cut.FindComponent<FileBrowser>().Instance;
        var pending = cut.InvokeAsync(() => original.ItemInvoked.InvokeAsync(new(
            Assert.Single(original.Session.Snapshot.Items), FileBrowserInvocationKind.PointerDoubleClick)));
        await factory.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await cut.InvokeAsync(() => cut.Render(parameters => parameters.Add(component => component.Opening,
            new ProjectStructureFileCollectionOpening(request, _ => Task.CompletedTask))));
        await cut.WaitForElement(".ft-file-browser__item-main").DoubleClickAsync(new MouseEventArgs());
        var accepted = Assert.IsType<FileInteractionRequest>(cut.FindComponent<FileInteraction>().Instance.Request).File;
        if (fail) {
            factory.Finish.SetException(new IOException("Retired activation failed."));
        } else {
            factory.Finish.SetResult();
        }
        await pending;

        Assert.Equal(accepted, Assert.IsType<FileInteractionRequest>(cut.FindComponent<FileInteraction>().Instance.Request).File);
        Assert.Equal(factory.OriginalFile, Assert.Single(releaser.Released));
        Assert.NotEqual(accepted, factory.OriginalFile);
        Assert.Empty(cut.FindAll("[data-testid='project-structure-file-browser-activation-error']"));
        await cut.InvokeAsync(async () => await cut.Instance.DisposeAsync());
        Assert.Equal(2, releaser.Released.Count);
        Assert.Single(releaser.Released, file => file == accepted);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Reopening_the_same_request_fences_a_delayed_scope_result_or_ordinary_error(bool fail) {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        var project = Guid.NewGuid();
        var scope = new FileToolsSemanticScope(FileToolsSemanticScopeKind.ProjectNode, new("original-scope"), "Files");
        var scopes = new HeldThenCurrentNodeScopeProvider(scope);
        RegisterServices(context, new(project, [new(FileToolsSemanticScopeKind.Project, new(project.ToString("N")), "Project")], new string('a', 64)), scopes);
        var request = new ProjectStructureNodeFileCollectionRequest(project, "same-node", "Same public request");
        var first = new ProjectStructureFileCollectionOpening(request, _ => Task.CompletedTask);
        var cut = context.Render<ProjectStructureFileBrowserWindow>(parameters => parameters
            .Add(component => component.WindowId, "test-files")
            .Add(component => component.Opening, first)
            .Add(component => component.State, new CanvasWorkbenchWindowState { IsVisible = true }));
        await scopes.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var second = new ProjectStructureFileCollectionOpening(request, _ => Task.CompletedTask);
        await cut.InvokeAsync(() => cut.Render(parameters => parameters.Add(component => component.Opening, second)));
        cut.WaitForAssertion(() => Assert.Single(cut.FindComponents<FileBrowser>()));
        var accepted = cut.FindComponent<FileBrowser>().Instance.Session;
        if (fail) {
            scopes.Finish.SetException(new IOException("Retired ordinary scope failure"));
        } else {
            scopes.Finish.SetResult(scope);
        }
        await scopes.Returned.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await cut.InvokeAsync(() => Task.CompletedTask);
        cut.WaitForAssertion(() => {
            Assert.Same(accepted, cut.FindComponent<FileBrowser>().Instance.Session);
            Assert.Empty(cut.FindAll("[data-testid='project-structure-file-browser-loading']"));
            Assert.DoesNotContain("Unable to open this file collection", cut.Markup, StringComparison.Ordinal);
        });
        Assert.Equal(2, scopes.Calls);
    }

    [Fact]
    public async Task A_retired_browser_callback_cannot_acquire_a_successor_file_grant() {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        var project = Guid.NewGuid();
        var scope = new FileToolsSemanticScope(FileToolsSemanticScopeKind.Project, new(project.ToString("N")), "Files");
        var activator = new StaticBrowseItemActivator();
        RegisterServices(context, new(project, [scope], new string('b', 64)), new ThrowingNodeFileScopeProvider(), activator, new StaticKnownFileSessionFactory());
        var request = new ProjectStructureProjectFileCollectionRequest(project, "Files");
        var cut = context.Render<ProjectStructureFileBrowserWindow>(parameters => parameters
            .Add(component => component.WindowId, "test-files")
            .Add(component => component.Opening, new ProjectStructureFileCollectionOpening(request, _ => Task.CompletedTask))
            .Add(component => component.State, new CanvasWorkbenchWindowState { IsVisible = true }));
        cut.WaitForElement(".ft-file-browser__item-main");
        var first = cut.FindComponent<FileBrowser>().Instance;
        var invoked = first.ItemInvoked;
        var item = Assert.Single(first.Session.Snapshot.Items);
        await cut.InvokeAsync(() => cut.Render(parameters => parameters.Add(component => component.Opening,
            new ProjectStructureFileCollectionOpening(request, _ => Task.CompletedTask))));
        await cut.InvokeAsync(() => invoked.InvokeAsync(new(item, FileBrowserInvocationKind.PointerDoubleClick)));
        Assert.Equal(0, activator.Calls);
        Assert.Empty(cut.FindComponents<FileInteraction>());
    }

    [Fact]
    public void Node_collection_window_shows_loading_state_while_authorization_is_pending()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        Guid projectId = Guid.NewGuid();
        var scope = new FileToolsSemanticScope(
            FileToolsSemanticScopeKind.ProjectNode,
            new FileToolsSemanticScopeId("node:v2:collection:11111111111111111111111111111111:Zm9sZGVy"),
            "Run artifacts");
        var nodeScopeProvider = new DeferredNodeFileScopeProvider();
        RegisterServices(
            context,
            new ProjectFileScopeSet(
                projectId,
                [
                    new FileToolsSemanticScope(
                        FileToolsSemanticScopeKind.Project,
                        new FileToolsSemanticScopeId(projectId.ToString("N")),
                        "Delivery")
                ],
                new string('a', 64)),
            nodeScopeProvider);

        var cut = context.Render<ProjectStructureFileBrowserWindow>(parameters => parameters
            .Add(component => component.WindowId, "project-structure.fileBrowser")
            .Add(
                component => component.Opening,
                new ProjectStructureFileCollectionOpening(new ProjectStructureNodeFileCollectionRequest(projectId, "run-output", "Run artifacts"), _ => Task.CompletedTask))
            .Add(component => component.State, new CanvasWorkbenchWindowState { IsVisible = true }));

        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(cut.Find("[data-testid='project-structure-file-browser-window']"));
            Assert.NotNull(cut.Find("[data-testid='project-structure-file-browser-loading'][aria-busy='true']"));
            Assert.Single(cut.FindComponents<LoadingState>());
            Assert.Empty(cut.FindComponents<FileBrowser>());
            Assert.Contains("animate-spin", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("Resolving authorized sources", cut.Markup, StringComparison.Ordinal);
        });

        nodeScopeProvider.Complete(scope);

        cut.WaitForAssertion(() =>
        {
            Assert.Empty(cut.FindAll("[data-testid='project-structure-file-browser-loading']"));
            Assert.Empty(cut.FindComponents<LoadingState>());
            Assert.Single(cut.FindComponents<FileBrowser>());
        });
    }

    [Fact]
    public void Project_collection_window_uses_compact_browser_and_host_owned_subproject_control()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        Guid projectId = Guid.NewGuid();
        var scope = new FileToolsSemanticScope(
            FileToolsSemanticScopeKind.Project,
            new FileToolsSemanticScopeId(projectId.ToString("N")),
            "Delivery");
        RegisterServices(
            context,
            new ProjectFileScopeSet(projectId, [scope], new string('b', 64)),
            new ThrowingNodeFileScopeProvider());
        var state = new CanvasWorkbenchWindowState
        {
            IsVisible = true,
            Width = 440,
            Height = 560
        };

        var cut = context.Render<ProjectStructureFileBrowserWindow>(parameters => parameters
            .Add(component => component.WindowId, "project-structure.fileBrowser")
            .Add(
                component => component.Opening,
                new ProjectStructureFileCollectionOpening(new ProjectStructureProjectFileCollectionRequest(projectId, "Delivery files"), _ => Task.CompletedTask))
            .Add(component => component.State, state));

        cut.WaitForAssertion(() =>
        {
            var browser = cut.FindComponent<FileBrowser>();
            Assert.Equal(FileBrowserDisplayMode.Compact, browser.Instance.DisplayMode);
            Assert.NotNull(cut.Find("[data-testid='project-structure-file-browser-window']"));
            Assert.NotNull(cut.Find("input[aria-label='Include subprojects']"));
            Assert.Single(cut.FindAll(".project-structure-file-browser-window__browser"));
            Assert.Contains("Compact", cut.Markup);
        });
    }

    [Fact]
    public void Node_collection_window_does_not_offer_project_hierarchy_control()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        Guid projectId = Guid.NewGuid();
        var scope = new FileToolsSemanticScope(
            FileToolsSemanticScopeKind.ProjectNode,
            new FileToolsSemanticScopeId("node:v1:collection:11111111111111111111111111111111"),
            "Reports");
        RegisterServices(
            context,
            new ProjectFileScopeSet(
                projectId,
                [
                    new FileToolsSemanticScope(
                        FileToolsSemanticScopeKind.Project,
                        new FileToolsSemanticScopeId(projectId.ToString("N")),
                        "Delivery")
                ],
                new string('c', 64)),
            new StaticNodeFileScopeProvider(scope));

        var cut = context.Render<ProjectStructureFileBrowserWindow>(parameters => parameters
            .Add(component => component.WindowId, "project-structure.fileBrowser")
            .Add(
                component => component.Opening,
                new ProjectStructureFileCollectionOpening(new ProjectStructureNodeFileCollectionRequest(projectId, "storage-node", "Reports"), _ => Task.CompletedTask))
            .Add(component => component.State, new CanvasWorkbenchWindowState { IsVisible = true }));

        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(cut.FindComponent<FileBrowser>());
            Assert.Empty(cut.FindAll("input[aria-label='Include subprojects']"));
        });
    }

    [Fact]
    public void Node_collection_window_exposes_the_host_authorized_explorer_action()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        Guid projectId = Guid.NewGuid();
        var scope = new FileToolsSemanticScope(
            FileToolsSemanticScopeKind.ProjectNode,
            new FileToolsSemanticScopeId("node:v2:collection:11111111111111111111111111111111:Zm9sZGVy"),
            "Run artifacts");
        RegisterServices(
            context,
            new ProjectFileScopeSet(
                projectId,
                [
                    new FileToolsSemanticScope(
                        FileToolsSemanticScopeKind.Project,
                        new FileToolsSemanticScopeId(projectId.ToString("N")),
                        "Delivery")
                ],
                new string('c', 64)),
            new StaticNodeFileScopeProvider(scope));
        bool opened = false;

        var cut = context.Render<ProjectStructureFileBrowserWindow>(parameters => parameters
            .Add(component => component.WindowId, "project-structure.fileBrowser")
            .Add(
                component => component.Opening,
                new ProjectStructureFileCollectionOpening(new ProjectStructureNodeFileCollectionRequest(projectId, "run-output", "Run artifacts"), _ => Task.CompletedTask))
            .Add(component => component.State, new CanvasWorkbenchWindowState { IsVisible = true })
            .Add(component => component.CanOpenRootInExplorer, true)
            .Add(
                component => component.OpenRootInExplorer,
                EventCallback.Factory.Create(this, () => opened = true)));

        cut.WaitForElement("[data-testid='project-structure-file-browser-open-root-explorer']").Click();

        Assert.True(opened);
    }

    [Fact]
    public async Task Opening_file_replaces_resolving_status_with_explicit_read_only_state()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        Guid projectId = Guid.NewGuid();
        var scope = new FileToolsSemanticScope(
            FileToolsSemanticScopeKind.Project,
            new FileToolsSemanticScopeId(projectId.ToString("N")),
            "Delivery");
        RegisterServices(
            context,
            new ProjectFileScopeSet(projectId, [scope], new string('d', 64)),
            new ThrowingNodeFileScopeProvider(),
            new StaticBrowseItemActivator(),
            new StaticKnownFileSessionFactory());
        var cut = context.Render<ProjectStructureFileBrowserWindow>(parameters => parameters
            .Add(component => component.WindowId, "project-structure.fileBrowser")
            .Add(
                component => component.Opening,
                new ProjectStructureFileCollectionOpening(new ProjectStructureProjectFileCollectionRequest(projectId, "Delivery files"), _ => Task.CompletedTask))
            .Add(component => component.State, new CanvasWorkbenchWindowState { IsVisible = true }));

        await cut.WaitForElement(".ft-file-browser__item-main").DoubleClickAsync(new MouseEventArgs());

        cut.WaitForAssertion(() =>
        {
            Assert.Single(cut.FindComponents<FileInteraction>());
            Assert.Contains("Authorized read-only file", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("File open", cut.Markup, StringComparison.Ordinal);
            Assert.DoesNotContain("Resolving", cut.Markup, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task Double_click_opens_supported_file_internally_when_desktop_launch_is_available()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        Guid projectId = Guid.NewGuid();
        var scope = new FileToolsSemanticScope(
            FileToolsSemanticScopeKind.Project,
            new FileToolsSemanticScopeId(projectId.ToString("N")),
            "Delivery");
        var itemActionService = new RecordingBrowseItemActionService();
        RegisterServices(
            context,
            new ProjectFileScopeSet(projectId, [scope], new string('e', 64)),
            new ThrowingNodeFileScopeProvider(),
            new StaticBrowseItemActivator(),
            new StaticKnownFileSessionFactory(),
            itemActionService);
        var cut = context.Render<ProjectStructureFileBrowserWindow>(parameters => parameters
            .Add(component => component.WindowId, "project-structure.fileBrowser")
            .Add(
                component => component.Opening,
                new ProjectStructureFileCollectionOpening(new ProjectStructureProjectFileCollectionRequest(projectId, "Delivery files"), _ => Task.CompletedTask))
            .Add(component => component.State, new CanvasWorkbenchWindowState { IsVisible = true }));

        await cut.WaitForElement(".ft-file-browser__item-main").DoubleClickAsync(new MouseEventArgs());

        cut.WaitForAssertion(() =>
        {
            Assert.Single(cut.FindComponents<FileInteraction>());
            Assert.Equal(0, itemActionService.LaunchCount);
        });
    }

    private static void RegisterServices(
        BunitContext context,
        ProjectFileScopeSet projectScopes,
        IProjectStructureNodeFileScopeProvider nodeScopes,
        IFileToolsBrowseItemActivator? itemActivator = null,
        IFileToolsKnownFileSessionFactory? knownFileSessionFactory = null,
        IFileToolsBrowseItemActionService? itemActionService = null,
        IFileToolsKnownFileSessionReleaser? sessionReleaser = null)
    {
        context.Services.AddLogging();
        context.Services.AddSingleton(new FileInteractionComponentBuilder()
            .AddBuiltIns()
            .AddMarkdown()
            .AddWorkbenchMermaid()
            .AddSpreadsheet()
            .Build());
        context.Services.AddSingleton<IProjectFileScopeProvider>(
            new StaticProjectFileScopeProvider(projectScopes));
        context.Services.AddSingleton(nodeScopes);
        context.Services.AddSingleton<IFileToolsBrowseSessionFactory, StaticBrowseSessionFactory>();
        context.Services.AddSingleton(itemActivator ?? new ThrowingBrowseItemActivator());
        context.Services.AddSingleton(
            itemActionService ?? new UnavailableFileToolsBrowseItemActionService());
        context.Services.AddSingleton(knownFileSessionFactory ?? new ThrowingKnownFileSessionFactory());
        context.Services.AddSingleton(sessionReleaser ?? new NoopKnownFileSessionReleaser());
        context.Services.AddSingleton<ProjectStructureFileActionCoordinator>();
    }

    private sealed class StaticProjectFileScopeProvider(ProjectFileScopeSet scopes) : IProjectFileScopeProvider
    {
        public ValueTask<ProjectFileScopeSet> ResolveAsync(
            Guid projectId,
            bool includeSubprojects,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(scopes);
    }

    private sealed class StaticBrowseSessionFactory : IFileToolsBrowseSessionFactory
    {
        public ValueTask<FileToolsBrowseSession> CreateAsync(
            FileToolsSemanticScope scope,
            CancellationToken cancellationToken = default)
        {
            var provider = new StaticFileBrowserProvider(scope.Id.Value);
            return ValueTask.FromResult(new FileToolsBrowseSession(
                scope,
                [provider],
                new FileBrowserSortDescriptor(
                    FileBrowserSortField.ProviderNative,
                    FileBrowserSortDirection.Ascending,
                    FoldersFirst: false),
                new FileToolsBrowseSessionRevision("window-test-revision")));
        }
    }

    private sealed class StaticFileBrowserProvider : IFileBrowserProvider, IFileToolsBrowseSourceActionCapabilities
    {
        private readonly FileBrowserItem root;
        private readonly FileBrowserItem file;

        public StaticFileBrowserProvider(string sourceSuffix)
        {
            var sourceId = new FileBrowserSourceId($"window-{sourceSuffix}");
            Descriptor = new FileBrowserSourceDescriptor(sourceId, "Window files");
            root = new FileBrowserItem(
                new FileBrowserItemKey(sourceId, "root", "r1"),
                parentKey: null,
                "Root",
                FileBrowserItemKind.Container,
                FileBrowserItemCategory.Folder,
                childState: FileBrowserChildState.HasChildren,
                capabilities: FileBrowserItemCapabilities.Select | FileBrowserItemCapabilities.Navigate);
            file = new FileBrowserItem(
                new FileBrowserItemKey(sourceId, "readme.md", "r1"),
                root.Key,
                "README.md",
                FileBrowserItemKind.File,
                FileBrowserItemCategory.Document,
                childState: FileBrowserChildState.Empty,
                mediaType: "text/markdown",
                capabilities: FileBrowserItemCapabilities.Select | FileBrowserItemCapabilities.Open);
        }

        public FileBrowserSourceDescriptor Descriptor { get; }

        public FileToolsBrowseSourceActionAvailability ActionAvailability { get; } = new(
            SupportsLocalOpen: true,
            SupportsDownload: false);

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

    private sealed class ThrowingBrowseItemActivator : IFileToolsBrowseItemActivator
    {
        public ValueTask<FileToolsKnownFileActivation> ActivateAsync(
            FileToolsSemanticScope scope,
            FileBrowserItemKey itemKey,
            FileToolsKnownFileIntent intent,
            CancellationToken cancellationToken = default)
            => ValueTask.FromException<FileToolsKnownFileActivation>(new InvalidOperationException("Unexpected activation."));
    }

    private sealed class StaticBrowseItemActivator : IFileToolsBrowseItemActivator
    {
        public int Calls { get; private set; }
        public ValueTask<FileToolsKnownFileActivation> ActivateAsync(
            FileToolsSemanticScope scope,
            FileBrowserItemKey itemKey,
            FileToolsKnownFileIntent intent,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return ValueTask.FromResult(new FileToolsKnownFileActivation(
                new FileToolsKnownFileRequest(scope, new FileReference("authorized", $"window-handle-{Calls}"), intent),
                "README.md",
                "text/markdown",
                size: 3));
        }
    }

    private sealed class ThrowingKnownFileSessionFactory : IFileToolsKnownFileSessionFactory
    {
        public ValueTask<FileToolsKnownFileSession> CreateAsync(
            FileToolsKnownFileRequest request,
            CancellationToken cancellationToken = default)
            => ValueTask.FromException<FileToolsKnownFileSession>(new InvalidOperationException("Unexpected session."));
    }

    private sealed class StaticKnownFileSessionFactory : IFileToolsKnownFileSessionFactory
    {
        public ValueTask<FileToolsKnownFileSession> CreateAsync(
            FileToolsKnownFileRequest request,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(new FileToolsKnownFileSession(
                request.File,
                new StaticContentSource(),
                request.Intent));
    }

    private sealed class HeldKnownFileSessionFactory : IFileToolsKnownFileSessionFactory {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Finish { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public FileReference? OriginalFile { get; private set; }

        public async ValueTask<FileToolsKnownFileSession> CreateAsync(FileToolsKnownFileRequest request, CancellationToken cancellationToken = default) {
            if (OriginalFile is null) {
                OriginalFile = request.File;
                Entered.SetResult();
                await Finish.Task;
            }
            return new(request.File, new StaticContentSource(), request.Intent);
        }
    }

    private sealed class RecordingKnownFileSessionReleaser : IFileToolsKnownFileSessionReleaser {
        public List<FileReference> Released { get; } = [];
        public ValueTask ReleaseAsync(FileReference file, CancellationToken cancellationToken = default) {
            Released.Add(file);
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
                "text/markdown",
                3));
    }

    private sealed class NoopKnownFileSessionReleaser : IFileToolsKnownFileSessionReleaser
    {
        public ValueTask ReleaseAsync(
            FileReference file,
            CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;
    }

    private sealed class RecordingBrowseItemActionService : IFileToolsBrowseItemActionService
    {
        public bool IsLocalLaunchAvailable => true;

        public int LaunchCount { get; private set; }

        public ValueTask<FileToolsBrowseItemActionResult> LaunchAsync(
            FileToolsSemanticScope scope,
            FileBrowserItemKey itemKey,
            FileToolsLocalFileAction action,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LaunchCount++;
            return ValueTask.FromResult(FileToolsBrowseItemActionResult.Success("Opened."));
        }

        public ValueTask<IFileToolsDownloadLease> AuthorizeDownloadAsync(
            FileToolsSemanticScope scope,
            FileBrowserItemKey itemKey,
            CancellationToken cancellationToken = default)
            => ValueTask.FromException<IFileToolsDownloadLease>(
                new InvalidOperationException("Unexpected download authorization."));
    }

    private sealed class ThrowingNodeFileScopeProvider : IProjectStructureNodeFileScopeProvider
    {
        public ValueTask<FileToolsKnownFileScope> ResolveKnownFileAsync(
            Guid projectId,
            string nodeId,
            CancellationToken cancellationToken = default)
            => ValueTask.FromException<FileToolsKnownFileScope>(
                new InvalidOperationException("Unexpected known-file scope."));

        public ValueTask<FileToolsSemanticScope> ResolveNodeCollectionAsync(
            Guid projectId,
            string nodeId,
            CancellationToken cancellationToken = default)
            => ValueTask.FromException<FileToolsSemanticScope>(
                new InvalidOperationException("Unexpected node scope."));
    }

    private sealed class StaticNodeFileScopeProvider(FileToolsSemanticScope scope)
        : IProjectStructureNodeFileScopeProvider
    {
        public ValueTask<FileToolsKnownFileScope> ResolveKnownFileAsync(
            Guid projectId,
            string nodeId,
            CancellationToken cancellationToken = default)
            => ValueTask.FromException<FileToolsKnownFileScope>(
                new InvalidOperationException("Unexpected known-file scope."));

        public ValueTask<FileToolsSemanticScope> ResolveNodeCollectionAsync(
            Guid projectId,
            string nodeId,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(scope);
    }

    private sealed class HeldThenCurrentNodeScopeProvider(FileToolsSemanticScope scope) : IProjectStructureNodeFileScopeProvider {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<FileToolsSemanticScope> Finish { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Returned { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Calls { get; private set; }

        public async ValueTask<FileToolsSemanticScope> ResolveNodeCollectionAsync(Guid projectId, string nodeId, CancellationToken cancellationToken = default) {
            Calls++;
            if (Calls != 1) {
                return scope;
            }
            Entered.SetResult();
            try {
                return await Finish.Task;
            } finally {
                Returned.SetResult();
            }
        }

        public ValueTask<FileToolsKnownFileScope> ResolveKnownFileAsync(Guid projectId, string nodeId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class DeferredNodeFileScopeProvider : IProjectStructureNodeFileScopeProvider
    {
        private readonly TaskCompletionSource<FileToolsSemanticScope> completion = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public void Complete(FileToolsSemanticScope scope)
            => completion.SetResult(scope);

        public ValueTask<FileToolsKnownFileScope> ResolveKnownFileAsync(
            Guid projectId,
            string nodeId,
            CancellationToken cancellationToken = default)
            => ValueTask.FromException<FileToolsKnownFileScope>(
                new InvalidOperationException("Unexpected known-file scope."));

        public ValueTask<FileToolsSemanticScope> ResolveNodeCollectionAsync(
            Guid projectId,
            string nodeId,
            CancellationToken cancellationToken = default)
            => new(completion.Task.WaitAsync(cancellationToken));
    }
}
