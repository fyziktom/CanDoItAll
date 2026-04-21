namespace CanDoItAll.Components.WebGlLib;

public sealed record WebGlSelectionChangedEventArgs(
    string? PrimaryNodeId,
    IReadOnlyList<string> SelectedNodeIds);

public sealed record WebGlNodePositionChange(
    string NodeId,
    double X,
    double Y,
    double Z);

public sealed record WebGlNodeMovedEventArgs(
    IReadOnlyList<WebGlNodePositionChange> Positions);

public sealed record WebGlConnectionChangeRequest(
    string ActionId,
    string? EdgeId,
    string SourceNodeId,
    string SourceAnchorId,
    string? SourcePortId,
    string TargetNodeId,
    string TargetAnchorId,
    string? TargetPortId,
    string Kind,
    string CategoryKey);

public sealed record WebGlDeleteRequest(
    string? NodeId,
    string? EdgeId);
