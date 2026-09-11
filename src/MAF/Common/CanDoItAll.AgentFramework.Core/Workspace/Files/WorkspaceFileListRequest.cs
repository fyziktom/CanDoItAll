namespace CanDoItAll.AgentFramework.Core;

public readonly record struct WorkspaceFileListRequest(string? RelativePath, string SearchPattern) {
    public static WorkspaceFileListRequest Normalize(
        string? relativePath,
        string? searchPattern = null) {
        var normalizedSearchPattern = string.IsNullOrWhiteSpace(searchPattern) ? "*" : searchPattern.Trim();
        if (!string.Equals(normalizedSearchPattern, "*", StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(relativePath)) {
            return new WorkspaceFileListRequest(relativePath, normalizedSearchPattern);
        }

        var normalizedPath = relativePath.Replace('\\', '/').Trim();
        var globstarIndex = normalizedPath.IndexOf("**", StringComparison.Ordinal);
        if (globstarIndex < 0) {
            return new WorkspaceFileListRequest(relativePath, normalizedSearchPattern);
        }

        var normalizedRelativePath = normalizedPath[..globstarIndex].TrimEnd('/');
        var embeddedSearchPattern = normalizedPath[globstarIndex..].TrimStart('/');
        if (embeddedSearchPattern is "" or "**") {
            embeddedSearchPattern = "**/*";
        }

        return new WorkspaceFileListRequest(
            string.IsNullOrWhiteSpace(normalizedRelativePath) ? null : normalizedRelativePath,
            embeddedSearchPattern);
    }
}
