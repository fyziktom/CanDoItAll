namespace CanDoItAll.Components.CanvasLib;

public sealed class LayoutEngineSnapshot
{
    public string TestHookId { get; init; } = "layout-engine";

    public string Label { get; init; } = "Layout engine";

    public string Title { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string StatePill { get; init; } = string.Empty;

    public bool IsEnabled { get; init; }

    public IReadOnlyList<string> Metrics { get; init; } = [];
}

public static class LayoutEngineFactory
{
    public static LayoutEngineSnapshot CreateForWorkbench(CanvasWorkbenchSurface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);

        var manualPositions = surface.UiState.ManualPositions.Count;
        var autoPositions = Math.Max(surface.Nodes.Count - manualPositions, 0);
        var branchLaneCount = surface.Nodes
            .Select(node => node.BranchLabel?.Trim())
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .Distinct(StringComparer.Ordinal)
            .Count();

        return new LayoutEngineSnapshot
        {
            Title = "Placement resolution now has a shared layout engine boundary",
            Summary = "Manual positions, fallback auto layout, branch lanes, and group-frame coverage can evolve behind one engine instead of staying distributed across page code and the JS runtime.",
            StatePill = "Ready",
            IsEnabled = true,
            Metrics =
            [
                $"{manualPositions} manual positions",
                $"{autoPositions} auto positions",
                $"{surface.UiState.GroupFrames.Count} group frames",
                $"{branchLaneCount} branch lanes"
            ]
        };
    }
}


