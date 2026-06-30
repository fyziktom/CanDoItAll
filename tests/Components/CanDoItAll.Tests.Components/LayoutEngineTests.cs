using Bunit;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Components.BaseLib;

namespace CanDoItAll.Tests.Components;

public sealed class LayoutEngineTests
{
    [Fact]
    public void Factory_reports_manual_auto_and_branch_lane_metrics()
    {
        var surface = new CanvasWorkbenchSurface
        {
            Nodes =
            [
                new CanvasWorkbenchNode { Id = "alpha", BranchLabel = "Main" },
                new CanvasWorkbenchNode { Id = "beta", BranchLabel = "Review" },
                new CanvasWorkbenchNode { Id = "gamma", BranchLabel = "Main" }
            ],
            UiState = new CanvasWorkbenchUiState
            {
                ManualPositions =
                {
                    ["alpha"] = new CanvasWorkbenchPoint { X = 10, Y = 20 }
                },
                GroupFrames =
                [
                    new CanvasWorkbenchGroupFrame { Id = "frame-a" }
                ]
            }
        };

        var snapshot = LayoutEngineFactory.CreateForWorkbench(surface);

        Assert.Equal("Ready", snapshot.StatePill);
        Assert.Contains("1 manual positions", snapshot.Metrics);
        Assert.Contains("2 auto positions", snapshot.Metrics);
        Assert.Contains("2 branch lanes", snapshot.Metrics);
    }

    [Fact]
    public void Component_renders_layout_engine_preview_card()
    {
        using var context = new TestContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = context.RenderComponent<LayoutEngine>(
            parameters => parameters.Add(component => component.Snapshot, new LayoutEngineSnapshot
            {
                Title = "Placement resolution now has a shared layout engine boundary",
                Summary = "Manual and auto layout are both surfaced.",
                StatePill = "Ready",
                IsEnabled = true,
                Metrics = ["3 manual positions", "8 auto positions"]
            }));

        Assert.Contains("Placement resolution now has a shared layout engine boundary", cut.Markup);
        Assert.Contains("8 auto positions", cut.Markup);
    }
}


