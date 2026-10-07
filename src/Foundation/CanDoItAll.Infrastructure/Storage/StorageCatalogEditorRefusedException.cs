namespace CanDoItAll.Infrastructure.Storage;

public enum StorageCatalogEditorRefusal { Missing, Protected, AlreadyExists }

public sealed class StorageCatalogEditorRefusedException(StorageCatalogEditorRefusal reason)
    : InvalidOperationException("The exact storage catalog editor target cannot be changed.") {
    public StorageCatalogEditorRefusal Reason { get; } = reason;
}
