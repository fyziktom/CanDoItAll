namespace CanDoItAll.Components.CanvasLib;

public sealed class ChipBadgePrimitiveSample
{
    public string Text { get; init; } = string.Empty;

    public string Tone { get; init; } = "neutral";

    public string Icon { get; init; } = string.Empty;
}

public sealed class ChipBadgePrimitiveSnapshot
{
    public string TestHookId { get; init; } = "chip-badge-primitive";

    public string Label { get; init; } = "Chip badge primitive";

    public string Title { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string StatePill { get; init; } = string.Empty;

    public IReadOnlyList<string> Metrics { get; init; } = [];

    public IReadOnlyList<ChipBadgePrimitiveSample> Chips { get; init; } = [];
}

public static class ChipBadgePrimitiveFactory
{
    public static ChipBadgePrimitiveSnapshot CreateForWorkbench(CanvasWorkbenchSurface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);

        var chips = surface.Nodes
            .SelectMany(node => node.Chips.Concat(node.FooterChips))
            .Select(chip => new ChipBadgePrimitiveSample
            {
                Text = string.IsNullOrWhiteSpace(chip.Text) ? "Untitled" : chip.Text,
                Tone = string.IsNullOrWhiteSpace(chip.Tone) ? "neutral" : chip.Tone,
                Icon = ResolveToneIcon(chip.Tone)
            })
            .DistinctBy(chip => $"{chip.Text}:{chip.Tone}")
            .Take(6)
            .ToList();

        if (chips.Count == 0)
        {
            chips =
            [
                new ChipBadgePrimitiveSample { Text = "Priority", Tone = "accent", Icon = "priority_high" },
                new ChipBadgePrimitiveSample { Text = "Ready", Tone = "success", Icon = "check_circle" },
                new ChipBadgePrimitiveSample { Text = "Linked", Tone = "info", Icon = "arrow_forward" }
            ];
        }

        var tones = chips
            .Select(chip => chip.Tone)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        var nodesWithChips = surface.Nodes.Count(node => node.Chips.Count > 0 || node.FooterChips.Count > 0);

        return new ChipBadgePrimitiveSnapshot
        {
            Title = "Compact badges now reuse one chip contract instead of being painted ad hoc in cards and menus",
            Summary = "Status pills, marker tags, and metadata badges can all share one tone-aware primitive with consistent spacing, icon alignment, and truncation rules.",
            StatePill = chips.Count > 0 ? "Ready" : "Fallback",
            Metrics =
            [
                $"{chips.Count} chip samples",
                $"{tones} tone families",
                $"{nodesWithChips} nodes with chip data",
                $"{surface.Nodes.Count(node => !string.IsNullOrWhiteSpace(node.StatusPill))} status pill nodes"
            ],
            Chips = chips
        };
    }

    private static string ResolveToneIcon(string? tone)
        => tone?.Trim().ToLowerInvariant() switch
        {
            "accent" => "priority_high",
            "success" => "check_circle",
            "warning" => "warning",
            "danger" => "close",
            "info" => "info",
            _ => "fiber_manual_record"
        };
}


