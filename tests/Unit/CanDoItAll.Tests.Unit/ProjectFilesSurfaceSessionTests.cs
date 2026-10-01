using CanDoItAll.FileTools.FileBrowser;
using CanDoItAll.FileTools.FileBrowser.Components;
using CanDoItAll.FileTools.FileInteraction;
using CanDoItAll.FileTools.Integration;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Projects.Files.UiSandbox;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace CanDoItAll.Tests.Unit.Projects;

public sealed class ProjectFilesSurfaceSessionTests {
    [Fact]
    public async Task Open_captures_context_before_held_old_cleanup_and_refuses_a_context_switch() {
        var fixture = new SurfaceFixture();
        await using var surface = fixture.Create();
        var release = new FilesFixtureGate();
        await surface.OpenAsync(new OwnedWorkspace("alpha") { PreviewRelease = release }.Open, "A");
        await PreviewAsync(surface);
        var replacement = new OwnedWorkspace("beta");
        var pending = surface.OpenAsync(replacement.Open, "B");
        await release.Entered.Task;
        fixture.Contexts.Generation++;
        release.Release();
        await pending;
        Assert.Null(surface.State.Browse);
        Assert.Contains("context changed", surface.State.OpenError);
        await replacement.DisposeAsync();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Noncooperating_A_open_cannot_replace_or_fail_a_new_A_after_B(bool fails) {
        var fixture = new SurfaceFixture();
        await using var surface = fixture.Create();
        var held = new TaskCompletionSource<ProjectFilesOwnedWorkspace>(TaskCreationOptions.RunContinuationsAsynchronously);
        var old = new OwnedWorkspace("alpha");
        CancellationToken admitted = default;
        var pending = surface.OpenAsync(token => {
            admitted = token;
            return new(held.Task);
        }, "Original A");
        await surface.OpenAsync(new OwnedWorkspace("beta").Open, "B");
        var current = new OwnedWorkspace("alpha");
        await surface.OpenAsync(current.Open, "New A");
        Assert.True(admitted.IsCancellationRequested);
        using var registration = admitted.Register(() => { });
        if (fails) {
            held.SetException(new IOException("Late original resolution failure"));
        } else {
            held.SetResult(old.Value);
        }
        await pending;
        Assert.Same(current.Browser, surface.State.Browse!.Session);
        Assert.Null(surface.State.OpenError);
        Assert.False(surface.State.IsLoading);
        Assert.Equal(fails ? 0 : 1, old.Disposals);
        Assert.Equal(0, current.Disposals);
        await old.DisposeAsync();
    }

    [Fact]
    public async Task Held_old_close_detaches_both_resources_and_cannot_call_a_successor_callback() {
        var fixture = new SurfaceFixture();
        await using var surface = fixture.Create();
        var release = new FilesFixtureGate();
        var original = new OwnedWorkspace("alpha") { PreviewRelease = release };
        int oldClosed = 0;
        int newClosed = 0;
        await surface.OpenAsync(original.Open, "A", EventCallback.Factory.Create(new object(), () => oldClosed++));
        await PreviewAsync(surface);
        var oldContent = surface.State.Preview!;
        var closing = surface.State.Closed.InvokeAsync();
        await release.Entered.Task;
        Assert.Null(surface.State.Browse);
        var current = new OwnedWorkspace("beta");
        await surface.OpenAsync(current.Open, "B", EventCallback.Factory.Create(new object(), () => newClosed++));
        await PreviewAsync(surface);
        var newContent = surface.State.Preview!;
        release.Release();
        await closing;
        Assert.Equal(0, oldClosed);
        Assert.Equal(0, newClosed);
        Assert.Same(newContent, surface.State.Preview);
        Assert.Equal(1, original.Disposals);
        Assert.Equal(0, current.Disposals);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => oldContent.ContentSource.OpenReadAsync(new(oldContent.Request.File)).AsTask());
        await using var lease = await newContent.ContentSource.OpenReadAsync(new(newContent.Request.File));
        Assert.True(lease.Length > 0);
        await surface.State.Closed.InvokeAsync();
        Assert.Equal(1, newClosed);
    }

    [Fact]
    public async Task Historical_item_and_snapshot_callbacks_have_no_authority_in_a_replacement() {
        var fixture = new SurfaceFixture();
        await using var surface = fixture.Create();
        var original = new OwnedWorkspace("alpha");
        await surface.OpenAsync(original.Open, "A");
        var oldView = surface.State.Browse!;
        var oldSnapshot = original.Browser.Snapshot;
        await surface.OpenAsync(new OwnedWorkspace("beta").Open, "B");
        var state = surface.State;
        await oldView.SnapshotChanged.InvokeAsync(oldSnapshot);
        await oldView.ItemInvoked.InvokeAsync(new(oldSnapshot.Items[0], FileBrowserInvocationKind.Keyboard));
        await oldView.ActionRequested.InvokeAsync(new(oldSnapshot.Items[0], new(FileBrowserActionIds.Download, "Download", "download")));
        Assert.Same(state, surface.State);
        Assert.Equal(0, original.Activations);
        Assert.Equal(0, fixture.Actions.Launches);
    }

    [Fact]
    public async Task Late_preview_grant_is_released_once_and_its_token_remains_usable_until_completion() {
        var fixture = new SurfaceFixture();
        await using var surface = fixture.Create();
        var original = new OwnedWorkspace("alpha") { PreviewGate = new() };
        await surface.OpenAsync(original.Open, "A");
        var preview = PreviewAsync(surface);
        await original.PreviewGate.Entered.Task;
        await surface.OpenAsync(new OwnedWorkspace("beta").Open, "B");
        using var registration = original.PreviewToken.Register(() => { });
        Assert.True(original.PreviewToken.IsCancellationRequested);
        original.PreviewGate.Release();
        await preview;
        Assert.Null(surface.State.Preview);
        Assert.Null(surface.State.ActivationError);
        Assert.Equal(1, Assert.Single(original.Contents).Releases);
    }

    [Fact]
    public async Task Context_change_after_preview_acquisition_releases_the_grant_and_reports_refusal() {
        var fixture = new SurfaceFixture();
        await using var surface = fixture.Create();
        var workspace = new OwnedWorkspace("alpha") { PreviewGate = new() };
        await surface.OpenAsync(workspace.Open, "A");
        var preview = PreviewAsync(surface);
        await workspace.PreviewGate.Entered.Task;
        fixture.Contexts.Generation++;
        workspace.PreviewGate.Release();
        await preview;
        Assert.Null(surface.State.Preview);
        Assert.Contains("context changed", surface.State.ActivationError);
        Assert.Equal(1, Assert.Single(workspace.Contents).Releases);
    }

    [Fact]
    public async Task Cleanup_failure_attempts_browser_release_and_preserves_the_surviving_view() {
        var fixture = new SurfaceFixture();
        await using var surface = fixture.Create();
        var original = new OwnedWorkspace("alpha") { FailPreviewRelease = true };
        await surface.OpenAsync(original.Open, "A");
        await PreviewAsync(surface);
        var current = new OwnedWorkspace("beta");
        await surface.OpenAsync(current.Open, "B");
        Assert.Equal(1, original.Disposals);
        Assert.Equal(1, Assert.Single(original.Contents).Releases);
        Assert.Single(fixture.Log.Failures, exception => exception.Message == "Injected preview release failure");
        Assert.Same(current.Browser, surface.State.Browse!.Session);
        Assert.Null(surface.State.OpenError);
    }

    [Fact]
    public async Task Failed_refresh_is_unavailable_until_explicit_retry_and_empty_success_retires_preview() {
        var fixture = new SurfaceFixture();
        await using var surface = fixture.Create();
        int reads = 0;
        async ValueTask<ProjectFilesOwnedWorkspace> Open(CancellationToken token) {
            reads++;
            if (reads == 2) {
                throw new IOException("Injected refresh failure");
            }
            return await new OwnedWorkspace("alpha").Open(token);
        }
        await surface.OpenAsync(Open, "A");
        await PreviewAsync(surface);
        var old = surface.State.Preview!;
        await surface.State.Refresh.InvokeAsync();
        Assert.NotNull(surface.State.OpenError);
        Assert.False(surface.State.IsEmpty);
        Assert.Null(surface.State.Preview);
        Assert.Equal(2, reads);
        await surface.State.Retry.InvokeAsync();
        Assert.Null(surface.State.OpenError);
        Assert.Equal(3, reads);
        await surface.OpenAsync(new OwnedWorkspace("empty", empty: true).Open, "Empty selection");
        Assert.True(surface.State.IsEmpty);
        Assert.Empty(surface.State.Browse!.Session.Snapshot.Sources);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => old.ContentSource.OpenReadAsync(new(old.Request.File)).AsTask());
    }

    [Fact]
    public async Task Replacement_preserves_a_valid_selected_source_and_does_not_mutate_the_original_session() {
        var fixture = new SurfaceFixture();
        await using var surface = fixture.Create();
        var first = new OwnedWorkspace("alpha", additional: "beta");
        await surface.OpenAsync(first.Open, "All");
        await first.Browser.ChangeSourceAsync(new("beta"));
        var current = new OwnedWorkspace("alpha", additional: "beta");
        await surface.OpenAsync(current.Open, "All refreshed");
        Assert.Equal("beta", current.Browser.Snapshot.CurrentSource!.Id.Value);
        Assert.Equal(current.Browser.Snapshot.CurrentSource.Id, current.Browser.Snapshot.Location!.Current.Key.SourceId);
        Assert.All(current.Browser.Snapshot.Items, item => Assert.Equal("beta", item.Key.SourceId.Value));
        Assert.NotSame(first.Browser, current.Browser);
        Assert.Equal(1, first.Disposals);
        Assert.Equal(2, current.Value.ProjectCount);
    }

    [Fact]
    public async Task A_pending_local_action_is_not_replayed_and_cannot_publish_into_a_successor() {
        var fixture = new SurfaceFixture();
        fixture.Actions.Gate = new();
        await using var surface = fixture.Create();
        await surface.OpenAsync(new OwnedWorkspace("alpha").Open, "A");
        await PreviewAsync(surface);
        var callback = surface.State.Preview!.OpenExternally;
        var request = surface.State.Preview.Request;
        var action = callback.InvokeAsync(request);
        await fixture.Actions.Gate.Entered.Task;
        await callback.InvokeAsync(request);
        await surface.OpenAsync(new OwnedWorkspace("beta").Open, "B");
        fixture.Actions.Gate.Release();
        await action;
        Assert.Equal(1, fixture.Actions.Launches);
        Assert.Null(surface.State.ActionFeedback);
        Assert.Null(surface.State.ActivationError);
    }

    private static Task PreviewAsync(ProjectFilesSurfaceSession surface) => surface.State.Browse!.ItemInvoked.InvokeAsync(
        new(surface.State.Browse.Session.Snapshot.Items[0], FileBrowserInvocationKind.Keyboard));

    private sealed class SurfaceFixture {
        public MutableContext Contexts { get; } = new();
        public RecordingActions Actions { get; } = new();
        public RecordingLog Log { get; } = new();
        public ProjectFilesSurfaceSession Create() => new(new object(), Contexts, Actions, FilesFixtureComposition.Create(), new NoJs(), Log);
    }

    private sealed class MutableContext : IFileAccessContextProvider {
        private readonly Guid profile = Guid.NewGuid();
        public long Generation { get; set; }
        public ValueTask<FileAccessContext> GetCurrentAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult(new FileAccessContext(new("fixture-actor"), new("fixture-session"), profile, Generation, 0, new(Guid.NewGuid().ToString("N"))));
    }

    private sealed class OwnedWorkspace : IAsyncDisposable, IFileToolsKnownFileSessionReleaser {
        private readonly IReadOnlyList<FilesFixtureProvider> providers;
        public FileBrowserSession Browser { get; }
        public ProjectFilesOwnedWorkspace Value { get; }
        public List<FilesFixtureContent> Contents { get; } = [];
        public FilesFixtureGate? PreviewGate { get; init; }
        public FilesFixtureGate? PreviewRelease { get; init; }
        public bool FailPreviewRelease { get; init; }
        public CancellationToken PreviewToken { get; private set; }
        public int Disposals { get; private set; }
        public int Activations { get; private set; }

        public OwnedWorkspace(string source, string? additional = null, bool empty = false) {
            providers = empty ? [] : additional is null ? [new(source)] : [new(source), new(additional)];
            Browser = new(new FileBrowserSourceSet("fixture-initial", providers));
            Value = new(this, Browser, id => new(FileToolsSemanticScopeKind.Project, new(id.Value), id.Value), _ => new(true, true), ActivateAsync, providers.Count, source);
        }

        public ValueTask<ProjectFilesOwnedWorkspace> Open(CancellationToken token) => ValueTask.FromResult(Value);

        private async ValueTask<ProjectFilesPilotInteraction> ActivateAsync(FileBrowserItemKey item, CancellationToken token) {
            Activations++;
            PreviewToken = token;
            var content = new FilesFixtureContent(providers.Single(provider => provider.Descriptor.Id == item.SourceId).Get(item));
            Contents.Add(content);
            if (PreviewGate is not null) {
                await PreviewGate.WaitAsync();
            }
            return new(content.Request, new(content.File, content, FileToolsKnownFileIntent.ReadOnly), this);
        }

        public async ValueTask ReleaseAsync(FileReference file, CancellationToken cancellationToken = default) {
            Assert.False(cancellationToken.IsCancellationRequested);
            if (PreviewRelease is not null) {
                await PreviewRelease.WaitAsync();
            }
            await Contents.Single(content => content.File == file).DisposeAsync();
            if (FailPreviewRelease) {
                throw new IOException("Injected preview release failure");
            }
        }

        public async ValueTask DisposeAsync() {
            if (Disposals == 0) {
                Disposals++;
                await Browser.DisposeAsync();
            }
        }
    }

    private sealed class RecordingActions : IFileToolsBrowseItemActionService {
        public bool IsLocalLaunchAvailable => true;
        public int Launches { get; private set; }
        public FilesFixtureGate? Gate { get; set; }
        public async ValueTask<FileToolsBrowseItemActionResult> LaunchAsync(FileToolsSemanticScope scope, FileBrowserItemKey itemKey, FileToolsLocalFileAction action, CancellationToken cancellationToken = default) {
            Launches++;
            if (Gate is not null) {
                await Gate.WaitAsync();
            }
            return FileToolsBrowseItemActionResult.Success("Recorded original launch");
        }
        public ValueTask<IFileToolsDownloadLease> AuthorizeDownloadAsync(FileToolsSemanticScope scope, FileBrowserItemKey itemKey, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Download was not requested by this control.");
    }

    private sealed class NoJs : IJSRuntime {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => throw new InvalidOperationException("Unexpected JS call");
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => InvokeAsync<TValue>(identifier, args);
    }

    private sealed class RecordingLog : ILogger {
        public List<Exception> Failures { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState, Exception?, string> formatter) {
            if (exception is not null) {
                Failures.Add(exception);
            }
        }
    }
}
