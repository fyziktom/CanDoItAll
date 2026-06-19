using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Maf;

internal static class BrowserMcpArtifactPathService
{
    private const string ArtifactsRoot = "artifacts";
    private const string ScopedArtifactsRoot = "artifacts/scopes";
    private const string PlaywrightMcpRoot = ".playwright-mcp";
    private const string BrowserArtifactFolder = "browser";

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

    public static BrowserMcpArtifactImportResult TryImportAfterInvocation(
        string workspaceRoot,
        WorkspaceScopeDescriptor workspaceScope,
        string? fileName,
        string? processRunId)
    {
        var importedPaths = new List<string>();
        if (TryMirrorToScopedArtifactPath(workspaceRoot, workspaceScope, fileName, out var scopedRelativePath) &&
            !string.IsNullOrWhiteSpace(scopedRelativePath))
        {
            importedPaths.Add(scopedRelativePath);
        }

        if (!TryNormalizePlaywrightRelativePath(fileName, out var normalizedFileName) ||
            !TryNormalizeProcessRunId(processRunId, out var normalizedProcessRunId) ||
            !TryResolveWorkspacePath(workspaceRoot, normalizedFileName, out var providerNativeFullPath) ||
            !File.Exists(providerNativeFullPath))
        {
            return new BrowserMcpArtifactImportResult(importedPaths);
        }

        var browserArtifactRelativePath = BuildProcessRunBrowserArtifactPath(normalizedProcessRunId, normalizedFileName);
        if (TryCopyWorkspaceFile(workspaceRoot, providerNativeFullPath, browserArtifactRelativePath))
        {
            importedPaths.Add(browserArtifactRelativePath);
        }

        if (!workspaceScope.IsDefaultSandbox)
        {
            var scopedBrowserArtifactRelativePath = WorkspaceScopeDescriptor.NormalizeRelativePath(Path.Combine(
                workspaceScope.ArtifactRootRelativePath,
                RemoveRoot(browserArtifactRelativePath, ArtifactsRoot)));
            if (TryCopyWorkspaceFile(workspaceRoot, providerNativeFullPath, scopedBrowserArtifactRelativePath))
            {
                importedPaths.Add(scopedBrowserArtifactRelativePath);
            }
        }

        return new BrowserMcpArtifactImportResult(importedPaths
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray());
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
            return TryNormalizePlaywrightRelativePath(fileName, out var normalizedPlaywrightPath)
                ? [normalizedPlaywrightPath]
                : [];
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

    private static bool TryNormalizeProcessRunId(
        string? processRunId,
        out string normalizedProcessRunId)
    {
        normalizedProcessRunId = string.Empty;
        if (string.IsNullOrWhiteSpace(processRunId))
        {
            return false;
        }

        normalizedProcessRunId = WorkspaceScopeDescriptor.NormalizeRelativePath(processRunId);
        if (string.IsNullOrWhiteSpace(normalizedProcessRunId) ||
            normalizedProcessRunId.Contains('/', StringComparison.Ordinal) ||
            string.Equals(normalizedProcessRunId, "..", StringComparison.Ordinal))
        {
            normalizedProcessRunId = string.Empty;
            return false;
        }

        return true;
    }

    private static bool TryNormalizePlaywrightRelativePath(
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
            !MatchesRoot(normalizedFileName, PlaywrightMcpRoot))
        {
            normalizedFileName = string.Empty;
            return false;
        }

        var segments = normalizedFileName.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Any(segment => string.Equals(segment, "..", StringComparison.Ordinal)) ||
            segments.Length < 2)
        {
            normalizedFileName = string.Empty;
            return false;
        }

        return true;
    }

    private static string BuildProcessRunBrowserArtifactPath(
        string processRunId,
        string normalizedProviderNativePath)
    {
        var suffix = RemoveRoot(normalizedProviderNativePath, PlaywrightMcpRoot);
        return WorkspaceScopeDescriptor.NormalizeRelativePath(Path.Combine(
            ArtifactsRoot,
            "process-runs",
            processRunId,
            BrowserArtifactFolder,
            suffix));
    }

    private static bool TryCopyWorkspaceFile(
        string workspaceRoot,
        string sourceFullPath,
        string destinationRelativePath)
    {
        if (!TryResolveWorkspacePath(workspaceRoot, destinationRelativePath, out var destinationFullPath))
        {
            return false;
        }

        var destinationDirectory = Path.GetDirectoryName(destinationFullPath);
        if (string.IsNullOrWhiteSpace(destinationDirectory))
        {
            return false;
        }

        Directory.CreateDirectory(destinationDirectory);
        File.Copy(sourceFullPath, destinationFullPath, overwrite: true);
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

internal sealed record BrowserMcpArtifactImportResult(IReadOnlyList<string> ImportedRelativePaths)
{
    public bool Imported => ImportedRelativePaths.Count > 0;
}
