using Bunit;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Components.BaseLib;

namespace CanDoItAll.Tests.Components;

public sealed class EmptyStateOverlayTests
{
    [Fact]
    public void Factory_marks_the_snapshot_visible_when_there_are_no_nodes()
    {
        var surface = new CanvasWorkbenchSurface
        {
            Chrome = new CanvasWorkbenchChrome
            {
                EmptyStateKicker = "Canvas",
                EmptyStateTitle = "No nodes yet",
                EmptyStateDescription = "Use quick create to get started."
            }
        };

        var snapshot = EmptyStateOverlayFactory.CreateForWorkbench(surface);

        Assert.True(snapshot.IsVisible);
        Assert.Equal("Visible", snapshot.StatePill);
    }

    [Fact]
    public void Component_renders_the_preview_card()
    {
        using var context = new TestContext();

        var cut = context.RenderComponent<EmptyStateOverlay>(
            parameters => parameters.Add(component => component.Snapshot, new EmptyStateOverlaySnapshot
            {
                Title = "No nodes yet",
                Summary = "Use quick create to get started.",
                StatePill = "Visible",
                Metrics = ["Canvas", "0 nodes"]
            }));

        Assert.Contains("No nodes yet", cut.Markup);
        Assert.Contains("Visible", cut.Markup);
    }
}


