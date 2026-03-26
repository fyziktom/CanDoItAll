namespace CanDoItAll.Components.CanvasLib;

public sealed class HoverFocusRouterSnapshot
{
    public string TestHookId { get; init; } = "hover-focus-router";

    public string Label { get; init; } = "Hover-focus router";

    public string Title { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string StatePill { get; init; } = string.Empty;

    public bool IsEnabled { get; init; }

    public IReadOnlyList<string> Metrics { get; init; } = [];
}

public static class HoverFocusRouterFactory
{
    public static HoverFocusRouterSnapshot CreateForWorkbench(
        CanvasWorkbenchSurface surface,
        SelectionModel selection)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(selection);

        var primaryLabel = ResolvePrimaryLabel(surface, selection);
        var annotatedNodes = surface.Nodes.Count(node => node.Annotations.Count > 0);

        return new HoverFocusRouterSnapshot
        {
            Title = selection.SelectedNodeIds.Count > 0
                ? $"Hover and focus now stay coherent around {primaryLabel}"
                : "Hover and focus routing is armed for the next active target",
            Summary = "Node focus, annotation popovers, inspector docking, and menu-open suppression now have an explicit shared router instead of competing local DOM state.",
            StatePill = selection.SelectedNodeIds.Count > 0 ? "Focused" : "Armed",
            IsEnabled = true,
            Metrics =
            [
                primaryLabel,
                $"{annotatedNodes} annotated nodes",
                surface.Chrome.TooltipPopover.FocusTriggers ? "Focus triggers" : "Pointer only",
                surface.Chrome.TooltipPopover.SupportsRichPreview ? "Rich previews" : "Text previews"
            ]
        };
    }

    private static string ResolvePrimaryLabel(CanvasWorkbenchSurface surface, SelectionModel selection)
    {
        var primaryId = selection.PrimaryNodeId ?? selection.SelectedNodeIds.FirstOrDefault();
        var node = surface.Nodes.FirstOrDefault(candidate => string.Equals(candidate.Id, primaryId, StringComparison.Ordinal))
            ?? surface.Nodes.FirstOrDefault(candidate => candidate.Annotations.Count > 0)
            ?? surface.Nodes.FirstOrDefault();

        if (node is null)
        {
            return "No active target";
        }

        if (!string.IsNullOrWhiteSpace(node.Title))
        {
            return node.Title;
        }

        if (!string.IsNullOrWhiteSpace(node.InlineText))
        {
            return node.InlineText;
        }

        return node.Id;
    }
}


