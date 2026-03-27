namespace CanDoItAll.Components.CanvasLib;

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

public sealed class CanvasSceneHostPreviewSnapshot
{
    public string TestHookId { get; init; } = "canvas-scene-host";

    public string Label { get; init; } = "Canvas scene host";

    public string Title { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string StatePill { get; init; } = string.Empty;

    public IReadOnlyList<string> Metrics { get; init; } = [];
}

public static class CanvasSceneHostPreviewFactory
{
    public static CanvasSceneHostPreviewSnapshot CreateForWorkbench(CanvasWorkbenchSurface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);

        var stateKey = SerializationPersistencePack.Serialize(new
        {
            surface.SurfaceId,
            NodeCount = surface.Nodes.Count,
            LinkCount = surface.Links.Count,
            Selected = surface.UiState.SelectedNodeIds.Count
        });

        var host = new CanvasSceneHost();
        host.QueueSync(stateKey, true);
        var createPath = host.ShouldCreate();
        host.MarkApplied();
        host.QueueSync($"{stateKey}:update", true);
        var updatePath = host.ShouldUpdate();

        return new CanvasSceneHostPreviewSnapshot
        {
            Title = "Scene host tracks create and update sync without page-specific state flags",
            Summary = "The shared host now owns pending state keys, applied keys, and initialization transitions so every preview boundary can follow the same create-update lifecycle.",
            StatePill = host.IsInitialized ? "Synced" : "Pending",
            Metrics =
            [
                createPath ? "Create path armed" : "Create path idle",
                updatePath ? "Update path armed" : "Update path idle",
                $"{surface.Nodes.Count} nodes mirrored",
                host.SurfaceSyncPending ? "Sync pending" : "Sync settled"
            ]
        };
    }
}


