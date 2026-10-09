using Bunit;
using CanDoItAll.FileTools.FileBrowser;
using CanDoItAll.FileTools.FileBrowser.Components;
using CanDoItAll.AppComponents.FileTools;
using CanDoItAll.Processes.UI;
using CanDoItAll.FileTools.FileInteraction;
using CanDoItAll.FileTools.FileInteraction.Components;
using CanDoItAll.FileTools.Integration;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Processes.Application;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.Processes;

public sealed class ProcessRunFilesDialogTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Activation_error_is_visible_only_to_its_opening(int retargets) {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        var controls = new ControlledFileSessions { HoldActivation = true };
        RegisterControlledServices(context, controls);
        var original = Guid.NewGuid();
        var cut = context.Render<ProcessRunFilesDialog>(p => p.Add(c => c.IsOpen, true).Add(c => c.RunId, original));
        var item = CurrentItem(cut);
        var pending = cut.InvokeAsync(() => cut.FindComponent<ProcessRunFilesSurface>().Instance.ActivateAsync.InvokeAsync(new(item, FileBrowserInvocationKind.Keyboard)));
        cut.WaitForAssertion(() => Assert.Equal(1, controls.Activations));
        for (var index = 0; index < retargets; index++) {
            await cut.InvokeAsync(() => cut.Render(p => p.Add(c => c.RunId, index == 0 ? Guid.NewGuid() : original)));
            CurrentItem(cut);
        }
        controls.Activation.SetException(new InvalidOperationException("Private retired activation details"));
        await pending;
        Assert.Contains("current.txt", cut.Markup, StringComparison.Ordinal);
        Assert.Equal(retargets == 0, cut.Markup.Contains("Unable to open the selected process-run file.", StringComparison.Ordinal));
        Assert.DoesNotContain("Private retired activation details", cut.Markup, StringComparison.Ordinal);
        await context.DisposeRenderedComponentsAsync();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Retired_authorized_file_action_cannot_replace_successor_feedback(bool download) {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        var controls = new ControlledFileSessions();
        RegisterControlledServices(context, controls);
        var actions = new ControlledActions();
        context.Services.AddSingleton<IFileToolsBrowseItemActionService>(actions);
        var cut = context.Render<ProcessRunFilesDialog>(p => p.Add(c => c.IsOpen, true).Add(c => c.RunId, Guid.NewGuid()));
        var item = CurrentItem(cut);
        var method = typeof(ProcessRunFilesDialog).GetMethod("ExecuteFileActionAsync", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var pending = cut.InvokeAsync(() => (Task)method.Invoke(cut.Instance, [item.Key, download ? FileToolsHostAction.Download : FileToolsHostAction.OpenInPreferredApplication])!);
        cut.WaitForAssertion(() => Assert.Equal(1, actions.Calls));
        await cut.InvokeAsync(() => cut.Render(p => p.Add(c => c.RunId, Guid.NewGuid())));
        CurrentItem(cut);
        if (download) {
            actions.Download.SetException(new InvalidOperationException("Private retired download details"));
        } else {
            actions.Launch.SetException(new InvalidOperationException("Private retired launch details"));
        }
        await pending;
        Assert.Contains("current.txt", cut.Markup, StringComparison.Ordinal);
        Assert.Empty(cut.FindAll("[data-testid='process-run-files-activation-error']"));
        Assert.False(cut.Find("[data-testid='process-run-files-refresh']").HasAttribute("disabled"));
        await context.DisposeRenderedComponentsAsync();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Suspended_cleanup_cannot_release_or_close_a_successor(bool separateDialog) {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        var controls = new ControlledFileSessions { HoldFirstRelease = true };
        RegisterControlledServices(context, controls);
        var closed = 0;
        var cut = context.Render<ProcessRunFilesDialog>(p => p.Add(c => c.IsOpen, true).Add(c => c.RunId, Guid.NewGuid()).Add(c => c.Closed, () => closed++));
        await cut.InvokeAsync(() => cut.FindComponent<ProcessRunFilesSurface>().Instance.ActivateAsync.InvokeAsync(new(CurrentItem(cut), FileBrowserInvocationKind.Keyboard)));
        var firstFile = Assert.Single(controls.Opened);
        var closing = cut.InvokeAsync(() => cut.FindComponent<ProcessRunFilesSurface>().Instance.CloseAsync.InvokeAsync());
        cut.WaitForAssertion(() => Assert.Equal(1, controls.Released.GetValueOrDefault(firstFile)));
        var successor = cut;
        if (separateDialog) {
            successor = context.Render<ProcessRunFilesDialog>(p => p.Add(c => c.IsOpen, true).Add(c => c.RunId, Guid.NewGuid()));
        } else {
            await cut.InvokeAsync(() => cut.Render(p => p.Add(c => c.RunId, Guid.NewGuid())));
        }
        await successor.InvokeAsync(() => successor.FindComponent<ProcessRunFilesSurface>().Instance.ActivateAsync.InvokeAsync(new(CurrentItem(successor), FileBrowserInvocationKind.Keyboard)));
        var secondFile = controls.Opened[1];
        controls.Release.SetResult();
        await closing;
        Assert.Equal(separateDialog ? 1 : 0, closed);
        Assert.Equal(1, controls.Released[firstFile]);
        Assert.False(controls.Released.ContainsKey(secondFile));
        Assert.Equal(secondFile, successor.FindComponent<ProcessRunFilesSurface>().Instance.View.Request?.File);
        await context.DisposeRenderedComponentsAsync();
        Assert.Equal(1, controls.Released[secondFile]);
    }

    private static FileBrowserItem CurrentItem(IRenderedComponent<ProcessRunFilesDialog> cut) {
        cut.WaitForAssertion(() => Assert.Contains("current.txt", cut.Markup, StringComparison.Ordinal));
        return cut.FindComponent<ProcessRunFilesSurface>().Instance.View.Browser!.Snapshot.Items.Single();
    }

    private static void RegisterControlledServices(BunitContext context, ControlledFileSessions controls) {
        RegisterServices(context, new CurrentScopeProvider(), new MutableBrowseSessionFactory(new MutableFileSet(["current.txt"])));
        context.Services.AddSingleton<IFileToolsBrowseItemActivator>(controls);
        context.Services.AddSingleton<IFileToolsKnownFileSessionFactory>(controls);
        context.Services.AddSingleton<IFileToolsKnownFileSessionReleaser>(controls);
    }

    private sealed class CurrentScopeProvider : IProcessRunFileScopeProvider {
        public ValueTask<ProcessRunFileScopeSet> ResolveAsync(Guid runId, CancellationToken cancellationToken = default)
            => new RecordingScopeProvider(runId).ResolveAsync(runId, cancellationToken);
        public ValueTask<FileToolsStorageBinding> ResolveRootAsync(Guid runId, string directoryPath, Guid projectId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class ControlledFileSessions : IFileToolsBrowseItemActivator, IFileToolsKnownFileSessionFactory, IFileToolsKnownFileSessionReleaser, IFileContentSource {
        public bool HoldActivation { get; init; }
        public bool HoldFirstRelease { get; init; }
        public int Activations { get; private set; }
        public TaskCompletionSource<FileToolsKnownFileActivation> Activation { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public List<FileReference> Opened { get; } = [];
        public Dictionary<FileReference, int> Released { get; } = [];
        public ValueTask<FileToolsKnownFileActivation> ActivateAsync(FileToolsSemanticScope scope, FileBrowserItemKey itemKey,
            FileToolsKnownFileIntent intent, CancellationToken cancellationToken = default) {
            Activations++;
            return HoldActivation ? new(Activation.Task) : ValueTask.FromResult(new FileToolsKnownFileActivation(
                new(scope, new FileReference("controlled", Guid.NewGuid().ToString("N")), intent), "current.txt", "text/plain", 7));
        }
        public ValueTask<FileToolsKnownFileSession> CreateAsync(FileToolsKnownFileRequest request, CancellationToken cancellationToken = default) {
            Opened.Add(request.File);
            return ValueTask.FromResult(new FileToolsKnownFileSession(request.File, this, request.Intent, null));
        }
        public async ValueTask ReleaseAsync(FileReference file, CancellationToken cancellationToken = default) {
            Released[file] = Released.GetValueOrDefault(file) + 1;
            if (HoldFirstRelease && file == Opened[0]) {
                await Release.Task;
            }
        }
        public ValueTask<FileContentLease> OpenReadAsync(FileContentReadRequest request, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(new FileContentLease(new MemoryStream("current"u8.ToArray()), "text/plain", 7));
    }

    private sealed class ControlledActions : IFileToolsBrowseItemActionService {
        public bool IsLocalLaunchAvailable => true;
        public int Calls { get; private set; }
        public TaskCompletionSource<FileToolsBrowseItemActionResult> Launch { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<IFileToolsDownloadLease> Download { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ValueTask<FileToolsBrowseItemActionResult> LaunchAsync(FileToolsSemanticScope scope, FileBrowserItemKey itemKey, FileToolsLocalFileAction action, CancellationToken cancellationToken = default) {
            Calls++;
            return new(Launch.Task);
        }
        public ValueTask<IFileToolsDownloadLease> AuthorizeDownloadAsync(FileToolsSemanticScope scope, FileBrowserItemKey itemKey, CancellationToken cancellationToken = default) {
            Calls++;
            return new(Download.Task);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Late_open_error_cannot_replace_a_successor_opening(bool reopenOriginalRun) {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        var originalRun = Guid.NewGuid();
        var successorRun = Guid.NewGuid();
        var scopes = new DeferredScopeProvider();
        RegisterServices(context, scopes, new MutableBrowseSessionFactory(new MutableFileSet(["current.txt"])));
        var cut = context.Render<ProcessRunFilesDialog>(parameters => parameters
            .Add(component => component.IsOpen, true)
            .Add(component => component.RunId, originalRun));
        cut.WaitForAssertion(() => Assert.Equal(1, scopes.ResolveCalls));

        await cut.InvokeAsync(() => cut.Render(parameters => parameters.Add(component => component.RunId, successorRun)));
        cut.WaitForElement("[data-testid='process-run-files-refresh']");
        if (reopenOriginalRun) {
            await cut.InvokeAsync(() => cut.Render(parameters => parameters.Add(component => component.RunId, originalRun)));
        }
        cut.WaitForAssertion(() => Assert.Contains("current.txt", cut.Markup, StringComparison.Ordinal));
        var renderCount = cut.RenderCount;
        scopes.First.SetException(new InvalidOperationException("Retired private failure"));
        cut.WaitForState(() => cut.RenderCount > renderCount);

        Assert.Contains("current.txt", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Unable to open process-run files.", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Retired private failure", cut.Markup, StringComparison.Ordinal);
        Assert.Empty(cut.FindAll("[data-testid='process-run-files-retry']"));
        Assert.False(cut.Find("[data-testid='process-run-files-refresh']").HasAttribute("disabled"));
    }

    [Fact]
    public void Refresh_re_resolves_scope_and_re_enumerates_mutable_run_files()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        Guid runId = Guid.NewGuid();
        var files = new MutableFileSet(["before.txt"]);
        var scopeProvider = new RecordingScopeProvider(runId);
        var sessionFactory = new MutableBrowseSessionFactory(files);
        RegisterServices(context, scopeProvider, sessionFactory);

        var cut = context.Render<ProcessRunFilesDialog>(parameters => parameters
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
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        var scopeProvider = new ThrowingScopeProvider();
        RegisterServices(context, scopeProvider, new MutableBrowseSessionFactory(new MutableFileSet([])));

        var cut = context.Render<ProcessRunFilesDialog>(parameters => parameters
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
        BunitContext context,
        IProcessRunFileScopeProvider scopeProvider,
        IFileToolsBrowseSessionFactory sessionFactory)
    {
        context.Services.AddLogging();
        context.Services.AddSingleton(new FileInteractionComponentBuilder()
            .AddBuiltIns()
            .Build());
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

    private sealed class DeferredScopeProvider : IProcessRunFileScopeProvider {
        public TaskCompletionSource<ProcessRunFileScopeSet> First { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int ResolveCalls { get; private set; }

        public ValueTask<ProcessRunFileScopeSet> ResolveAsync(Guid runId, CancellationToken cancellationToken = default) {
            ResolveCalls++;
            return ResolveCalls == 1
                ? new(First.Task)
                : new RecordingScopeProvider(runId).ResolveAsync(runId, cancellationToken);
        }

        public ValueTask<FileToolsStorageBinding> ResolveRootAsync(Guid runId, string directoryPath, Guid projectId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class RecordingScopeProvider(Guid runId) : IProcessRunFileScopeProvider
    {
        public ValueTask<FileToolsStorageBinding> ResolveRootAsync(Guid runId, string directoryPath, Guid projectId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

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
        public ValueTask<FileToolsStorageBinding> ResolveRootAsync(Guid runId, string directoryPath, Guid projectId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

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

    private sealed class MutableFileBrowserProvider(MutableFileSet files) : IFileBrowserProvider, IFileToolsBrowseSourceActionCapabilities
    {
        public FileToolsBrowseSourceActionAvailability ActionAvailability => new(true, true);
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
