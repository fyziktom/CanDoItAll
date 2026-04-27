namespace CanDoItAll.Modules.Workbench;

public sealed record ArtifactReference(
    string Kind,
    Guid? EntityId,
    string Title,
    string Route,
    string Description,
    Guid? ProjectId = null,
    string? ArtifactKey = null,
    string? ProjectName = null,
    string? PhaseName = null,
    string? SnapshotJson = null,
    string? TabKind = null);
