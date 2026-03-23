namespace CanDoItAll.ComponentKit.Canvas;

public sealed class CanvasSceneHost
{
    public bool IsInitialized { get; private set; }

    public bool SurfaceSyncPending { get; private set; } = true;

    public string? PendingStateKey { get; private set; }

    public string? AppliedStateKey { get; private set; }

    public void QueueSync(string? stateKey, bool shouldSync = true)
    {
        PendingStateKey = stateKey;
        SurfaceSyncPending = shouldSync;
    }

    public bool ShouldCreate() => !IsInitialized;

    public bool ShouldUpdate() => IsInitialized && SurfaceSyncPending;

    public void MarkApplied()
    {
        AppliedStateKey = PendingStateKey;
        SurfaceSyncPending = false;
        IsInitialized = true;
    }

    public void Reset()
    {
        PendingStateKey = null;
        AppliedStateKey = null;
        SurfaceSyncPending = true;
        IsInitialized = false;
    }
}
