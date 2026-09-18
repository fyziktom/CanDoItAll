namespace CanDoItAll.Infrastructure.Storage;

public interface IStorageTransferProvenancePolicy {
    void ValidateExport(StorageObjectReference reference, StorageCatalogPlanningFact storage, Guid bindingId);
    StorageObjectReference StampImport(StorageObjectReference reference, string requestedPath, StorageCatalogPlanningFact storage);
}
