using Bunit;
using CanDoItAll.ComponentKit.Canvas;
using CanDoItAll.ComponentKit.Components;

namespace CanDoItAll.Tests.Components;

public sealed class GroupFrameOverlayTests
{
    [Fact]
    public void Factory_projects_persisted_group_frames()
    {
        var surface = new CanvasWorkbenchSurface
        {
            UiState = new CanvasWorkbenchUiState
            {
                GroupFrames = [new CanvasWorkbenchGroupFrame { Id = "frame", Label = "Validation", Tone = "warning", AnchorNodeIds = ["a", "b"] }]
            },
            Nodes = [new CanvasWorkbenchNode { Id = "a" }, new CanvasWorkbenchNode { Id = "b", ParentId = "a" }]
        };

        var snapshot = GroupFrameOverlayFactory.CreateForWorkbench(surface);

        Assert.Single(snapshot.Frames);
        Assert.Contains("1 persisted frames", snapshot.Metrics);
    }

    [Fact]
    public void Component_renders_group_frame_stage()
    {
        using var context = new TestContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = context.RenderComponent<GroupFrameOverlay>(
            parameters => parameters.Add(component => component.Snapshot, new GroupFrameOverlaySnapshot
            {
                Title = "Grouping shells now have a dedicated overlay boundary instead of living only as ad hoc frame data",
                Summary = "Grouping stays explicit.",
                StatePill = "Visible",
                Metrics = ["1 persisted frames"],
                Frames = [new GroupFrameOverlaySample { Label = "Validation cluster", Tone = "warning", NodeCount = 3 }]
            }));

        Assert.Contains("Grouping shells now have a dedicated overlay boundary", cut.Markup);
        Assert.Contains("Validation cluster", cut.Markup);
    }
}
