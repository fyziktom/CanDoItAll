namespace CanDoItAll.ComponentKit.Canvas;

public sealed class CanvasWorkbenchAnnotation
{
    public string Id { get; set; } = string.Empty;

    public string Kind { get; set; } = "info";

    public string Tone { get; set; } = "accent";

    public string Label { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Icon { get; set; } = string.Empty;

    public string ActionId { get; set; } = string.Empty;
}

public sealed class CanvasWorkbenchDiagnosticsOptions
{
    public bool IsEnabled { get; set; }

    public bool ShowNodeBounds { get; set; } = true;

    public bool ShowConnectorAnchors { get; set; } = true;

    public bool ShowViewportStats { get; set; } = true;
}

public sealed class CanvasWorkbenchMinimapOptions
{
    public bool IsEnabled { get; set; } = true;

    public string Title { get; set; } = "Scene overview";
}

public sealed class CanvasWorkbenchClipboardOptions
{
    public bool IsEnabled { get; set; } = true;

    public bool AllowCopy { get; set; } = true;

    public bool AllowPaste { get; set; } = true;

    public bool AllowDuplicate { get; set; } = true;

    public string Format { get; set; } = "application/vnd.candoitall.canvas+json";
}

public sealed record CanvasWorkbenchClipboardRequest(
    string ActionId,
    string PayloadJson);
