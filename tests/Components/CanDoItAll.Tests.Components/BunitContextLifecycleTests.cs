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

    private sealed class DisposalProbe : ComponentBase, IDisposable {
        public bool IsDisposed { get; private set; }

        public void Dispose() {
            IsDisposed = true;
        }
    }
}
