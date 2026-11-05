using Bunit;

namespace CanDoItAll.Tests.Components;

internal static class BunitContextLifecycleExtensions {
    public static Task DisposeRenderedComponentsAsync(this BunitContext context) {
        // Keep bUnit's root list and async disposal continuations on the renderer dispatcher.
        return context.Renderer.Dispatcher.InvokeAsync(async () => {
            var scheduler = TaskScheduler.FromCurrentSynchronizationContext();
            await Task.Factory.StartNew(context.DisposeComponentsAsync, CancellationToken.None,
                TaskCreationOptions.DenyChildAttach, scheduler).Unwrap();
        });
    }
}
