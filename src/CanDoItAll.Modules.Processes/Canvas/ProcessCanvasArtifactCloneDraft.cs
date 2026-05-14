namespace CanDoItAll.Modules.Processes;

public sealed record ProcessCanvasArtifactCloneDraft(
    string NodeId,
    Guid ArtifactExpectationId,
    double X,
    double Y);
