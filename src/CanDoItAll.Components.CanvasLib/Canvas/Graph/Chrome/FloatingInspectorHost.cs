namespace CanDoItAll.Components.CanvasLib;

public sealed class FloatingInspectorHostSnapshot
{
    public string TestHookId { get; init; } = "floating-inspector-host";

    public string Label { get; init; } = "Floating inspector host";

    public string Title { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string StatePill { get; init; } = string.Empty;

    public IReadOnlyList<string> Metrics { get; init; } = [];

    public string InspectorTitle { get; init; } = string.Empty;

    public string InspectorBody { get; init; } = string.Empty;

    public string ActiveTab { get; init; } = string.Empty;

    public bool IsDetached { get; init; }
}

public static class FloatingInspectorHostFactory
{
    public static FloatingInspectorHostSnapshot CreateForWorkbench(
        CanvasWorkbenchSurface surface,
        SelectionModel selection)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(selection);

        var selectedNode = surface.Nodes.FirstOrDefault(node => string.Equals(node.Id, selection.PrimaryNodeId, StringComparison.Ordinal));
        var tab = string.IsNullOrWhiteSpace(surface.UiState.ActiveInspectorTab) ? "details" : surface.UiState.ActiveInspectorTab;
        var isDetached = surface.UiState.IsMaximized || selection.SelectedNodeIds.Count > 0;

        return new FloatingInspectorHostSnapshot
        {
            Title = "Inspector docking is now a named host instead of incidental stage behavior",
            Summary = "Floating, docked, and future compact inspector presentations can share one host that owns placement and focus without living only inside the workbench runtime.",
            StatePill = isDetached ? "Floating" : "Docked",
            Metrics =
            [
                $"{selection.SelectedNodeIds.Count} selected nodes",
                $"{surface.UiState.GroupFrames.Count} nearby frames",
                $"{surface.Nodes.Count(node => node.ContextActions.Count > 0)} actionable nodes",
                $"{surface.UiState.IsMaximized.ToString().ToLowerInvariant()} maximized stage"
            ],
            InspectorTitle = selectedNode?.Title ?? "Selection inspector",
            InspectorBody = selectedNode?.LeadText ?? selectedNode?.InlineText ?? selectedNode?.Subtitle ?? "The host can pin inspector content near the current selection while keeping tab state explicit.",
            ActiveTab = tab,
            IsDetached = isDetached
        };
    }
}


