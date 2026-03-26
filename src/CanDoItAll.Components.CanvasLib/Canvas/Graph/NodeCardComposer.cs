namespace CanDoItAll.Components.CanvasLib;

public sealed class NodeCardComposerSnapshot
{
    public string TestHookId { get; init; } = "node-card-composer";

    public string Label { get; init; } = "Node card composer";

    public string Title { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string StatePill { get; init; } = string.Empty;

    public IReadOnlyList<string> Metrics { get; init; } = [];

    public string Icon { get; init; } = string.Empty;

    public string CardTitle { get; init; } = string.Empty;

    public string CardSubtitle { get; init; } = string.Empty;

    public string LeadText { get; init; } = string.Empty;

    public string StatusPill { get; init; } = string.Empty;

    public string AccentColor { get; init; } = "#0f172a";

    public IReadOnlyList<CanvasWorkbenchChip> Chips { get; init; } = [];
}

public static class NodeCardComposerFactory
{
    public static NodeCardComposerSnapshot CreateForWorkbench(
        CanvasWorkbenchSurface surface,
        SelectionModel selection)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(selection);

        var node = surface.Nodes.FirstOrDefault(candidate => string.Equals(candidate.Id, selection.PrimaryNodeId, StringComparison.Ordinal))
            ?? surface.Nodes.FirstOrDefault();
        var chips = node?.Chips.Concat(node.FooterChips).Take(5).ToList() ?? [];

        return new NodeCardComposerSnapshot
        {
            Title = "Node cards now compose from explicit primitives instead of a single monolithic renderer",
            Summary = "Container, text, chip, icon, and media rules can now be reasoned about independently while the composer still owns the combined card shape.",
            StatePill = selection.IsEmpty ? "Canvas sample" : "Selection sample",
            Metrics =
            [
                $"{surface.Nodes.Count} projected node cards",
                $"{surface.Links.Count} linked relationships",
                $"{chips.Count} chip slots",
                $"{node?.ContextActions.Count ?? 0} context actions"
            ],
            Icon = !string.IsNullOrWhiteSpace(node?.Icon) ? node.Icon : node?.MarkerIcon ?? "[]",
            CardTitle = node?.Title ?? "Shared node card",
            CardSubtitle = node?.Subtitle ?? node?.Family ?? "Unified card shell",
            LeadText = node?.LeadText ?? node?.InlineText ?? "This preview keeps the card composition boundary explicit without losing the combined visual contract.",
            StatusPill = !string.IsNullOrWhiteSpace(node?.StatusPill) ? node.StatusPill : node?.Status ?? "Ready",
            AccentColor = string.IsNullOrWhiteSpace(node?.AccentColor) ? "#0f172a" : node.AccentColor,
            Chips = chips
        };
    }
}


