namespace CanDoItAll.Modules.Workbench;

public sealed record ProjectPlanSummaryQuery(
    DateTimeOffset? AsOfUtc = null,
    int TaskPreviewLimit = 20,
    decimal HoursPerManDay = ProjectTaskEstimatePolicy.DefaultHoursPerManDay);

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
    Mixed
}

public sealed record ProjectPlanTaskStateSummary(
    ProjectPlanTaskState State,
    int TaskCount,
    decimal TaskRatioPercent);

public sealed record ProjectPlanExpectedCostTotal(
    string CurrencyCode,
    decimal Amount,
    int PricedTaskCount);

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

    public required IReadOnlyList<ProjectPlanResourceGroupSummary> ResourceGroups { get; init; }

    public required IReadOnlyList<ProjectPlanTaskPreview> RunningTasks { get; init; }

    public required IReadOnlyList<ProjectPlanTaskPreview> BlockedTasks { get; init; }

    public required IReadOnlyList<ProjectPlanTaskPreview> WaitingTasks { get; init; }

    public required ProjectPlanDataCompleteness Completeness { get; init; }

    public required IReadOnlyList<string> Warnings { get; init; }
}
