namespace CanDoItAll.Modules.Workspace;

public interface IStorageCatalogSelectionSource
{
    Task<IReadOnlyList<StorageCatalogSummary>> ListAsync(
        CancellationToken cancellationToken = default);
}

public sealed class WorkspaceStorageCatalogSelectionSource(WorkspaceService workspaceService)
    : IStorageCatalogSelectionSource
{
    public Task<IReadOnlyList<StorageCatalogSummary>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        return workspaceService.ListStorageCatalogAsync(cancellationToken);
    }
}
