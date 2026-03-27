using Bunit;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Components.BaseLib;

namespace CanDoItAll.Tests.Components;

public sealed class ConnectorAnchorOverlayTests
{
    [Fact]
    public void Factory_reports_visible_connector_anchors_for_selected_nodes()
    {
        var surface = new CanvasWorkbenchSurface
        {
            Nodes =
            [
                new CanvasWorkbenchNode { Id = "alpha", Title = "Alpha" },
                new CanvasWorkbenchNode { Id = "beta", Title = "Beta" }
            ],
            Links =
            [
                new CanvasWorkbenchLink { SourceId = "alpha", TargetId = "beta", Kind = "dependency" }
            ],
            Chrome = new CanvasWorkbenchChrome
            {
                ConnectorAnchors = new CanvasWorkbenchConnectorAnchorOptions
                {
                    IsEnabled = true,
                    ShowOnHover = true,
                    ShowOnSelection = true,
                    PlacementMode = "Edges"
                }
            }
        };

        var snapshot = ConnectorAnchorOverlayFactory.CreateForWorkbench(surface, SelectionModel.From(["alpha"]));

        Assert.True(snapshot.IsVisible);
        Assert.Contains("Selection anchors", snapshot.Metrics);
        Assert.Contains("1 links", snapshot.Metrics);
    }

    [Fact]
    public void Component_renders_the_preview_card()
    {
        using var context = new TestContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = context.RenderComponent<ConnectorAnchorOverlay>(
            parameters => parameters.Add(component => component.Snapshot, new ConnectorAnchorOverlaySnapshot
            {
                Title = "Connector anchors are visible on intent",
                Summary = "Selected or hovered nodes expose shared anchor affordances.",
                StatePill = "Live",
                Metrics = ["Selection anchors", "Hover anchors", "2 links"]
            }));

        Assert.Contains("Connector anchors are visible on intent", cut.Markup);
        Assert.Contains("Hover anchors", cut.Markup);
    }
}


