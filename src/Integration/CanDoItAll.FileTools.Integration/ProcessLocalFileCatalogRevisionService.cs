using System.Collections.Concurrent;

namespace CanDoItAll.FileTools.Integration;

internal sealed class ProcessLocalFileCatalogRevisionService :
    IFileCatalogRevisionReader,
    IFileCatalogChangeSink
{
    private readonly ConcurrentDictionary<Guid, RevisionCounter> _storage = new();
    private readonly ConcurrentDictionary<ScopeRevisionKey, RevisionCounter> _scopes = new();

    public FileCatalogRevision Get(FileToolsSemanticScope scope, Guid storageId)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ValidateStorageId(storageId);
        long storageRevision = _storage.TryGetValue(storageId, out RevisionCounter? storage)
            ? storage.Value
            : 0;
        var key = new ScopeRevisionKey(scope.Kind, scope.Id.Value, storageId);
        long scopeRevision = _scopes.TryGetValue(key, out RevisionCounter? scoped)
            ? scoped.Value
            : 0;
        return new FileCatalogRevision(storageRevision, scopeRevision);
    }

    public FileCatalogRevision PublishStorageChanged(Guid storageId)
    {
        ValidateStorageId(storageId);
        long revision = _storage.GetOrAdd(storageId, static _ => new RevisionCounter()).Increment();
        return new FileCatalogRevision(revision, 0);
    }

    public FileCatalogRevision PublishScopeChanged(FileToolsSemanticScope scope, Guid storageId)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ValidateStorageId(storageId);
        var key = new ScopeRevisionKey(scope.Kind, scope.Id.Value, storageId);
        long revision = _scopes.GetOrAdd(key, static _ => new RevisionCounter()).Increment();
        return new FileCatalogRevision(
            _storage.TryGetValue(storageId, out RevisionCounter? storage) ? storage.Value : 0,
            revision);
    }

    private static void ValidateStorageId(Guid storageId)
    {
        if (storageId == Guid.Empty)
        {
            throw new ArgumentException("A storage identifier is required.", nameof(storageId));
        }
    }

    private readonly record struct ScopeRevisionKey(
        FileToolsSemanticScopeKind Kind,
        string Id,
        Guid StorageId);

    private sealed class RevisionCounter
    {
        private long _value;

        public long Value => Interlocked.Read(ref _value);

        public long Increment() => Interlocked.Increment(ref _value);
    }
}
