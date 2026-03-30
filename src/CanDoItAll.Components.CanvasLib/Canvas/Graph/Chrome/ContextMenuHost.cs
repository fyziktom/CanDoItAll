namespace CanDoItAll.Components.CanvasLib;

public sealed class ContextMenuHostSnapshot
{
    public string TestHookId { get; init; } = "context-menu-host";

    public string Label { get; init; } = "Context menu host";

    public string Title { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string StatePill { get; init; } = string.Empty;

    public IReadOnlyList<string> Metrics { get; init; } = [];

    public IReadOnlyList<CanvasWorkbenchAction> Actions { get; init; } = [];
}

public static class ContextMenuHostFactory
{
    public static ContextMenuHostSnapshot CreateForWorkbench(
        CanvasWorkbenchSurface surface,
        SelectionModel selection)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(selection);

        var selectedNode = surface.Nodes.FirstOrDefault(node => string.Equals(node.Id, selection.PrimaryNodeId, StringComparison.Ordinal));
        var actions = (selectedNode?.ContextActions.Count > 0
                ? selectedNode.ContextActions
                : surface.Chrome.QuickCreateActions.Concat(surface.Chrome.GroupContextActions))
            .Take(5)
            .ToList();

        return new ContextMenuHostSnapshot
        {
            Title = "Context actions now have a reusable host for placement, nesting, and dismissal",
            Summary = "The workbench can surface contextual commands without coupling menu focus, submenu layout, and action dispatch directly to the monolithic runtime.",
            StatePill = actions.Count > 0 ? "Bound" : "Empty",
            Metrics =
            [
                $"{actions.Count} visible actions",
                $"{actions.Count(action => action.Children.Count > 0)} nested actions",
                $"{surface.Chrome.QuickCreateActions.Count} quick create actions",
                $"{surface.Chrome.GroupContextActions.Count} group actions"
            ],
            Actions = actions
        };
    }
}


