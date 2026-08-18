using CanDoItAll.Processes.Projections;

namespace CanDoItAll.Modules.Workbench;

public enum ProjectManagerSummaryTimeRange
{
    Day,
    Week,
    Month,
    Quarter,
    Year,
    All
}

public enum ProjectManagerSummaryContentMode
{
    HistoryOnly,
    HistoryAndFuture
}

public enum ProjectManagerSummaryScope
{
    CurrentProject,
    ProjectAndDescendants,
    UncategorizedAgentActivity
}

public static class ProjectManagerSummaryScopePolicy
{
    public const int ConfirmationDescendantCount = 25;
    public const int MaximumProjectCount = ProjectPlanAnalyticsPayloadPolicy.MaximumProjectCount;
    public const int HierarchyFrontierBatchSize = 64;
}

public enum ProjectManagerCostCategory
{
    ChatsAndAgents,
    Workflows,
    Processes,
    Workforce,
    External,
    Other
}

public enum ProjectManagerActivityKind
{
    Conversation,
    Workflow,
    Process,
    SimpleChat
}

public enum ProjectManagerActivityStatus
{
    Active,
    Waiting,
    Succeeded,
    Failed,
    Cancelled,
    Blocked,
    Escalated,
    Unknown
}

public enum ProjectManagerActivityStatusFilter
{
    All,
    Active,
    Succeeded,
    Failed,
    Cancelled
}

public sealed record ProjectManagerSummaryOptions(
    ProjectManagerSummaryContentMode ContentMode = ProjectManagerSummaryContentMode.HistoryOnly,
    ProjectManagerSummaryScope Scope = ProjectManagerSummaryScope.CurrentProject,
    ProjectManagerSummaryTimeRange TimeRange = ProjectManagerSummaryTimeRange.Month);

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

public sealed record ProjectManagerSummaryLoadProgress(
    string Stage,
    string Message,
    int CompletedStageCount,
    int TotalStageCount);

public sealed record ProjectManagerTaskSchedule(
    int TaskCount,
    DateTimeOffset? StartUtc,
    DateTimeOffset? EndUtc,
    decimal? DeliveryLeadTimeHours,
    decimal ScheduledTaskDurationHours);

public sealed record ProjectManagerCostBreakdown(
    ProjectManagerCostCategory Category,
    decimal HistoricalKnownUsd,
    decimal HistoricalEstimatedUsd,
    decimal FuturePlannedUsd,
    int UnknownHistoricalCostCount)
{
    public decimal CommittedUsd => HistoricalKnownUsd + FuturePlannedUsd;
}

public sealed record ProjectManagerCurrencyCostTotal(
    string CurrencyCode,
    decimal FuturePlannedAmount,
    int PricedTaskCount);

public sealed record ProjectManagerCostTotals(
    decimal HistoricalKnownUsd,
    decimal HistoricalEstimatedUsd,
    decimal FuturePlannedUsd,
    int UnknownHistoricalCostCount)
{
    public decimal CommittedUsd => HistoricalKnownUsd + FuturePlannedUsd;
}

public sealed record ProjectManagerExpensePoint(
    DateOnly Date,
    decimal HistoricalKnownUsd,
    decimal HistoricalEstimatedUsd,
    decimal FuturePlannedUsd);

public readonly record struct ProjectManagerActivityId(Guid Value);

public sealed record ProjectManagerActivity(
    ProjectManagerActivityId Id,
    ProjectManagerActivityKind Kind,
    string Title,
    string Summary,
    ProjectManagerActivityStatus Status,
    DateTimeOffset ActivityAtUtc,
    long DurationMilliseconds,
    decimal KnownCostUsd,
    decimal EstimatedCostUsd,
    bool HasUnknownCost,
    IReadOnlyList<string> Tags);

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

public sealed record ProjectManagerActivityAggregate(
    int TotalCount,
    ProjectManagerCostTotals Totals,
    long TotalDurationMilliseconds);

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
