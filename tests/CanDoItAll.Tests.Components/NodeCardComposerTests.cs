using Bunit;
using CanDoItAll.ComponentKit.Canvas;
using CanDoItAll.ComponentKit.Components;

namespace CanDoItAll.Tests.Components;

public sealed class NodeCardComposerTests
{
    [Fact]
    public void Factory_projects_selected_node_as_composed_card()
    {
        var surface = new CanvasWorkbenchSurface
        {
            Nodes =
            [
                new CanvasWorkbenchNode
                {
                    Id = "alpha",
                    Title = "Architecture note",
                    Subtitle = "Shared canvas",
                    LeadText = "Preview composition",
                    ContextActions = [new CanvasWorkbenchAction { ActionId = "open", Label = "Open" }],
                    Chips = [new CanvasWorkbenchChip { Text = "Ready", Tone = "success" }]
                }
            ]
        };

        var snapshot = NodeCardComposerFactory.CreateForWorkbench(surface, SelectionModel.From(["alpha"]));

        Assert.Equal("Architecture note", snapshot.CardTitle);
        Assert.Contains("1 chip slots", snapshot.Metrics);
    }

    [Fact]
    public void Component_renders_composed_node_card()
    {
        using var context = new TestContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = context.RenderComponent<NodeCardComposer>(
            parameters => parameters.Add(component => component.Snapshot, new NodeCardComposerSnapshot
            {
                Title = "Node cards now compose from explicit primitives instead of a single monolithic renderer",
                Summary = "Card composition stays explicit.",
                StatePill = "Selection sample",
                Metrics = ["4 projected node cards"],
                Icon = "[]",
                CardTitle = "Architecture note",
                CardSubtitle = "Shared canvas",
                LeadText = "Preview composition",
                StatusPill = "Ready",
                Chips = [new CanvasWorkbenchChip { Text = "Ready", Tone = "success" }]
            }));

        Assert.Contains("Node cards now compose from explicit primitives", cut.Markup);
        Assert.Contains("Architecture note", cut.Markup);
    }
}
