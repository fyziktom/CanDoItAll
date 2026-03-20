using App.Blazor.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace App.Blazor.Services;

public sealed class HarmonicAssistantCanvasInterop(IJSRuntime jsRuntime) : IAsyncDisposable
{
    private readonly IJSRuntime jsRuntime = jsRuntime;
    private readonly SemaphoreSlim gate = new(1, 1);

    private IJSObjectReference? module;
    private int? rendererId;

    public async Task InitializeAsync(ElementReference canvas, CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (module is null)
            {
                module = await jsRuntime.InvokeAsync<IJSObjectReference>(
                    "import",
                    "./harmonicAssistantCanvas.js").ConfigureAwait(false);
            }

            if (!rendererId.HasValue)
            {
                rendererId = await module.InvokeAsync<int>("init", canvas).ConfigureAwait(false);
            }
        }
        catch
        {
            rendererId = null;
            module = null;
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task RenderAsync(HarmonicAssistantCanvasSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        if (!rendererId.HasValue)
        {
            return;
        }

        if (module is null)
        {
            return;
        }

        try
        {
            await module.InvokeVoidAsync("render", rendererId.Value, snapshot).ConfigureAwait(false);
        }
        catch
        {
            rendererId = null;
        }
    }

    public async Task RenderV2Async(HarmonicAssistantCanvasSnapshotV2 snapshot, CancellationToken cancellationToken = default)
    {
        if (!rendererId.HasValue)
        {
            return;
        }

        if (module is null)
        {
            return;
        }

        try
        {
            await module.InvokeVoidAsync("render", rendererId.Value, snapshot).ConfigureAwait(false);
        }
        catch
        {
            rendererId = null;
        }
    }

    public async Task ResizeAsync(CancellationToken cancellationToken = default)
    {
        if (!rendererId.HasValue || module is null)
        {
            return;
        }

        try
        {
            await module.InvokeVoidAsync("resize", rendererId.Value).ConfigureAwait(false);
        }
        catch
        {
            rendererId = null;
        }
    }

    public async Task ObserveResizeAsync(ElementReference element, CancellationToken cancellationToken = default)
    {
        if (!rendererId.HasValue || module is null)
        {
            return;
        }

        try
        {
            await module.InvokeVoidAsync("observeResize", rendererId.Value, element).ConfigureAwait(false);
        }
        catch
        {
            rendererId = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (module is not null && rendererId.HasValue)
            {
                try
                {
                    await module.InvokeVoidAsync("dispose", rendererId.Value).ConfigureAwait(false);
                }
                catch
                {
                    // Ignore cleanup issues during navigation/disposal.
                }
            }

            rendererId = null;
            if (module is not null)
            {
                await module.DisposeAsync().ConfigureAwait(false);
                module = null;
            }
        }
        finally
        {
            gate.Release();
        }
    }
}
