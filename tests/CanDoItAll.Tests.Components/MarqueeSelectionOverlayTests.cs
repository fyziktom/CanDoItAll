using Bunit;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Components.BaseLib;

namespace CanDoItAll.Tests.Components;

public sealed class MarqueeSelectionOverlayTests
{
    [Fact]
    public void Factory_reports_when_marquee_selection_is_armed()
    {
        var surface = new CanvasWorkbenchSurface
        {
            Nodes =
            [
                new CanvasWorkbenchNode { Id = "alpha", Title = "Alpha" },
                new CanvasWorkbenchNode { Id = "beta", Title = "Beta" }
            ],
            Chrome = new CanvasWorkbenchChrome
            {
                MarqueeSelection = new CanvasWorkbenchMarqueeOptions
                {
                    IsEnabled = true,
                    ModifierKey = "Alt",
                    SelectionMode = "Intersect"
                }
            }
        };

        var snapshot = MarqueeSelectionOverlayFactory.CreateForWorkbench(surface, SelectionModel.From(["alpha"]));

        Assert.True(snapshot.IsEnabled);
        Assert.Contains("Alt modifier", snapshot.Metrics);
        Assert.Contains("Intersect mode", snapshot.Metrics);
    }

    [Fact]
    public void Component_renders_the_preview_card()
    {
        using var context = new TestContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = context.RenderComponent<MarqueeSelectionOverlay>(
            parameters => parameters.Add(component => component.Snapshot, new MarqueeSelectionOverlaySnapshot
            {
                Title = "Alt-drag marquee selection is armed",
                Summary = "The shared workbench can box-select intersecting nodes.",
                StatePill = "Armed",
                Metrics = ["Alt modifier", "Intersect mode", "2 selected"]
            }));

        Assert.Contains("Alt-drag marquee selection is armed", cut.Markup);
        Assert.Contains("Intersect mode", cut.Markup);
    }
}


