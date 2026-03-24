using Bunit;
using CanDoItAll.ComponentKit.Canvas;
using CanDoItAll.ComponentKit.Components;

namespace CanDoItAll.Tests.Components;

public sealed class GridBackdropTests
{
    [Fact]
    public void Factory_reports_zoom_aware_grid_metrics()
    {
        var surface = new CanvasWorkbenchSurface
        {
            UiState = new CanvasWorkbenchUiState
            {
                Zoom = 1.25,
                PanX = 44.4,
                PanY = -18.2,
                ShowMinimap = true
            }
        };

        var snapshot = GridBackdropFactory.CreateForWorkbench(surface);

        Assert.True(snapshot.IsEnabled);
        Assert.Contains("125% zoom", snapshot.Metrics);
        Assert.Contains("Overview aligned", snapshot.Metrics);
    }

    [Fact]
    public void Component_renders_grid_backdrop_preview_card()
    {
        using var context = new TestContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = context.RenderComponent<GridBackdrop>(
            parameters => parameters.Add(component => component.Snapshot, new GridBackdropSnapshot
            {
                Title = "Zoom-aware grid framing now comes from one shared backdrop",
                Summary = "Shared spacing cues are now explicit.",
                StatePill = "Live",
                IsEnabled = true,
                Metrics = ["35px major spacing", "125% zoom"]
            }));

        Assert.Contains("Zoom-aware grid framing now comes from one shared backdrop", cut.Markup);
        Assert.Contains("35px major spacing", cut.Markup);
    }
}
