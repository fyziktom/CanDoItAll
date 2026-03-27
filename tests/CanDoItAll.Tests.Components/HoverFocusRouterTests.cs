using Bunit;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Components.BaseLib;

namespace CanDoItAll.Tests.Components;

public sealed class HoverFocusRouterTests
{
    [Fact]
    public void Factory_reports_primary_focus_and_annotation_metrics()
    {
        var surface = new CanvasWorkbenchSurface
        {
            Nodes =
            [
                new CanvasWorkbenchNode
                {
                    Id = "alpha",
                    Title = "Alpha",
                    Annotations = [new CanvasWorkbenchAnnotation { Id = "a1", Label = "Review" }]
                },
                new CanvasWorkbenchNode { Id = "beta", Title = "Beta" }
            ],
            Chrome = new CanvasWorkbenchChrome
            {
                TooltipPopover = new CanvasWorkbenchTooltipPopoverOptions
                {
                    FocusTriggers = true,
                    SupportsRichPreview = true
                }
            }
        };

        var snapshot = HoverFocusRouterFactory.CreateForWorkbench(surface, SelectionModel.From(["alpha"]));

        Assert.Equal("Focused", snapshot.StatePill);
        Assert.Contains("Alpha", snapshot.Metrics);
        Assert.Contains("1 annotated nodes", snapshot.Metrics);
        Assert.Contains("Focus triggers", snapshot.Metrics);
    }

    [Fact]
    public void Component_renders_hover_focus_preview_card()
    {
        using var context = new TestContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = context.RenderComponent<HoverFocusRouter>(
            parameters => parameters.Add(component => component.Snapshot, new HoverFocusRouterSnapshot
            {
                Title = "Hover and focus now stay coherent around Escalated dependency",
                Summary = "Focus and annotation popovers stay synchronized.",
                StatePill = "Focused",
                IsEnabled = true,
                Metrics = ["Escalated dependency", "4 annotated nodes"]
            }));

        Assert.Contains("Hover and focus now stay coherent around Escalated dependency", cut.Markup);
        Assert.Contains("4 annotated nodes", cut.Markup);
    }
}


