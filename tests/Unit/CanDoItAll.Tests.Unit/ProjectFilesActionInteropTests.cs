using CanDoItAll.AppComponents.FileTools;
using CanDoItAll.FileTools.FileInteraction;
using CanDoItAll.FileTools.Integration;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Projects.Files.UiSandbox;
using Microsoft.JSInterop;

namespace CanDoItAll.Tests.Unit.Projects;

public sealed class ProjectFilesActionInteropTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Held_import_requires_original_owner_before_JS_delivery_and_releases_all_resources(bool retire) {
        var runtime = new DownloadJs();
        var lease = new DownloadLease();
        using var cancellation = new CancellationTokenSource();
        bool contextChanged = false;
        await using var runner = new FileToolsHostActionRunner(new ProjectFilesActionInterop(runtime, token => {
            token.ThrowIfCancellationRequested();
            if (contextChanged) {
                throw new FileAccessDeniedException(FileAccessFailureCode.ContextMismatch, "Original context changed");
            }
            return ValueTask.CompletedTask;
        }));
        var pending = runner.ExecuteAsync(FileToolsHostAction.Download, UnexpectedLaunch, _ => ValueTask.FromResult<IFileToolsDownloadLease>(lease), cancellation.Token).AsTask();
        await runtime.Gate.Entered.Task;
        if (retire) {
            cancellation.Cancel();
        } else {
            contextChanged = true;
        }
        runtime.Gate.Release();
        if (retire) {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        } else {
            var denied = await Assert.ThrowsAsync<FileAccessDeniedException>(() => pending);
            Assert.Equal(FileAccessFailureCode.ContextMismatch, denied.Code);
        }
        Assert.Equal(0, runtime.Deliveries);
        Assert.Equal(1, runtime.Disposals);
        Assert.Equal(1, lease.Disposals);
        Assert.False(lease.Stream.CanRead);
    }

    [Fact]
    public async Task Delivery_and_cleanup_failures_retain_primary_diagnostic_and_release_stream_and_lease() {
        var runtime = new DownloadJs { FailDeliveryAndCleanup = true };
        runtime.Gate.Release();
        var lease = new DownloadLease();
        await using var runner = new FileToolsHostActionRunner(new ProjectFilesActionInterop(runtime, _ => ValueTask.CompletedTask));
        var failure = await Assert.ThrowsAsync<AggregateException>(() => runner.ExecuteAsync(FileToolsHostAction.Download,
            UnexpectedLaunch, _ => ValueTask.FromResult<IFileToolsDownloadLease>(lease)).AsTask());
        Assert.Collection(failure.InnerExceptions,
            primary => Assert.Equal("Injected download delivery failure", primary.Message),
            cleanup => Assert.Equal("Injected module cleanup failure", cleanup.Message));
        Assert.Equal(1, runtime.Deliveries);
        Assert.Equal(1, runtime.Disposals);
        Assert.Equal(1, lease.Disposals);
        Assert.False(lease.Stream.CanRead);
    }

    private static ValueTask<FileToolsBrowseItemActionResult> UnexpectedLaunch(FileToolsLocalFileAction action, CancellationToken token)
        => throw new InvalidOperationException("No native launch was requested.");

    private sealed class DownloadLease : IFileToolsDownloadLease {
        public string FileName => "notes.txt";
        public MemoryStream Stream { get; } = new("Authorized fixture bytes"u8.ToArray());
        public int Disposals { get; private set; }
        public ValueTask<FileContentLease> OpenReadAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult(new FileContentLease(Stream, "text/plain", Stream.Length));
        public ValueTask DisposeAsync() {
            Disposals++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class DownloadJs : IJSRuntime, IJSObjectReference {
        public FilesFixtureGate Gate { get; } = new();
        public bool FailDeliveryAndCleanup { get; init; }
        public int Deliveries { get; private set; }
        public int Disposals { get; private set; }
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => InvokeAsync<TValue>(identifier, CancellationToken.None, args);
        public async ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) {
            if (identifier == "import") {
                await Gate.WaitAsync();
                return (TValue)(object)this;
            }
            Assert.Equal("downloadFileFromStream", identifier);
            Deliveries++;
            if (FailDeliveryAndCleanup) {
                throw new JSException("Injected download delivery failure");
            }
            return default!;
        }
        public ValueTask DisposeAsync() {
            Disposals++;
            return FailDeliveryAndCleanup ? ValueTask.FromException(new JSException("Injected module cleanup failure")) : ValueTask.CompletedTask;
        }
    }

}
