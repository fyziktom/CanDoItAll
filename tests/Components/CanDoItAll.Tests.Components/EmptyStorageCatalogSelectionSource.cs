using CanDoItAll.Modules.Workspace;

namespace CanDoItAll.Tests.Components;

internal sealed class EmptyStorageCatalogSelectionSource : IStorageCatalogSelectionSource
{
    public Task<IReadOnlyList<StorageCatalogSummary>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<StorageCatalogSummary>>([]);
    }
}
