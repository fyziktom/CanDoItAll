using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Infrastructure.Storage;

public sealed class FileSystemStoragePathPolicy
{
    private readonly IWorkspacePathResolver workspacePathResolver;
    private readonly IPhysicalFileSystemPathPolicyFactory physicalPathPolicyFactory;

    public FileSystemStoragePathPolicy(IWorkspacePathResolver workspacePathResolver)
        : this(workspacePathResolver, new PhysicalFileSystemPathPolicyFactory())
    {
    }

    public FileSystemStoragePathPolicy(
        IWorkspacePathResolver workspacePathResolver,
        IPhysicalFileSystemPathPolicyFactory physicalPathPolicyFactory)
    {
        this.workspacePathResolver = workspacePathResolver ?? throw new ArgumentNullException(nameof(workspacePathResolver));
        this.physicalPathPolicyFactory = physicalPathPolicyFactory ?? throw new ArgumentNullException(nameof(physicalPathPolicyFactory));
    }

    public string ResolveWorkspaceRootPath()
        => ResolveWorkspaceRoot();

    public string ResolveRootPath(StorageCatalogRecord storage)
        => ResolveRootPolicy(storage).RootPath;

    private string ResolveConfiguredRootPath(StorageCatalogRecord storage)
    {
        ArgumentNullException.ThrowIfNull(storage);
        return StorageCatalogHostBindingPolicy.ResolveRequired(
            storage,
            workspacePathResolver.ResolveWorkspaceRoot());
    }

    public string ResolveFullPath(StorageCatalogRecord storage, string relativePath)
    {
        ArgumentNullException.ThrowIfNull(relativePath);
        IPhysicalFileSystemPathPolicy rootPolicy = ResolveRootPolicy(storage);
        string normalizedPath = NormalizeRelativeKey(relativePath);
        return TranslateValidation(() => rootPolicy.ResolveContainedPath(normalizedPath));
    }

    public string ResolveTrustedWorkspacePath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        PhysicalPathSyntaxPolicy.EnsureNativeOrRelative(path, "trusted workspace path");
        IPhysicalFileSystemPathPolicy workspacePolicy = ResolveWorkspacePolicy();
        return TranslateValidation(() => workspacePolicy.ResolveContainedPath(path));
    }

    public string ResolveReparseSafeFullPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        PhysicalPathSyntaxPolicy.EnsureNativeOrRelative(path, "filesystem path");
        string fullPath = Path.GetFullPath(path);
        string? rootPath = Path.GetPathRoot(fullPath);
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            throw new StorageBrowseException(new StorageBrowseError(
                StorageBrowseErrorCode.AccessDenied,
                "The requested filesystem path does not have a trusted root."));
        }

        IPhysicalFileSystemPathPolicy rootPolicy = physicalPathPolicyFactory.Create(rootPath);
        return TranslateValidation(() => rootPolicy.ResolveContainedPath(fullPath));
    }

    public string ResolveTrustedLocalOpenPath(StorageCatalogRecord storage, string relativePath)
    {
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(relativePath);
        IPhysicalFileSystemPathPolicy workspacePolicy = ResolveWorkspacePolicy();
        IPhysicalFileSystemPathPolicy storagePolicy = ResolveRootPolicy(storage);
        TranslateValidation(() => workspacePolicy.EnsureSafePath(storagePolicy.RootPath, allowMissingLeaf: true));
        string normalizedPath = NormalizeRelativeKey(relativePath);
        string fullPath = TranslateValidation(() => storagePolicy.ResolveContainedPath(normalizedPath));
        TranslateValidation(() => workspacePolicy.EnsureSafePath(fullPath, allowMissingLeaf: true));
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
            IPhysicalFileSystemPathPolicy workspacePolicy = ResolveWorkspacePolicy();
            IPhysicalFileSystemPathPolicy storagePolicy = ResolveRootPolicy(storage);
            workspacePolicy.EnsureSafePath(storagePolicy.RootPath, allowMissingLeaf: true);
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
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return ".";
        }

        var trimmedPath = path.Trim();
        if (string.Equals(trimmedPath, ".", StringComparison.Ordinal))
        {
            return trimmedPath;
        }

        try
        {
            var physicalSegments = FileSystemStorageKeyCodec.Decode(trimmedPath)
                .Select(segment => segment.Physical)
                .ToArray();
            return physicalSegments.Length == 0
                ? "."
                : Path.Combine(physicalSegments);
        }
        catch (ArgumentException exception)
        {
            throw new StorageBrowseException(new StorageBrowseError(
                StorageBrowseErrorCode.AccessDenied,
                "The requested filesystem path is not a valid storage-relative path."), exception);
        }
    }

    private string ResolveWorkspaceRoot()
        => ResolveWorkspacePolicy().RootPath;

    private string ResolveConfiguredWorkspaceRoot()
    {
        var root = workspacePathResolver.ResolveWorkspaceRoot();
        PhysicalPathSyntaxPolicy.EnsureNativeOrRelative(root, "workspace root");
        return Path.GetFullPath(root);
    }

    internal IPhysicalFileSystemPathPolicy ResolveRootPolicy(StorageCatalogRecord storage)
        => TranslateValidation(() => physicalPathPolicyFactory.Create(ResolveConfiguredRootPath(storage)));

    internal void RevalidateMutationTarget(StorageCatalogRecord storage, string fullPath)
        => TranslateValidation(() => ResolveRootPolicy(storage).RevalidateMutationTarget(fullPath));

    private IPhysicalFileSystemPathPolicy ResolveWorkspacePolicy()
        => TranslateValidation(() => physicalPathPolicyFactory.Create(ResolveConfiguredWorkspaceRoot()));

    private static T TranslateValidation<T>(Func<T> action)
    {
        try
        {
            return action();
        }
        catch (PhysicalPathValidationException exception)
        {
            throw new StorageBrowseException(new StorageBrowseError(
                StorageBrowseErrorCode.AccessDenied,
                exception.Message), exception);
        }
    }

    private static void TranslateValidation(Action action)
    {
        try
        {
            action();
        }
        catch (PhysicalPathValidationException exception)
        {
            throw new StorageBrowseException(new StorageBrowseError(
                StorageBrowseErrorCode.AccessDenied,
                exception.Message), exception);
        }
    }
}
