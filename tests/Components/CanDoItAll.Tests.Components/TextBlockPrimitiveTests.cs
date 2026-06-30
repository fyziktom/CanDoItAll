using Bunit;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Components.BaseLib;

namespace CanDoItAll.Tests.Components;

public sealed class TextBlockPrimitiveTests
{
    [Fact]
    public void Factory_measures_title_subtitle_and_overflow_blocks()
    {
        var surface = new CanvasWorkbenchSurface
        {
            Nodes =
            [
                new CanvasWorkbenchNode
                {
                    Id = "alpha",
                    Title = "Shared canvas title",
                    Subtitle = "Unified subtitle copy",
                    InlineText = "A longer inline text sample that should wrap into the overflow preview."
                }
            ]
        };

        var snapshot = TextBlockPrimitiveFactory.CreateForWorkbench(surface);

        Assert.Equal(3, snapshot.Samples.Count);
        Assert.Contains("3 text samples", snapshot.Metrics);
    }

    [Fact]
    public void Component_renders_text_samples()
    {
        using var context = new TestContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = context.RenderComponent<TextBlockPrimitive>(
            parameters => parameters.Add(component => component.Snapshot, new TextBlockPrimitiveSnapshot
            {
                Title = "Titles, captions, and overflow copy now share one clamping and wrapping primitive",
                Summary = "Text layout stays consistent.",
                StatePill = "Clamped",
                Metrics = ["3 text samples"],
                Samples =
                [
                    new TextBlockPrimitiveSample
                    {
                        Label = "Title",
                        FullText = "Shared title",
                        DisplayText = "Shared title",
                        LineCount = 1
                    }
                ]
            }));

        Assert.Contains("Titles, captions, and overflow copy now share one clamping", cut.Markup);
        Assert.Contains("Shared title", cut.Markup);
    }
}


