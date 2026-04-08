namespace CanDoItAll.Components.CanvasLib;

public sealed class TextBlockPrimitiveSample
{
    public string Label { get; init; } = string.Empty;

    public string FullText { get; init; } = string.Empty;

    public string DisplayText { get; init; } = string.Empty;

    public int LineCount { get; init; }

    public bool IsTruncated { get; init; }
}

public sealed class TextBlockPrimitiveSnapshot
{
    public string TestHookId { get; init; } = "text-block-primitive";

    public string Label { get; init; } = "Text block primitive";

    public string Title { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string StatePill { get; init; } = string.Empty;

    public IReadOnlyList<string> Metrics { get; init; } = [];

    public IReadOnlyList<TextBlockPrimitiveSample> Samples { get; init; } = [];
}

public static class TextBlockPrimitiveFactory
{
    public static TextBlockPrimitiveSnapshot CreateForWorkbench(CanvasWorkbenchSurface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);

        var primaryNode = surface.Nodes.FirstOrDefault(node => !string.IsNullOrWhiteSpace(node.Title)) ?? surface.Nodes.FirstOrDefault();
        var service = new TextMeasureService();
        var sampleTexts = new[]
        {
            ("Title", primaryNode?.Title ?? "Shared workbench title"),
            ("Subtitle", primaryNode?.Subtitle ?? primaryNode?.LeadText ?? "Context copy and semantic metadata should keep one typography contract."),
            ("Overflow", primaryNode?.InlineText ?? primaryNode?.LeadText ?? "A long inline description can wrap, clamp, and ellipsize without each card or menu inventing a separate text layout rule.")
        };

        var samples = sampleTexts
            .Where(sample => !string.IsNullOrWhiteSpace(sample.Item2))
            .Select((sample, index) =>
            {
                var result = service.Measure(new TextMeasureRequest
                {
                    Id = $"text-{index}",
                    Text = sample.Item2,
                    MaxWidth = sample.Item1 == "Overflow" ? 180 : 220,
                    MaxLines = sample.Item1 == "Overflow" ? 2 : 1,
                    Font = TextMeasureFontSpec.Default
                });

                return new TextBlockPrimitiveSample
                {
                    Label = sample.Item1,
                    FullText = sample.Item2,
                    DisplayText = result.DisplayText,
                    LineCount = result.LineCount,
                    IsTruncated = result.IsTruncated
                };
            })
            .ToList();

        return new TextBlockPrimitiveSnapshot
        {
            Title = "Titles, captions, and overflow copy now share one clamping and wrapping primitive",
            Summary = "Shared typography rules can now move through cards, menus, and future calendar labels without re-encoding line limits and emphasis states at every render point.",
            StatePill = samples.Any(sample => sample.IsTruncated) ? "Clamped" : "Wrapped",
            Metrics =
            [
                $"{samples.Count} text samples",
                $"{samples.Count(sample => sample.IsTruncated)} truncated blocks",
                $"{samples.Max(sample => sample.LineCount)} max lines",
                $"{surface.Nodes.Count(node => !string.IsNullOrWhiteSpace(node.InlineText))} inline text nodes"
            ],
            Samples = samples
        };
    }
}


