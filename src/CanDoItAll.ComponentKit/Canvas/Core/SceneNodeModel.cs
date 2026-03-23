namespace CanDoItAll.ComponentKit.Canvas;

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
