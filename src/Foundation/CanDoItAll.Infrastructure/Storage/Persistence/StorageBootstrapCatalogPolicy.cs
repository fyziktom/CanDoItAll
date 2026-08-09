using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.FileSystem;

namespace CanDoItAll.Infrastructure.Storage;

public static class StorageBootstrapCatalogPolicy
{
    private static readonly IPhysicalFileSystemPathPolicyFactory PhysicalPathPolicyFactory =
        new PhysicalFileSystemPathPolicyFactory();

    public static StorageCatalogRecord? ResolveAuthoritativeFileSystemStorage(
        IEnumerable<StorageCatalogRecord> storages,
        string currentWorkspaceRoot)
    {
        ArgumentNullException.ThrowIfNull(storages);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentWorkspaceRoot);
        PhysicalPathSyntaxPolicy.EnsureNativeOrRelative(currentWorkspaceRoot, "workspace root");
        IPhysicalFileSystemPathPolicy workspacePathPolicy = PhysicalPathPolicyFactory.Create(currentWorkspaceRoot);
        List<StorageCatalogRecord> candidates = storages
            .Where(storage => storage.IsSystemDefault &&
                storage.IsEnabled &&
                storage.ProviderKind == StorageProviderKind.FileSystem)
            .OrderBy(storage => storage.DisplayOrder)
            .ThenBy(storage => storage.CreatedAtUtc)
            .ThenBy(storage => storage.Id)
            .Where(storage => StorageCatalogHostBindingPolicy.TryResolve(
                storage,
                workspacePathPolicy.RootPath,
                out string configuredRoot,
                out _) &&
                workspacePathPolicy.PathComparer.Equals(configuredRoot, workspacePathPolicy.RootPath))
            .ToList();
        return candidates.Count switch
        {
            0 => null,
            1 => candidates[0],
            _ => throw new InvalidOperationException(
                "The storage catalog contains multiple authoritative filesystem roots for the current workspace.")
        };
    }
}
