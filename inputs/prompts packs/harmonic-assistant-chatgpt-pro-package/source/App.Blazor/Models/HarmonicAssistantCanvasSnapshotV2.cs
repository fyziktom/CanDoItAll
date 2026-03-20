namespace App.Blazor.Models;

public sealed record HarmonicCanvasNodeV2(
    string Id,
    string Label,
    string Kind,
    bool IsCurrent,
    int XIndex,
    string? PathId,
    int? StepIndex,
    double Probability,
    double WorldY,
    string Color,
    IReadOnlyDictionary<string, object?>? Meta = null);

public sealed record HarmonicCanvasEdgeV2(
    string FromId,
    string ToId,
    string Kind,
    double Probability);

public sealed record HarmonicCanvasLayoutV2(
    int HistorySteps,
    int HorizonSteps);

public sealed record HarmonicCanvasRenderHintsV2(
    double? CurrentWorldY = null);

public sealed record HarmonicAssistantCanvasSnapshotV2(
    IReadOnlyList<HarmonicCanvasNodeV2> Nodes,
    IReadOnlyList<HarmonicCanvasEdgeV2> Edges,
    string? Caption = null,
    HarmonicCanvasLayoutV2? Layout = null,
    HarmonicCanvasRenderHintsV2? RenderHints = null);
