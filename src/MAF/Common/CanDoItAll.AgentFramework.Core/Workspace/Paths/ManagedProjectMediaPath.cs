namespace CanDoItAll.AgentFramework.Core;

public static class ManagedProjectMediaPath
{
    public const string FilesRoot = "managed-files/project-media/files";
    public const string ImagesRoot = "managed-files/project-media/images";
    public const string VideosRoot = "managed-files/project-media/videos";
    public const string RelativeRoot = "managed-files/project-media";

    public static IReadOnlyList<string> ResolveProjectSegments(string projectKey)
    {
        var segments = new HashSet<string>(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(projectKey))
        {
            segments.Add(projectKey.Trim());
        }

        if (Guid.TryParse(projectKey, out var projectId))
        {
            segments.Add(projectId.ToString("N"));
            segments.Add(projectId.ToString("D"));
        }

        return segments.Order(StringComparer.Ordinal).ToArray();
    }

    public static IReadOnlyList<string> ResolveTextAssetRelativeRoots(string projectKey)
    {
        return ResolveProjectSegments(projectKey)
            .Select(segment => $"{FilesRoot}/{segment}")
            .ToArray();
    }

    public static bool IsForProject(
        string path,
        string projectKey)
    {
        return TryResolveProjectSegment(path, out var projectSegment) &&
               ResolveProjectSegments(projectKey)
                   .Contains(projectSegment, StringComparer.Ordinal);
    }

    public static bool TryResolveProjectSegment(
        string path,
        out string projectSegment)
    {
        if (HasParentTraversalSegment(path))
        {
            projectSegment = string.Empty;
            return false;
        }

        var normalizedPath = NormalizeForMatching(path);
        var segments = normalizedPath.Split(
            '/',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length >= 4 &&
            string.Equals(segments[0], "managed-files", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(segments[1], "project-media", StringComparison.OrdinalIgnoreCase) &&
            (string.Equals(segments[2], "files", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(segments[2], "images", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(segments[2], "videos", StringComparison.OrdinalIgnoreCase)))
        {
            projectSegment = segments[3];
            return true;
        }

        projectSegment = string.Empty;
        return false;
    }

    public static bool IsProjectMediaPath(string path)
    {
        var normalizedPath = NormalizeForMatching(path);
        return string.Equals(normalizedPath, RelativeRoot, StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.StartsWith(RelativeRoot + "/", StringComparison.OrdinalIgnoreCase);
    }

    public static bool HasParentTraversalSegment(string path)
    {
        return NormalizeSeparatorsForMatching(path)
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(segment => segment == "..");
    }

    public static bool HasProjectMediaMarker(string path)
    {
        var segments = NormalizeSeparatorsForMatching(path)
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (var index = 0; index < segments.Length - 1; index++)
        {
            if (string.Equals(segments[index], "managed-files", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(segments[index + 1], "project-media", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static string NormalizeForMatching(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var normalizedPath = NormalizeSeparatorsForMatching(path)
            .Trim()
            .Trim('`', '"', '\'');
        while (normalizedPath.Contains("//", StringComparison.Ordinal))
        {
            normalizedPath = normalizedPath.Replace("//", "/", StringComparison.Ordinal);
        }

        var isRooted = Path.IsPathRooted(normalizedPath);
        if (!isRooted)
        {
            while (normalizedPath.StartsWith("./", StringComparison.Ordinal))
            {
                normalizedPath = normalizedPath[2..];
            }

            return normalizedPath.Trim('/');
        }

        const string workspaceMarker = "/workspace/";
        var workspaceIndex = normalizedPath.IndexOf(
            workspaceMarker,
            StringComparison.OrdinalIgnoreCase);
        if (workspaceIndex >= 0)
        {
            normalizedPath = normalizedPath[(workspaceIndex + workspaceMarker.Length)..];
        }
        else if (normalizedPath.EndsWith("/workspace", StringComparison.OrdinalIgnoreCase))
        {
            return "workspace";
        }

        return normalizedPath.Trim('/');
    }

    private static string NormalizeSeparatorsForMatching(string path)
    {
        return OperatingSystem.IsWindows()
            ? path.Replace('\\', '/')
            : path;
    }
}
