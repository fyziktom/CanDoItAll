using Bunit;
using CanDoItAll.ComponentKit.Canvas;
using CanDoItAll.ComponentKit.Components;

namespace CanDoItAll.Tests.Components;

public sealed class TextMeasureServiceTests
{
    [Fact]
    public void Measure_wraps_and_truncates_multiline_text()
    {
        var service = new TextMeasureService();

        var result = service.Measure(new TextMeasureRequest
        {
            Text = "North star delivery plan with approvals and external dependencies",
            MaxWidth = 124,
            MaxLines = 2,
            Font = new TextMeasureFontSpec
            {
                SizePx = 14,
                Weight = 700,
                LineHeightPx = 18
            }
        });

        Assert.True(result.IsTruncated);
        Assert.Equal(2, result.LineCount);
        Assert.Equal("North star" + Environment.NewLine + "delivery plan...", result.DisplayText);
        Assert.Equal("North star delivery plan with approvals and external dependencies", result.FullText);
    }

    [Fact]
    public void Measure_handles_long_words_and_emoji_without_throwing()
    {
        var service = new TextMeasureService();

        var result = service.Measure(new TextMeasureRequest
        {
            Text = "Deployment\U0001F680CoordinationSequence",
            MaxWidth = 118,
            MaxLines = 1,
            Font = new TextMeasureFontSpec
            {
                SizePx = 12,
                Weight = 700,
                LineHeightPx = 16
            }
        });

        Assert.True(result.IsTruncated);
        Assert.EndsWith("...", result.DisplayText, StringComparison.Ordinal);
        Assert.Equal(1, result.LineCount);
    }

    [Fact]
    public void Preview_factory_surfaces_graph_and_calendar_samples()
    {
        var surface = new CanvasWorkbenchSurface
        {
            Nodes =
            [
                new CanvasWorkbenchNode
                {
                    Id = "alpha",
                    Title = "North star delivery plan with approvals and external dependencies",
                    Subtitle = "Client planning sync with logistics and venue notes",
                    Chips =
                    [
                        new CanvasWorkbenchChip
                        {
                            Text = "Validation required before Friday handoff",
                            Tone = "warn"
                        }
                    ]
                }
            ]
        };

        var snapshot = TextMeasurePreviewFactory.CreateForWorkbench(surface);

        Assert.Equal(3, snapshot.Samples.Count);
        Assert.Contains("Graph + calendar", snapshot.Metrics);
        Assert.Contains(snapshot.Samples, sample => sample.Result.IsTruncated);
    }

    [Fact]
    public void Preview_component_renders_measurement_samples()
    {
        using var context = new TestContext();

        var cut = context.RenderComponent<TextMeasureServicePreview>(
            parameters => parameters.Add(component => component.Snapshot, new TextMeasurePreviewSnapshot
            {
                Title = "Shared text fitting now owns truncation and line-clamp rules",
                Summary = "Workbench node estimates, radial menu labels, and calendar tiles now route through the same seam.",
                StatePill = "Ready",
                Metrics = ["Graph + calendar", "Canvas + DOM"],
                Samples =
                [
                    new TextMeasurePreviewSample
                    {
                        Label = "Node title",
                        ConstraintLabel = "184 px / 2 lines",
                        FontLabel = "700 / 16 px",
                        FullText = "North star delivery plan with approvals and external dependencies",
                        Result = new TextMeasureResult(
                            128,
                            38,
                            2,
                            "North star" + Environment.NewLine + "delivery plan...",
                            true,
                            "North star delivery plan with approvals and external dependencies",
                            [
                                new TextMeasureLineResult(0, "North star", 88, false),
                                new TextMeasureLineResult(1, "delivery plan...", 128, true)
                            ])
                    }
                ]
            }));

        Assert.Contains("Text measure service", cut.Markup);
        Assert.Contains("Tooltip fallback", cut.Markup);
        Assert.Contains("delivery plan...", cut.Markup);
    }
}
