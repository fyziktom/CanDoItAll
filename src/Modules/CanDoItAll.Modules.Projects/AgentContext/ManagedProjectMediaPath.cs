using CanDoItAll.AgentFramework.Core;

namespace CanDoItAll.Modules.Projects;

public static class ManagedProjectMediaPath {
    public const string FilesRoot = "managed-files/project-media/files";
    public const string ImagesRoot = "managed-files/project-media/images";
    public const string VideosRoot = "managed-files/project-media/videos";
    public const string RelativeRoot = "managed-files/project-media";

    public static IReadOnlyList<string> ResolveProjectSegments(string projectKey) {
        var segments = new HashSet<string>(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(projectKey)) {
            segments.Add(projectKey.Trim());
        }

        if (Guid.TryParse(projectKey, out var projectId)) {
            segments.Add(projectId.ToString("N"));
            segments.Add(projectId.ToString("D"));
        }

        return segments.Order(StringComparer.Ordinal).ToArray();
    }

    public static IReadOnlyList<string> ResolveTextAssetRelativeRoots(string projectKey) {
        return ResolveProjectSegments(projectKey)
            .Select(segment => $"{FilesRoot}/{segment}")
            .ToArray();
    }

    public static bool IsForProject(
        string path,
        string projectKey) {
        return TryResolveProjectSegment(path, out var projectSegment) &&
               ResolveProjectSegments(projectKey)
                   .Contains(projectSegment, StringComparer.Ordinal);
    }

    public static bool TryResolveProjectSegment(
        string path,
        out string projectSegment) {
        if (WorkspacePathMatching.HasParentTraversalSegment(path)) {
            projectSegment = string.Empty;
            return false;
        }

        var normalizedPath = WorkspacePathMatching.NormalizeForMatching(path);
        var segments = normalizedPath.Split(
            '/',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length >= 4 &&
            string.Equals(segments[0], "managed-files", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(segments[1], "project-media", StringComparison.OrdinalIgnoreCase) &&
            (string.Equals(segments[2], "files", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(segments[2], "images", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(segments[2], "videos", StringComparison.OrdinalIgnoreCase))) {
            projectSegment = segments[3];
            return true;
        }

        projectSegment = string.Empty;
        return false;
    }

    public static bool IsProjectMediaPath(string path) {
        var normalizedPath = WorkspacePathMatching.NormalizeForMatching(path);
        return string.Equals(normalizedPath, RelativeRoot, StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.StartsWith(RelativeRoot + "/", StringComparison.OrdinalIgnoreCase);
    }

    public static bool HasProjectMediaMarker(string path) {
        var segments = WorkspacePathMatching.SplitSegments(path);
        for (var index = 0; index < segments.Length - 1; index++) {
            if (string.Equals(segments[index], "managed-files", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(segments[index + 1], "project-media", StringComparison.OrdinalIgnoreCase)) {
                return true;
            }
        }

        return false;
    }

}
