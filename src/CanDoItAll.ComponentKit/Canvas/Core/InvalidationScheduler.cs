namespace CanDoItAll.ComponentKit.Canvas;

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
