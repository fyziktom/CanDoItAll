using CanDoItAll.Infrastructure.Storage;

namespace CanDoItAll.FileTools.Integration;

internal sealed record ResolvedStorageKnownFile(
    StorageCatalogRecord Storage,
    StorageObjectReference Reference,
    string GrantOccurrenceId);

internal sealed class StorageFileToolsKnownFileResolver(
    IStorageCatalogService storageCatalog)
{
    public async ValueTask<ResolvedStorageKnownFile> ResolveAsync(
        FileToolsKnownFileOccurrence occurrence,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(occurrence);
        StorageCatalogRecord? storage = await storageCatalog.GetAsync(
            occurrence.StorageId,
            cancellationToken);
        if (storage is null || !storage.IsEnabled)
        {
            throw SourceUnavailable();
        }

        StorageLocatorKind locatorKind = ResolveLocatorKind(storage.ProviderKind, occurrence.Kind);
        string grantOccurrenceId = locatorKind == StorageLocatorKind.ContentAddress
            ? $"cid:{occurrence.OccurrenceId}"
            : occurrence.OccurrenceId;
        var reference = new StorageObjectReference(
            storage.Id,
            storage.ProviderKind,
            locatorKind,
            occurrence.OccurrenceId,
            occurrence.FileName,
            occurrence.MediaType ?? "application/octet-stream",
            occurrence.Size);
        return new ResolvedStorageKnownFile(storage, reference, grantOccurrenceId);
    }

    private static StorageLocatorKind ResolveLocatorKind(
        StorageProviderKind providerKind,
        FileToolsKnownFileOccurrenceKind occurrenceKind)
        => (providerKind, occurrenceKind) switch
        {
            (StorageProviderKind.FileSystem, FileToolsKnownFileOccurrenceKind.RelativePath) => StorageLocatorKind.RelativePath,
            (StorageProviderKind.Ipfs, FileToolsKnownFileOccurrenceKind.ContentAddress) => StorageLocatorKind.ContentAddress,
            (StorageProviderKind.Ipfs, FileToolsKnownFileOccurrenceKind.RemotePath) => StorageLocatorKind.RemotePath,
            (StorageProviderKind.Ftp, FileToolsKnownFileOccurrenceKind.RemotePath) => StorageLocatorKind.RemotePath,
            _ => throw SourceUnavailable()
        };

    private static FileAccessDeniedException SourceUnavailable()
        => new(
            FileAccessFailureCode.SourceUnavailable,
            "The authorized file source is unavailable.");
}
