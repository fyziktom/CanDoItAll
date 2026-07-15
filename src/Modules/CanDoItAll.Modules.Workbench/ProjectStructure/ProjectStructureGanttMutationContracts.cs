using CanDoItAll.Components.Gantt;

namespace CanDoItAll.Modules.Workbench;

public enum ProjectStructureGanttMutationErrorCode
{
    ProjectNotFound,
    TaskNotFound,
    InvalidTask,
    InvalidTaskIdentifier,
    InvalidTitle,
    StaleTask,
    ProjectionOnlySchedule,
    InvalidSchedule,
    DependencyNotFound,
    DuplicateDependency,
    InvalidDependency,
    SystemManagedDependency,
    CycleDetected,
    InvalidInsertion
}

public sealed class ProjectStructureGanttMutationException : InvalidOperationException
{
    public ProjectStructureGanttMutationException(
        ProjectStructureGanttMutationErrorCode code,
        string message)
        : base(message)
    {
        Code = code;
    }

    public ProjectStructureGanttMutationErrorCode Code { get; }
}

public sealed record ProjectStructureGanttMutationResult(
    IReadOnlyList<GanttTaskId> AffectedTaskIds,
    int AddedDependencyCount,
    int RemovedDependencyCount);

internal static class ProjectStructureGanttMutationConventions
{
    private const string PersistedDependencyPrefix = "project-link:";
    private const string PendingDependencyPrefix = "gantt-dependency:";
    private const string CustomNodePrefix = "custom:";

    public static GanttDependencyId DependencyId(Guid recordId)
    {
        if (recordId == Guid.Empty)
        {
            throw new ArgumentException("A dependency record identifier is required.", nameof(recordId));
        }

        return new GanttDependencyId($"{PersistedDependencyPrefix}{recordId:N}");
    }

    public static GanttDependencyId CreatePendingDependencyId()
        => new($"{PendingDependencyPrefix}{Guid.NewGuid():N}");

    public static GanttTaskId CreateCustomTaskId()
        => new($"{CustomNodePrefix}{Guid.NewGuid():N}");

    public static Guid RequirePersistedDependencyRecordId(GanttDependencyId dependencyId)
        => RequireDependencyRecordId(dependencyId, PersistedDependencyPrefix);

    public static Guid RequireNewDependencyRecordId(GanttDependencyId dependencyId)
    {
        if (TryParseDependencyRecordId(dependencyId, PersistedDependencyPrefix, out var persistedId) ||
            TryParseDependencyRecordId(dependencyId, PendingDependencyPrefix, out persistedId))
        {
            return persistedId;
        }

        throw new ProjectStructureGanttMutationException(
            ProjectStructureGanttMutationErrorCode.InvalidDependency,
            $"Dependency identifier '{Mask(dependencyId.Value)}' does not contain a valid record identifier.");
    }

    public static void ValidateNewTaskNodeKey(GanttTaskId taskId)
    {
        var value = taskId.Value;
        if (value.Length <= CustomNodePrefix.Length ||
            value.Length > 160 ||
            !value.StartsWith(CustomNodePrefix, StringComparison.Ordinal) ||
            !Guid.TryParseExact(value[CustomNodePrefix.Length..], "N", out _))
        {
            throw new ProjectStructureGanttMutationException(
                ProjectStructureGanttMutationErrorCode.InvalidTaskIdentifier,
                $"New Gantt task identifier '{Mask(value)}' must use the 'custom:<guid>' project node convention.");
        }
    }

    public static string Mask(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "***";
        }

        return value.Length <= 10
            ? "***"
            : $"{value[..5]}...{value[^4..]}";
    }

    private static Guid RequireDependencyRecordId(GanttDependencyId dependencyId, string prefix)
    {
        if (TryParseDependencyRecordId(dependencyId, prefix, out var recordId))
        {
            return recordId;
        }

        throw new ProjectStructureGanttMutationException(
            ProjectStructureGanttMutationErrorCode.InvalidDependency,
            $"Dependency identifier '{Mask(dependencyId.Value)}' is not a persisted project dependency identifier.");
    }

    private static bool TryParseDependencyRecordId(
        GanttDependencyId dependencyId,
        string prefix,
        out Guid recordId)
    {
        recordId = default;
        var value = dependencyId.Value;
        return value.StartsWith(prefix, StringComparison.Ordinal) &&
            Guid.TryParseExact(value[prefix.Length..], "N", out recordId) &&
            recordId != Guid.Empty;
    }
}
