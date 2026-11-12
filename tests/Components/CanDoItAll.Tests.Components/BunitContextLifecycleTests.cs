using Bunit;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Tests.Components;

public sealed class BunitContextLifecycleTests {
    [Fact]
    public async Task Disposal_removes_components_when_the_renderer_is_busy() {
        await using var context = new BunitContext();
        var component = context.Render<DisposalProbe>().Instance;
        var rendererEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var releaseRenderer = new ManualResetEventSlim();
        var timeout = TimeSpan.FromSeconds(30);
        var rendererWork = Task.Run(() => context.Renderer.Dispatcher.InvokeAsync(() => {
            rendererEntered.SetResult();
            if (!releaseRenderer.Wait(timeout)) {
                throw new TimeoutException("The test did not release the renderer dispatcher.");
            }
        }));

        Task disposal;
        try {
            await rendererEntered.Task.WaitAsync(timeout);
            disposal = context.DisposeRenderedComponentsAsync();
            Assert.False(disposal.IsCompleted);
        } finally {
            releaseRenderer.Set();
            await rendererWork.WaitAsync(timeout);
        }

        await disposal.WaitAsync(timeout);
        Assert.True(component.IsDisposed);
    }

    [Fact]
    public async Task Async_disposal_uses_the_renderer_scheduler_and_releases_each_component() {
        await using var context = new BunitContext();
        var first = context.Render<AsyncDisposalProbe>().Instance;
        var second = context.Render<AsyncDisposalProbe>().Instance;
        var timeout = TimeSpan.FromSeconds(30);
        var disposal = context.DisposeRenderedComponentsAsync();
        try {
            await Task.WhenAll(first.Entered.Task, second.Entered.Task).WaitAsync(timeout);
            Assert.NotSame(TaskScheduler.Default, first.Scheduler);
            Assert.Same(first.Scheduler, second.Scheduler);
        } finally {
            first.Release.TrySetResult();
            second.Release.TrySetResult();
            await disposal.WaitAsync(timeout);
        }
        Assert.True(first.IsDisposed);
        Assert.True(second.IsDisposed);
        Assert.NotNull(context.Render<DisposalProbe>().Instance);
    }

    private sealed class AsyncDisposalProbe : ComponentBase, IAsyncDisposable {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskScheduler? Scheduler { get; private set; }
        public bool IsDisposed { get; private set; }

        public async ValueTask DisposeAsync() {
            Scheduler = TaskScheduler.Current;
            Entered.TrySetResult();
            await Release.Task;
            IsDisposed = true;
        }
    }

    private sealed class DisposalProbe : ComponentBase, IDisposable {
        public bool IsDisposed { get; private set; }

        public void Dispose() {
            IsDisposed = true;
        }
    }
}
