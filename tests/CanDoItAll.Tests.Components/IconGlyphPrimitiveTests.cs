using Bunit;
using CanDoItAll.ComponentKit.Canvas;
using CanDoItAll.ComponentKit.Components;

namespace CanDoItAll.Tests.Components;

public sealed class IconGlyphPrimitiveTests
{
    [Fact]
    public void Factory_collects_node_and_action_glyphs()
    {
        var surface = new CanvasWorkbenchSurface
        {
            Nodes = [new CanvasWorkbenchNode { Id = "alpha", Title = "Alpha", Icon = "[]" }],
            Chrome = new CanvasWorkbenchChrome
            {
                QuickCreateActions = [new CanvasWorkbenchAction { ActionId = "open", Label = "Open", Icon = "->" }]
            }
        };

        var snapshot = IconGlyphPrimitiveFactory.CreateForWorkbench(surface);

        Assert.Equal(2, snapshot.Glyphs.Count);
        Assert.Contains("1 action icons", snapshot.Metrics);
    }

    [Fact]
    public void Component_renders_icon_tiles()
    {
        using var context = new TestContext();

        var cut = context.RenderComponent<IconGlyphPrimitive>(
            parameters => parameters.Add(component => component.Snapshot, new IconGlyphPrimitiveSnapshot
            {
                Title = "Symbolic markers now come from one glyph primitive with shared sizing and baseline rules",
                Summary = "Glyph sizing stays unified.",
                StatePill = "Mapped",
                Metrics = ["2 glyph samples"],
                Glyphs =
                [
                    new IconGlyphPrimitiveSample { Glyph = "[]", Label = "Card" }
                ]
            }));

        Assert.Contains("Symbolic markers now come from one glyph primitive", cut.Markup);
        Assert.Contains("Card", cut.Markup);
    }
}
