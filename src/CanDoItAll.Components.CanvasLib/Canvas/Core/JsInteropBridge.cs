using Microsoft.JSInterop;

namespace CanDoItAll.Components.CanvasLib;

public sealed class JsInteropBridge(IJSRuntime jsRuntime)
{
    public ValueTask InvokeVoidAsync(string identifier, params object?[] args)
        => jsRuntime.InvokeVoidAsync(identifier, args);

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, params object?[] args)
        => jsRuntime.InvokeAsync<TValue>(identifier, args);
}

public sealed class JsInteropBridgePreviewSnapshot
{
    public string TestHookId { get; init; } = "js-interop-bridge";

    public string Label { get; init; } = "JS interop bridge";

    public string Title { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string StatePill { get; init; } = string.Empty;

    public IReadOnlyList<string> Metrics { get; init; } = [];
}

public static class JsInteropBridgePreviewFactory
{
    public static JsInteropBridgePreviewSnapshot CreateForWorkbench(CanvasWorkbenchSurface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);

        return new JsInteropBridgePreviewSnapshot
        {
            Title = "Workbench JS calls now route through one minimal bridge seam",
            Summary = "Canvas create, update, focus, zoom, and state retrieval are funneled through a typed wrapper around IJSRuntime instead of binding components directly to raw identifiers everywhere.",
            StatePill = "Shared",
            Metrics =
            [
                "create / update / dispose",
                "fit / focus / zoom",
                "state + clipboard callbacks",
                $"{surface.Nodes.Count} node payload"
            ]
        };
    }
}


