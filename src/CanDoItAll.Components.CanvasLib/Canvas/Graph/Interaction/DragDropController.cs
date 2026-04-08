namespace CanDoItAll.Components.CanvasLib;

public sealed class DragDropControllerSnapshot
{
    public string TestHookId { get; init; } = "drag-drop-controller";

    public string Label { get; init; } = "Drag-drop controller";

    public string Title { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string StatePill { get; init; } = string.Empty;

    public bool IsEnabled { get; init; }

    public IReadOnlyList<string> Metrics { get; init; } = [];
}

public static class DragDropControllerFactory
{
    public static DragDropControllerSnapshot CreateForWorkbench(
        CanvasWorkbenchSurface surface,
        SelectionModel selection)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(selection);

        var dragCapableActions = surface.Chrome.QuickCreateActions.Count(action => action.SupportsDragDrop || action.RequiresFile);
        var isEnabled = surface.Nodes.Count > 0;

        return new DragDropControllerSnapshot
        {
            Title = isEnabled
                ? "Drag lifecycle owns node moves, grouped drags, and drop-capable create actions"
                : "Drag lifecycle is waiting for scene content",
            Summary = isEnabled
                ? "Pointer capture, drag thresholds, multi-select moves, and future drop targets now have one shared controller instead of being left implicit in the runtime."
                : "Add scene content to arm drag thresholds, shared move sets, and drop-target routing.",
            StatePill = isEnabled ? "Active" : "Idle",
            IsEnabled = isEnabled,
            Metrics =
            [
                $"{surface.Nodes.Count} draggable nodes",
                $"{selection.SelectedNodeIds.Count} selected move set",
                $"{surface.UiState.GroupFrames.Count} group frames",
                $"{dragCapableActions} drop-capable actions"
            ]
        };
    }
}


