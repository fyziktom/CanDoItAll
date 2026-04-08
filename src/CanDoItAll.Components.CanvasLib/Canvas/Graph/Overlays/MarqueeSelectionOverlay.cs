namespace CanDoItAll.Components.CanvasLib;

public sealed class MarqueeSelectionOverlaySnapshot
{
    public string TestHookId { get; init; } = "marquee-selection-overlay";

    public string Label { get; init; } = "Marquee selection overlay";

    public string Title { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string StatePill { get; init; } = string.Empty;

    public bool IsEnabled { get; init; }

    public IReadOnlyList<string> Metrics { get; init; } = [];
}

public static class MarqueeSelectionOverlayFactory
{
    public static MarqueeSelectionOverlaySnapshot CreateForWorkbench(
        CanvasWorkbenchSurface surface,
        SelectionModel selection)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(selection);

        var marquee = surface.Chrome.MarqueeSelection;
        var isEnabled = marquee.IsEnabled && surface.Nodes.Count > 0;

        return new MarqueeSelectionOverlaySnapshot
        {
            Title = isEnabled
                ? $"{marquee.ModifierKey}-drag marquee selection is armed"
                : "Marquee selection is disabled",
            Summary = isEnabled
                ? "The shared workbench can box-select intersecting nodes and replace the current selection set."
                : "Enable the shared marquee overlay to expose box selection on the canvas stage.",
            StatePill = isEnabled ? "Armed" : "Off",
            IsEnabled = isEnabled,
            Metrics =
            [
                $"{marquee.ModifierKey} modifier",
                $"{marquee.SelectionMode} mode",
                $"{surface.Nodes.Count} nodes",
                $"{selection.SelectedNodeIds.Count} selected"
            ]
        };
    }
}


