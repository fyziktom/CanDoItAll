namespace CanDoItAll.ComponentKit.Canvas;

public enum CanvasSceneLayer
{
    Backdrop,
    Connectors,
    Nodes,
    Overlays,
    Selection,
    ContextMenu,
    Diagnostics,
    Accessibility
}

public sealed class LayerStack
{
    public static LayerStack Workbench { get; } = new(
    [
        CanvasSceneLayer.Backdrop,
        CanvasSceneLayer.Connectors,
        CanvasSceneLayer.Nodes,
        CanvasSceneLayer.Overlays,
        CanvasSceneLayer.Selection,
        CanvasSceneLayer.ContextMenu,
        CanvasSceneLayer.Diagnostics,
        CanvasSceneLayer.Accessibility
    ]);

    public static LayerStack Calendar { get; } = new(
    [
        CanvasSceneLayer.Backdrop,
        CanvasSceneLayer.Nodes,
        CanvasSceneLayer.Overlays,
        CanvasSceneLayer.Diagnostics,
        CanvasSceneLayer.Accessibility
    ]);

    public LayerStack(IReadOnlyList<CanvasSceneLayer> layers)
    {
        Layers = layers ?? [];
    }

    public IReadOnlyList<CanvasSceneLayer> Layers { get; }

    public bool Contains(CanvasSceneLayer layer)
        => Layers.Contains(layer);
}
