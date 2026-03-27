namespace CanDoItAll.Components.CanvasLib;

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

public sealed class LayerStackPreviewSnapshot
{
    public string TestHookId { get; init; } = "layer-stack";

    public string Label { get; init; } = "Layer stack";

    public string Title { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string StatePill { get; init; } = string.Empty;

    public IReadOnlyList<string> Metrics { get; init; } = [];
}

public static class LayerStackPreviewFactory
{
    public static LayerStackPreviewSnapshot CreateForWorkbench()
    {
        var stack = LayerStack.Workbench;

        return new LayerStackPreviewSnapshot
        {
            Title = "Workbench draw order is formalized as a reusable layer stack",
            Summary = "Backdrop, connectors, nodes, overlays, selection, menus, diagnostics, and accessibility mirrors now have an explicit shared ordering instead of only relying on DOM structure.",
            StatePill = "Ordered",
            Metrics = stack.Layers.Select(layer => layer.ToString()).ToList()
        };
    }
}


