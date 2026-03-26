using Bunit;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Components.BaseLib;

namespace CanDoItAll.Tests.Components;

public sealed class ContainerPrimitiveTests
{
    [Fact]
    public void Factory_projects_selection_and_read_only_state()
    {
        var surface = new CanvasWorkbenchSurface
        {
            Nodes =
            [
                new CanvasWorkbenchNode
                {
                    Id = "alpha",
                    Title = "Selected card",
                    IsReadOnly = true
                }
            ]
        };

        var snapshot = ContainerPrimitiveFactory.CreateForWorkbench(surface, SelectionModel.From(["alpha"]));

        Assert.True(snapshot.IsSelected);
        Assert.True(snapshot.IsReadOnly);
        Assert.Contains("1 read-only nodes", snapshot.Metrics);
    }

    [Fact]
    public void Component_renders_container_shell()
    {
        using var context = new TestContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = context.RenderComponent<ContainerPrimitive>(
            parameters => parameters.Add(component => component.Snapshot, new ContainerPrimitiveSnapshot
            {
                Title = "Card, frame, and popover shells now share one container primitive",
                Summary = "Shared surface rules stay aligned.",
                StatePill = "Selected",
                Metrics = ["2 node-backed containers"],
                Kicker = "Architecture note",
                Body = "Selection chrome is now shared.",
                Footer = "Ready",
                IsSelected = true
            }));

        Assert.Contains("Card, frame, and popover shells now share one container primitive", cut.Markup);
        Assert.Contains("Architecture note", cut.Markup);
    }
}


