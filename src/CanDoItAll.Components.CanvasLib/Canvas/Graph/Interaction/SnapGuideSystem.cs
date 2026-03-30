namespace CanDoItAll.Components.CanvasLib;

public sealed class SnapGuideSystemSnapshot
{
    public string TestHookId { get; init; } = "snap-guide-system";

    public string Label { get; init; } = "Snap guide system";

    public string Title { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string StatePill { get; init; } = string.Empty;

    public bool IsEnabled { get; init; }

    public IReadOnlyList<string> Metrics { get; init; } = [];
}

public static class SnapGuideSystemFactory
{
    public static SnapGuideSystemSnapshot CreateForWorkbench(
        CanvasWorkbenchSurface surface,
        SelectionModel selection)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(selection);

        var snapGuides = surface.Chrome.SnapGuides;
        var isEnabled = snapGuides.IsEnabled && surface.Nodes.Count > 1;

        return new SnapGuideSystemSnapshot
        {
            Title = isEnabled
                ? "Snap guides are active during drag"
                : "Snap guides are disabled",
            Summary = isEnabled
                ? "Dragging nodes can align to nearby siblings and surface guide lines before positions are committed."
                : "Enable the shared snap system to surface alignment guides and drag-time snapping on the stage.",
            StatePill = isEnabled ? "Active" : "Off",
            IsEnabled = isEnabled,
            Metrics =
            [
                $"{Math.Round(snapGuides.Tolerance)}px tolerance",
                snapGuides.ModifierPolicy,
                $"{Math.Max(surface.Nodes.Count - 1, 0)} nearby candidates",
                $"{selection.SelectedNodeIds.Count} selected"
            ]
        };
    }
}


