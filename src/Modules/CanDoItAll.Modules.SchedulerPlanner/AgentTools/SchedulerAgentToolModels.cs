namespace CanDoItAll.Modules.SchedulerPlanner;

public sealed record SchedulerWorkflowTargetSearchInput(
    string? Text = null,
    int Take = 20);

public sealed record SchedulerWorkflowScheduleSearchInput(
    string? Text = null,
    bool? IsEnabled = null,
    int Take = 20);

public sealed record SchedulerWorkflowScheduleCreateInput(
    Guid WorkflowId,
    string Name,
    string CronExpression,
    string TimeZoneId,
    Guid? WorkflowVersionId = null,
    string Description = "",
    SchedulerPlanMisfirePolicy MisfirePolicy = SchedulerPlanMisfirePolicy.FireOnceNow,
    string InputJson = "{}",
    bool IsEnabled = true,
    DateTimeOffset? StartAtUtc = null,
    DateTimeOffset? EndAtUtc = null);

public sealed record SchedulerWorkflowTargetSearchItem(
    Guid WorkflowId,
    Guid? WorkflowVersionId,
    string Name,
    string Description,
    string Status);

public sealed record SchedulerWorkflowTargetSearchResult(
    IReadOnlyList<SchedulerWorkflowTargetSearchItem> Items,
    int TotalCount,
    int ReturnedCount);

public sealed record SchedulerWorkflowScheduleSearchItem(
    Guid PlanId,
    Guid WorkflowId,
    Guid? WorkflowVersionId,
    string Name,
    string Description,
    string WorkflowName,
    string CronExpression,
    string CronDescription,
    string TimeZoneId,
    SchedulerPlanMisfirePolicy MisfirePolicy,
    bool IsEnabled,
    DateTimeOffset? StartAtUtc,
    DateTimeOffset? EndAtUtc,
    DateTimeOffset? NextPlannedFireAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record SchedulerWorkflowScheduleSearchResult(
    IReadOnlyList<SchedulerWorkflowScheduleSearchItem> Items,
    int TotalCount,
    int ReturnedCount);

public sealed record SchedulerWorkflowScheduleCreateResult(
    Guid PlanId,
    Guid WorkflowId,
    Guid? WorkflowVersionId,
    string Name,
    string WorkflowName,
    string CronExpression,
    string CronDescription,
    string TimeZoneId,
    SchedulerPlanMisfirePolicy MisfirePolicy,
    bool IsEnabled,
    DateTimeOffset? NextPlannedFireAtUtc,
    DateTimeOffset UpdatedAtUtc);
