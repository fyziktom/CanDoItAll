namespace CanDoItAll.Modules.Workbench;

public static class ProjectPlanAnalyticsPayloadPolicy
{
    public const int ConfirmationNodeCount = 5_000;
    public const int ConfirmationLinkCount = 10_000;
    public const int MaximumProjectCount = 250;
    public const int MaximumNodeCount = 50_000;
    public const int MaximumLinkCount = 100_000;
}

public static class ProjectPlanAnalyticsErrorCodes
{
    public const string ScopeLimitExceeded = "PlanSummaryScopeLimitExceeded";
    public const string PayloadLimitExceeded = "PlanSummaryPayloadLimitExceeded";
}

public sealed record ProjectPlanAnalyticsPreflight(
    int ProjectCount,
    long PlanNodeCount,
    long PlanLinkCount,
    IReadOnlyList<string> Warnings)
{
    public long PayloadItemCount => PlanNodeCount + PlanLinkCount;

    public bool RequiresConfirmation => Warnings.Count > 0;
}

public sealed record ProjectPlanAnalyticsLimitDetails(
    int ProjectCount,
    long? PlanNodeCount,
    long? PlanLinkCount,
    int MaximumProjectCount,
    int MaximumNodeCount,
    int MaximumLinkCount);

public sealed record ProjectPlanSummaryQuery(
    DateTimeOffset? AsOfUtc = null,
    int TaskPreviewLimit = 20,
    decimal HoursPerManDay = ProjectTaskEstimatePolicy.DefaultHoursPerManDay);

public enum ProjectPlanManagerSummaryMode
{
    ScheduleOnly,
    ScheduleAndRemainingCosts
}

public sealed record ProjectPlanManagerSummaryQuery(
    ProjectPlanManagerSummaryMode Mode,
    DateTimeOffset? AsOfUtc = null);

public enum ProjectPlanTaskState
{
    Unscheduled,
    Planned,
    Ready,
    Running,
    Waiting,
    Blocked,
    Completed,
    Cancelled
}

public enum ProjectPlanResourceGroup
{
    Person,
    Agent,
    Workflow,
    Process,
    Unassigned,
    Mixed,
    External
}

public sealed record ProjectPlanTaskStateSummary(
    ProjectPlanTaskState State,
    int TaskCount,
    decimal TaskRatioPercent);

public sealed record ProjectPlanExpectedCostTotal(
    string CurrencyCode,
    decimal Amount,
    int PricedTaskCount);

public sealed record ProjectPlanExpectedResourceCostTotal(
    ProjectPlanResourceGroup Group,
    string CurrencyCode,
    decimal Amount,
    int PricedTaskCount);

public sealed record ProjectPlanExpectedCostTrendPoint(
    DateOnly Date,
    ProjectPlanResourceGroup Group,
    string CurrencyCode,
    decimal Amount);

public sealed record ProjectPlanResourceGroupSummary(
    ProjectPlanResourceGroup Group,
    int BindingCount,
    decimal BindingSharePercent,
    int CoveredTaskCount,
    decimal TaskCoveragePercent,
    int ExclusiveTaskCount);

public sealed record ProjectPlanTaskPreview(
    string NodeId,
    string Title,
    ProjectPlanTaskState State,
    string SourceStatus,
    DateTimeOffset? StartUtc,
    DateTimeOffset? EndUtc,
    int? ProgressPercent,
    decimal? ExpectedEffortHours,
    decimal? ExpectedCostAmount,
    string ExpectedCostCurrencyCode,
    int BlockingTaskCount,
    IReadOnlyList<string> BlockingTaskNodeIds,
    IReadOnlyList<ProjectPlanResourceGroup> ResourceGroups);

public sealed record ProjectPlanScheduleSummary(
    DateTimeOffset? EarliestStartUtc,
    DateTimeOffset? LatestEndUtc,
    decimal? DeliveryLeadTimeHours,
    decimal ScheduledTaskDurationHours);

public sealed record ProjectPlanManagerSummary(
    Guid ProjectId,
    string ProjectName,
    DateTimeOffset AsOfUtc,
    int TotalTaskCount,
    ProjectPlanScheduleSummary Schedule,
    IReadOnlyList<ProjectPlanExpectedResourceCostTotal> FutureExpectedCostTotals,
    IReadOnlyList<ProjectPlanExpectedCostTrendPoint> FutureExpectedCostTrend,
    int UnscheduledFutureExpectedCostTaskCount,
    IReadOnlyList<string> Warnings);

public sealed record ProjectPlanDataCompleteness(
    int MissingScheduleTaskCount,
    int InvalidScheduleTaskCount,
    int MissingEffortTaskCount,
    int MissingExpectedCostTaskCount,
    int MissingProgressTaskCount,
    int UnassignedTaskCount,
    int InvalidProgressTaskCount,
    int InvalidMetadataTaskCount,
    int MixedResourceTaskCount,
    int DependencyCycleAffectedTaskCount);

public sealed record ProjectPlanSummary
{
    public required Guid ProjectId { get; init; }

    public required string ProjectName { get; init; }

    public required DateTimeOffset AsOfUtc { get; init; }

    public required int TotalTaskCount { get; init; }

    public required IReadOnlyList<ProjectPlanTaskStateSummary> TaskStates { get; init; }

    public required ProjectPlanScheduleSummary Schedule { get; init; }

    public required decimal TotalExpectedEffortHours { get; init; }

    public required decimal TotalExpectedEffortManDays { get; init; }

    public required decimal? TaskWeightedProgressPercent { get; init; }

    public required decimal? EffortWeightedProgressPercent { get; init; }

    public required IReadOnlyList<ProjectPlanExpectedCostTotal> ExpectedCostTotals { get; init; }

    public required IReadOnlyList<ProjectPlanExpectedResourceCostTotal> FutureExpectedCostTotals { get; init; }

    public required IReadOnlyList<ProjectPlanExpectedCostTrendPoint> FutureExpectedCostTrend { get; init; }

    public required int UnscheduledFutureExpectedCostTaskCount { get; init; }

    public required IReadOnlyList<ProjectPlanResourceGroupSummary> ResourceGroups { get; init; }

    public required IReadOnlyList<ProjectPlanTaskPreview> RunningTasks { get; init; }

    public required IReadOnlyList<ProjectPlanTaskPreview> BlockedTasks { get; init; }

    public required IReadOnlyList<ProjectPlanTaskPreview> WaitingTasks { get; init; }

    public required ProjectPlanDataCompleteness Completeness { get; init; }

    public required IReadOnlyList<string> Warnings { get; init; }
}
