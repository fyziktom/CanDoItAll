namespace CanDoItAll.Infrastructure.Storage;

public sealed partial class FtpStorageDriver : IStorageStablePlacementDriver {
    bool IStorageStablePlacementDriver.CanRecoverWithoutWriteAcknowledgement => false;

    Task<StorageObjectReference> IStorageStablePlacementDriver.PrepareStableTargetAsync(StorageDriverInput storage,
        StoragePlacementIntentId intentId, StorageWriteRequest request, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        var relativePath = string.IsNullOrWhiteSpace(request.RelativePathHint)
            ? $"placements/{intentId.Value:N}/{CanDoItAll.SharedKernel.PortablePhysicalFileNamePolicy.Encode(request.FileName).PhysicalName}"
            : request.RelativePathHint;
        return Task.FromResult(new StorageObjectReference(storage.Id, ProviderKind, StorageLocatorKind.RemotePath,
            NormalizeRemotePath(relativePath, request.FileName), request.FileName,
            string.IsNullOrWhiteSpace(request.ContentType) ? "application/octet-stream" : request.ContentType,
            request.Content.LongLength) { PlacementIntentId = intentId.Value });
    }

    async Task<StorageWriteResult> IStorageStablePlacementDriver.WriteStableTargetAsync(StorageDriverInput storage,
        StorageObjectReference target, StorageWriteRequest request, CancellationToken cancellationToken) {
        if (target.ProviderKind != ProviderKind || target.StorageId != storage.Id || target.LocatorKind != StorageLocatorKind.RemotePath) {
            throw new InvalidOperationException("The prepared FTP target does not match this storage.");
        }
        var result = await SaveAsync(storage, request with { RelativePathHint = target.Locator }, cancellationToken);
        return result with { Reference = result.Reference with { PlacementIntentId = target.PlacementIntentId } };
    }

    Task IStorageStablePlacementDriver.CompleteStableTargetAsync(StorageDriverInput storage,
        StorageObjectReference target, CancellationToken cancellationToken) => Task.CompletedTask;
}
