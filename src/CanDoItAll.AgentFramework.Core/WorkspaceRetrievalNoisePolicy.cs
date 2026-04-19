namespace CanDoItAll.AgentFramework.Core;

public static class WorkspaceRetrievalNoisePolicy
{
    private static readonly string[] SeedWorkspaceRagExcludedPaths =
    [
        "data",
        "artifacts",
        "output",
        "process-runs",
        ".playwright-mcp",
        ".playwright-cli",
        ".vs",
        "data/workspace.json",
        "integration-map",
        "integration-readiness-bundle",
        "post-implementation-bundle-v4",
        "remediation-bundle-v3"
    ];

    public static IReadOnlyList<string> BuildSeedWorkspaceRagExcludedPaths(IEnumerable<string>? additionalPaths = null)
    {
        var excludedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddPaths(excludedPaths, SeedWorkspaceRagExcludedPaths);
        AddPaths(excludedPaths, additionalPaths);
        return excludedPaths
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static bool ShouldExcludeFromAmbientRetrieval(
        string workspaceRoot,
        string filePath,
        IEnumerable<string>? configuredExcludedPaths = null)
    {
        var relativePath = GetWorkspaceRelativePath(workspaceRoot, filePath);
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return false;
        }

        return MatchesExcludedPath(relativePath, configuredExcludedPaths)
               || MatchesExcludedPath(relativePath, SeedWorkspaceRagExcludedPaths)
               || IsBundleLikeRootPath(relativePath);
    }

    public static string NormalizeRelativePath(string path)
    {
        return path
            .Replace('\\', '/')
            .Trim()
            .TrimStart('/');
    }

    private static void AddPaths(HashSet<string> destination, IEnumerable<string>? paths)
    {
        if (paths is null)
        {
            return;
        }

        foreach (var path in paths)
        {
            var normalized = NormalizeRelativePath(path);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                destination.Add(normalized);
            }
        }
    }

    private static string GetWorkspaceRelativePath(string workspaceRoot, string filePath)
    {
        var comparisonRoot = File.Exists(workspaceRoot)
            ? Path.GetDirectoryName(workspaceRoot) ?? Path.GetPathRoot(workspaceRoot) ?? workspaceRoot
            : workspaceRoot;

        return NormalizeRelativePath(Path.GetRelativePath(comparisonRoot, filePath));
    }

    private static bool MatchesExcludedPath(string relativePath, IEnumerable<string>? excludedPaths)
    {
        if (excludedPaths is null)
        {
            return false;
        }

        foreach (var excludedPath in excludedPaths)
        {
            var normalized = NormalizeRelativePath(excludedPath);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                continue;
            }

            if (!normalized.Contains('/', StringComparison.Ordinal))
            {
                var segments = relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (segments.Any(segment => string.Equals(segment, normalized, StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }
            }

            if (string.Equals(relativePath, normalized, StringComparison.OrdinalIgnoreCase)
                || relativePath.StartsWith(normalized + "/", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsBundleLikeRootPath(string relativePath)
    {
        var slashIndex = relativePath.IndexOf('/');
        var firstSegment = slashIndex >= 0
            ? relativePath[..slashIndex]
            : relativePath;

        return firstSegment.Contains("bundle", StringComparison.OrdinalIgnoreCase);
    }
}
