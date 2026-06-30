namespace CanDoItAll.Modules.Workbench;

internal static class ProjectStructureProcessRunFolderProjectionPolicy
{
    private const string ArtifactsRootSegment = "artifacts";
    private const string OutputRootSegment = "output";
    private const string ProcessRunsSegment = "process-runs";

    public static ProjectStructureProcessRunFolderProjection Resolve(string? managedStoragePath, Guid runId)
    {
        if (runId == Guid.Empty ||
            string.IsNullOrWhiteSpace(managedStoragePath) ||
            Path.IsPathRooted(managedStoragePath.Trim()))
        {
            return ProjectStructureProcessRunFolderProjection.Ignored("The artifact path is empty, unbound, or absolute.");
        }

        var segments = NormalizeSegments(managedStoragePath);
        if (segments.Count == 0)
        {
            return ProjectStructureProcessRunFolderProjection.Ignored("The artifact path has no projectable relative segments.");
        }

        var processRunsIndex = IndexOfSegment(segments, ProcessRunsSegment);
        if (processRunsIndex >= 0)
        {
            return ResolveProcessRunsProjection(segments, runId, processRunsIndex);
        }

        if (TryResolveRunIdSegmentIndex(segments, runId, 0, out var genericRunIdSegmentIndex))
        {
            return ProjectStructureProcessRunFolderProjection.Project(
                string.Join('/', segments.Take(genericRunIdSegmentIndex + 1)),
                ProjectStructureProcessRunFolderProjectionKind.ManagedRunRoot);
        }

        return ProjectStructureProcessRunFolderProjection.Ignored("The artifact path is not anchored to the current process run.");
    }

    private static ProjectStructureProcessRunFolderProjection ResolveProcessRunsProjection(
        IReadOnlyList<string> segments,
        Guid runId,
        int processRunsIndex)
    {
        if (!TryResolveRunIdSegmentIndex(segments, runId, processRunsIndex + 1, out var runIdSegmentIndex))
        {
            return ProjectStructureProcessRunFolderProjection.Ignored("The process-runs path does not contain the current run id.");
        }

        if (IsRootSegment(segments[0], OutputRootSegment))
        {
            var outputRootSegmentCount = ResolveOutputRootSegmentCount(segments, runIdSegmentIndex);
            var projectionKind = outputRootSegmentCount > runIdSegmentIndex + 1
                ? ProjectStructureProcessRunFolderProjectionKind.ManagedProductOutputRoot
                : ProjectStructureProcessRunFolderProjectionKind.ManagedRunRoot;
            return ProjectStructureProcessRunFolderProjection.Project(
                string.Join('/', segments.Take(outputRootSegmentCount)),
                projectionKind);
        }

        var kind = IsRootSegment(segments[0], ArtifactsRootSegment)
            ? ProjectStructureProcessRunFolderProjectionKind.ManagedArtifactRunRoot
            : ProjectStructureProcessRunFolderProjectionKind.ManagedRunRoot;
        return ProjectStructureProcessRunFolderProjection.Project(
            string.Join('/', segments.Take(runIdSegmentIndex + 1)),
            kind);
    }

    private static int ResolveOutputRootSegmentCount(IReadOnlyList<string> segments, int runIdSegmentIndex)
    {
        var firstOutputChildIndex = runIdSegmentIndex + 1;
        if (firstOutputChildIndex >= segments.Count)
        {
            return runIdSegmentIndex + 1;
        }

        if (firstOutputChildIndex == segments.Count - 1 &&
            Path.GetExtension(segments[firstOutputChildIndex]).Length > 0)
        {
            return runIdSegmentIndex + 1;
        }

        return firstOutputChildIndex + 1;
    }

    private static IReadOnlyList<string> NormalizeSegments(string managedStoragePath)
    {
        var normalizedPath = managedStoragePath
            .Trim()
            .Replace('\\', '/')
            .Trim('/');
        if (string.IsNullOrWhiteSpace(normalizedPath))
        {
            return [];
        }

        var segments = new List<string>();
        foreach (var segment in normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (string.Equals(segment, ".", StringComparison.Ordinal))
            {
                continue;
            }

            if (string.Equals(segment, "..", StringComparison.Ordinal))
            {
                return [];
            }

            segments.Add(segment);
        }

        return segments;
    }

    private static int IndexOfSegment(IReadOnlyList<string> segments, string value)
    {
        for (var index = 0; index < segments.Count; index++)
        {
            if (string.Equals(segments[index], value, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    private static bool TryResolveRunIdSegmentIndex(
        IReadOnlyList<string> segments,
        Guid runId,
        int startIndex,
        out int runIdSegmentIndex)
    {
        var runIdD = runId.ToString("D");
        var runIdN = runId.ToString("N");
        for (var index = Math.Max(0, startIndex); index < segments.Count; index++)
        {
            if (string.Equals(segments[index], runIdD, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(segments[index], runIdN, StringComparison.OrdinalIgnoreCase))
            {
                runIdSegmentIndex = index;
                return true;
            }
        }

        runIdSegmentIndex = -1;
        return false;
    }

    private static bool IsRootSegment(string segment, string expected)
        => string.Equals(segment, expected, StringComparison.OrdinalIgnoreCase);
}

internal sealed record ProjectStructureProcessRunFolderProjection(
    string DirectoryPath,
    ProjectStructureProcessRunFolderProjectionKind Kind,
    string IgnoreReason)
{
    public bool ShouldProject => Kind != ProjectStructureProcessRunFolderProjectionKind.Ignored &&
                                 !string.IsNullOrWhiteSpace(DirectoryPath);

    public static ProjectStructureProcessRunFolderProjection Project(
        string directoryPath,
        ProjectStructureProcessRunFolderProjectionKind kind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        return new ProjectStructureProcessRunFolderProjection(directoryPath, kind, string.Empty);
    }

    public static ProjectStructureProcessRunFolderProjection Ignored(string reason)
        => new(string.Empty, ProjectStructureProcessRunFolderProjectionKind.Ignored, reason);
}

internal enum ProjectStructureProcessRunFolderProjectionKind
{
    Ignored = 0,
    ManagedRunRoot = 1,
    ManagedArtifactRunRoot = 2,
    ManagedProductOutputRoot = 3
}
