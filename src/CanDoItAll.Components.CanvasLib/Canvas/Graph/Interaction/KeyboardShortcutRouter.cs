namespace CanDoItAll.Components.CanvasLib;

public sealed class KeyboardShortcutRouterSnapshot
{
    public string TestHookId { get; init; } = "keyboard-shortcut-router";

    public string Label { get; init; } = "Keyboard shortcut router";

    public string Title { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string StatePill { get; init; } = string.Empty;

    public bool IsEnabled { get; init; }

    public IReadOnlyList<string> Metrics { get; init; } = [];
}

public static class KeyboardShortcutRouterFactory
{
    public static KeyboardShortcutRouterSnapshot CreateForWorkbench(
        CanvasWorkbenchSurface surface,
        SelectionModel selection)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(selection);

        var metrics = new List<string>
        {
            "0 / +/- fit and zoom",
            "M / D / ? overlays and help"
        };

        if (surface.Chrome.Clipboard.IsEnabled)
        {
            metrics.Add("Ctrl/Cmd+X / C / V clipboard");
        }

        metrics.Add(selection.SelectedNodeIds.Count > 0
            ? $"Selection scoped to {selection.SelectedNodeIds.Count}"
            : "Selection scope idle");

        return new KeyboardShortcutRouterSnapshot
        {
            Title = "Shared keyboard routing owns zoom, help, clipboard, and selection scope",
            Summary = "Graph shortcuts no longer depend on page-local listeners alone. The workbench can expose one keyboard contract for fit, focus, diagnostics, clipboard, and future undo/redo wiring.",
            StatePill = "Scoped",
            IsEnabled = true,
            Metrics = metrics
        };
    }
}


