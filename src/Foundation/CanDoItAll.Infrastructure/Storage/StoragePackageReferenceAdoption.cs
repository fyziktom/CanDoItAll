using CanDoItAll.Infrastructure.Persistence;

namespace CanDoItAll.Infrastructure.Storage;

public sealed record StorageReferenceImportHistory(RetainedEvidenceImport Origin, StorageObjectReference SourceReference);

public static class StoragePackageReferenceAdoption {
    public static StorageObjectReference AdoptImmutable(StorageObjectReference source, Guid targetStorageId,
        long contentLength, Guid sourceProfileId, Guid packageId) {
        ArgumentNullException.ThrowIfNull(source);
        if (source.ProviderKind != StorageProviderKind.Ipfs || source.LocatorKind != StorageLocatorKind.ContentAddress ||
                string.IsNullOrWhiteSpace(source.Locator) || source.StorageId == Guid.Empty || targetStorageId == Guid.Empty ||
                contentLength < 0) {
            throw new InvalidDataException("Immutable package adoption requires a content address, target storage and nonnegative content length.");
        }
        if (source.FormatVersion is < 1 or > StorageObjectReference.MaximumSupportedFormatVersion ||
                source.PlacementIntentId == Guid.Empty ||
                source.FormatVersion == StorageObjectReference.StablePlacementFormatVersion && source.PlacementIntentId is null) {
            throw new InvalidDataException("The source storage reference has invalid format or placement identity evidence.");
        }
        var history = new StorageReferenceImportHistory(new(sourceProfileId, packageId), source);
        return new(targetStorageId, StorageProviderKind.Ipfs, StorageLocatorKind.ContentAddress, source.Locator,
            source.DisplayName, source.ContentType, contentLength) { ImportedHistory = history };
    }
}
