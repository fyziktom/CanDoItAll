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
    InvalidTaskProgress,
    InvalidTaskEstimate,
    InvalidAssignment
}

public sealed record ProjectStructureGanttProjectionIssue(
    ProjectStructureGanttProjectionIssueCode Code,
    ProjectStructureGanttProjectionIssueSeverity Severity,
    string Message,
    GanttTaskId? TaskId = null,
    GanttTaskId? RelatedTaskId = null,
    GanttDependencyId? DependencyId = null);

public sealed record ProjectStructureGanttExpectedCostTotal(
    string CurrencyCode,
    decimal Amount);

public sealed class ProjectStructureGanttProjectionOptions
{
    public ProjectStructureGanttProjectionOptions(
        DateTimeOffset projectionOriginUtc,
        TimeSpan defaultTaskDuration,
        IReadOnlyList<string>? preferredTaskNodeIds = null,
        decimal hoursPerManDay = 8m)
    {
        if (defaultTaskDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(defaultTaskDuration),
                defaultTaskDuration,
                "The default projection duration must be greater than zero.");
        }

        if (hoursPerManDay <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(hoursPerManDay),
                hoursPerManDay,
                "Hours per man-day must be greater than zero.");
        }

        ProjectionOriginUtc = projectionOriginUtc.ToUniversalTime();
        DefaultTaskDuration = defaultTaskDuration;
        HoursPerManDay = hoursPerManDay;
        PreferredTaskNodeIds = Array.AsReadOnly((preferredTaskNodeIds ?? [])
            .Where(static nodeId => !string.IsNullOrWhiteSpace(nodeId))
            .Select(static nodeId => nodeId.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray());
    }

    public DateTimeOffset ProjectionOriginUtc { get; }

    public TimeSpan DefaultTaskDuration { get; }

    public decimal HoursPerManDay { get; }

    public IReadOnlyList<string> PreferredTaskNodeIds { get; }
}

public sealed class ProjectStructureGanttProjectionResult
{
    internal ProjectStructureGanttProjectionResult(
        IEnumerable<GanttTask> tasks,
        IEnumerable<GanttDependency> dependencies,
        IEnumerable<GanttTaskId> projectionOnlyTaskIds,
        IEnumerable<ProjectStructureGanttProjectionIssue> issues,
        IEnumerable<ProjectStructureGanttExpectedCostTotal>? expectedCostTotals = null)
    {
        Tasks = Array.AsReadOnly(tasks.ToArray());
        Dependencies = Array.AsReadOnly(dependencies.ToArray());
        ProjectionOnlyTaskIds = projectionOnlyTaskIds.ToFrozenSet();
        Issues = Array.AsReadOnly(issues.ToArray());
        ExpectedCostTotals = Array.AsReadOnly((expectedCostTotals ?? [])
            .OrderBy(static total => total.CurrencyCode, StringComparer.Ordinal)
            .ToArray());
    }

    public IReadOnlyList<GanttTask> Tasks { get; }

    public IReadOnlyList<GanttDependency> Dependencies { get; }

    public IReadOnlySet<GanttTaskId> ProjectionOnlyTaskIds { get; }

    public IReadOnlyList<ProjectStructureGanttProjectionIssue> Issues { get; }

    public IReadOnlyList<ProjectStructureGanttExpectedCostTotal> ExpectedCostTotals { get; }

    public bool IsValid => Issues.All(issue => issue.Severity != ProjectStructureGanttProjectionIssueSeverity.Error);

    public bool IsProjectionOnly(GanttTask task)
    {
        ArgumentNullException.ThrowIfNull(task);
        return ProjectionOnlyTaskIds.Contains(task.Id);
    }
}
