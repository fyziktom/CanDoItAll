using Bunit;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Components.BaseLib;

namespace CanDoItAll.Tests.Components;

public sealed class MinimapOverviewTests
{
    [Fact]
    public void Factory_reports_when_the_minimap_is_live()
    {
        var surface = new CanvasWorkbenchSurface
        {
            Nodes = [new CanvasWorkbenchNode { Id = "alpha", Title = "Alpha" }],
            UiState = new CanvasWorkbenchUiState { ShowMinimap = true },
            Chrome = new CanvasWorkbenchChrome
            {
                Minimap = new CanvasWorkbenchMinimapOptions
                {
                    IsEnabled = true,
                    Title = "Scene overview"
                }
            }
        };

        var snapshot = MinimapOverviewFactory.CreateForWorkbench(surface, SelectionModel.From(["alpha"]));

        Assert.True(snapshot.IsVisible);
        Assert.Contains("Scene overview", snapshot.Metrics);
    }

    [Fact]
    public void Component_renders_the_preview_card()
    {
        using var context = new TestContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = context.RenderComponent<MinimapOverview>(
            parameters => parameters.Add(component => component.Snapshot, new MinimapOverviewSnapshot
            {
                Title = "Minimap viewport is live",
                Summary = "A live viewport rectangle is visible over the scene overview.",
                StatePill = "Live",
                Metrics = ["11 nodes", "10 links"]
            }));

        Assert.Contains("Minimap viewport is live", cut.Markup);
        Assert.Contains("10 links", cut.Markup);
    }
}


