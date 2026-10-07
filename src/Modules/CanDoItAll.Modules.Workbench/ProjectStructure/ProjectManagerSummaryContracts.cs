using CanDoItAll.Processes.Projections;

namespace CanDoItAll.Modules.Workbench;

public static class ProjectManagerSummaryScopePolicy
{
    public const int ConfirmationDescendantCount = 25;
    public const int MaximumProjectCount = ProjectPlanAnalyticsPayloadPolicy.MaximumProjectCount;
    public const int HierarchyFrontierBatchSize = 64;
}

public sealed record ProjectManagerSummaryScopeResolution(
    Guid RootProjectId,
    string RootProjectName,
    ProjectManagerSummaryScope Scope,
    IReadOnlyList<Guid> ProjectIds,
    int DescendantCount,
    bool RequiresConfirmation)
{
    public ProjectPlanAnalyticsPreflight? PlanPreflight { get; init; }
}

public sealed record ProjectManagerSummarySnapshot(
    Guid ProjectId,
    string ProjectName,
    ProjectManagerSummaryOptions Options,
    ProjectManagerSummaryScopeResolution Scope,
    DateTimeOffset? HistoryFromUtc,
    DateTimeOffset AsOfUtc,
    DateTimeOffset GeneratedAtUtc,
    ProjectManagerTaskSchedule Schedule,
    ProjectManagerCostTotals Costs,
    IReadOnlyList<ProjectManagerCostBreakdown> CostBreakdown,
    IReadOnlyList<ProjectManagerCurrencyCostTotal> OtherCurrencyFutureCosts,
    IReadOnlyList<ProjectManagerExpensePoint> ExpenseTrend,
    IReadOnlyList<ProjectManagerActivity> LatestActivities,
    IReadOnlyList<string> Warnings);

public sealed record ProjectManagerActivityPageRequest(
    ProjectManagerSummarySnapshot Summary,
    ProjectManagerActivityKind Kind,
    ProjectManagerActivityStatusFilter StatusFilter,
    int PageIndex = 0,
    int PageSize = 20)
{
    public ProcessRunRecordCursor? ProcessCursor { get; init; }

    public ProjectManagerActivityAggregate? KnownAggregate { get; init; }
}

public sealed record ProjectManagerActivityPage(
    IReadOnlyList<ProjectManagerActivity> Items,
    int PageIndex,
    int PageSize,
    int TotalCount,
    ProjectManagerCostTotals Totals,
    long TotalDurationMilliseconds)
{
    public ProcessRunRecordCursor? NextProcessCursor { get; init; }

    public ProjectManagerActivityAggregate Aggregate
        => new(TotalCount, Totals, TotalDurationMilliseconds);
}
