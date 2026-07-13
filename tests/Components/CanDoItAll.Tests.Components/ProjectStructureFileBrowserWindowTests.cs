using Bunit;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.FileTools.FileBrowser;
using CanDoItAll.FileTools.FileBrowser.Components;
using CanDoItAll.FileTools.FileInteraction;
using CanDoItAll.FileTools.FileInteraction.Components;
using CanDoItAll.FileTools.FileInteraction.Markdown;
using CanDoItAll.FileTools.Integration;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.Pages;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class ProjectStructureFileBrowserWindowTests
{
    [Fact]
    public void Project_collection_window_uses_compact_browser_and_host_owned_subproject_control()
    {
        using var context = new TestContext();
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

        var cut = context.RenderComponent<ProjectStructureFileBrowserWindow>(parameters => parameters
            .Add(component => component.WindowId, "project-structure.fileBrowser")
            .Add(
                component => component.Request,
                new ProjectStructureProjectFileCollectionRequest(projectId, "Delivery files"))
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
        using var context = new TestContext();
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

        var cut = context.RenderComponent<ProjectStructureFileBrowserWindow>(parameters => parameters
            .Add(component => component.WindowId, "project-structure.fileBrowser")
            .Add(
                component => component.Request,
                new ProjectStructureNodeFileCollectionRequest(projectId, "storage-node", "Reports"))
            .Add(component => component.State, new CanvasWorkbenchWindowState { IsVisible = true }));

        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(cut.FindComponent<FileBrowser>());
            Assert.Empty(cut.FindAll("input[aria-label='Include subprojects']"));
        });
    }

    [Fact]
    public async Task Opening_file_replaces_resolving_status_with_explicit_read_only_state()
    {
        using var context = new TestContext();
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
        var cut = context.RenderComponent<ProjectStructureFileBrowserWindow>(parameters => parameters
            .Add(component => component.WindowId, "project-structure.fileBrowser")
            .Add(
                component => component.Request,
                new ProjectStructureProjectFileCollectionRequest(projectId, "Delivery files"))
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

    private static void RegisterServices(
        TestContext context,
        ProjectFileScopeSet projectScopes,
        IProjectStructureNodeFileScopeProvider nodeScopes,
        IFileToolsBrowseItemActivator? itemActivator = null,
        IFileToolsKnownFileSessionFactory? knownFileSessionFactory = null)
    {
        context.Services.AddLogging();
        context.Services.AddSingleton(new FileInteractionComponentBuilder()
            .AddBuiltIns()
            .AddMarkdown()
            .AddWorkbenchMermaid()
            .Build());
        context.Services.AddSingleton<IProjectFileScopeProvider>(
            new StaticProjectFileScopeProvider(projectScopes));
        context.Services.AddSingleton(nodeScopes);
        context.Services.AddSingleton<IFileToolsBrowseSessionFactory, StaticBrowseSessionFactory>();
        context.Services.AddSingleton(itemActivator ?? new ThrowingBrowseItemActivator());
        context.Services.AddSingleton(knownFileSessionFactory ?? new ThrowingKnownFileSessionFactory());
        context.Services.AddSingleton<IFileToolsKnownFileSessionReleaser, NoopKnownFileSessionReleaser>();
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

    private sealed class StaticFileBrowserProvider : IFileBrowserProvider
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
        public ValueTask<FileToolsKnownFileActivation> ActivateAsync(
            FileToolsSemanticScope scope,
            FileBrowserItemKey itemKey,
            FileToolsKnownFileIntent intent,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(new FileToolsKnownFileActivation(
                new FileToolsKnownFileRequest(scope, new FileReference("authorized", "window-handle"), intent),
                "README.md",
                "text/markdown",
                size: 3));
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
}
