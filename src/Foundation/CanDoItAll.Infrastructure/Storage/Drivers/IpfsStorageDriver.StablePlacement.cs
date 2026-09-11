namespace CanDoItAll.Infrastructure.Storage;

public sealed partial class IpfsStorageDriver : IStorageStablePlacementDriver {
    bool IStorageStablePlacementDriver.CanRecoverWithoutWriteAcknowledgement => true;

    async Task<StorageObjectReference> IStorageStablePlacementDriver.PrepareStableTargetAsync(StorageCatalogRecord storage,
        StoragePlacementIntentId intentId, StorageWriteRequest request, CancellationToken cancellationToken) {
        var token = await secretResolver.ResolveCredentialAsync(storage.CredentialSecretId, cancellationToken);
        var content = await RequireStableTransport().AddStableAsync(storage, token, request.FileName, request.Content,
            IpfsStableAddMode.ComputeOnly, cancellationToken);
        return new(storage.Id, ProviderKind, StorageLocatorKind.ContentAddress, content.ContentId, request.FileName,
            string.IsNullOrWhiteSpace(request.ContentType) ? "application/octet-stream" : request.ContentType,
            request.Content.LongLength, ResolveDirectUrl(storage, content.ContentId)) { PlacementIntentId = intentId.Value };
    }

    async Task<StorageWriteResult> IStorageStablePlacementDriver.WriteStableTargetAsync(StorageCatalogRecord storage,
        StorageObjectReference target, StorageWriteRequest request, CancellationToken cancellationToken) {
        if (target.ProviderKind != ProviderKind || target.StorageId != storage.Id || target.LocatorKind != StorageLocatorKind.ContentAddress) {
            throw new InvalidOperationException("The prepared IPFS target does not match this storage.");
        }
        var token = await secretResolver.ResolveCredentialAsync(storage.CredentialSecretId, cancellationToken);
        var content = await RequireStableTransport().AddStableAsync(storage, token, request.FileName, request.Content,
            IpfsStableAddMode.Store, cancellationToken);
        if (!string.Equals(content.ContentId, target.Locator, StringComparison.Ordinal)) {
            throw new InvalidOperationException("The IPFS add result differs from the prepared content identifier.");
        }
        return new(target, new(StorageJson.BuildPreviewUrl(target), StorageJson.BuildDownloadUrl(target), target.Route,
            true, true, false, target.DisplayName, target.ContentType, target.ContentLength, string.Empty));
    }

    async Task IStorageStablePlacementDriver.CompleteStableTargetAsync(StorageCatalogRecord storage,
        StorageObjectReference target, CancellationToken cancellationToken) {
        if (StorageJson.ParseProviderConfiguration(storage.ConfigJson).PinOnUpload) {
            var token = await secretResolver.ResolveCredentialAsync(storage.CredentialSecretId, cancellationToken);
            await transport.PinAsync(storage, token, target.Locator, cancellationToken);
        }
    }

    private IIpfsStableStorageTransport RequireStableTransport() => transport as IIpfsStableStorageTransport
        ?? throw new InvalidOperationException("The IPFS transport does not support explicitly configured stable content addressing.");
}
