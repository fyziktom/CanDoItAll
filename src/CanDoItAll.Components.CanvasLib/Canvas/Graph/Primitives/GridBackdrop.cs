namespace CanDoItAll.Components.CanvasLib;

public sealed class GridBackdropSnapshot
{
    public string TestHookId { get; init; } = "grid-backdrop";

    public string Label { get; init; } = "Grid backdrop";

    public string Title { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string StatePill { get; init; } = string.Empty;

    public bool IsEnabled { get; init; }

    public IReadOnlyList<string> Metrics { get; init; } = [];
}

public static class GridBackdropFactory
{
    public static GridBackdropSnapshot CreateForWorkbench(CanvasWorkbenchSurface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);

        var zoomPercent = Math.Round(Math.Max(0.15d, surface.UiState.Zoom) * 100d);
        var majorSpacing = Math.Max(16, (int)Math.Round(28d * Math.Max(surface.UiState.Zoom, 0.15d)));

        return new GridBackdropSnapshot
        {
            Title = "Zoom-aware grid framing now comes from one shared backdrop",
            Summary = "The graph surface exposes shared spacing cues for placement, snap alignment, and future presentation-mode toggles instead of leaving the backdrop as unowned CSS.",
            StatePill = "Live",
            IsEnabled = true,
            Metrics =
            [
                $"{majorSpacing}px major spacing",
                $"{zoomPercent:0}% zoom",
                $"{Math.Round(surface.UiState.PanX, 1)}, {Math.Round(surface.UiState.PanY, 1)} pan",
                surface.UiState.ShowMinimap ? "Overview aligned" : "Overview hidden"
            ]
        };
    }
}


