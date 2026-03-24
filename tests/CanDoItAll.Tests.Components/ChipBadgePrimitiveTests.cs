using Bunit;
using CanDoItAll.ComponentKit.Canvas;
using CanDoItAll.ComponentKit.Components;

namespace CanDoItAll.Tests.Components;

public sealed class ChipBadgePrimitiveTests
{
    [Fact]
    public void Factory_collects_chip_samples_from_node_data()
    {
        var surface = new CanvasWorkbenchSurface
        {
            Nodes =
            [
                new CanvasWorkbenchNode
                {
                    Id = "alpha",
                    Chips = [new CanvasWorkbenchChip { Text = "Ready", Tone = "success" }],
                    FooterChips = [new CanvasWorkbenchChip { Text = "High", Tone = "accent" }]
                }
            ]
        };

        var snapshot = ChipBadgePrimitiveFactory.CreateForWorkbench(surface);

        Assert.Equal(2, snapshot.Chips.Count);
        Assert.Contains("1 nodes with chip data", snapshot.Metrics);
    }

    [Fact]
    public void Component_renders_chip_samples()
    {
        using var context = new TestContext();

        var cut = context.RenderComponent<ChipBadgePrimitive>(
            parameters => parameters.Add(component => component.Snapshot, new ChipBadgePrimitiveSnapshot
            {
                Title = "Compact badges now reuse one chip contract instead of being painted ad hoc in cards and menus",
                Summary = "Tone-aware chips stay aligned.",
                StatePill = "Ready",
                Metrics = ["3 chip samples"],
                Chips =
                [
                    new ChipBadgePrimitiveSample { Text = "Ready", Tone = "success", Icon = "OK" }
                ]
            }));

        Assert.Contains("Compact badges now reuse one chip contract", cut.Markup);
        Assert.Contains("Ready", cut.Markup);
    }
}
