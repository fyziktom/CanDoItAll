using Bunit;
using CanDoItAll.ComponentKit.Canvas;
using CanDoItAll.ComponentKit.Components;

namespace CanDoItAll.Tests.Components;

public sealed class SkeletonStateOverlayTests
{
    [Fact]
    public void Factory_marks_the_snapshot_visible_when_the_scene_is_empty()
    {
        var snapshot = SkeletonStateOverlayFactory.CreateForWorkbench(new CanvasWorkbenchSurface());

        Assert.True(snapshot.IsVisible);
        Assert.Equal("Visible", snapshot.StatePill);
    }

    [Fact]
    public void Factory_can_create_an_explicit_loading_snapshot()
    {
        var snapshot = SkeletonStateOverlayFactory.CreateLoadingSnapshot(
            "Loading project structure",
            "Shared loading chrome keeps the stage stable.");

        Assert.True(snapshot.IsVisible);
        Assert.Contains("Busy region", snapshot.Metrics);
    }

    [Fact]
    public void Component_renders_the_skeleton_preview()
    {
        using var context = new TestContext();

        var cut = context.RenderComponent<SkeletonStateOverlay>(
            parameters => parameters.Add(component => component.Snapshot, new SkeletonStateOverlaySnapshot
            {
                Title = "Loading project structure",
                Summary = "Shared loading chrome keeps the stage stable.",
                StatePill = "Visible",
                IsVisible = true,
                StageCardCount = 2,
                InspectorBlockCount = 1,
                Metrics = ["Toolbar chrome", "Busy region"]
            }));

        Assert.Contains("Loading project structure", cut.Markup);
        Assert.Contains("cw-skeleton-card", cut.Markup);
        Assert.Contains("aria-busy", cut.Markup);
    }
}
