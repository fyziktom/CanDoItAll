namespace CanDoItAll.Components.CanvasLib;

public sealed class EmptyStateOverlaySnapshot
{
    public string TestHookId { get; init; } = "empty-state-overlay";

    public string Label { get; init; } = "Empty state overlay";

    public string Title { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string StatePill { get; init; } = "Ready";

    public bool IsVisible { get; init; }

    public IReadOnlyList<string> Metrics { get; init; } = [];
}

public static class EmptyStateOverlayFactory
{
    public static EmptyStateOverlaySnapshot CreateForWorkbench(CanvasWorkbenchSurface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);

        var isVisible = surface.Nodes.Count == 0;
        return new EmptyStateOverlaySnapshot
        {
            Title = surface.Chrome.EmptyStateTitle,
            Summary = isVisible
                ? surface.Chrome.EmptyStateDescription
                : "The empty-state boundary is ready to take over whenever the workbench has no visible graph projection.",
            StatePill = isVisible ? "Visible" : "Standby",
            IsVisible = isVisible,
            Metrics =
            [
                surface.Chrome.EmptyStateKicker,
                $"{surface.Nodes.Count} nodes",
                $"{surface.Chrome.QuickCreateActions.Count} quick create actions"
            ]
        };
    }
}


