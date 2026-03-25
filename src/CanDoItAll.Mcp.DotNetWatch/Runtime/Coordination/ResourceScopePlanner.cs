using CanDoItAll.Mcp.DotNetWatch.Configuration;

namespace CanDoItAll.Mcp.DotNetWatch.Runtime.Coordination;

public sealed record ResourceScopePlan(IReadOnlyList<string> ResourceKeys)
{
    public static ResourceScopePlan Empty { get; } = new([]);
}

public sealed class ResourceScopePlanner(RuntimeConfiguration configuration)
{
    public ResourceScopePlan ForBridgeRepair()
        => Create("bridge", "backend-registration");

    public ResourceScopePlan ForAppStart(string logicalAppId, string projectPath)
        => Create($"logical-app:{logicalAppId}", $"source-tree:{NormalizeSourceTree(projectPath)}");

    public ResourceScopePlan ForAppStop(string logicalAppId)
        => Create($"logical-app:{logicalAppId}");

    public ResourceScopePlan ForOperation(string targetPath, IEnumerable<string> logicalAppIds)
        => Create(
            new[] { $"source-tree:{NormalizeSourceTree(targetPath)}" }
            .Concat(GetOperationWorkspaceSegments(targetPath).Select(static segment => $"workspace-segment:{segment}"))
            .Concat(logicalAppIds.Where(static id => !string.IsNullOrWhiteSpace(id)).Select(id => $"logical-app:{id}"))
            .ToArray());

    public ResourceScopePlan ForAtomicPrepare(string logicalAppId, string projectPath, string slotId)
        => Create($"logical-app:{logicalAppId}", $"source-tree:{NormalizeSourceTree(projectPath)}", $"slot:{logicalAppId}:{slotId}");

    public ResourceScopePlan ForAtomicCommit(string logicalAppId, string slotId)
        => Create($"logical-app:{logicalAppId}", $"slot:{logicalAppId}:{slotId}");

    public ResourceScopePlan ForRollback(string logicalAppId)
        => Create($"logical-app:{logicalAppId}");

    private ResourceScopePlan Create(params string[] resourceKeys)
    {
        return new ResourceScopePlan(
            resourceKeys
                .Where(static key => !string.IsNullOrWhiteSpace(key))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(static key => key, StringComparer.OrdinalIgnoreCase)
                .ToArray());
    }

    private IReadOnlyList<string> GetOperationWorkspaceSegments(string targetPath)
    {
        if (string.Equals(targetPath, configuration.SolutionPath, StringComparison.OrdinalIgnoreCase))
        {
            return configuration.AllowedProjectRoots
                .Select(TryResolveWorkspaceSegment)
                .Where(static segment => !string.IsNullOrWhiteSpace(segment))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(static segment => segment, StringComparer.OrdinalIgnoreCase)
                .ToArray()!;
        }

        var segment = TryResolveWorkspaceSegment(targetPath);
        return string.IsNullOrWhiteSpace(segment) ? [] : [segment];
    }

    private string NormalizeSourceTree(string path)
    {
        if (string.Equals(path, configuration.SolutionPath, StringComparison.OrdinalIgnoreCase))
        {
            return "solution";
        }

        var directory = File.Exists(path) ? Path.GetDirectoryName(path) : path;
        directory ??= configuration.WorkspaceRoot;
        return directory.Trim().ToLowerInvariant();
    }

    private string? TryResolveWorkspaceSegment(string path)
    {
        var normalizedPath = File.Exists(path) ? Path.GetDirectoryName(path) ?? path : path;
        var relative = Path.GetRelativePath(configuration.WorkspaceRoot, normalizedPath);
        if (string.IsNullOrWhiteSpace(relative) ||
            relative.StartsWith("..", StringComparison.Ordinal) ||
            Path.IsPathRooted(relative))
        {
            return null;
        }

        var segments = relative.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
        return segments.Length == 0 ? null : segments[0].Trim().ToLowerInvariant();
    }
}
