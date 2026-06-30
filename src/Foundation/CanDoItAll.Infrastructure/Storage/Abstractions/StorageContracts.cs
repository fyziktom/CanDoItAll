namespace CanDoItAll.Infrastructure.Storage;

public interface IStorageDriver
{
    StorageProviderKind ProviderKind { get; }

    StorageCapability SupportedCapabilities { get; }

    Task<StorageConnectionTestResult> TestConnectionAsync(
        StorageCatalogRecord storage,
        string? secretValue,
        CancellationToken cancellationToken = default);

    Task<StorageWriteResult> SaveAsync(
        StorageCatalogRecord storage,
        StorageWriteRequest request,
        CancellationToken cancellationToken = default);

    Task<Stream> OpenReadAsync(
        StorageCatalogRecord storage,
        StorageObjectReference reference,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        StorageCatalogRecord storage,
        StorageObjectReference reference,
        CancellationToken cancellationToken = default);
}

public interface IStorageDriverRegistry
{
    IReadOnlyCollection<StorageProviderKind> RegisteredKinds { get; }

    bool TryResolve(StorageProviderKind providerKind, out IStorageDriver driver);

    IStorageDriver Resolve(StorageProviderKind providerKind);
}

public interface IStorageCatalogService
{
    Task<IReadOnlyList<StorageCatalogRecord>> ListAsync(CancellationToken cancellationToken = default);

    Task<StorageCatalogRecord?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<StorageCatalogRecord> EnsureBootstrapFileSystemStorageAsync(CancellationToken cancellationToken = default);

    Task<StorageCatalogRecord> SaveAsync(StorageCatalogRecord record, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StorageRoutingRule>> ListRulesAsync(CancellationToken cancellationToken = default);

    Task<StorageRoutingRule> SaveRuleAsync(StorageRoutingRule rule, CancellationToken cancellationToken = default);
}

public interface IStorageRoutingService
{
    Task<StorageRecommendation> RecommendAsync(StorageSelectionContext context, CancellationToken cancellationToken = default);
}

public interface IStorageConnectionTestService
{
    Task<StorageConnectionTestResult> TestAsync(Guid storageId, CancellationToken cancellationToken = default);
}

public interface IStorageAccessService
{
    Task<StorageAccessDescriptor> DescribeAsync(
        StorageObjectReference reference,
        CancellationToken cancellationToken = default);
}

public interface IStoragePlacementService
{
    Task<StoragePlacementResult> PlaceAsync(
        StoragePlacementRequest request,
        CancellationToken cancellationToken = default);
}

public interface IStorageTransferPipeline
{
    Task<StorageTransferResult> ExecuteAsync(StorageTransferManifest manifest, CancellationToken cancellationToken = default);
}

public interface IStorageSecretResolver
{
    Task<string?> ResolveCredentialAsync(Guid? secretId, CancellationToken cancellationToken = default);
}

public interface IStorageCompatibilityFileStoreAdapter : IFileStore
{
}

public interface IStorageCompatibilityArtifactStoreAdapter : IManagedArtifactStore
{
}
