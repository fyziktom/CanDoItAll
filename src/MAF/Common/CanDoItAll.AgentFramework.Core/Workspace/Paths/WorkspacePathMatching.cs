namespace CanDoItAll.AgentFramework.Core;

public static class WorkspacePathMatching {
    public static bool IsExternalTargetAliasPath(string? path) {
        return WorkspacePathPolicy.IsExternalTargetAliasPath(path);
    }

    public static bool HasParentTraversalSegment(string path) {
        return SplitSegments(path)
            .Any(segment => segment == "..");
    }

    public static string NormalizeForMatching(string? path) {
        if (string.IsNullOrWhiteSpace(path)) {
            return string.Empty;
        }

        var normalizedPath = NormalizeSeparatorsForMatching(path)
            .Trim()
            .Trim('`', '"', '\'');
        while (normalizedPath.Contains("//", StringComparison.Ordinal)) {
            normalizedPath = normalizedPath.Replace("//", "/", StringComparison.Ordinal);
        }

        var isRooted = Path.IsPathRooted(normalizedPath);
        if (!isRooted) {
            while (normalizedPath.StartsWith("./", StringComparison.Ordinal)) {
                normalizedPath = normalizedPath[2..];
            }

            return normalizedPath.Trim('/');
        }

        const string workspaceMarker = "/workspace/";
        var workspaceIndex = normalizedPath.IndexOf(
            workspaceMarker,
            StringComparison.OrdinalIgnoreCase);
        if (workspaceIndex >= 0) {
            normalizedPath = normalizedPath[(workspaceIndex + workspaceMarker.Length)..];
        } else if (normalizedPath.EndsWith("/workspace", StringComparison.OrdinalIgnoreCase)) {
            return "workspace";
        }

        return normalizedPath.Trim('/');
    }

    public static string[] SplitSegments(string path)
        => NormalizeSeparatorsForMatching(path).Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string NormalizeSeparatorsForMatching(string path) {
        return OperatingSystem.IsWindows()
            ? path.Replace('\\', '/')
            : path;
    }
}
