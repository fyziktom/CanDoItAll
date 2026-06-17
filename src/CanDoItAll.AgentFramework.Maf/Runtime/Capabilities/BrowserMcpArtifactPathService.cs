using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Maf;

internal static class BrowserMcpArtifactPathService
{
    private const string ArtifactsRoot = "artifacts";
    private const string ScopedArtifactsRoot = "artifacts/scopes";

    public static void EnsureWritableArtifactDirectories(
        string workspaceRoot,
        WorkspaceScopeDescriptor workspaceScope,
        string? fileName)
    {
        foreach (var relativePath in ResolveWritableArtifactRelativePaths(workspaceScope, fileName))
        {
            if (!TryResolveWorkspacePath(workspaceRoot, relativePath, out var fullPath))
            {
                continue;
            }

            var directory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                continue;
            }

            Directory.CreateDirectory(directory);
        }
    }

    public static bool TryMirrorToScopedArtifactPath(
        string workspaceRoot,
        WorkspaceScopeDescriptor workspaceScope,
        string? fileName,
        out string? scopedRelativePath)
    {
        scopedRelativePath = null;
        if (workspaceScope.IsDefaultSandbox ||
            !TryNormalizeArtifactRelativePath(fileName, out var normalizedFileName) ||
            IsScopedArtifactPath(normalizedFileName) ||
            MatchesRoot(normalizedFileName, workspaceScope.ArtifactRootRelativePath))
        {
            return false;
        }

        if (!TryResolveWorkspacePath(workspaceRoot, normalizedFileName, out var unscopedFullPath) ||
            !File.Exists(unscopedFullPath))
        {
            return false;
        }

        var suffix = RemoveRoot(normalizedFileName, ArtifactsRoot);
        scopedRelativePath = string.IsNullOrWhiteSpace(suffix)
            ? workspaceScope.ArtifactRootRelativePath
            : WorkspaceScopeDescriptor.NormalizeRelativePath(Path.Combine(workspaceScope.ArtifactRootRelativePath, suffix));

        if (!TryResolveWorkspacePath(workspaceRoot, scopedRelativePath, out var scopedFullPath))
        {
            scopedRelativePath = null;
            return false;
        }

        var scopedDirectory = Path.GetDirectoryName(scopedFullPath);
        if (string.IsNullOrWhiteSpace(scopedDirectory))
        {
            scopedRelativePath = null;
            return false;
        }

        Directory.CreateDirectory(scopedDirectory);
        File.Copy(unscopedFullPath, scopedFullPath, overwrite: true);
        return true;
    }

    private static IReadOnlyList<string> ResolveWritableArtifactRelativePaths(
        WorkspaceScopeDescriptor workspaceScope,
        string? fileName)
    {
        if (!TryNormalizeArtifactRelativePath(fileName, out var normalizedFileName))
        {
            return [];
        }

        if (workspaceScope.IsDefaultSandbox ||
            IsScopedArtifactPath(normalizedFileName) ||
            MatchesRoot(normalizedFileName, workspaceScope.ArtifactRootRelativePath))
        {
            return [normalizedFileName];
        }

        var suffix = RemoveRoot(normalizedFileName, ArtifactsRoot);
        var scopedRelativePath = string.IsNullOrWhiteSpace(suffix)
            ? workspaceScope.ArtifactRootRelativePath
            : WorkspaceScopeDescriptor.NormalizeRelativePath(Path.Combine(workspaceScope.ArtifactRootRelativePath, suffix));

        return [normalizedFileName, scopedRelativePath];
    }

    private static bool TryNormalizeArtifactRelativePath(
        string? fileName,
        out string normalizedFileName)
    {
        normalizedFileName = string.Empty;
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        normalizedFileName = WorkspaceScopeDescriptor.NormalizeRelativePath(fileName);
        if (string.IsNullOrWhiteSpace(normalizedFileName) ||
            Path.IsPathRooted(normalizedFileName) ||
            !MatchesRoot(normalizedFileName, ArtifactsRoot))
        {
            normalizedFileName = string.Empty;
            return false;
        }

        var segments = normalizedFileName.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Any(segment => string.Equals(segment, "..", StringComparison.Ordinal)))
        {
            normalizedFileName = string.Empty;
            return false;
        }

        return true;
    }

    private static bool TryResolveWorkspacePath(
        string workspaceRoot,
        string relativePath,
        out string fullPath)
    {
        fullPath = string.Empty;
        if (string.IsNullOrWhiteSpace(workspaceRoot) ||
            string.IsNullOrWhiteSpace(relativePath))
        {
            return false;
        }

        var rootFullPath = Path.GetFullPath(workspaceRoot);
        var candidateFullPath = Path.GetFullPath(Path.Combine(
            rootFullPath,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!IsWithinRoot(candidateFullPath, rootFullPath))
        {
            return false;
        }

        fullPath = candidateFullPath;
        return true;
    }

    private static bool IsScopedArtifactPath(string relativePath)
        => MatchesRoot(relativePath, ScopedArtifactsRoot);

    private static bool MatchesRoot(string relativePath, string rootRelativePath)
    {
        return string.Equals(relativePath, rootRelativePath, StringComparison.OrdinalIgnoreCase) ||
               relativePath.StartsWith(rootRelativePath + "/", StringComparison.OrdinalIgnoreCase);
    }

    private static string RemoveRoot(string relativePath, string rootRelativePath)
    {
        return string.Equals(relativePath, rootRelativePath, StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : relativePath[(rootRelativePath.Length + 1)..];
    }

    private static bool IsWithinRoot(string fullPath, string rootFullPath)
    {
        var normalizedRoot = rootFullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return string.Equals(fullPath, normalizedRoot, StringComparison.OrdinalIgnoreCase) ||
               fullPath.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
               fullPath.StartsWith(normalizedRoot + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }
}
