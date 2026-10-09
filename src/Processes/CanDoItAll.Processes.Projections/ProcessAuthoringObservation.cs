namespace CanDoItAll.Processes.Projections;

public sealed record ProcessAuthoringObservation(Guid DatabaseProfileId, Guid ProjectId, Guid ProjectLifetimeId,
    ProcessDefinitionCatalogItemKey DefinitionKey, long Revision, string ContentHash, Guid? PublishedId) {
    public bool HasSameOwner(ProcessAuthoringObservation other)
        => DatabaseProfileId == other.DatabaseProfileId && ProjectId == other.ProjectId &&
            ProjectLifetimeId == other.ProjectLifetimeId && DefinitionKey == other.DefinitionKey;

    public bool Supersedes(ProcessAuthoringObservation other) => HasSameOwner(other) && Revision > other.Revision;
}
