namespace CanDoItAll.Components.CanvasLib;

public sealed class MinimapOverviewSnapshot
{
    public string TestHookId { get; init; } = "minimap-overview";

    public string Label { get; init; } = "Minimap overview";

    public string Title { get; init; } = "Scene overview is ready";

    public string Summary { get; init; } = string.Empty;

    public string StatePill { get; init; } = "Ready";

    public bool IsVisible { get; init; }

    public IReadOnlyList<string> Metrics { get; init; } = [];
}

public static class MinimapOverviewFactory
{
    public static MinimapOverviewSnapshot CreateForWorkbench(
        CanvasWorkbenchSurface surface,
        SelectionModel selection)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(selection);

        var minimap = surface.Chrome.Minimap;
        var isVisible = minimap.IsEnabled && surface.UiState.ShowMinimap && surface.Nodes.Count > 0;

        return new MinimapOverviewSnapshot
        {
            Title = isVisible
                ? "Minimap viewport is live"
                : "Minimap can reveal scene spread",
            Summary = isVisible
                ? "The workbench is rendering a live viewport rectangle over the scene overview."
                : "Use the minimap toggle to reveal a live overview with the current viewport rectangle.",
            StatePill = isVisible ? "Live" : "Hidden",
            IsVisible = isVisible,
            Metrics =
            [
                $"{surface.Nodes.Count} nodes",
                $"{surface.Links.Count} links",
                $"{selection.SelectedNodeIds.Count} selected",
                minimap.Title
            ]
        };
    }
}


