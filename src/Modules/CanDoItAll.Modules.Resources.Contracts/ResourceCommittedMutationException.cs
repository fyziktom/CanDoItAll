namespace CanDoItAll.Modules.Resources;

public enum ResourceMutationKind {
    Save,
    Delete
}

public sealed class ResourceCommittedMutationException(Guid resourceId, ResourceMutationKind mutationKind, Exception innerException)
    : Exception($"Resource '{resourceId:D}' was {(mutationKind == ResourceMutationKind.Delete ? "deleted" : "saved")}, but a subsequent operation failed. Reload Resources to view the committed state.", innerException) {
    public Guid ResourceId { get; } = resourceId;
    public ResourceMutationKind MutationKind { get; } = mutationKind;
}
