using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Workflows.Abstractions;

public interface IWorkflowProjectStructureReportStore
{
    Task<WorkflowProjectStructureReport> QueryProjectStructureReportAsync(
        WorkflowProjectStructureReportQuery query,
        CancellationToken cancellationToken = default);
}

public sealed record WorkflowProjectStructureReportQuery
{
    public const int MaximumPageSize = 100;

    public WorkflowProjectStructureReportQuery(
        IReadOnlyList<Guid> projectIds,
        DateTimeOffset? activityFromUtc,
        DateTimeOffset activityToUtc,
        DateTimeOffset chartFromUtc,
        IReadOnlyList<WorkflowRunState> states,
        int pageIndex = 0,
        int pageSize = 20,
        bool includeAggregate = true)
    {
        ArgumentNullException.ThrowIfNull(projectIds);
        ArgumentNullException.ThrowIfNull(states);
        if (projectIds.Count == 0)
        {
            throw new ArgumentException(
                "At least one project identifier is required for a project-structure workflow report.",
                nameof(projectIds));
        }

        if (projectIds.Any(static projectId => projectId == Guid.Empty))
        {
            throw new ArgumentException(
                "Project-structure workflow report identifiers cannot be empty.",
                nameof(projectIds));
        }

        if (states.Any(static state => !Enum.IsDefined(state)))
        {
            throw new ArgumentOutOfRangeException(
                nameof(states),
                states,
                "The project-structure workflow report contains an unsupported run state.");
        }

        if (pageIndex < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pageIndex),
                pageIndex,
                "The project-structure workflow report page index cannot be negative.");
        }

        if (pageSize is < 1 or > MaximumPageSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pageSize),
                pageSize,
                $"The project-structure workflow report page size must be between 1 and {MaximumPageSize}.");
        }

        if (pageIndex > int.MaxValue / pageSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pageIndex),
                pageIndex,
                "The project-structure workflow report page offset is too large.");
        }

        var normalizedActivityToUtc = activityToUtc.ToUniversalTime();
        var normalizedActivityFromUtc = activityFromUtc?.ToUniversalTime();
        var normalizedChartFromUtc = chartFromUtc.ToUniversalTime();
        if (normalizedActivityFromUtc > normalizedActivityToUtc)
        {
            throw new ArgumentException(
                "The project-structure workflow report activity-from timestamp cannot be later than activity-to.",
                nameof(activityFromUtc));
        }

        if (normalizedChartFromUtc > normalizedActivityToUtc)
        {
            throw new ArgumentException(
                "The project-structure workflow report chart-from timestamp cannot be later than activity-to.",
                nameof(chartFromUtc));
        }

        ProjectIds = projectIds.Distinct().ToArray();
        ActivityFromUtc = normalizedActivityFromUtc;
        ActivityToUtc = normalizedActivityToUtc;
        ChartFromUtc = normalizedChartFromUtc;
        States = states.Distinct().ToArray();
        PageIndex = pageIndex;
        PageSize = pageSize;
        IncludeAggregate = includeAggregate;
    }

    public IReadOnlyList<Guid> ProjectIds { get; }

    public DateTimeOffset? ActivityFromUtc { get; }

    public DateTimeOffset ActivityToUtc { get; }

    public DateTimeOffset ChartFromUtc { get; }

    public IReadOnlyList<WorkflowRunState> States { get; }

    public int PageIndex { get; }

    public int PageSize { get; }

    public bool IncludeAggregate { get; }
}

public sealed record WorkflowProjectStructureReport(
    IReadOnlyList<WorkflowProjectStructureReportRun> Runs,
    int PageIndex,
    int PageSize,
    int TotalCount,
    decimal KnownCostUsd,
    int UnknownCostRunCount,
    long TotalDurationMilliseconds,
    IReadOnlyList<WorkflowProjectStructureDailyCost> DailyCost);

public sealed record WorkflowProjectStructureReportRun(
    WorkflowRunId RunId,
    WorkflowId WorkflowId,
    WorkflowRunState State,
    WorkflowRuntimeBackendKind Backend,
    string Summary,
    DateTimeOffset ActivityAtUtc,
    long DurationMilliseconds,
    decimal KnownCostUsd,
    bool HasUnknownCost);

public sealed record WorkflowProjectStructureDailyCost(
    DateOnly Date,
    decimal KnownCostUsd);
