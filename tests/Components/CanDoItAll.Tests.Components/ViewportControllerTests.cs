using Bunit;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Components.BaseLib;

namespace CanDoItAll.Tests.Components;

public sealed class ViewportControllerTests
{
    [Fact]
    public void Controller_creates_fit_focus_and_anchor_zoom_targets()
    {
        var controller = new ViewportController();
        var bounds = new ViewportSceneBounds
        {
            MinX = 40,
            MaxX = 440,
            MinY = 120,
            MaxY = 520
        };
        var hostFrame = new ViewportFrame
        {
            Width = 960,
            Height = 640
        };

        var fitTarget = controller.CreateFitViewTarget(bounds, hostFrame);
        var focusTarget = controller.CreateFocusTarget(
            new ViewportPoint
            {
                X = 280,
                Y = 320
            },
            1.1,
            bounds,
            hostFrame);
        var zoomTarget = controller.ZoomAroundPoint(
            new ViewportState
            {
                Zoom = 1,
                PanX = 120,
                PanY = 140
            },
            135,
            new ViewportPoint
            {
                X = 480,
                Y = 320
            },
            bounds,
            hostFrame);

        Assert.InRange(fitTarget.Zoom, 0.15, 1.75);
        Assert.Equal(1.1, focusTarget.Zoom, 2);
        Assert.InRange(zoomTarget.Zoom, 1.34, 1.36);
        Assert.NotEqual(120, zoomTarget.PanX);
        Assert.NotEqual(140, zoomTarget.PanY);
    }

    [Fact]
    public void Preview_factory_surfaces_fit_focus_and_coordinate_metrics()
    {
        var surface = new CanvasWorkbenchSurface
        {
            Nodes =
            [
                new CanvasWorkbenchNode
                {
                    Id = "root",
                    Title = "Root node",
                    X = 120,
                    Y = 160
                },
                new CanvasWorkbenchNode
                {
                    Id = "selected",
                    Title = "Selected dependency",
                    Subtitle = "Critical path",
                    X = 520,
                    Y = 340
                }
            ],
            UiState = new CanvasWorkbenchUiState
            {
                Zoom = 1.1,
                PanX = -48,
                PanY = 92,
                SelectedNodeIds = ["selected"]
            }
        };

        var snapshot = ViewportControllerPreviewFactory.CreateForWorkbench(surface);

        Assert.Equal("Viewport controller", snapshot.Label);
        Assert.Equal("Selected dependency", snapshot.FocusNodeLabel);
        Assert.Contains(snapshot.Metrics, metric => metric.Contains("current zoom", StringComparison.Ordinal));
        Assert.Contains(snapshot.Cards, card => card.Label == "Fit to view");
        Assert.Contains(snapshot.Cards, card => card.Label == "Coordinate map");
    }

    [Fact]
    public void Preview_component_renders_stage_cards_and_metrics()
    {
        using var context = new TestContext();

        var cut = context.RenderComponent<ViewportControllerPreview>(
            parameters => parameters.Add(component => component.Snapshot, new ViewportControllerPreviewSnapshot
            {
                Title = "Shared viewport now owns zoom, pan, fit, focus, and coordinate mapping",
                Summary = "Toolbar actions and wheel anchors route through one controller.",
                StatePill = "Live",
                Metrics = ["110% current zoom", "11 nodes", "1 selected"],
                CurrentZoomLabel = "110%",
                CurrentPanLabel = "-48, 92",
                FitZoomLabel = "84%",
                FocusNodeLabel = "Selected dependency",
                SceneCenterLabel = "190, 154",
                Cards =
                [
                    new ViewportControllerPreviewCard
                    {
                        Label = "Fit to view",
                        ValueLabel = "84%",
                        Summary = "Frames the graph before clamping."
                    },
                    new ViewportControllerPreviewCard
                    {
                        Label = "Focus node",
                        ValueLabel = "Selected dependency",
                        Summary = "Centers the primary selection."
                    }
                ]
            }));

        Assert.Contains("Viewport controller", cut.Markup);
        Assert.Contains("Selected dependency", cut.Markup);
        Assert.Contains("Fit to view", cut.Markup);
        Assert.Contains("Scene center 190, 154", cut.Markup);
    }
}


