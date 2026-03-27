namespace CanDoItAll.Components.CanvasLib;

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

public sealed class CanvasWorkbenchTooltipPopoverOptions
{
    public bool IsEnabled { get; set; } = true;

    public bool FocusTriggers { get; set; } = true;

    public bool SupportsRichPreview { get; set; } = true;
}

public sealed class CanvasWorkbenchMarqueeOptions
{
    public bool IsEnabled { get; set; } = true;

    public string ModifierKey { get; set; } = "Alt";

    public string SelectionMode { get; set; } = "Intersect";
}

public sealed class CanvasWorkbenchSnapGuideOptions
{
    public bool IsEnabled { get; set; } = true;

    public double Tolerance { get; set; } = 18;

    public string ModifierPolicy { get; set; } = "ShiftBypassesSnap";
}

public sealed class CanvasWorkbenchConnectorAnchorOptions
{
    public bool IsEnabled { get; set; } = true;

    public bool ShowOnHover { get; set; } = true;

    public bool ShowOnSelection { get; set; } = true;

    public string PlacementMode { get; set; } = "Edges";
}

public sealed class CanvasWorkbenchTransformHandleOptions
{
    public bool IsEnabled { get; set; } = true;

    public bool ShowResizeHandles { get; set; } = true;

    public bool ShowRotateHandle { get; set; } = true;

    public string PlacementMode { get; set; } = "SelectionBounds";
}

public sealed record CanvasWorkbenchClipboardRequest(
    string ActionId,
    string PayloadJson);


