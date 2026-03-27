namespace CanDoItAll.Components.CanvasLib;

public sealed class ContainerPrimitiveSnapshot
{
    public string TestHookId { get; init; } = "container-primitive";

    public string Label { get; init; } = "Container primitive";

    public string Title { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string StatePill { get; init; } = string.Empty;

    public IReadOnlyList<string> Metrics { get; init; } = [];

    public string Kicker { get; init; } = string.Empty;

    public string Body { get; init; } = string.Empty;

    public string Footer { get; init; } = string.Empty;

    public bool IsSelected { get; init; }

    public bool IsReadOnly { get; init; }
}

public static class ContainerPrimitiveFactory
{
    public static ContainerPrimitiveSnapshot CreateForWorkbench(
        CanvasWorkbenchSurface surface,
        SelectionModel selection)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(selection);

        var primaryNode = surface.Nodes.FirstOrDefault(node => string.Equals(node.Id, selection.PrimaryNodeId, StringComparison.Ordinal))
            ?? surface.Nodes.FirstOrDefault();
        var title = primaryNode?.Title ?? "Shared canvas container";
        var subtitle = primaryNode?.Subtitle ?? primaryNode?.LeadText ?? "Selection, padding, elevation, and clipping now reuse a dedicated surface contract.";
        var footer = !string.IsNullOrWhiteSpace(primaryNode?.StatusPill)
            ? primaryNode.StatusPill
            : primaryNode?.Family ?? "Shared shell";

        return new ContainerPrimitiveSnapshot
        {
            Title = "Card, frame, and popover shells now share one container primitive",
            Summary = "Selection chrome, padding, border emphasis, and content slots can move into one reusable surface instead of being repeated across nodes, inspectors, and overlays.",
            StatePill = primaryNode?.IsReadOnly == true ? "Read only" : selection.IsEmpty ? "Neutral" : "Selected",
            Metrics =
            [
                $"{surface.Nodes.Count} node-backed containers",
                $"{surface.UiState.GroupFrames.Count} grouped frames",
                $"{selection.SelectedNodeIds.Count} selected containers",
                $"{surface.Nodes.Count(node => node.IsReadOnly)} read-only nodes"
            ],
            Kicker = title,
            Body = subtitle,
            Footer = footer,
            IsSelected = !selection.IsEmpty,
            IsReadOnly = primaryNode?.IsReadOnly == true
        };
    }
}


