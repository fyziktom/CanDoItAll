using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.SchedulerPlanner;

public enum SchedulerPlanTargetKind {
    Process,
    Workflow
}

public enum SchedulerPlanRunDispatchStatus {
    Received,
    Dispatching,
    Dispatched,
    Failed,
    NoMessages,
    WaitingForApproval,
    ObservationPending
}

public enum SchedulerPlanRunRetryCategory {
    None,
    NoAction,
    WorkflowWaitingForApproval,
    TransientExternalFailure,
    ProjectWriteFailure,
    WorkflowFailure,
    SchedulerFailure
}

public enum SchedulerPlanMisfirePolicy {
    FireOnceNow,
    DoNothing,
    IgnoreMisfire
}

public static class SchedulerPlanRunRoutes {
    public const string Processed = "processed";
    public const string NoMessages = "no_messages";
    public const string Failed = "failed";
    public const string WaitingForApproval = "waiting_for_approval";
}


public sealed record SchedulerTargetOption(
    SchedulerPlanTargetKind Kind,
    Guid Id,
    Guid? VersionId,
    string Name,
    string Description,
    string Status);

public sealed record SchedulerPlanSummary(
    Guid Id,
    string Name,
    string Description,
    SchedulerPlanTargetKind TargetKind,
    Guid TargetId,
    Guid? TargetVersionId,
    string TargetName,
    string CronExpression,
    string CronDescription,
    string TimeZoneId,
    SchedulerPlanMisfirePolicy MisfirePolicy,
    bool IsEnabled,
    DateTimeOffset? StartAtUtc,
    DateTimeOffset? EndAtUtc,
    DateTimeOffset? NextPlannedFireAtUtc,
    DateTimeOffset? LastFiredAtUtc,
    string LastError,
    DateTimeOffset UpdatedAtUtc);

public sealed record SchedulerPlanRunSummary(
    Guid Id,
    Guid PlanId,
    string PlanName,
    SchedulerPlanTargetKind TargetKind,
    string TargetName,
    DateTimeOffset FiredAtUtc,
    SchedulerPlanRunDispatchStatus Status,
    int AttemptCount,
    Guid? TargetRunId,
    string Route,
    SchedulerPlanRunRetryCategory RetryCategory,
    string Summary,
    string ErrorMessage,
    DateTimeOffset UpdatedAtUtc);


public sealed record SchedulerWorkflowInputSchema(
    WorkflowId WorkflowId,
    WorkflowVersionId VersionId,
    string WorkflowName,
    IReadOnlyList<WorkflowInputParameterDescriptor> Parameters,
    bool UsesRawJsonFallback);

public sealed record SchedulerWorkflowInputValidationIssue(
    string ParameterKey,
    string Message);

public sealed record SchedulerWorkflowInputValidationResult(
    bool Succeeded,
    string NormalizedInputJson,
    IReadOnlyList<SchedulerWorkflowInputValidationIssue> Issues);

public sealed class SchedulerHistoryQuery {
    public string Search { get; set; } = string.Empty;

    public SchedulerPlanRunDispatchStatus? Status { get; set; }

    public SchedulerPlanTargetKind? TargetKind { get; set; }

    public DateTimeOffset? FromUtc { get; set; }

    public DateTimeOffset? ToUtc { get; set; }

    public int Take { get; set; } = 50;
}
