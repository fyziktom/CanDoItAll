using Microsoft.JSInterop;

namespace CanDoItAll.ComponentKit.Canvas;

public sealed class JsInteropBridge(IJSRuntime jsRuntime)
{
    public ValueTask InvokeVoidAsync(string identifier, params object?[] args)
        => jsRuntime.InvokeVoidAsync(identifier, args);

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, params object?[] args)
        => jsRuntime.InvokeAsync<TValue>(identifier, args);
}
