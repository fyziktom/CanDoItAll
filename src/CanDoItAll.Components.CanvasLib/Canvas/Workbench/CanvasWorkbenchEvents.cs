namespace CanDoItAll.Components.CanvasLib;

public sealed record CanvasWorkbenchSelectionChangedEventArgs(
    string? PrimaryNodeId,
    IReadOnlyList<string> SelectedNodeIds);

public sealed record CanvasWorkbenchContextActionRequest(
    string? NodeId,
    string ActionId,
    double X,
    double Y,
    string TargetKind = "node",
    string? LinkSourceId = null,
    string? LinkTargetId = null,
    string? LinkKind = null,
    string? LinkSourcePortId = null,
    string? LinkTargetPortId = null);

public sealed record CanvasWorkbenchCreateActionRequest(
    string ActionId,
    string? SourceNodeId,
    double X,
    double Y,
    string? ParentNodeId,
    string Title,
    string Subtitle,
    string Notes,
    string PlacementKind,
    string CreateMode,
    string ObjectSubtype,
    CanvasWorkbenchUploadedFile? UploadedFile,
    IReadOnlyList<CanvasWorkbenchInputValue>? InputValues = null);

public sealed record CanvasWorkbenchNodeEditRequest(
    string NodeId,
    string Title,
    string Notes);

public sealed record CanvasWorkbenchNodePositionChange(
    string NodeId,
    double X,
    double Y);

public sealed record CanvasWorkbenchNodesMovedEventArgs(
    IReadOnlyList<CanvasWorkbenchNodePositionChange> Positions);

public sealed record CanvasWorkbenchClipboardRequest(
    string ActionId,
    string PayloadJson);
