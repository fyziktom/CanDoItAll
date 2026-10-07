using System.Text.Json.Serialization;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Modules.SchedulerPlanner;

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

    public string? StructureAuthorityJson { get; set; }

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
        builder.Property(item => item.StructureAuthorityJson).HasColumnType("TEXT");
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

    [JsonIgnore]
    public WorkflowStructureAuthority? StructureAuthority { get; set; }
}

public sealed record SchedulerTargetLaunchContext
{
    public SchedulerTargetLaunchContext(
        Guid planId,
        Guid planRunId,
        WorkflowSchedulerFireId schedulerFireId,
        DateTimeOffset firedAtUtc,
        WorkflowLaunchCorrelationId correlationId)
    {
        if (planId == Guid.Empty)
        {
            throw new ArgumentException("Scheduler plan id cannot be empty.", nameof(planId));
        }

        if (planRunId == Guid.Empty)
        {
            throw new ArgumentException("Scheduler plan run id cannot be empty.", nameof(planRunId));
        }

        if (schedulerFireId.Value == Guid.Empty)
        {
            throw new ArgumentException("Scheduler fire id cannot be empty.", nameof(schedulerFireId));
        }

        if (firedAtUtc == default)
        {
            throw new ArgumentException("Scheduler fired-at timestamp is required.", nameof(firedAtUtc));
        }

        if (string.IsNullOrWhiteSpace(correlationId.Value))
        {
            throw new ArgumentException("Scheduler launch correlation id is required.", nameof(correlationId));
        }

        PlanId = planId;
        PlanRunId = planRunId;
        SchedulerFireId = schedulerFireId;
        FiredAtUtc = firedAtUtc;
        CorrelationId = correlationId;
        IdempotencyKey = new WorkflowLaunchIdempotencyKey($"scheduler-plan-run:{planRunId:N}");
    }

    public Guid PlanId { get; }

    public Guid PlanRunId { get; }

    public WorkflowSchedulerFireId SchedulerFireId { get; }

    public DateTimeOffset FiredAtUtc { get; }

    public WorkflowLaunchCorrelationId CorrelationId { get; }

    public WorkflowLaunchIdempotencyKey IdempotencyKey { get; }

    public WorkflowRunId? PreparedRunId { get; init; }

    public WorkflowStructureAuthority? StructureAuthority { get; init; }

    public bool MayLaunch { get; init; } = true;
}

public sealed record SchedulerTargetLaunchResult(
    SchedulerPlanTargetKind TargetKind,
    Guid TargetRunId,
    string State,
    string Summary,
    SchedulerPlanRunDispatchStatus DispatchStatus = SchedulerPlanRunDispatchStatus.Dispatched,
    string Route = SchedulerPlanRunRoutes.Processed,
    SchedulerPlanRunRetryCategory RetryCategory = SchedulerPlanRunRetryCategory.None) {
    public WorkflowRunState? WorkflowState { get; init; }
    public bool RequiresObservation { get; init; }
    [JsonIgnore]
    public Exception? ObservationException { get; init; }
}

