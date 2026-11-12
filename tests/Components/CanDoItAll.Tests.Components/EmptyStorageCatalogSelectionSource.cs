using CanDoItAll.Modules.Workspace.StorageSelection.Contracts;

namespace CanDoItAll.Tests.Components;

internal sealed class EmptyStorageCatalogSelectionSource : IStorageCatalogSelectionSource {
    public StorageCatalogSelectionContext Context { get; } = new(Guid.NewGuid(), 0);
    public bool IsCurrent => true;
    public event Action? ContextChanged { add { } remove { } }
    public Task<IReadOnlyList<StorageSelectionItem>> ListAsync(
        CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<StorageSelectionItem>>([]);
    }
}
