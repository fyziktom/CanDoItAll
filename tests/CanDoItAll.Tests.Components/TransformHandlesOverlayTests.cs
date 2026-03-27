using Bunit;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Components.BaseLib;

namespace CanDoItAll.Tests.Components;

public sealed class TransformHandlesOverlayTests
{
    [Fact]
    public void Factory_surfaces_transform_handles_for_the_current_selection()
    {
        var surface = new CanvasWorkbenchSurface
        {
            Nodes =
            [
                new CanvasWorkbenchNode { Id = "alpha", Title = "Alpha" },
                new CanvasWorkbenchNode { Id = "beta", Title = "Beta", IsReadOnly = true }
            ],
            Chrome = new CanvasWorkbenchChrome
            {
                TransformHandles = new CanvasWorkbenchTransformHandleOptions
                {
                    IsEnabled = true,
                    ShowResizeHandles = true,
                    ShowRotateHandle = true,
                    PlacementMode = "SelectionBounds"
                }
            }
        };

        var snapshot = TransformHandlesOverlayFactory.CreateForWorkbench(surface, SelectionModel.From(["alpha"]));

        Assert.True(snapshot.IsVisible);
        Assert.Equal("Live", snapshot.StatePill);
        Assert.Contains("Resize handles", snapshot.Metrics);
    }

    [Fact]
    public void Factory_marks_read_only_selections()
    {
        var surface = new CanvasWorkbenchSurface
        {
            Nodes =
            [
                new CanvasWorkbenchNode { Id = "beta", Title = "Beta", IsReadOnly = true }
            ]
        };

        var snapshot = TransformHandlesOverlayFactory.CreateForWorkbench(surface, SelectionModel.From(["beta"]));

        Assert.Equal("Read-only", snapshot.StatePill);
        Assert.Contains("Read-only selection", snapshot.Metrics);
    }

    [Fact]
    public void Component_renders_the_preview_card()
    {
        using var context = new TestContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = context.RenderComponent<TransformHandlesOverlay>(
            parameters => parameters.Add(component => component.Snapshot, new TransformHandlesOverlaySnapshot
            {
                Title = "Transform handles wrap the current selection",
                Summary = "Selected nodes render shared bounds and resize affordances.",
                StatePill = "Live",
                Metrics = ["1 selected", "Resize handles", "Rotate cue"]
            }));

        Assert.Contains("Transform handles wrap the current selection", cut.Markup);
        Assert.Contains("Rotate cue", cut.Markup);
    }
}


