using System.Collections.Frozen;
using CanDoItAll.Components.Gantt;

namespace CanDoItAll.Modules.Workbench.CanvasAdapters;

public enum ProjectStructureGanttProjectionIssueSeverity
{
    Warning,
    Error
}

public enum ProjectStructureGanttProjectionIssueCode
{
    InvalidTaskId,
    DuplicateTaskId,
    InvalidTaskTitle,
    InvalidTaskDuration,
    InvalidTaskSchedule,
    CanonicalDurationMismatch,
    MissingDependencyRecordId,
    DependencyEndpointNotTask,
    DuplicateDependencyId,
    DuplicateDependency,
    SelfDependency,
    DependencyCycle,
    DependencyScheduleConflict,
    ScheduleSynthesized,
    ScheduleStartSynthesized,
    ScheduleEndSynthesized,
    InvalidAssignment
}

public sealed record ProjectStructureGanttProjectionIssue(
    ProjectStructureGanttProjectionIssueCode Code,
    ProjectStructureGanttProjectionIssueSeverity Severity,
    string Message,
    GanttTaskId? TaskId = null,
    GanttTaskId? RelatedTaskId = null,
    GanttDependencyId? DependencyId = null);

public sealed class ProjectStructureGanttProjectionOptions
{
    public ProjectStructureGanttProjectionOptions(
        DateTimeOffset projectionOriginUtc,
        TimeSpan defaultTaskDuration,
        IReadOnlyList<string>? preferredTaskNodeIds = null)
    {
        if (defaultTaskDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(defaultTaskDuration),
                defaultTaskDuration,
                "The default projection duration must be greater than zero.");
        }

        ProjectionOriginUtc = projectionOriginUtc.ToUniversalTime();
        DefaultTaskDuration = defaultTaskDuration;
        PreferredTaskNodeIds = Array.AsReadOnly((preferredTaskNodeIds ?? [])
            .Where(static nodeId => !string.IsNullOrWhiteSpace(nodeId))
            .Select(static nodeId => nodeId.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray());
    }

    public DateTimeOffset ProjectionOriginUtc { get; }

    public TimeSpan DefaultTaskDuration { get; }

    public IReadOnlyList<string> PreferredTaskNodeIds { get; }
}

public sealed class ProjectStructureGanttProjectionResult
{
    internal ProjectStructureGanttProjectionResult(
        IEnumerable<GanttTask> tasks,
        IEnumerable<GanttDependency> dependencies,
        IEnumerable<GanttTaskId> projectionOnlyTaskIds,
        IEnumerable<ProjectStructureGanttProjectionIssue> issues)
    {
        Tasks = Array.AsReadOnly(tasks.ToArray());
        Dependencies = Array.AsReadOnly(dependencies.ToArray());
        ProjectionOnlyTaskIds = projectionOnlyTaskIds.ToFrozenSet();
        Issues = Array.AsReadOnly(issues.ToArray());
    }

    public IReadOnlyList<GanttTask> Tasks { get; }

    public IReadOnlyList<GanttDependency> Dependencies { get; }

    public IReadOnlySet<GanttTaskId> ProjectionOnlyTaskIds { get; }

    public IReadOnlyList<ProjectStructureGanttProjectionIssue> Issues { get; }

    public bool IsValid => Issues.All(issue => issue.Severity != ProjectStructureGanttProjectionIssueSeverity.Error);

    public bool IsProjectionOnly(GanttTask task)
    {
        ArgumentNullException.ThrowIfNull(task);
        return ProjectionOnlyTaskIds.Contains(task.Id);
    }
}
