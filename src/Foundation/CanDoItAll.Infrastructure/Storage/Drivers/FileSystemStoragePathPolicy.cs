namespace CanDoItAll.Infrastructure.Storage;

public sealed class FileSystemStoragePathPolicy(IWorkspacePathResolver workspacePathResolver)
{
    public string ResolveWorkspaceRootPath()
        => Path.GetFullPath(workspacePathResolver.ResolveWorkspaceRoot());

    public string ResolveRootPath(StorageCatalogRecord storage)
    {
        ArgumentNullException.ThrowIfNull(storage);
        string configuredRoot = string.IsNullOrWhiteSpace(storage.EndpointOrRoot)
            ? workspacePathResolver.ResolveWorkspaceRoot()
            : storage.EndpointOrRoot;
        return Path.GetFullPath(configuredRoot);
    }

    public string ResolveFullPath(StorageCatalogRecord storage, string relativePath)
    {
        ArgumentNullException.ThrowIfNull(relativePath);
        string rootPath = ResolveRootPath(storage);
        string normalizedPath = NormalizeRelativeKey(relativePath);
        string fullPath = Path.GetFullPath(Path.Combine(rootPath, normalizedPath));
        EnsureWithinRoot(rootPath, fullPath);
        EnsureNoReparseTraversal(rootPath, fullPath);
        return fullPath;
    }

    public string ResolveTrustedWorkspacePath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string workspaceRoot = Path.GetFullPath(workspacePathResolver.ResolveWorkspaceRoot());
        string fullPath = Path.GetFullPath(path);
        EnsureWithinRoot(workspaceRoot, fullPath);
        EnsureNoReparseTraversal(workspaceRoot, fullPath);
        return fullPath;
    }

    public static string ResolveReparseSafeFullPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string fullPath = Path.GetFullPath(path);
        string? rootPath = Path.GetPathRoot(fullPath);
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            throw new StorageBrowseException(new StorageBrowseError(
                StorageBrowseErrorCode.AccessDenied,
                "The requested filesystem path does not have a trusted root."));
        }

        EnsureNoReparseTraversal(rootPath, fullPath);
        return fullPath;
    }

    public string ResolveTrustedLocalOpenPath(StorageCatalogRecord storage, string relativePath)
    {
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(relativePath);
        string workspaceRoot = Path.GetFullPath(workspacePathResolver.ResolveWorkspaceRoot());
        string storageRoot = ResolveRootPath(storage);
        EnsureWithinRoot(workspaceRoot, storageRoot);
        string normalizedPath = NormalizeRelativeKey(relativePath);
        string fullPath = Path.GetFullPath(Path.Combine(storageRoot, normalizedPath));
        EnsureWithinRoot(storageRoot, fullPath);
        EnsureNoReparseTraversal(workspaceRoot, fullPath);
        return fullPath;
    }

    public string ResolveDirectory(StorageCatalogRecord storage, StorageBrowseContainer container)
    {
        ArgumentNullException.ThrowIfNull(container);
        string fullPath = ResolveFullPath(storage, container.Key);
        if (!Directory.Exists(fullPath))
        {
            throw new StorageBrowseException(new StorageBrowseError(
                StorageBrowseErrorCode.ProviderUnavailable,
                "The requested filesystem container is unavailable."));
        }

        return fullPath;
    }

    public bool IsTrustedForLocalOpen(StorageCatalogRecord storage)
    {
        try
        {
            string workspaceRoot = Path.GetFullPath(workspacePathResolver.ResolveWorkspaceRoot());
            string storageRoot = ResolveRootPath(storage);
            EnsureWithinRoot(workspaceRoot, storageRoot);
            EnsureNoReparseTraversal(workspaceRoot, storageRoot);
            return true;
        }
        catch (Exception exception) when (
            exception is ArgumentException
                or IOException
                or NotSupportedException
                or StorageBrowseException
                or UnauthorizedAccessException)
        {
            return false;
        }
    }

    internal static string NormalizeRelativeKey(string path)
        => path
            .Trim()
            .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
            .TrimStart(Path.DirectorySeparatorChar);

    private static void EnsureWithinRoot(string rootPath, string fullPath)
    {
        if (!IsWithinRoot(rootPath, fullPath))
        {
            throw new StorageBrowseException(new StorageBrowseError(
                StorageBrowseErrorCode.AccessDenied,
                "The requested filesystem path is outside the configured storage root."));
        }
    }

    private static bool IsWithinRoot(string rootPath, string fullPath)
    {
        string normalizedRoot = rootPath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return fullPath.StartsWith(normalizedRoot, PathComparison) ||
               string.Equals(
                   fullPath,
                   rootPath.TrimEnd(Path.DirectorySeparatorChar),
                   PathComparison);
    }

    private static StringComparison PathComparison => OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    private static void EnsureNoReparseTraversal(string rootPath, string fullPath)
    {
        string relativePath = Path.GetRelativePath(rootPath, fullPath);
        if (relativePath == ".")
        {
            return;
        }

        string currentPath = rootPath;
        foreach (string segment in relativePath.Split(
                     Path.DirectorySeparatorChar,
                     StringSplitOptions.RemoveEmptyEntries))
        {
            currentPath = Path.Combine(currentPath, segment);
            if (!Directory.Exists(currentPath) && !File.Exists(currentPath))
            {
                continue;
            }

            if (File.GetAttributes(currentPath).HasFlag(FileAttributes.ReparsePoint))
            {
                throw new StorageBrowseException(new StorageBrowseError(
                    StorageBrowseErrorCode.AccessDenied,
                    "Filesystem reparse-point traversal is not allowed."));
            }
        }
    }
}
