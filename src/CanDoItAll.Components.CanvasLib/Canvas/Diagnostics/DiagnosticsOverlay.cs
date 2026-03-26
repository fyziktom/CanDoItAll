namespace CanDoItAll.Components.CanvasLib;

public sealed class DiagnosticsOverlaySnapshot
{
    public string TestHookId { get; init; } = "diagnostics-overlay";

    public string Label { get; init; } = "Diagnostics overlay";

    public string Title { get; init; } = "Diagnostics are ready";

    public string Summary { get; init; } = string.Empty;

    public string StatePill { get; init; } = "Ready";

    public bool IsVisible { get; init; }

    public IReadOnlyList<string> Metrics { get; init; } = [];
}

public static class DiagnosticsOverlayFactory
{
    public static DiagnosticsOverlaySnapshot CreateForWorkbench(
        CanvasWorkbenchSurface surface,
        SelectionModel selection)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(selection);

        var diagnostics = surface.Chrome.Diagnostics;
        var isVisible = diagnostics.IsEnabled && surface.UiState.ShowDiagnostics;
        var metrics = new List<string>
        {
            $"{surface.Nodes.Count} nodes",
            $"{surface.Links.Count} links",
            $"{selection.SelectedNodeIds.Count} selected"
        };

        if (diagnostics.ShowNodeBounds)
        {
            metrics.Add("Node bounds");
        }

        if (diagnostics.ShowConnectorAnchors)
        {
            metrics.Add("Anchor hints");
        }

        if (diagnostics.ShowViewportStats)
        {
            metrics.Add("Viewport stats");
        }

        return new DiagnosticsOverlaySnapshot
        {
            Title = isVisible
                ? "Diagnostics are live on the workbench"
                : "Diagnostics can surface bounds and routing state",
            Summary = isVisible
                ? "Live bounds, connector anchors, and viewport metrics are being surfaced on the stage."
                : "Use the diagnostics toggle to reveal bounds, anchors, viewport metrics, and interaction state.",
            StatePill = isVisible ? "Live" : "Hidden",
            IsVisible = isVisible,
            Metrics = metrics
        };
    }
}


