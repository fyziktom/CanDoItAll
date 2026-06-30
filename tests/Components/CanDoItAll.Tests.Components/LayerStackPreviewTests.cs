using Bunit;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Components.BaseLib;

namespace CanDoItAll.Tests.Components;

public sealed class LayerStackPreviewTests
{
    [Fact]
    public void Factory_reports_workbench_layer_order()
    {
        var snapshot = LayerStackPreviewFactory.CreateForWorkbench();

        Assert.Contains("Backdrop", snapshot.Metrics);
        Assert.Contains("Accessibility", snapshot.Metrics);
    }

    [Fact]
    public void Component_renders_layer_stack_preview_card()
    {
        using var context = new TestContext();

        var cut = context.RenderComponent<LayerStackPreview>(
            parameters => parameters.Add(component => component.Snapshot, new LayerStackPreviewSnapshot
            {
                Title = "Workbench draw order is formalized as a reusable layer stack",
                Summary = "Layer order is explicit.",
                StatePill = "Ordered",
                Metrics = ["Backdrop", "Connectors", "Nodes"]
            }));

        Assert.Contains("Workbench draw order is formalized as a reusable layer stack", cut.Markup);
        Assert.Contains("Connectors", cut.Markup);
    }
}


