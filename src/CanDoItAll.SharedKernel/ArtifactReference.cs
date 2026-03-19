namespace CanDoItAll.SharedKernel;

public sealed record ArtifactReference(
    string Kind,
    Guid? EntityId,
    string Title,
    string Route,
    string? Description = null,
    Guid? ProjectId = null,
    string? ArtifactKey = null,
    string? ProjectName = null,
    string? PhaseName = null,
    string? TabKind = null,
    string? SnapshotJson = null);
