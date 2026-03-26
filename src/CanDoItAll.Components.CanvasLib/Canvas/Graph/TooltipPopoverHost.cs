namespace CanDoItAll.Components.CanvasLib;

public sealed class TooltipPopoverHostSnapshot
{
    public string TestHookId { get; init; } = "tooltip-popover-host";

    public string Label { get; init; } = "Tooltip popover host";

    public string Title { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string StatePill { get; init; } = string.Empty;

    public bool IsEnabled { get; init; }

    public IReadOnlyList<string> Metrics { get; init; } = [];
}

public static class TooltipPopoverHostFactory
{
    public static TooltipPopoverHostSnapshot CreateForWorkbench(CanvasWorkbenchSurface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);

        var tooltip = surface.Chrome.TooltipPopover;
        var annotationCount = surface.Nodes.Sum(node => node.Annotations.Count);
        var annotatedNodes = surface.Nodes.Count(node => node.Annotations.Count > 0);
        var isEnabled = tooltip.IsEnabled && annotationCount > 0;

        return new TooltipPopoverHostSnapshot
        {
            Title = isEnabled
                ? "Contextual popovers are wired"
                : "Tooltip popovers are on standby",
            Summary = isEnabled
                ? "Hovering or focusing annotation badges reveals the shared popover host with contextual details."
                : "Add annotation badges or enable the shared host to surface contextual tooltip and popover guidance.",
            StatePill = isEnabled ? "Ready" : "Standby",
            IsEnabled = isEnabled,
            Metrics =
            [
                $"{annotationCount} annotation badges",
                $"{annotatedNodes} annotated nodes",
                tooltip.FocusTriggers ? "Focus + hover" : "Hover only",
                tooltip.SupportsRichPreview ? "Rich previews" : "Static hints"
            ]
        };
    }
}


