namespace CanDoItAll.Infrastructure.Storage;

public sealed record StoragePlacementRequest(
    string FileName,
    string ContentType,
    byte[] Content,
    StorageUsagePurpose UsagePurpose,
    StorageContentKind ContentKind = StorageContentKind.Unknown,
    Guid? ProjectId = null,
    string? NodeKey = null,
    string? RelativePathHint = null,
    bool PreviewRequired = false,
    bool PublishIntent = false,
    Guid? PreferredStorageId = null);

public sealed record StoragePlacementResult(
    StorageCatalogRecord Storage,
    StorageRecommendation Recommendation,
    StorageWriteResult WriteResult,
    string Route,
    string Location,
    string RelativePath);
