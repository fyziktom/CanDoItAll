using CanDoItAll.Infrastructure.Storage;

namespace CanDoItAll.FileTools.Integration;

internal sealed class StorageFileToolsKnownFileActivator(
    IStorageCatalogService storageCatalog,
    IFileAccessContextProvider contextProvider,
    IStorageFileAccessAuthorizationCoordinator authorizationCoordinator) : IFileToolsKnownFileActivator
{
    public async ValueTask<FileToolsKnownFileActivation> ActivateAsync(
        FileToolsSemanticScope scope,
        FileToolsKnownFileOccurrence occurrence,
        FileToolsKnownFileIntent intent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(occurrence);
        if (!Enum.IsDefined(intent))
        {
            throw new ArgumentOutOfRangeException(nameof(intent));
        }

        StorageCatalogRecord? storage = await storageCatalog.GetAsync(occurrence.StorageId, cancellationToken);
        if (storage is null || !storage.IsEnabled)
        {
            throw Denied(FileAccessFailureCode.SourceUnavailable);
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
        FileAccessContext context = await contextProvider.GetCurrentAsync(cancellationToken);
        FileAccessOperation operations = intent == FileToolsKnownFileIntent.Edit
            ? FileAccessOperation.View | FileAccessOperation.Edit
            : FileAccessOperation.View;
        var grantRequest = new FileAccessGrantRequest(
            context,
            scope,
            storage.Id,
            grantOccurrenceId,
            operations);
        var file = await authorizationCoordinator.GrantAsync(grantRequest, reference, cancellationToken);
        return new FileToolsKnownFileActivation(
            new FileToolsKnownFileRequest(scope, file, intent),
            occurrence.FileName,
            occurrence.MediaType,
            occurrence.Size);
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
            _ => throw Denied(FileAccessFailureCode.SourceUnavailable)
        };

    private static FileAccessDeniedException Denied(FileAccessFailureCode code)
        => new(code, "The authorized file source is unavailable.");
}
