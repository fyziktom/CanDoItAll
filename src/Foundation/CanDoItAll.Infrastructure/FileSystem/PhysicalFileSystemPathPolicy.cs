using CanDoItAll.Infrastructure.FileSystem;

namespace CanDoItAll.Infrastructure;

public sealed class PhysicalFileSystemPathPolicyFactory : IPhysicalFileSystemPathPolicyFactory
{
    public IPhysicalFileSystemPathPolicy Create(string managedRoot)
        => new PhysicalFileSystemPathPolicy(managedRoot);
}

internal sealed class PhysicalFileSystemPathPolicy : IPhysicalFileSystemPathPolicy
{
    private readonly string rootWithSeparator;

    public PhysicalFileSystemPathPolicy(string managedRoot)
        : this(managedRoot, caseSensitivity: null)
    {
    }

    internal PhysicalFileSystemPathPolicy(
        string managedRoot,
        PhysicalFileSystemCaseSensitivity? caseSensitivity)
    {
        RootPath = NormalizeRoot(managedRoot);
        rootWithSeparator = EnsureTrailingSeparator(RootPath);
        EnsureNoLinkTraversalFromNativeRoot(RootPath, allowMissingLeaf: true);
        CaseSensitivity = caseSensitivity ?? DetectCaseSensitivity(RootPath);
        PathComparer = CaseSensitivity == PhysicalFileSystemCaseSensitivity.Insensitive
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
        PathComparison = CaseSensitivity == PhysicalFileSystemCaseSensitivity.Insensitive
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
    }

    public string RootPath { get; }

    public PhysicalFileSystemCaseSensitivity CaseSensitivity { get; }

    public StringComparer PathComparer { get; }

    public StringComparison PathComparison { get; }

    public bool IsWithinRoot(string fullPath)
    {
        if (string.IsNullOrWhiteSpace(fullPath))
        {
            return false;
        }

        try
        {
            PhysicalPathSyntaxPolicy.EnsureNativeOrRelative(fullPath, "physical path");
            string normalizedPath = Path.GetFullPath(fullPath);
            return PathComparer.Equals(normalizedPath, RootPath) ||
                   normalizedPath.StartsWith(rootWithSeparator, PathComparison);
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    public string ResolveContainedPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        try
        {
            PhysicalPathSyntaxPolicy.EnsureNativeOrRelative(path, "physical path");
            string fullPath = Path.GetFullPath(
                Path.IsPathRooted(path)
                    ? path
                    : Path.Combine(RootPath, path));
            EnsureContained(fullPath);
            EnsureSafePath(fullPath, allowMissingLeaf: true);
            return fullPath;
        }
        catch (PhysicalPathValidationException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or NotSupportedException or PathTooLongException)
        {
            throw new PhysicalPathValidationException(
                PhysicalPathValidationErrorCode.InvalidPath,
                "The requested physical path is invalid for this host.",
                exception);
        }
    }

    public void EnsureSafePath(string fullPath, bool allowMissingLeaf = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);
        string normalizedPath;
        try
        {
            PhysicalPathSyntaxPolicy.EnsureNativeOrRelative(fullPath, "physical path");
            normalizedPath = Path.GetFullPath(fullPath);
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or NotSupportedException or PathTooLongException)
        {
            throw new PhysicalPathValidationException(
                PhysicalPathValidationErrorCode.InvalidPath,
                "The requested physical path is invalid for this host.",
                exception);
        }

        EnsureContained(normalizedPath);
        EnsureNoLinkTraversalFromNativeRoot(RootPath, allowMissingLeaf: true);
        EnsureNoLinkTraversal(RootPath, normalizedPath, allowMissingLeaf);
    }

    public void RevalidateMutationTarget(string fullPath)
    {
        EnsureSafePath(fullPath, allowMissingLeaf: true);
        string normalizedPath = Path.GetFullPath(fullPath);
        string? parentPath = Path.GetDirectoryName(normalizedPath);
        if (string.IsNullOrWhiteSpace(parentPath) || !Directory.Exists(parentPath))
        {
            throw new PhysicalPathValidationException(
                PhysicalPathValidationErrorCode.InvalidPath,
                "The physical mutation target does not have an existing directory parent.");
        }

        EnsureSafePath(parentPath);
    }

    private void EnsureContained(string fullPath)
    {
        if (IsWithinRoot(fullPath))
        {
            return;
        }

        throw new PhysicalPathValidationException(
            PhysicalPathValidationErrorCode.OutsideRoot,
            "The requested physical path is outside the configured managed root.");
    }

    private static string NormalizeRoot(string managedRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(managedRoot);

        try
        {
            PhysicalPathSyntaxPolicy.EnsureNativeOrRelative(managedRoot, "managed filesystem root");
            if (!Path.IsPathRooted(managedRoot))
            {
                throw new PhysicalPathValidationException(
                    PhysicalPathValidationErrorCode.InvalidRoot,
                    "A managed filesystem root must be an absolute native path.");
            }

            string fullPath = Path.GetFullPath(managedRoot);
            string? nativeRoot = Path.GetPathRoot(fullPath);
            if (string.IsNullOrWhiteSpace(nativeRoot))
            {
                throw new PhysicalPathValidationException(
                    PhysicalPathValidationErrorCode.InvalidRoot,
                    "The managed filesystem root does not have a native root.");
            }

            return fullPath.Length == nativeRoot.Length
                ? nativeRoot
                : fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch (PhysicalPathValidationException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or NotSupportedException or PathTooLongException)
        {
            throw new PhysicalPathValidationException(
                PhysicalPathValidationErrorCode.InvalidRoot,
                "The managed filesystem root is invalid for this host.",
                exception);
        }
    }

    private static PhysicalFileSystemCaseSensitivity DetectCaseSensitivity(string rootPath)
    {
        if (!Directory.Exists(rootPath))
        {
            return PhysicalFileSystemCaseSensitivity.Unknown;
        }

        string probeName = $".candoitall-case-probe-{Guid.NewGuid():N}";
        string probePath = Path.Combine(rootPath, probeName);
        string alternateCasePath = Path.Combine(rootPath, probeName.ToUpperInvariant());
        bool probeCreated = false;

        try
        {
            using (var stream = new FileStream(
                       probePath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.Read | FileShare.Delete,
                       bufferSize: 1,
                       FileOptions.WriteThrough))
            {
                probeCreated = true;
                stream.Flush(flushToDisk: true);
                return File.Exists(alternateCasePath)
                    ? PhysicalFileSystemCaseSensitivity.Insensitive
                    : PhysicalFileSystemCaseSensitivity.Sensitive;
            }
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return PhysicalFileSystemCaseSensitivity.Unknown;
        }
        finally
        {
            if (probeCreated)
            {
                File.Delete(probePath);
            }
        }
    }

    private static void EnsureNoLinkTraversalFromNativeRoot(string fullPath, bool allowMissingLeaf)
    {
        string? nativeRoot = Path.GetPathRoot(fullPath);
        if (string.IsNullOrWhiteSpace(nativeRoot))
        {
            throw new PhysicalPathValidationException(
                PhysicalPathValidationErrorCode.InvalidRoot,
                "The physical path does not have a native root.");
        }

        EnsureNoLinkTraversal(nativeRoot, fullPath, allowMissingLeaf);
    }

    private static void EnsureNoLinkTraversal(string rootPath, string fullPath, bool allowMissingLeaf)
    {
        EnsureNotLink(rootPath, allowMissing: allowMissingLeaf);
        string relativePath = Path.GetRelativePath(rootPath, fullPath);
        if (string.Equals(relativePath, ".", StringComparison.Ordinal))
        {
            return;
        }

        string currentPath = rootPath;
        string[] segments = relativePath.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);
        for (var index = 0; index < segments.Length; index++)
        {
            currentPath = Path.Combine(currentPath, segments[index]);
            bool isLeaf = index == segments.Length - 1;
            if (!TryGetAttributes(currentPath, out FileAttributes attributes))
            {
                if (allowMissingLeaf)
                {
                    return;
                }

                throw new PhysicalPathValidationException(
                    PhysicalPathValidationErrorCode.InvalidPath,
                    isLeaf
                        ? "The requested physical path does not exist."
                        : "An ancestor of the requested physical path does not exist.");
            }

            if (attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                throw new PhysicalPathValidationException(
                    PhysicalPathValidationErrorCode.LinkTraversal,
                    "Filesystem symbolic-link or reparse-point traversal is not allowed for managed paths.");
            }
        }
    }

    private static void EnsureNotLink(string path, bool allowMissing)
    {
        if (!TryGetAttributes(path, out FileAttributes attributes))
        {
            if (allowMissing)
            {
                return;
            }

            throw new PhysicalPathValidationException(
                PhysicalPathValidationErrorCode.InvalidRoot,
                "The native filesystem root is unavailable.");
        }

        if (attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            throw new PhysicalPathValidationException(
                PhysicalPathValidationErrorCode.LinkTraversal,
                "A managed filesystem root cannot traverse a symbolic link or reparse point.");
        }
    }

    private static bool TryGetAttributes(string path, out FileAttributes attributes)
    {
        try
        {
            attributes = File.GetAttributes(path);
            return true;
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            attributes = default;
            return false;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            throw new PhysicalPathValidationException(
                PhysicalPathValidationErrorCode.InvalidPath,
                "The physical path attributes could not be validated safely.",
                exception);
        }
    }

    private static string EnsureTrailingSeparator(string path)
        => path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;
}
