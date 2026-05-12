namespace CanDoItAll.Components.CanvasLib;

public sealed class CanvasWorkbenchSurface
{
    public string SurfaceId { get; set; } = string.Empty;

    public string Mode { get; set; } = "authoring";

    public string DependencySourceId { get; set; } = string.Empty;

    public List<CanvasWorkbenchNode> Nodes { get; set; } = [];

    public List<CanvasWorkbenchLink> Links { get; set; } = [];

    public CanvasWorkbenchUiState UiState { get; set; } = new();

    public CanvasWorkbenchChrome Chrome { get; set; } = new();
}

public sealed class CanvasWorkbenchLink
{
    public string SourceId { get; set; } = string.Empty;

    public string TargetId { get; set; } = string.Empty;

    public string SourcePortId { get; set; } = string.Empty;

    public string TargetPortId { get; set; } = string.Empty;

    public string Kind { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string Tone { get; set; } = "neutral";

    public bool IsUserAuthored { get; set; }
}
