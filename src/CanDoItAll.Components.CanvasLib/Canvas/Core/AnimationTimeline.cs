namespace CanDoItAll.Components.CanvasLib;

public sealed class AnimationTimelineOptions
{
    public static AnimationTimelineOptions Default { get; } = new();

    public int ViewportDurationMs { get; init; } = 320;

    public int OverlayFadeDurationMs { get; init; } = 180;

    public int ConnectorFlowDurationMs { get; init; } = 1200;

    public int BadgePulseDurationMs { get; init; } = 640;

    public string ViewportEasing { get; init; } = "softInOut";

    public string OverlayEasing { get; init; } = "cubicOut";
}

public sealed class AnimationTimelinePhase
{
    public string Key { get; init; } = string.Empty;

    public string Label { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string DurationLabel { get; init; } = string.Empty;
}

public sealed class AnimationTimelinePreviewSnapshot
{
    public string TestHookId { get; init; } = "animation-timeline-preview";

    public string Label { get; init; } = "Animation timeline";

    public string Title { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string StatePill { get; init; } = string.Empty;

    public string ReducedMotionLabel { get; init; } = "Reduced motion jumps to end state";

    public IReadOnlyList<string> Metrics { get; init; } = [];

    public IReadOnlyList<AnimationTimelinePhase> Phases { get; init; } = [];
}

public static class AnimationTimelinePreviewFactory
{
    public static AnimationTimelinePreviewSnapshot CreateForWorkbench(CanvasWorkbenchSurface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);

        var options = AnimationTimelineOptions.Default;
        var selectedCount = Math.Max(1, surface.UiState.SelectedNodeIds.Count);
        var guideEnabled = surface.Chrome.SnapGuides.IsEnabled;
        var authoredLinks = surface.Links.Count(link => link.IsUserAuthored);

        return new AnimationTimelinePreviewSnapshot
        {
            Title = "Shared motion now owns viewport easing, overlay fades, and connector flow",
            Summary = "Fit-to-view, focus pans, snap guides, and authored links now share one reduced-motion-aware timeline instead of sprinkling bespoke requestAnimationFrame loops across the runtime.",
            StatePill = guideEnabled ? "Live" : "Ready",
            Metrics =
            [
                "Viewport + overlays",
                "Reduced motion aware",
                $"{selectedCount} focus target{(selectedCount == 1 ? string.Empty : "s")}",
                $"{authoredLinks} authored link{(authoredLinks == 1 ? string.Empty : "s")}"
            ],
            Phases =
            [
                new AnimationTimelinePhase
                {
                    Key = "viewport",
                    Label = "Viewport transition",
                    Summary = "Fit and focus actions tween pan plus zoom instead of jumping the entire scene.",
                    DurationLabel = $"{options.ViewportDurationMs} ms"
                },
                new AnimationTimelinePhase
                {
                    Key = "guides",
                    Label = "Guide fade",
                    Summary = "Snap and anchor overlays fade in fast enough to clarify alignment without blocking drag work.",
                    DurationLabel = $"{options.OverlayFadeDurationMs} ms"
                },
                new AnimationTimelinePhase
                {
                    Key = "connector",
                    Label = "Connector flow",
                    Summary = "Authored links and badge states get subtle motion accents that stay disabled when reduced motion is requested.",
                    DurationLabel = $"{options.ConnectorFlowDurationMs} ms"
                },
                new AnimationTimelinePhase
                {
                    Key = "badge",
                    Label = "Badge pulse",
                    Summary = "Preview-only badge motion calls out focus change cadence without inventing a second animation system.",
                    DurationLabel = $"{options.BadgePulseDurationMs} ms"
                }
            ]
        };
    }
}


