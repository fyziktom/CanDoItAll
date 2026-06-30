using CanDoItAll.Components.CanvasLib;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Modules.SchedulerPlanner;

public enum SchedulerPlanTargetKind
{
    Process,
    Workflow
}

public enum SchedulerPlanRunDispatchStatus
{
    Received,
    Dispatching,
    Dispatched,
    Failed,
    NoMessages,
    WaitingForApproval
}

public enum SchedulerPlanRunRetryCategory
{
    None,
    NoAction,
    WorkflowWaitingForApproval,
    TransientExternalFailure,
    ProjectWriteFailure,
    WorkflowFailure,
    SchedulerFailure
}

public enum SchedulerPlanMisfirePolicy
{
    FireOnceNow,
    DoNothing,
    IgnoreMisfire
}

public static class SchedulerPlanRunRoutes
{
    public const string Processed = "processed";
    public const string NoMessages = "no_messages";
    public const string Failed = "failed";
    public const string WaitingForApproval = "waiting_for_approval";
}

public sealed class SchedulerPlan
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public SchedulerPlanTargetKind TargetKind { get; set; }

    public Guid TargetId { get; set; }

    public Guid? TargetVersionId { get; set; }

    public string TargetNameSnapshot { get; set; } = string.Empty;

    public string CronExpression { get; set; } = string.Empty;

    public string CronDescription { get; set; } = string.Empty;

    public string TimeZoneId { get; set; } = "UTC";

    public SchedulerPlanMisfirePolicy MisfirePolicy { get; set; } = SchedulerPlanMisfirePolicy.FireOnceNow;

    public bool IsEnabled { get; set; } = true;

    public DateTimeOffset? StartAtUtc { get; set; }

    public DateTimeOffset? EndAtUtc { get; set; }

    public string InputJson { get; set; } = "{}";

    public Guid SchedulerTriggerId { get; set; }

    public string SchedulerTriggerKey { get; set; } = string.Empty;

    public DateTimeOffset? NextPlannedFireAtUtc { get; set; }

    public DateTimeOffset? LastFiredAtUtc { get; set; }

    public string LastError { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}

internal sealed class SchedulerPlanConfiguration : IEntityTypeConfiguration<SchedulerPlan>
{
    public void Configure(EntityTypeBuilder<SchedulerPlan> builder)
    {
        builder.ToTable("SchedulerPlanner_Plans");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Name).HasMaxLength(180).IsRequired();
        builder.Property(item => item.Description).HasColumnType("TEXT");
        builder.Property(item => item.TargetNameSnapshot).HasMaxLength(240).IsRequired();
        builder.Property(item => item.CronExpression).HasMaxLength(160).IsRequired();
        builder.Property(item => item.CronDescription).HasMaxLength(500).IsRequired();
        builder.Property(item => item.TimeZoneId).HasMaxLength(120).IsRequired();
        builder.Property(item => item.InputJson).HasColumnType("TEXT");
        builder.Property(item => item.SchedulerTriggerId).HasColumnName("AutomationTriggerId");
        builder.Property(item => item.SchedulerTriggerKey).HasColumnName("AutomationTriggerKey").HasMaxLength(180).IsRequired();
        builder.Property(item => item.LastError).HasColumnType("TEXT");
        builder.HasIndex(item => item.SchedulerTriggerId)
            .HasDatabaseName("IX_SchedulerPlanner_Plans_AutomationTriggerId")
            .IsUnique();
        builder.HasIndex(item => new
        {
            item.TargetKind,
            item.TargetId,
            item.IsEnabled
        });
        builder.HasIndex(item => item.NextPlannedFireAtUtc);
    }
}

public sealed class SchedulerPlanRun
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PlanId { get; set; }

    public string DedupeKey { get; set; } = string.Empty;

    public Guid SchedulerFireId { get; set; }

    public Guid? CorrelationId { get; set; }

    public DateTimeOffset FiredAtUtc { get; set; }

    public SchedulerPlanRunDispatchStatus Status { get; set; } = SchedulerPlanRunDispatchStatus.Received;

    public int AttemptCount { get; set; }

    public Guid? TargetRunId { get; set; }

    public string TargetRunKind { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string ErrorMessage { get; set; } = string.Empty;

    public string Route { get; set; } = string.Empty;

    public SchedulerPlanRunRetryCategory RetryCategory { get; set; } = SchedulerPlanRunRetryCategory.None;

    public DateTimeOffset? DispatchedAtUtc { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}

internal sealed class SchedulerPlanRunConfiguration : IEntityTypeConfiguration<SchedulerPlanRun>
{
    public void Configure(EntityTypeBuilder<SchedulerPlanRun> builder)
    {
        builder.ToTable("SchedulerPlanner_Runs");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.DedupeKey).HasMaxLength(260).IsRequired();
        builder.Property(item => item.SchedulerFireId).HasColumnName("AutomationEnvelopeId");
        builder.Property(item => item.TargetRunKind).HasMaxLength(80);
        builder.Property(item => item.Summary).HasColumnType("TEXT");
        builder.Property(item => item.ErrorMessage).HasColumnType("TEXT");
        builder.Property(item => item.Route).HasMaxLength(80);
        builder.HasIndex(item => item.DedupeKey).IsUnique();
        builder.HasIndex(item => new
        {
            item.PlanId,
            item.FiredAtUtc
        });
        builder.HasOne<SchedulerPlan>()
            .WithMany()
            .HasForeignKey(item => item.PlanId)
            .OnDelete(DeleteBehavior.Cascade);
    }
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

public sealed record SchedulerPlannerWorkspace(
    IReadOnlyList<SchedulerPlanSummary> Plans,
    IReadOnlyList<SchedulerPlanRunSummary> History,
    IReadOnlyList<SchedulerTargetOption> TargetOptions,
    CanvasCalendarSurface CalendarSurface);

public sealed class SchedulerPlanEditorModel
{
    public Guid? Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public SchedulerPlanTargetKind TargetKind { get; set; } = SchedulerPlanTargetKind.Process;

    public Guid TargetId { get; set; }

    public Guid? TargetVersionId { get; set; }

    public string CronExpression { get; set; } = "0 0 9 ? * MON-FRI";

    public string TimeZoneId { get; set; } = "UTC";

    public SchedulerPlanMisfirePolicy MisfirePolicy { get; set; } = SchedulerPlanMisfirePolicy.FireOnceNow;

    public bool IsEnabled { get; set; } = true;

    public DateTimeOffset? StartAtUtc { get; set; }

    public DateTimeOffset? EndAtUtc { get; set; }

    public string InputJson { get; set; } = "{}";
}

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

public sealed class SchedulerHistoryQuery
{
    public string Search { get; set; } = string.Empty;

    public SchedulerPlanRunDispatchStatus? Status { get; set; }

    public SchedulerPlanTargetKind? TargetKind { get; set; }

    public DateTimeOffset? FromUtc { get; set; }

    public DateTimeOffset? ToUtc { get; set; }

    public int Take { get; set; } = 50;
}

public sealed record SchedulerTargetLaunchResult(
    SchedulerPlanTargetKind TargetKind,
    Guid TargetRunId,
    string State,
    string Summary,
    SchedulerPlanRunDispatchStatus DispatchStatus = SchedulerPlanRunDispatchStatus.Dispatched,
    string Route = SchedulerPlanRunRoutes.Processed,
    SchedulerPlanRunRetryCategory RetryCategory = SchedulerPlanRunRetryCategory.None);

