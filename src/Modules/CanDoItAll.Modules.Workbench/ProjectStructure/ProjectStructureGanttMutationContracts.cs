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

/// <summary>
/// Committed result of a Gantt task or schedule mutation: which tasks the owner changed and how many dependencies it
/// added or removed. It carries no task state; read the project structure again to obtain the stored values,
/// including those the owner normalized or recalculated.
/// </summary>
/// <param name="AffectedTaskIds">
/// Tasks whose stored values the mutation changed. Each item is an object whose <c>value</c> member holds the task's
/// string node identifier, for example <c>{ "value": "custom:3f2504e04f8911d39a0c0305e82c3301" }</c>.
/// </param>
/// <param name="AddedDependencyCount">Number of task dependencies the mutation added.</param>
/// <param name="RemovedDependencyCount">Number of task dependencies the mutation removed.</param>
public sealed record ProjectStructureGanttMutationResult(
    IReadOnlyList<GanttTaskId> AffectedTaskIds,
    int AddedDependencyCount,
    int RemovedDependencyCount);

public sealed record ProjectStructureTaskScheduleSnapshot(
    GanttTaskId TaskId,
    DateTimeOffset? StartUtc,
    DateTimeOffset? EndUtc,
    int? DurationSeconds,
    DateTimeOffset ProjectedStartUtc,
    DateTimeOffset ProjectedEndUtc);

public sealed record ProjectStructureGanttScheduleMutationRequest
{
    public ProjectStructureGanttScheduleMutationRequest(
        GanttTaskScheduleChangeRequest scheduleChange,
        IEnumerable<ProjectStructureTaskScheduleSnapshot> expectedTaskSchedules)
    {
        ArgumentNullException.ThrowIfNull(scheduleChange);
        ArgumentNullException.ThrowIfNull(expectedTaskSchedules);
        ScheduleChange = scheduleChange;
        ExpectedTaskSchedules = Array.AsReadOnly(expectedTaskSchedules.ToArray());
    }

    public GanttTaskScheduleChangeRequest ScheduleChange { get; }

    public IReadOnlyList<ProjectStructureTaskScheduleSnapshot> ExpectedTaskSchedules { get; }
}

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
