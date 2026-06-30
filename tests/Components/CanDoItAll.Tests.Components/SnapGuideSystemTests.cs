using Bunit;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Components.BaseLib;

namespace CanDoItAll.Tests.Components;

public sealed class SnapGuideSystemTests
{
    [Fact]
    public void Factory_reports_snap_guides_when_multiple_nodes_can_align()
    {
        var surface = new CanvasWorkbenchSurface
        {
            Nodes =
            [
                new CanvasWorkbenchNode { Id = "alpha", Title = "Alpha" },
                new CanvasWorkbenchNode { Id = "beta", Title = "Beta" },
                new CanvasWorkbenchNode { Id = "gamma", Title = "Gamma" }
            ],
            Chrome = new CanvasWorkbenchChrome
            {
                SnapGuides = new CanvasWorkbenchSnapGuideOptions
                {
                    IsEnabled = true,
                    Tolerance = 14,
                    ModifierPolicy = "ShiftBypassesSnap"
                }
            }
        };

        var snapshot = SnapGuideSystemFactory.CreateForWorkbench(surface, SelectionModel.From(["alpha"]));

        Assert.True(snapshot.IsEnabled);
        Assert.Contains("14px tolerance", snapshot.Metrics);
        Assert.Contains("ShiftBypassesSnap", snapshot.Metrics);
    }

    [Fact]
    public void Component_renders_the_preview_card()
    {
        using var context = new TestContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = context.RenderComponent<SnapGuideSystem>(
            parameters => parameters.Add(component => component.Snapshot, new SnapGuideSystemSnapshot
            {
                Title = "Snap guides are active during drag",
                Summary = "Dragging nodes can align to nearby siblings and surface guide lines.",
                StatePill = "Active",
                Metrics = ["18px tolerance", "ShiftBypassesSnap", "4 nearby candidates"]
            }));

        Assert.Contains("Snap guides are active during drag", cut.Markup);
        Assert.Contains("18px tolerance", cut.Markup);
    }
}


