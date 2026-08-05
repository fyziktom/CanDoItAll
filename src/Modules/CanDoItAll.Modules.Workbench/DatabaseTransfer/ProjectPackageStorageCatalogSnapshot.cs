using CanDoItAll.Infrastructure.Storage;

namespace CanDoItAll.Modules.Workbench;

internal sealed class ProjectPackageStorageCatalogSnapshot(
    IReadOnlyList<StorageCatalogRecord> storages,
    IReadOnlyList<StorageRoutingRule> rules) : IStorageCatalogService
{
    public Task<IReadOnlyList<StorageCatalogRecord>> ListAsync(
        CancellationToken cancellationToken = default)
        => Task.FromResult(storages);

    public Task<StorageCatalogRecord?> GetAsync(
        Guid id,
        CancellationToken cancellationToken = default)
        => Task.FromResult(storages.FirstOrDefault(storage => storage.Id == id));

    public Task<StorageCatalogRecord> EnsureBootstrapFileSystemStorageAsync(
        CancellationToken cancellationToken = default)
        => Task.FromResult(
            storages.First(storage =>
                storage.IsSystemDefault &&
                storage.ProviderKind == StorageProviderKind.FileSystem));

    public Task<IReadOnlyList<StorageRoutingRule>> ListRulesAsync(
        CancellationToken cancellationToken = default)
        => Task.FromResult(rules);

    public Task<StorageCatalogRecord> SaveAsync(
        StorageCatalogRecord record,
        CancellationToken cancellationToken = default)
        => throw CreateReadOnlyException();

    public Task DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
        => throw CreateReadOnlyException();

    public Task<StorageRoutingRule> SaveRuleAsync(
        StorageRoutingRule rule,
        CancellationToken cancellationToken = default)
        => throw CreateReadOnlyException();

    private static NotSupportedException CreateReadOnlyException()
        => new("The inactive-profile storage snapshot is read-only.");
}
