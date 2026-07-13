namespace CanDoItAll.FileTools.Integration;

public readonly record struct FileCatalogRevision
{
    public FileCatalogRevision(long storage, long scope)
    {
        if (storage < 0 || scope < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(storage));
        }

        Storage = storage;
        Scope = scope;
    }

    public long Storage { get; }

    public long Scope { get; }
}

public interface IFileCatalogRevisionReader
{
    FileCatalogRevision Get(FileToolsSemanticScope scope, Guid storageId);
}

public interface IFileCatalogChangeSink
{
    FileCatalogRevision PublishStorageChanged(Guid storageId);

    FileCatalogRevision PublishScopeChanged(FileToolsSemanticScope scope, Guid storageId);
}
