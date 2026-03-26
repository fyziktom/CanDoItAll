namespace CanDoItAll.Components.CanvasLib;

public sealed class SkeletonStateOverlaySnapshot
{
    public string TestHookId { get; init; } = "skeleton-state-overlay";

    public string Label { get; init; } = "Skeleton state overlay";

    public string Title { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string StatePill { get; init; } = string.Empty;

    public bool IsVisible { get; init; }

    public int StageCardCount { get; init; } = 3;

    public int InspectorBlockCount { get; init; } = 2;

    public IReadOnlyList<string> Metrics { get; init; } = [];
}

public static class SkeletonStateOverlayFactory
{
    public static SkeletonStateOverlaySnapshot CreateForWorkbench(CanvasWorkbenchSurface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);

        var isVisible = surface.Nodes.Count == 0;
        return new SkeletonStateOverlaySnapshot
        {
            Title = isVisible
                ? "Loading skeleton is covering the stage"
                : "Loading skeleton is ready for scene swaps",
            Summary = isVisible
                ? "Shared skeleton placeholders preserve the workbench rhythm while nodes, inspector content, or toolbar context are still loading."
                : "The shared loading overlay can reuse the same shell rhythm whenever graph or inspector content refreshes asynchronously.",
            StatePill = isVisible ? "Visible" : "Standby",
            IsVisible = isVisible,
            StageCardCount = 3,
            InspectorBlockCount = 2,
            Metrics =
            [
                "Toolbar chrome",
                "3 stage cards",
                "2 inspector blocks",
                "Busy region"
            ]
        };
    }

    public static SkeletonStateOverlaySnapshot CreateLoadingSnapshot(
        string title,
        string summary,
        int stageCardCount = 3,
        int inspectorBlockCount = 2)
        => new()
        {
            Title = title,
            Summary = summary,
            StatePill = "Visible",
            IsVisible = true,
            StageCardCount = Math.Max(1, stageCardCount),
            InspectorBlockCount = Math.Max(1, inspectorBlockCount),
            Metrics =
            [
                "Toolbar chrome",
                $"{Math.Max(1, stageCardCount)} stage cards",
                $"{Math.Max(1, inspectorBlockCount)} inspector blocks",
                "Busy region"
            ]
        };
}


