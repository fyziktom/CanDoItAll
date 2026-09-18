using CanDoItAll.SharedKernel;

namespace CanDoItAll.Infrastructure.Storage;

public sealed partial class FileSystemStorageDriver : IStorageStablePlacementDriver {
    bool IStorageStablePlacementDriver.CanRecoverWithoutWriteAcknowledgement => true;

    Task<StorageObjectReference> IStorageStablePlacementDriver.PrepareStableTargetAsync(StorageDriverInput storage,
        StoragePlacementIntentId intentId, StorageWriteRequest request, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        var relativePath = FileSystemStorageKeyCodec.Canonicalize(string.IsNullOrWhiteSpace(request.RelativePathHint)
            ? $"placements/{intentId.Value:N}/{PortablePhysicalFileNamePolicy.Encode(request.FileName).PhysicalName}"
            : request.RelativePathHint);
        pathPolicy.ResolveFullPath(storage, relativePath);
        return Task.FromResult(new StorageObjectReference(storage.Id, ProviderKind, StorageLocatorKind.RelativePath,
            relativePath, request.FileName, string.IsNullOrWhiteSpace(request.ContentType) ? "application/octet-stream" : request.ContentType,
            request.Content.LongLength, BuildLegacyRoute(relativePath)) { PlacementIntentId = intentId.Value });
    }

    async Task<StorageWriteResult> IStorageStablePlacementDriver.WriteStableTargetAsync(StorageDriverInput storage,
        StorageObjectReference target, StorageWriteRequest request, CancellationToken cancellationToken) {
        if (target.ProviderKind != ProviderKind || target.StorageId != storage.Id || target.LocatorKind != StorageLocatorKind.RelativePath) {
            throw new InvalidOperationException("The prepared filesystem target does not match this storage.");
        }
        var fullPath = pathPolicy.ResolveFullPath(storage, target.Locator);
        await durableFileWriter.WriteBytesAsync(pathPolicy.ResolveRootPath(storage), fullPath, request.Content,
            options: DurableFileWriteOptions.CreateNew, cancellationToken: cancellationToken,
            beforeCommit: token => EnsureAllocatedTargetRemainsAvailableAsync(fullPath, token));
        return new(target, new(StorageJson.BuildPreviewUrl(target), StorageJson.BuildDownloadUrl(target), null,
            true, true, IsTrustedForLocalOpen(storage), target.DisplayName, target.ContentType, target.ContentLength, string.Empty));
    }

    Task IStorageStablePlacementDriver.CompleteStableTargetAsync(StorageDriverInput storage,
        StorageObjectReference target, CancellationToken cancellationToken) => Task.CompletedTask;
}
