namespace CanDoItAll.Components.CanvasLib;

public sealed class TransformHandlesOverlaySnapshot
{
    public string TestHookId { get; init; } = "transform-handles-overlay";

    public string Label { get; init; } = "Transform handles overlay";

    public string Title { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string StatePill { get; init; } = string.Empty;

    public bool IsVisible { get; init; }

    public IReadOnlyList<string> Metrics { get; init; } = [];
}

public static class TransformHandlesOverlayFactory
{
    public static TransformHandlesOverlaySnapshot CreateForWorkbench(
        CanvasWorkbenchSurface surface,
        SelectionModel selection)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(selection);

        var handles = surface.Chrome.TransformHandles;
        var selectedNodes = surface.Nodes
            .Where(node => selection.SelectedNodeIds.Contains(node.Id, StringComparer.Ordinal))
            .ToList();
        var isReadOnlySelection = selectedNodes.Count > 0 && selectedNodes.All(node => node.IsReadOnly);
        var isVisible = handles.IsEnabled && selectedNodes.Count > 0;

        return new TransformHandlesOverlaySnapshot
        {
            Title = isVisible
                ? "Transform handles wrap the current selection"
                : "Transform handles are on standby",
            Summary = isVisible
                ? "Selected nodes render shared bounds, resize affordances, and an optional rotation cue without duplicating page-level editor logic."
                : "Select one or more nodes to surface the shared transform boundary around the active selection set.",
            StatePill = isVisible
                ? (isReadOnlySelection ? "Read-only" : "Live")
                : "Standby",
            IsVisible = isVisible,
            Metrics =
            [
                $"{selectedNodes.Count} selected",
                handles.ShowResizeHandles ? "Resize handles" : "Bounds only",
                handles.ShowRotateHandle ? "Rotate cue" : "No rotate cue",
                handles.PlacementMode,
                isReadOnlySelection ? "Read-only selection" : "Editable selection"
            ]
        };
    }
}


