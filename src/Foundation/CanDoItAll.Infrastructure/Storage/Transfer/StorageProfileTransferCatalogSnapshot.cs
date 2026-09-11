namespace CanDoItAll.Infrastructure.Storage;

internal sealed class StorageProfileTransferCatalogSnapshot(
    IReadOnlyList<StorageCatalogRecord> storages,
    IReadOnlyList<StorageRoutingRule> rules) : IStorageCatalogService {
    public Task<IReadOnlyList<StorageCatalogSnapshot>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<StorageCatalogSnapshot>>(storages.Select(StorageCatalogMapping.ToSnapshot).ToArray());

    public Task<StorageCatalogSnapshot?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(storages.FirstOrDefault(storage => storage.Id == id)?.ToSnapshot());

    public Task<StorageDriverInput?> GetDriverAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(storages.FirstOrDefault(storage => storage.Id == id)?.ToDriverInput());

    public Task<StorageCatalogEditorSnapshot?> GetEditorAsync(Guid id, CancellationToken cancellationToken = default) {
        var storage = storages.FirstOrDefault(storage => storage.Id == id);
        return Task.FromResult(storage is null ? null : new StorageCatalogEditorSnapshot(
            storage.ToSnapshot(), StorageJson.ParseProviderConfiguration(storage.ConfigJson)));
    }

    public Task<StorageDriverInput> EnsureBootstrapFileSystemStorageAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(storages.First(storage => storage.IsSystemDefault &&
            storage.ProviderKind == StorageProviderKind.FileSystem).ToDriverInput());

    public Task<IReadOnlyList<StorageRoutingRuleSnapshot>> ListRulesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<StorageRoutingRuleSnapshot>>(rules.Select(StorageCatalogMapping.ToSnapshot).ToArray());

    internal Task<IReadOnlyList<StorageRoutingRule>> ListRoutingRuleRecordsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(rules);

    public Task<StorageCatalogSnapshot> SaveAsync(StorageCatalogSaveRequest record, CancellationToken cancellationToken = default) =>
        throw CreateReadOnlyException();

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) => throw CreateReadOnlyException();

    public Task<StorageRoutingRuleSnapshot> SaveRuleAsync(StorageRoutingRuleSaveRequest rule,
        CancellationToken cancellationToken = default) => throw CreateReadOnlyException();

    public Task ApplyDefaultPurposesAsync(Guid storageId, IReadOnlyCollection<StorageUsagePurpose> defaultPurposes,
        CancellationToken cancellationToken = default) => throw CreateReadOnlyException();

    private static NotSupportedException CreateReadOnlyException() =>
        new("The inactive-profile storage snapshot is read-only.");
}
