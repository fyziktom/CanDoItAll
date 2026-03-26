namespace CanDoItAll.Components.CanvasLib;

public sealed class IconGlyphPrimitiveSample
{
    public string Glyph { get; init; } = string.Empty;

    public string Label { get; init; } = string.Empty;
}

public sealed class IconGlyphPrimitiveSnapshot
{
    public string TestHookId { get; init; } = "icon-glyph-primitive";

    public string Label { get; init; } = "Icon glyph primitive";

    public string Title { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string StatePill { get; init; } = string.Empty;

    public IReadOnlyList<string> Metrics { get; init; } = [];

    public IReadOnlyList<IconGlyphPrimitiveSample> Glyphs { get; init; } = [];
}

public static class IconGlyphPrimitiveFactory
{
    public static IconGlyphPrimitiveSnapshot CreateForWorkbench(CanvasWorkbenchSurface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);

        var glyphs = surface.Nodes
            .Where(node => !string.IsNullOrWhiteSpace(node.Icon) || !string.IsNullOrWhiteSpace(node.MarkerIcon))
            .Select(node => new IconGlyphPrimitiveSample
            {
                Glyph = !string.IsNullOrWhiteSpace(node.Icon) ? node.Icon : node.MarkerIcon,
                Label = !string.IsNullOrWhiteSpace(node.Title) ? node.Title : node.Id
            })
            .Concat(surface.Chrome.QuickCreateActions
                .Where(action => !string.IsNullOrWhiteSpace(action.Icon))
                .Select(action => new IconGlyphPrimitiveSample
                {
                    Glyph = action.Icon,
                    Label = action.Label
                }))
            .DistinctBy(sample => $"{sample.Glyph}:{sample.Label}")
            .Take(8)
            .ToList();

        if (glyphs.Count == 0)
        {
            glyphs =
            [
                new IconGlyphPrimitiveSample { Glyph = "[]", Label = "Card" },
                new IconGlyphPrimitiveSample { Glyph = "->", Label = "Open" },
                new IconGlyphPrimitiveSample { Glyph = "+", Label = "Create" }
            ];
        }

        return new IconGlyphPrimitiveSnapshot
        {
            Title = "Symbolic markers now come from one glyph primitive with shared sizing and baseline rules",
            Summary = "Node icons, menu affordances, and small semantic markers can align on one reusable contract instead of inheriting whatever inline markup each surface happens to emit.",
            StatePill = glyphs.Count > 0 ? "Mapped" : "Fallback",
            Metrics =
            [
                $"{glyphs.Count} glyph samples",
                $"{surface.Nodes.Count(node => !string.IsNullOrWhiteSpace(node.Icon))} node icons",
                $"{surface.Nodes.Count(node => !string.IsNullOrWhiteSpace(node.MarkerIcon))} marker icons",
                $"{surface.Chrome.QuickCreateActions.Count(action => !string.IsNullOrWhiteSpace(action.Icon))} action icons"
            ],
            Glyphs = glyphs
        };
    }
}


