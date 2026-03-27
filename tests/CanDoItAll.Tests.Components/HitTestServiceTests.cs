using Bunit;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Components.BaseLib;

namespace CanDoItAll.Tests.Components;

public sealed class HitTestServiceTests
{
    [Fact]
    public void Factory_reports_scene_target_counts()
    {
        var surface = new CanvasWorkbenchSurface
        {
            Nodes =
            [
                new CanvasWorkbenchNode { Id = "alpha" },
                new CanvasWorkbenchNode { Id = "beta" }
            ],
            Links =
            [
                new CanvasWorkbenchLink { SourceId = "alpha", TargetId = "beta" }
            ],
            UiState = new CanvasWorkbenchUiState
            {
                GroupFrames =
                [
                    new CanvasWorkbenchGroupFrame { Id = "frame-a" }
                ]
            }
        };

        var snapshot = HitTestServiceFactory.CreateForWorkbench(surface, SelectionModel.From(["alpha"]));

        Assert.Equal("Ready", snapshot.StatePill);
        Assert.Contains("2 node targets", snapshot.Metrics);
        Assert.Contains("1 link targets", snapshot.Metrics);
        Assert.Contains("1 frame targets", snapshot.Metrics);
    }

    [Fact]
    public void Component_renders_hit_test_preview_card()
    {
        using var context = new TestContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = context.RenderComponent<HitTestService>(
            parameters => parameters.Add(component => component.Snapshot, new HitTestServiceSnapshot
            {
                Title = "Pointer hit testing resolves nodes, links, frames, and overlays from one scene model",
                Summary = "Selection and drag intent stay on the same target map.",
                StatePill = "Ready",
                IsEnabled = true,
                Metrics = ["11 node targets", "10 link targets"]
            }));

        Assert.Contains("Pointer hit testing resolves nodes, links, frames, and overlays from one scene model", cut.Markup);
        Assert.Contains("11 node targets", cut.Markup);
    }
}


