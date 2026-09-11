using CanDoItAll.Infrastructure.Storage;

namespace CanDoItAll.Modules.Workbench;

internal sealed class ProjectStorageTransferProvenancePolicy(ProjectManagedStoragePhysicalIdentityPolicy physicalIdentityPolicy)
    : IStorageTransferProvenancePolicy {
    public void ValidateExport(StorageObjectReference reference, StorageCatalogPlanningFact storage, Guid bindingId) {
        if (ProjectManagedStorageProvenancePolicy.HasManagedMarker(reference) &&
            !ProjectManagedStorageProvenancePolicy.TryValidateCurrentStorageFromFacts(reference, storage, physicalIdentityPolicy, out var error)) {
            throw new InvalidDataException($"Project media binding '{bindingId:D}' cannot be exported because {error}");
        }
    }

    public StorageObjectReference StampImport(StorageObjectReference reference, string requestedPath, StorageCatalogPlanningFact storage)
        => ProjectManagedStorageProvenancePolicy.StampFromFacts(reference, requestedPath, storage, physicalIdentityPolicy);
}
