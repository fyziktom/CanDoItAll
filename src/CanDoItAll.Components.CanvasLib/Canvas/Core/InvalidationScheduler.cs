namespace CanDoItAll.Components.CanvasLib;

[Flags]
public enum CanvasInvalidationReason
{
    None = 0,
    Data = 1,
    Layout = 2,
    Viewport = 4,
    Selection = 8,
    Diagnostics = 16,
    PublishState = 32
}

public sealed class InvalidationScheduler
{
    private CanvasInvalidationReason pendingReasons;

    public CanvasInvalidationReason PendingReasons => pendingReasons;

    public bool HasPendingWork => pendingReasons != CanvasInvalidationReason.None;

    public void Invalidate(CanvasInvalidationReason reasons)
        => pendingReasons |= reasons;

    public CanvasInvalidationReason Consume()
    {
        var consumed = pendingReasons;
        pendingReasons = CanvasInvalidationReason.None;
        return consumed;
    }
}

public sealed class InvalidationSchedulerPreviewSnapshot
{
    public string TestHookId { get; init; } = "invalidation-scheduler";

    public string Label { get; init; } = "Invalidation scheduler";

    public string Title { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string StatePill { get; init; } = string.Empty;

    public IReadOnlyList<string> Metrics { get; init; } = [];
}

public static class InvalidationSchedulerPreviewFactory
{
    public static InvalidationSchedulerPreviewSnapshot CreateForWorkbench(
        CanvasWorkbenchSurface surface,
        SelectionModel selection)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(selection);

        var scheduler = new InvalidationScheduler();
        var reasons = CanvasInvalidationReason.Data | CanvasInvalidationReason.Viewport;
        if (selection.SelectedNodeIds.Count > 0)
        {
            reasons |= CanvasInvalidationReason.Selection;
        }

        if (surface.UiState.ShowDiagnostics)
        {
            reasons |= CanvasInvalidationReason.Diagnostics;
        }

        scheduler.Invalidate(reasons);
        var consumed = scheduler.Consume();

        return new InvalidationSchedulerPreviewSnapshot
        {
            Title = "Invalidation reasons are batched before the canvas republishes state",
            Summary = "Data, viewport, selection, and diagnostics refreshes can accumulate in one scheduler instead of triggering ad hoc redraw paths across the runtime.",
            StatePill = scheduler.HasPendingWork ? "Queued" : "Drained",
            Metrics = consumed == CanvasInvalidationReason.None
                ? ["No reasons pending"]
                : consumed.ToString().Split(", ", StringSplitOptions.RemoveEmptyEntries)
        };
    }
}


