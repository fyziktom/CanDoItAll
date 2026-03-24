using Bunit;
using CanDoItAll.ComponentKit.Canvas;
using CanDoItAll.ComponentKit.Components;

namespace CanDoItAll.Tests.Components;

public sealed class ContextMenuHostTests
{
    [Fact]
    public void Factory_prefers_selected_node_actions()
    {
        var surface = new CanvasWorkbenchSurface
        {
            Nodes =
            [
                new CanvasWorkbenchNode
                {
                    Id = "alpha",
                    ContextActions =
                    [
                        new CanvasWorkbenchAction { ActionId = "open", Label = "Open" },
                        new CanvasWorkbenchAction { ActionId = "branch", Label = "Branch", Children = [new CanvasWorkbenchAction { ActionId = "child", Label = "Child" }] }
                    ]
                }
            ]
        };

        var snapshot = ContextMenuHostFactory.CreateForWorkbench(surface, SelectionModel.From(["alpha"]));

        Assert.Equal(2, snapshot.Actions.Count);
        Assert.Contains("1 nested actions", snapshot.Metrics);
    }

    [Fact]
    public void Component_renders_context_menu_items()
    {
        using var context = new TestContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = context.RenderComponent<ContextMenuHost>(
            parameters => parameters.Add(component => component.Snapshot, new ContextMenuHostSnapshot
            {
                Title = "Context actions now have a reusable host for placement, nesting, and dismissal",
                Summary = "Menu focus stays shared.",
                StatePill = "Bound",
                Metrics = ["3 visible actions"],
                Actions =
                [
                    new CanvasWorkbenchAction { ActionId = "open", Label = "Open" }
                ]
            }));

        Assert.Contains("Context actions now have a reusable host", cut.Markup);
        Assert.Contains("Open", cut.Markup);
    }
}
