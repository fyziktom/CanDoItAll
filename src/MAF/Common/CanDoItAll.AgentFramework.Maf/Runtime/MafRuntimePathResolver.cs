namespace CanDoItAll.AgentFramework.Maf;

internal static class MafRuntimePathResolver
{
    public static string ResolvePathFromWorkspace(
        string workspaceRoot,
        string path,
        bool allowExternal,
        IReadOnlyList<string>? allowedExternalRoots = null)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return workspaceRoot;
        }

        var expandedPath = ExpandPortablePath(path);
        var fullPath = Path.GetFullPath(Path.IsPathRooted(expandedPath)
            ? expandedPath
            : Path.Combine(workspaceRoot, expandedPath));
        if (IsPathWithinRoot(fullPath, workspaceRoot))
        {
            return fullPath;
        }

        if (!allowExternal)
        {
            throw new InvalidOperationException($"Path '{path}' resolves outside the workspace root. Use a workspace-relative path or import the external file into chat attachments first.");
        }

        var allowedRoots = allowedExternalRoots?
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(ExpandPortablePath)
            .Select(item => Path.GetFullPath(Path.IsPathRooted(item) ? item : Path.Combine(workspaceRoot, item)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList()
            ?? [];

        if (allowedRoots.Any(allowedRoot => IsPathWithinRoot(fullPath, allowedRoot)))
        {
            return fullPath;
        }

        throw new InvalidOperationException($"Path '{path}' resolves outside the workspace root and is not covered by an explicit external-root allowlist.");
    }

    public static string ExpandPortablePath(string path)
    {
        var expanded = Environment.ExpandEnvironmentVariables(path.Trim());
        if (string.Equals(expanded, "~", StringComparison.Ordinal))
        {
            var homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return string.IsNullOrWhiteSpace(homeDirectory)
                ? expanded
                : homeDirectory;
        }

        if (!expanded.StartsWith("~" + Path.DirectorySeparatorChar, StringComparison.Ordinal) &&
            !expanded.StartsWith("~" + Path.AltDirectorySeparatorChar, StringComparison.Ordinal))
        {
            return expanded;
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(home))
        {
            return expanded;
        }

        var relativePath = expanded[2..]
            .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        return Path.Combine(home, relativePath);
    }

    public static bool IsPathWithinRoot(string fullPath, string rootPath)
    {
        var normalizedRoot = Path.GetFullPath(rootPath);
        if (string.Equals(fullPath, normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var rootWithSeparator = normalizedRoot.EndsWith(Path.DirectorySeparatorChar) || normalizedRoot.EndsWith(Path.AltDirectorySeparatorChar)
            ? normalizedRoot
            : normalizedRoot + Path.DirectorySeparatorChar;
        return fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase);
    }
}
