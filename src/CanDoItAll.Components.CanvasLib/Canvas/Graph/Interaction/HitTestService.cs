namespace CanDoItAll.Components.CanvasLib;

public sealed class HitTestServiceSnapshot
{
    public string TestHookId { get; init; } = "hit-test-service";

    public string Label { get; init; } = "Hit test service";

    public string Title { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string StatePill { get; init; } = string.Empty;

    public bool IsEnabled { get; init; }

    public IReadOnlyList<string> Metrics { get; init; } = [];
}

public static class HitTestServiceFactory
{
    public static HitTestServiceSnapshot CreateForWorkbench(
        CanvasWorkbenchSurface surface,
        SelectionModel selection)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(selection);

        return new HitTestServiceSnapshot
        {
            Title = "Pointer hit testing resolves nodes, links, frames, and overlays from one scene model",
            Summary = "Selection, drag intent, context menus, connector anchors, and future minimap jumps can all query the same shared hit surface instead of relying on scattered DOM assumptions.",
            StatePill = "Ready",
            IsEnabled = true,
            Metrics =
            [
                $"{surface.Nodes.Count} node targets",
                $"{surface.Links.Count} link targets",
                $"{surface.UiState.GroupFrames.Count} frame targets",
                $"{selection.SelectedNodeIds.Count} selected"
            ]
        };
    }
}


