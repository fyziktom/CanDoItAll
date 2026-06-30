using Bunit;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Components.BaseLib;

namespace CanDoItAll.Tests.Components;

public sealed class JsInteropBridgePreviewTests
{
    [Fact]
    public void Factory_reports_shared_js_identifier_groups()
    {
        var snapshot = JsInteropBridgePreviewFactory.CreateForWorkbench(new CanvasWorkbenchSurface
        {
            Nodes = [new CanvasWorkbenchNode { Id = "alpha" }]
        });

        Assert.Contains("create / update / dispose", snapshot.Metrics);
        Assert.Contains("fit / focus / zoom", snapshot.Metrics);
    }

    [Fact]
    public void Component_renders_js_interop_bridge_preview_card()
    {
        using var context = new TestContext();

        var cut = context.RenderComponent<JsInteropBridgePreview>(
            parameters => parameters.Add(component => component.Snapshot, new JsInteropBridgePreviewSnapshot
            {
                Title = "Workbench JS calls now route through one minimal bridge seam",
                Summary = "Identifiers are no longer scattered across components.",
                StatePill = "Shared",
                Metrics = ["create / update / dispose", "fit / focus / zoom"]
            }));

        Assert.Contains("Workbench JS calls now route through one minimal bridge seam", cut.Markup);
        Assert.Contains("fit / focus / zoom", cut.Markup);
    }
}


