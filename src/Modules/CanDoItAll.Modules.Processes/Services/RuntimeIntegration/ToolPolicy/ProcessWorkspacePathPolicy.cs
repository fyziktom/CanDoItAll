namespace CanDoItAll.Modules.Processes;

internal static class ProcessWorkspacePathPolicy {
    private static readonly string[] AllowedExternalRunManagedRoots =
    [
        ".playwright-mcp",
        "artifacts",
        "data",
        "integration-map",
        "output",
        "process-artifacts",
        "process-runs"
    ];

    internal static bool IsExternalArtifactDestinationPath(string normalizedAlias) {
        var segments = normalizedAlias
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Skip(2)
            .ToArray();
        if (segments.Length == 0) {
            return false;
        }

        if (segments.Any(segment =>
                string.Equals(segment, "product", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(segment, "source", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(segment, "src", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(segment, "app", StringComparison.OrdinalIgnoreCase))) {
            return false;
        }

        return segments.Any(segment =>
            string.Equals(segment, "artifact", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, "artifacts", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, "evidence", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, "report", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, "reports", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, "decision", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, "decisions", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, "output", StringComparison.OrdinalIgnoreCase));
    }

    internal static bool IsAllowedExternalRunManagedPath(string normalizedPath) {
        return AllowedExternalRunManagedRoots.Any(root =>
            string.Equals(normalizedPath, root, StringComparison.OrdinalIgnoreCase) ||
            normalizedPath.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase));
    }
}
