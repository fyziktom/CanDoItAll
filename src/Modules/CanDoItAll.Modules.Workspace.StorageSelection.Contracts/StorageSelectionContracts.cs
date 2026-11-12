namespace CanDoItAll.Modules.Workspace.StorageSelection.Contracts;

public readonly record struct StorageCatalogSelectionContext(Guid ProfileId, long Generation);

public sealed record StorageSelectionItem(
    Guid Id,
    string Name,
    string Provider,
    string Connection,
    string EndpointOrRoot,
    int DisplayOrder,
    bool IsEnabled,
    bool IsSystemDefault,
    bool IsReadOnly,
    string Health);

public interface IStorageCatalogSelectionSource {
    StorageCatalogSelectionContext Context { get; }
    bool IsCurrent { get; }
    event Action? ContextChanged;
    Task<IReadOnlyList<StorageSelectionItem>> ListAsync(CancellationToken cancellationToken = default);
}
