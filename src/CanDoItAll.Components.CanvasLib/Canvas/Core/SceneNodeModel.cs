namespace CanDoItAll.Components.CanvasLib;

public sealed record SceneNodeBounds(double X, double Y, double Width, double Height);

public sealed record SceneNodeModel(
    string Id,
    string? ParentId,
    string Family,
    string Kind,
    SceneNodeBounds Bounds,
    bool IsVisible,
    bool IsSelected,
    IReadOnlyList<string> ChildIds)
{
    public static SceneNodeModel FromWorkbenchNode(
        CanvasWorkbenchNode node,
        bool isSelected = false,
        IReadOnlyList<string>? childIds = null,
        double width = 0,
        double height = 0)
        => new(
            node.Id,
            node.ParentId,
            node.Family,
            node.Kind,
            new SceneNodeBounds(node.X, node.Y, width, height),
            true,
            isSelected,
            childIds ?? []);
}

public sealed class SceneNodeModelPreviewSnapshot
{
    public string TestHookId { get; init; } = "scene-node-model";

    public string Label { get; init; } = "Scene node model";

    public string Title { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string StatePill { get; init; } = string.Empty;

    public IReadOnlyList<string> Metrics { get; init; } = [];
}

public static class SceneNodeModelPreviewFactory
{
    public static SceneNodeModelPreviewSnapshot CreateForWorkbench(
        CanvasWorkbenchSurface surface,
        SelectionModel selection)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(selection);

        var target = surface.Nodes.FirstOrDefault(node => string.Equals(node.Id, selection.PrimaryNodeId, StringComparison.Ordinal))
            ?? surface.Nodes.FirstOrDefault(node => string.IsNullOrWhiteSpace(node.ParentId))
            ?? surface.Nodes.FirstOrDefault();

        if (target is null)
        {
            return new SceneNodeModelPreviewSnapshot
            {
                Title = "No scene nodes are available yet",
                Summary = "SceneNodeModel becomes meaningful once the workbench has projected nodes.",
                StatePill = "Empty",
                Metrics = ["0 nodes"]
            };
        }

        var childIds = surface.Nodes
            .Where(node => string.Equals(node.ParentId, target.Id, StringComparison.Ordinal))
            .Select(node => node.Id)
            .ToList();
        var model = SceneNodeModel.FromWorkbenchNode(target, selection.SelectedNodeIds.Contains(target.Id), childIds, 324, 196);

        return new SceneNodeModelPreviewSnapshot
        {
            Title = "Projected workbench nodes now map into a shared scene-node model",
            Summary = "Bounds, visibility, selection, kind, and parent-child relationships are normalized once so hit testing, layout, and overlays can consume the same typed scene representation.",
            StatePill = model.IsSelected ? "Selected" : "Projected",
            Metrics =
            [
                target.Title,
                $"{Math.Round(model.Bounds.X)}, {Math.Round(model.Bounds.Y)}",
                $"{model.ChildIds.Count} child ids",
                $"{model.Kind} / {model.Family}"
            ]
        };
    }
}


