using CanDoItAll.FileTools.Integration;

namespace CanDoItAll.Modules.Resources;

internal sealed class ResourcePromotionCommittedException(Guid resourceId, bool created, FileCatalogRevision? revision, Exception failure)
    : Exception($"Resource '{resourceId:D}' was confirmed, but a subsequent promotion operation failed. Refresh reads only; do not repeat promotion automatically.", failure) {
    public Guid ResourceId { get; } = resourceId;
    public bool Created { get; } = created;
    public FileCatalogRevision? Revision { get; } = revision;
}
