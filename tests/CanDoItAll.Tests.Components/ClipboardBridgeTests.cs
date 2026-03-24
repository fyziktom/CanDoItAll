using Bunit;
using CanDoItAll.ComponentKit.Canvas;
using CanDoItAll.ComponentKit.Components;

namespace CanDoItAll.Tests.Components;

public sealed class ClipboardBridgeTests
{
    [Fact]
    public void Factory_surfaces_the_enabled_clipboard_actions()
    {
        var surface = new CanvasWorkbenchSurface
        {
            Chrome = new CanvasWorkbenchChrome
            {
                Clipboard = new CanvasWorkbenchClipboardOptions
                {
                    IsEnabled = true,
                    AllowCopy = true,
                    AllowPaste = true,
                    AllowDuplicate = true,
                    Format = "cdi.canvas.selection"
                }
            }
        };

        var snapshot = ClipboardBridgeFactory.CreateForWorkbench(surface, SelectionModel.From(["alpha", "beta"]));

        Assert.True(snapshot.IsEnabled);
        Assert.Contains("Paste", snapshot.Metrics);
        Assert.Contains("2 selected", snapshot.Metrics);
    }

    [Fact]
    public void Component_renders_the_preview_card()
    {
        using var context = new TestContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = context.RenderComponent<ClipboardBridge>(
            parameters => parameters.Add(component => component.Snapshot, new ClipboardBridgeSnapshot
            {
                Title = "Copy, paste, and duplicate are wired",
                Summary = "Selection payloads are serialized through the shared bridge.",
                StatePill = "Ready",
                Metrics = ["cdi.canvas.selection", "Copy", "Paste"]
            }));

        Assert.Contains("Copy, paste, and duplicate are wired", cut.Markup);
        Assert.Contains("cdi.canvas.selection", cut.Markup);
    }
}
