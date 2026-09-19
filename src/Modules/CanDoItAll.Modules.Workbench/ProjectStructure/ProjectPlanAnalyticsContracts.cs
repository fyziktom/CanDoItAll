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

/// <summary>
/// Plan summary query for one project. Every member is optional; <c>{}</c> summarizes the plan as of now with up to
/// 20 tasks in each task preview and 8 hours per man-day.
/// </summary>
/// <param name="AsOfUtc">
/// Reference instant (with offset) for classifying tasks as running, ready or planned and for the future-cost trend;
/// null or omitted means now. Converted to UTC.
/// </param>
/// <param name="TaskPreviewLimit">
/// Maximum number of tasks listed in each of <c>runningTasks</c>, <c>blockedTasks</c> and <c>waitingTasks</c>, from
/// 0 through 100 (HTTP 400 <c>PlanSummaryQueryInvalid</c> otherwise). Defaults to 20.
/// </param>
/// <param name="HoursPerManDay">
/// Hours per man-day used for <c>totalExpectedEffortManDays</c> and for estimates entered in man-days, from 1 through
/// 24 (HTTP 400 <c>PlanSummaryQueryInvalid</c> otherwise). Defaults to 8.
/// </param>
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

/// <summary>
/// Plan state of a canonical task in a plan summary, as a JSON integer. A recorded execution state takes precedence
/// over the status text. 0 Unscheduled (no complete planned interval), 1 Planned (starts after the reference instant),
/// 2 Ready (planned start reached, not started), 3 Running (started, or in progress by status, progress or planned
/// interval), 4 Waiting (status waiting, on hold, paused or stopped), 5 Blocked (an unfinished DependsOn or Blocks
/// prerequisite, or status blocked), 6 Completed, 7 Cancelled.
/// </summary>
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

/// <summary>
/// Kind of resource a task is bound to in a plan summary, as a JSON integer: 0 Person, 1 Agent, 2 Workflow, 3 Process,
/// 4 Unassigned (no resource), 5 Mixed (several kinds), 6 External (another kind of binding).
/// </summary>
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

/// <summary>Number and share of the plan's tasks in one plan state.</summary>
/// <param name="State">Plan state, as a JSON integer: 0 Unscheduled, 1 Planned, 2 Ready, 3 Running, 4 Waiting,
/// 5 Blocked, 6 Completed, 7 Cancelled.</param>
/// <param name="TaskCount">Number of tasks in this state.</param>
/// <param name="TaskRatioPercent">Share of all tasks in this state, as a percentage.</param>
public sealed record ProjectPlanTaskStateSummary(
    ProjectPlanTaskState State,
    int TaskCount,
    decimal TaskRatioPercent);

/// <summary>Sum of the expected cost of all tasks priced in one currency; currencies are never converted.</summary>
/// <param name="CurrencyCode">Currency of the amount, for example <c>EUR</c>.</param>
/// <param name="Amount">Sum of the tasks' expected cost amounts in this currency.</param>
/// <param name="PricedTaskCount">Number of tasks with an expected cost in this currency.</param>
public sealed record ProjectPlanExpectedCostTotal(
    string CurrencyCode,
    decimal Amount,
    int PricedTaskCount);

/// <summary>
/// Remaining expected cost of the unfinished tasks of one resource group in one currency. A task's remaining cost is
/// its expected cost multiplied by its remaining progress share (the full amount when progress is unknown).
/// </summary>
/// <param name="Group">Resource group, as a JSON integer: 0 Person, 1 Agent, 2 Workflow, 3 Process, 4 Unassigned,
/// 5 Mixed, 6 External.</param>
/// <param name="CurrencyCode">Currency of the amount.</param>
/// <param name="Amount">Sum of the remaining expected cost.</param>
/// <param name="PricedTaskCount">Number of unfinished tasks that contribute a remaining cost.</param>
public sealed record ProjectPlanExpectedResourceCostTotal(
    ProjectPlanResourceGroup Group,
    string CurrencyCode,
    decimal Amount,
    int PricedTaskCount);

/// <summary>
/// Remaining expected cost attributed to one date for one resource group and currency, for a cost-over-time chart.
/// </summary>
/// <param name="Date">
/// Date (UTC, <c>yyyy-MM-dd</c>) the cost is attributed to: the task's planned end, or the later of its planned start
/// and the reference instant when the end is not later.
/// </param>
/// <param name="Group">Resource group, as a JSON integer: 0 Person, 1 Agent, 2 Workflow, 3 Process, 4 Unassigned,
/// 5 Mixed, 6 External.</param>
/// <param name="CurrencyCode">Currency of the amount.</param>
/// <param name="Amount">Remaining expected cost attributed to this date.</param>
public sealed record ProjectPlanExpectedCostTrendPoint(
    DateOnly Date,
    ProjectPlanResourceGroup Group,
    string CurrencyCode,
    decimal Amount);

/// <summary>How tasks are bound to one kind of resource in the plan.</summary>
/// <param name="Group">Resource group, as a JSON integer: 0 Person, 1 Agent, 2 Workflow, 3 Process, 4 Unassigned,
/// 5 Mixed, 6 External.</param>
/// <param name="BindingCount">Number of task resource bindings of this group.</param>
/// <param name="BindingSharePercent">Share of all task resource bindings that belong to this group, as a
/// percentage.</param>
/// <param name="CoveredTaskCount">Number of tasks with at least one binding of this group.</param>
/// <param name="TaskCoveragePercent">Share of all tasks covered by this group, as a percentage.</param>
/// <param name="ExclusiveTaskCount">Number of tasks bound only to this group.</param>
public sealed record ProjectPlanResourceGroupSummary(
    ProjectPlanResourceGroup Group,
    int BindingCount,
    decimal BindingSharePercent,
    int CoveredTaskCount,
    decimal TaskCoveragePercent,
    int ExclusiveTaskCount);

/// <summary>One task listed in a plan summary preview (running, blocked or waiting tasks).</summary>
/// <param name="NodeId">Identifier of the canonical task, as in <c>nodes[].id</c>.</param>
/// <param name="Title">Task title.</param>
/// <param name="State">Plan state, as a JSON integer: 0 Unscheduled, 1 Planned, 2 Ready, 3 Running, 4 Waiting,
/// 5 Blocked, 6 Completed, 7 Cancelled.</param>
/// <param name="SourceStatus">The task's free-text status as stored.</param>
/// <param name="StartUtc">Planned start as an instant with offset; null when not scheduled.</param>
/// <param name="EndUtc">Planned end as an instant with offset; null when not scheduled.</param>
/// <param name="ProgressPercent">Progress from 0 through 100; null when the task has no usable progress.</param>
/// <param name="ExpectedEffortHours">Expected effort in hours; null when not estimated.</param>
/// <param name="ExpectedCostAmount">Expected cost amount; null when not priced.</param>
/// <param name="ExpectedCostCurrencyCode">Currency of the expected cost; empty when not priced.</param>
/// <param name="BlockingTaskCount">Number of unfinished tasks that block this task.</param>
/// <param name="BlockingTaskNodeIds">Identifiers of the unfinished tasks that block this task.</param>
/// <param name="ResourceGroups">
/// Resource groups the task is bound to, as JSON integers (0 Person, 1 Agent, 2 Workflow, 3 Process, 4 Unassigned,
/// 5 Mixed, 6 External); <c>[4]</c> (Unassigned) when none.
/// </param>
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

/// <summary>Planned time span of a project's tasks.</summary>
/// <param name="EarliestStartUtc">Earliest planned start of a scheduled task; null when none is scheduled.</param>
/// <param name="LatestEndUtc">Latest planned end of a scheduled task; null when none is scheduled.</param>
/// <param name="DeliveryLeadTimeHours">
/// Hours from the earliest planned start to the latest planned end; null when either is missing.
/// </param>
/// <param name="ScheduledTaskDurationHours">Sum of the planned interval lengths of the scheduled tasks, in
/// hours.</param>
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

/// <summary>Counts of tasks whose plan data is missing or unusable, to judge how reliable the summary is.</summary>
/// <param name="MissingScheduleTaskCount">Tasks without a complete planned interval.</param>
/// <param name="InvalidScheduleTaskCount">
/// Tasks with both planned instants whose interval is not valid, for example an end before the start.
/// </param>
/// <param name="MissingEffortTaskCount">Tasks without expected effort.</param>
/// <param name="MissingExpectedCostTaskCount">Tasks without expected cost.</param>
/// <param name="MissingProgressTaskCount">Tasks without tracked progress (cancelled tasks excluded).</param>
/// <param name="UnassignedTaskCount">Tasks without any resource.</param>
/// <param name="InvalidProgressTaskCount">Tasks with a progress value that cannot be used.</param>
/// <param name="InvalidMetadataTaskCount">Tasks whose metadata cannot be read.</param>
/// <param name="MixedResourceTaskCount">Tasks bound to resources of several groups.</param>
/// <param name="DependencyCycleAffectedTaskCount">Tasks that take part in a dependency cycle.</param>
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

/// <summary>
/// Plan summary of one project computed from its canonical tasks (WorkItem nodes with subtype <c>task</c>): task
/// states, schedule, effort, progress, expected and remaining cost, resource coverage, previews of running, blocked and
/// waiting tasks, and data completeness. A computed snapshot as of <c>asOfUtc</c>; nothing is stored.
/// </summary>
public sealed record ProjectPlanSummary
{
    /// <summary>Identifier of the project.</summary>
    public required Guid ProjectId { get; init; }

    /// <summary>Name of the project at the time of the query.</summary>
    public required string ProjectName { get; init; }

    /// <summary>Reference instant (UTC, with offset) the summary was computed for.</summary>
    public required DateTimeOffset AsOfUtc { get; init; }

    /// <summary>Number of canonical tasks in the project.</summary>
    public required int TotalTaskCount { get; init; }

    /// <summary>Number and share of tasks in each plan state.</summary>
    public required IReadOnlyList<ProjectPlanTaskStateSummary> TaskStates { get; init; }

    /// <summary>Planned time span of the tasks.</summary>
    public required ProjectPlanScheduleSummary Schedule { get; init; }

    /// <summary>Sum of the tasks' expected effort, in hours; tasks without effort count as 0.</summary>
    public required decimal TotalExpectedEffortHours { get; init; }

    /// <summary><c>totalExpectedEffortHours</c> divided by the query's <c>hoursPerManDay</c>.</summary>
    public required decimal TotalExpectedEffortManDays { get; init; }

    /// <summary>
    /// Average progress of the tasks with tracked progress (cancelled tasks excluded), rounded to two decimals; null
    /// when no task has tracked progress.
    /// </summary>
    public required decimal? TaskWeightedProgressPercent { get; init; }

    /// <summary>
    /// Progress weighted by expected effort, over tasks with both tracked progress and expected effort, rounded to two
    /// decimals; null when there is no such task.
    /// </summary>
    public required decimal? EffortWeightedProgressPercent { get; init; }

    /// <summary>Total expected cost per currency over all tasks; currencies are never converted.</summary>
    public required IReadOnlyList<ProjectPlanExpectedCostTotal> ExpectedCostTotals { get; init; }

    /// <summary>Remaining expected cost of unfinished tasks per resource group and currency.</summary>
    public required IReadOnlyList<ProjectPlanExpectedResourceCostTotal> FutureExpectedCostTotals { get; init; }

    /// <summary>Remaining expected cost per date, resource group and currency.</summary>
    public required IReadOnlyList<ProjectPlanExpectedCostTrendPoint> FutureExpectedCostTrend { get; init; }

    /// <summary>
    /// Number of unfinished tasks with remaining cost but no planned start, so they are missing from the trend.
    /// </summary>
    public required int UnscheduledFutureExpectedCostTaskCount { get; init; }

    /// <summary>Task resource coverage per resource group.</summary>
    public required IReadOnlyList<ProjectPlanResourceGroupSummary> ResourceGroups { get; init; }

    /// <summary>Running tasks, at most the query's <c>taskPreviewLimit</c>.</summary>
    public required IReadOnlyList<ProjectPlanTaskPreview> RunningTasks { get; init; }

    /// <summary>Blocked tasks, at most the query's <c>taskPreviewLimit</c>.</summary>
    public required IReadOnlyList<ProjectPlanTaskPreview> BlockedTasks { get; init; }

    /// <summary>Waiting tasks, at most the query's <c>taskPreviewLimit</c>.</summary>
    public required IReadOnlyList<ProjectPlanTaskPreview> WaitingTasks { get; init; }

    /// <summary>Counts of tasks with missing or unusable plan data.</summary>
    public required ProjectPlanDataCompleteness Completeness { get; init; }

    /// <summary>Human-readable notes about data problems that affect the summary; empty when there are none.</summary>
    public required IReadOnlyList<string> Warnings { get; init; }
}
