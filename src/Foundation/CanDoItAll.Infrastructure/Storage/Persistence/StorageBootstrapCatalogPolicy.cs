namespace CanDoItAll.Infrastructure.Storage;

public static class StorageBootstrapCatalogPolicy
{
    public static StorageCatalogRecord? ResolveAuthoritativeFileSystemStorage(
        IEnumerable<StorageCatalogRecord> storages,
        string currentWorkspaceRoot)
    {
        ArgumentNullException.ThrowIfNull(storages);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentWorkspaceRoot);
        var authoritative = storages
            .Where(storage => storage.IsSystemDefault)
            .OrderBy(storage => storage.DisplayOrder)
            .ThenBy(storage => storage.CreatedAtUtc)
            .ThenBy(storage => storage.Id)
            .FirstOrDefault();
        if (authoritative?.ProviderKind != StorageProviderKind.FileSystem ||
            string.IsNullOrWhiteSpace(authoritative.EndpointOrRoot))
        {
            return null;
        }

        string configuredRoot;
        try
        {
            configuredRoot = Path.GetFullPath(authoritative.EndpointOrRoot);
        }
        catch (Exception exception) when (
            exception is ArgumentException or
                IOException or
                NotSupportedException)
        {
            return null;
        }

        var workspaceRoot = Path.GetFullPath(currentWorkspaceRoot);
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        return string.Equals(configuredRoot, workspaceRoot, comparison)
            ? authoritative
            : null;
    }
}
