namespace CanDoItAll.SharedKernel;

public sealed record ArtifactReference(
    string Kind,
    Guid? EntityId,
    string Title,
    string Route,
    string? Description = null);
