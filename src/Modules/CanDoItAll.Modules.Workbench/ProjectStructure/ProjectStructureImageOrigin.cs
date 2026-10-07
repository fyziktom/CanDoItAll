using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectStructureImageAuthority(ProjectWriteAdmission project, long databaseGeneration, string? actorId) {
    private int revoked;
    public ProjectWriteAdmission Project { get; } = project;
    public long DatabaseGeneration { get; } = databaseGeneration;
    public string? ActorId { get; } = actorId;
    public bool IsRevoked => Volatile.Read(ref revoked) != 0;
    public void Revoke() => Interlocked.Exchange(ref revoked, 1);
}

public sealed record ProjectStructureImageOrigin(ProjectStructureImageAuthority Authority, ProjectStructureNode Placeholder) {
    internal static ProjectStructureImageOrigin Validate(ProjectStructureDeferredNodeCompletionRequest request) {
        var origin = request.Origin ?? throw new InvalidOperationException("Deferred generation requires its original native owner.");
        if (request.ProjectId != origin.Authority.Project.ProjectId || request.NodeId != origin.Placeholder.Id ||
            origin.Placeholder.RecordId is null || request.OperationId == Guid.Empty || request.GeneratedImage?.ProviderFingerprint is null) {
            throw new InvalidOperationException("Deferred generation does not match its original placeholder.");
        }
        var metadata = ProjectObjectMetadataSerializer.Parse(origin.Placeholder.MetadataJson).DeferredCompletion;
        if (metadata?.OperationId != request.OperationId || metadata.State != ProjectStructureDeferredNodeCompletionState.Queued) {
            throw new InvalidOperationException("The original image operation is no longer queued. Observe its existing outcome.");
        }
        return origin;
    }
}
