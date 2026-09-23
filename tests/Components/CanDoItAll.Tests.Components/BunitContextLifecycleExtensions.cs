using Bunit;

namespace CanDoItAll.Tests.Components;

internal static class BunitContextLifecycleExtensions {
    public static Task DisposeRenderedComponentsAsync(this BunitContext context) {
        // bUnit 2.7 clears its root list before queued disposal runs on a busy dispatcher.
        return context.Renderer.Dispatcher.InvokeAsync(context.DisposeComponentsAsync);
    }
}
