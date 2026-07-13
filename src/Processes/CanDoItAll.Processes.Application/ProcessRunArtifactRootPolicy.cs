namespace CanDoItAll.Processes.Application;

public static class ProcessRunArtifactRootPolicy
{
    private const string ArtifactsRootSegment = "artifacts";
    private const string OutputRootSegment = "output";
    private const string ProcessRunsSegment = "process-runs";
    private const int MaximumManagedStoragePathLength = 4096;
    private const int MaximumPathSegmentCount = 128;
    private const int MaximumLaunchVariableSetCount = 512;
    private const int MaximumLaunchVariablesPerSet = 512;

    public const int MaximumRootCount = 64;

    public static ProcessRunArtifactRootResolution Resolve(string? managedStoragePath, Guid runId)
    {
        string candidate = managedStoragePath?.Trim() ?? string.Empty;
        if (runId == Guid.Empty ||
            candidate.Length == 0 ||
            candidate.Length > MaximumManagedStoragePathLength ||
            Path.IsPathRooted(candidate))
        {
            return ProcessRunArtifactRootResolution.Ignored("The artifact path is empty, unbound, or absolute.");
        }

        IReadOnlyList<string> segments = NormalizeSegments(candidate);
        if (segments.Count == 0)
        {
            return ProcessRunArtifactRootResolution.Ignored("The artifact path has no projectable relative segments.");
        }

        int processRunsIndex = IndexOfSegment(segments, ProcessRunsSegment);
        if (processRunsIndex >= 0)
        {
            return ResolveProcessRunsRoot(segments, runId, processRunsIndex);
        }

        return ProcessRunArtifactRootResolution.Ignored(
            "The artifact path is not anchored to a managed process-run namespace.");
    }

    public static IReadOnlyList<ProcessRunArtifactRootResolution> ResolveCurrentRunRoots(
        Guid runId,
        IReadOnlyList<IReadOnlyDictionary<string, string>> launchVariableSets)
    {
        if (runId == Guid.Empty)
        {
            throw new ArgumentException("A process run identifier is required.", nameof(runId));
        }

        ArgumentNullException.ThrowIfNull(launchVariableSets);
        if (launchVariableSets.Count > MaximumLaunchVariableSetCount)
        {
            throw new ArgumentOutOfRangeException(nameof(launchVariableSets));
        }

        var roots = new Dictionary<string, ProcessRunArtifactRootResolution>(StringComparer.OrdinalIgnoreCase);
        AddRoot(Resolve($"{ArtifactsRootSegment}/{ProcessRunsSegment}/{runId:D}", runId));
        foreach (IReadOnlyDictionary<string, string> launchVariables in launchVariableSets)
        {
            ArgumentNullException.ThrowIfNull(launchVariables);
            if (launchVariables.Count > MaximumLaunchVariablesPerSet)
            {
                throw new ArgumentOutOfRangeException(nameof(launchVariableSets));
            }

            foreach (string value in launchVariables.Values)
            {
                AddRoot(Resolve(value, runId));
            }
        }

        return roots.Values
            .OrderBy(root => root.DirectoryPath, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        void AddRoot(ProcessRunArtifactRootResolution root)
        {
            if (!root.ShouldProject || !roots.TryAdd(root.DirectoryPath, root))
            {
                return;
            }

            if (roots.Count > MaximumRootCount)
            {
                throw new InvalidOperationException($"A process run exposes more than {MaximumRootCount} managed file roots.");
            }
        }
    }

    private static ProcessRunArtifactRootResolution ResolveProcessRunsRoot(
        IReadOnlyList<string> segments,
        Guid runId,
        int processRunsIndex)
    {
        if (!TryResolveRunIdSegmentIndex(segments, runId, processRunsIndex + 1, out int runIdSegmentIndex))
        {
            return ProcessRunArtifactRootResolution.Ignored("The process-runs path does not contain the current run id.");
        }

        if (IsRootSegment(segments[0], OutputRootSegment))
        {
            int outputRootSegmentCount = ResolveOutputRootSegmentCount(segments, runIdSegmentIndex);
            ProcessRunArtifactRootKind rootKind = outputRootSegmentCount > runIdSegmentIndex + 1
                ? ProcessRunArtifactRootKind.ManagedProductOutputRoot
                : ProcessRunArtifactRootKind.ManagedRunRoot;
            return ProcessRunArtifactRootResolution.Project(
                string.Join('/', segments.Take(outputRootSegmentCount)),
                rootKind);
        }

        if (IsRootSegment(segments[0], ArtifactsRootSegment))
        {
            return ProcessRunArtifactRootResolution.Project(
                string.Join('/', segments.Take(runIdSegmentIndex + 1)),
                ProcessRunArtifactRootKind.ManagedArtifactRunRoot);
        }

        return ProcessRunArtifactRootResolution.Ignored(
            "The process-run path is outside the managed artifact and output namespaces.");
    }

    private static int ResolveOutputRootSegmentCount(IReadOnlyList<string> segments, int runIdSegmentIndex)
    {
        int firstOutputChildIndex = runIdSegmentIndex + 1;
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
        string normalizedPath = managedStoragePath
            .Trim()
            .Replace('\\', '/')
            .Trim('/');
        if (string.IsNullOrWhiteSpace(normalizedPath))
        {
            return [];
        }

        var segments = new List<string>();
        foreach (string segment in normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (string.Equals(segment, ".", StringComparison.Ordinal))
            {
                continue;
            }

            if (string.Equals(segment, "..", StringComparison.Ordinal))
            {
                return [];
            }

            if (segments.Count >= MaximumPathSegmentCount)
            {
                return [];
            }

            segments.Add(segment);
        }

        return segments;
    }

    private static int IndexOfSegment(IReadOnlyList<string> segments, string value)
    {
        for (int index = 0; index < segments.Count; index++)
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
        string runIdD = runId.ToString("D");
        string runIdN = runId.ToString("N");
        for (int index = Math.Max(0, startIndex); index < segments.Count; index++)
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

public sealed record ProcessRunArtifactRootResolution(
    string DirectoryPath,
    ProcessRunArtifactRootKind Kind,
    string IgnoreReason)
{
    public bool ShouldProject => Kind != ProcessRunArtifactRootKind.Ignored &&
                                 !string.IsNullOrWhiteSpace(DirectoryPath);

    public static ProcessRunArtifactRootResolution Project(
        string directoryPath,
        ProcessRunArtifactRootKind kind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        return new ProcessRunArtifactRootResolution(directoryPath, kind, string.Empty);
    }

    public static ProcessRunArtifactRootResolution Ignored(string reason)
        => new(string.Empty, ProcessRunArtifactRootKind.Ignored, reason);
}

public enum ProcessRunArtifactRootKind
{
    Ignored = 0,
    ManagedRunRoot = 1,
    ManagedArtifactRunRoot = 2,
    ManagedProductOutputRoot = 3
}
