namespace CanDoItAll.Components.CanvasLib;

public sealed class ClipboardBridgeSnapshot
{
    public string TestHookId { get; init; } = "clipboard-bridge";

    public string Label { get; init; } = "Clipboard bridge";

    public string Title { get; init; } = "Clipboard sync is ready";

    public string Summary { get; init; } = string.Empty;

    public string StatePill { get; init; } = "Ready";

    public bool IsEnabled { get; init; }

    public IReadOnlyList<string> Metrics { get; init; } = [];
}

public static class ClipboardBridgeFactory
{
    public static ClipboardBridgeSnapshot CreateForWorkbench(
        CanvasWorkbenchSurface surface,
        SelectionModel selection)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(selection);

        var clipboard = surface.Chrome.Clipboard;

        return new ClipboardBridgeSnapshot
        {
            Title = clipboard.IsEnabled
                ? "Copy, paste, and duplicate are wired"
                : "Clipboard actions are disabled",
            Summary = clipboard.IsEnabled
                ? "Selection payloads are serialized through the shared bridge so pages can duplicate or paste graph entities."
                : "Enable clipboard actions to surface copy, paste, and duplicate flows through the shared bridge.",
            StatePill = clipboard.IsEnabled ? "Ready" : "Off",
            IsEnabled = clipboard.IsEnabled,
            Metrics =
            [
                clipboard.Format,
                clipboard.AllowCopy ? "Copy" : "No copy",
                clipboard.AllowPaste ? "Paste" : "No paste",
                clipboard.AllowDuplicate ? "Duplicate" : "No duplicate",
                $"{selection.SelectedNodeIds.Count} selected"
            ]
        };
    }
}


