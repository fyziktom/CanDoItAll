using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.FileSystem;

namespace CanDoItAll.Infrastructure.Storage;

public static class StorageBootstrapCatalogPolicy
{
    private static readonly IPhysicalFileSystemPathPolicyFactory PhysicalPathPolicyFactory =
        new PhysicalFileSystemPathPolicyFactory();

    public static StorageCatalogRecord? ResolveAuthoritativeFileSystemStorage(
        IEnumerable<StorageCatalogRecord> storages, string currentWorkspaceRoot) {
        return ResolveAuthoritativeStorage(storages, currentWorkspaceRoot,
            static storage => StorageCatalogPlanningFact.FromCatalogRecord(storage, includeFtpAddressing: false));
    }

    public static StorageCatalogPlanningFact? ResolveAuthoritativeFileSystemStorageFact(
        IEnumerable<StorageCatalogPlanningFact> storages, string currentWorkspaceRoot) {
        return ResolveAuthoritativeStorage(storages, currentWorkspaceRoot, static storage => storage);
    }

    private static T? ResolveAuthoritativeStorage<T>(IEnumerable<T> storages, string currentWorkspaceRoot,
        Func<T, StorageCatalogPlanningFact> describe) where T : class {
        ArgumentNullException.ThrowIfNull(storages);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentWorkspaceRoot);
        PhysicalPathSyntaxPolicy.EnsureNativeOrRelative(currentWorkspaceRoot, "workspace root");
        var workspacePathPolicy = PhysicalPathPolicyFactory.Create(currentWorkspaceRoot);
        var candidates = storages.Select(storage => (Storage: storage, Fact: describe(storage)))
            .Where(item => item.Fact.IsSystemDefault && item.Fact.IsEnabled && item.Fact.ProviderKind == StorageProviderKind.FileSystem)
            .OrderBy(item => item.Fact.DisplayOrder).ThenBy(item => item.Fact.CreatedAtUtc).ThenBy(item => item.Fact.Id)
            .Where(item => StorageCatalogHostBindingPolicy.TryResolveFromFacts(item.Fact, workspacePathPolicy.RootPath,
                out var configuredRoot, out _) && workspacePathPolicy.PathComparer.Equals(configuredRoot, workspacePathPolicy.RootPath))
            .ToArray();
        return candidates.Length switch {
            0 => null,
            1 => candidates[0].Storage,
            _ => throw new InvalidOperationException("The storage catalog contains multiple authoritative filesystem roots for the current workspace.")
        };
    }
}
