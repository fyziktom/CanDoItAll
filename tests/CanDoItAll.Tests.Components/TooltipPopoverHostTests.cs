using Bunit;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Components.BaseLib;

namespace CanDoItAll.Tests.Components;

public sealed class TooltipPopoverHostTests
{
    [Fact]
    public void Factory_reports_when_annotation_popovers_are_available()
    {
        var surface = new CanvasWorkbenchSurface
        {
            Nodes =
            [
                new CanvasWorkbenchNode
                {
                    Id = "alpha",
                    Title = "Alpha",
                    Annotations =
                    [
                        new CanvasWorkbenchAnnotation
                        {
                            Id = "recommendation",
                            Kind = "info",
                            Label = "Recommendation",
                            Description = "Open recommendation details."
                        }
                    ]
                }
            ],
            Chrome = new CanvasWorkbenchChrome
            {
                TooltipPopover = new CanvasWorkbenchTooltipPopoverOptions
                {
                    IsEnabled = true,
                    FocusTriggers = true,
                    SupportsRichPreview = true
                }
            }
        };

        var snapshot = TooltipPopoverHostFactory.CreateForWorkbench(surface);

        Assert.True(snapshot.IsEnabled);
        Assert.Contains("1 annotation badges", snapshot.Metrics);
        Assert.Contains("Focus + hover", snapshot.Metrics);
    }

    [Fact]
    public void Component_renders_the_preview_card()
    {
        using var context = new TestContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = context.RenderComponent<TooltipPopoverHost>(
            parameters => parameters.Add(component => component.Snapshot, new TooltipPopoverHostSnapshot
            {
                Title = "Contextual popovers are wired",
                Summary = "Hovering badges reveals the shared popover host.",
                StatePill = "Ready",
                Metrics = ["1 annotation badges", "Focus + hover"]
            }));

        Assert.Contains("Contextual popovers are wired", cut.Markup);
        Assert.Contains("Focus + hover", cut.Markup);
    }
}


