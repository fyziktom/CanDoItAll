namespace CanDoItAll.Components.CanvasLib;

public sealed class ConnectorAnchorOverlaySnapshot
{
    public string TestHookId { get; init; } = "connector-anchor-overlay";

    public string Label { get; init; } = "Connector anchor overlay";

    public string Title { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string StatePill { get; init; } = string.Empty;

    public bool IsVisible { get; init; }

    public IReadOnlyList<string> Metrics { get; init; } = [];
}

public static class ConnectorAnchorOverlayFactory
{
    public static ConnectorAnchorOverlaySnapshot CreateForWorkbench(
        CanvasWorkbenchSurface surface,
        SelectionModel selection)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(selection);

        var anchors = surface.Chrome.ConnectorAnchors;
        var isVisible = anchors.IsEnabled && surface.Nodes.Count > 0 &&
            (selection.SelectedNodeIds.Count > 0 || surface.Links.Count > 0);

        return new ConnectorAnchorOverlaySnapshot
        {
            Title = isVisible
                ? "Connector anchors are visible on intent"
                : "Connector anchors are on standby",
            Summary = isVisible
                ? "Selected or hovered nodes expose shared anchor affordances so routing geometry can be inspected on the stage."
                : "The shared connector overlay appears when nodes are selected or hovered and connection affordances are enabled.",
            StatePill = isVisible ? "Live" : "Standby",
            IsVisible = isVisible,
            Metrics =
            [
                anchors.ShowOnSelection ? "Selection anchors" : "No selection anchors",
                anchors.ShowOnHover ? "Hover anchors" : "No hover anchors",
                anchors.PlacementMode,
                $"{surface.Nodes.Sum(node => node.InputPorts.Count + node.OutputPorts.Count)} ports",
                $"{surface.Links.Count} links",
                $"{selection.SelectedNodeIds.Count} selected"
            ]
        };
    }
}


