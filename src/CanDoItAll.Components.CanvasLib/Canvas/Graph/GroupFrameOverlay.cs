namespace CanDoItAll.Components.CanvasLib;

public sealed class GroupFrameOverlaySample
{
    public string Label { get; init; } = string.Empty;

    public string Tone { get; init; } = "neutral";

    public int NodeCount { get; init; }
}

public sealed class GroupFrameOverlaySnapshot
{
    public string TestHookId { get; init; } = "group-frame-overlay";

    public string Label { get; init; } = "Group frame overlay";

    public string Title { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string StatePill { get; init; } = string.Empty;

    public IReadOnlyList<string> Metrics { get; init; } = [];

    public IReadOnlyList<GroupFrameOverlaySample> Frames { get; init; } = [];
}

public static class GroupFrameOverlayFactory
{
    public static GroupFrameOverlaySnapshot CreateForWorkbench(CanvasWorkbenchSurface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);

        var frames = surface.UiState.GroupFrames
            .Select(frame => new GroupFrameOverlaySample
            {
                Label = string.IsNullOrWhiteSpace(frame.Label) ? "Untitled frame" : frame.Label,
                Tone = string.IsNullOrWhiteSpace(frame.Tone) ? "neutral" : frame.Tone,
                NodeCount = frame.AnchorNodeIds.Count
            })
            .Take(4)
            .ToList();

        if (frames.Count == 0)
        {
            frames =
            [
                new GroupFrameOverlaySample { Label = "Validation cluster", Tone = "warning", NodeCount = 3 },
                new GroupFrameOverlaySample { Label = "Branch lane", Tone = "info", NodeCount = 2 }
            ];
        }

        return new GroupFrameOverlaySnapshot
        {
            Title = "Grouping shells now have a dedicated overlay boundary instead of living only as ad hoc frame data",
            Summary = "Selection boxes, swimlanes, and future scope regions can reuse one overlay surface with explicit labels and tone semantics.",
            StatePill = frames.Count > 0 ? "Visible" : "Idle",
            Metrics =
            [
                $"{surface.UiState.GroupFrames.Count} persisted frames",
                $"{frames.Sum(frame => frame.NodeCount)} framed nodes",
                $"{surface.Nodes.Count(node => !string.IsNullOrWhiteSpace(node.ParentId))} child nodes",
                $"{surface.Nodes.Count} total canvas nodes"
            ],
            Frames = frames
        };
    }
}


