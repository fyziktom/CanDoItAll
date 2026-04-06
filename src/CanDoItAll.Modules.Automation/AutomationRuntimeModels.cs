using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Modules.Automation;

public enum AutomationEnvelopeState
{
    Pending,
    Completed,
    DeadLettered
}

public enum AutomationDeliveryState
{
    Pending,
    Running,
    RetryScheduled,
    Completed,
    DeadLettered
}

public enum AutomationTriggerOwnerKind
{
    Platform,
    Module,
    Plugin,
    Project,
    Agent
}

public enum AutomationTriggerKind
{
    Cron,
    Once,
    Relative,
    DueDateProjection
}

public enum AutomationTriggerMisfirePolicy
{
    FireOnceNow,
    DoNothing,
    IgnoreMisfire
}

public enum PluginIngressState
{
    Accepted,
    Materialized,
    Failed,
    Quarantined
}

public enum AutomationExecutionLogKind
{
    Published,
    DeliveryStarted,
    DeliveryCompleted,
    DeliveryRetryScheduled,
    DeliveryDeadLettered,
    TriggerProjected,
    TriggerFired,
    IngressAccepted,
    IngressMaterialized,
    BackgroundJobScheduled,
    BackgroundJobQueued
}

public enum AutomationDeliveryAttemptOutcome
{
    Completed,
    RetryScheduled,
    DeadLettered
}

public sealed class AutomationEnvelopeRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string EnvelopeType { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "{}";
    public AutomationEnvelopeState State { get; set; } = AutomationEnvelopeState.Pending;
    public int AttemptCount { get; set; }
    public string? DedupeKey { get; set; }
    public Guid? CorrelationId { get; set; }
    public Guid? CausationId { get; set; }
    public DateTimeOffset AvailableAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
}

internal sealed class AutomationEnvelopeRecordConfiguration : IEntityTypeConfiguration<AutomationEnvelopeRecord>
{
    public void Configure(EntityTypeBuilder<AutomationEnvelopeRecord> builder)
    {
        builder.ToTable("Automation_Envelopes");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.EnvelopeType).HasMaxLength(240).IsRequired();
        builder.Property(item => item.PayloadJson).HasColumnType("TEXT");
        builder.Property(item => item.DedupeKey).HasMaxLength(240);
        builder.HasIndex(item => new
        {
            item.EnvelopeType,
            item.DedupeKey
        }).IsUnique();
        builder.HasIndex(item => new
        {
            item.State,
            item.AvailableAtUtc
        });
    }
}

public sealed class AutomationEnvelopeDeliveryRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EnvelopeId { get; set; }
    public string EnvelopeType { get; set; } = string.Empty;
    public string HandlerKey { get; set; } = string.Empty;
    public AutomationDeliveryState State { get; set; } = AutomationDeliveryState.Pending;
    public int AttemptCount { get; set; }
    public int MaxAttempts { get; set; } = 3;
    public DateTimeOffset AvailableAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public DateTimeOffset? LastAttemptAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public string LastError { get; set; } = string.Empty;
    public string LockToken { get; set; } = string.Empty;
    public DateTimeOffset? LockedAtUtc { get; set; }
}

internal sealed class AutomationEnvelopeDeliveryRecordConfiguration : IEntityTypeConfiguration<AutomationEnvelopeDeliveryRecord>
{
    public void Configure(EntityTypeBuilder<AutomationEnvelopeDeliveryRecord> builder)
    {
        builder.ToTable("Automation_EnvelopeDeliveries");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.EnvelopeType).HasMaxLength(240).IsRequired();
        builder.Property(item => item.HandlerKey).HasMaxLength(240).IsRequired();
        builder.Property(item => item.LastError).HasColumnType("TEXT");
        builder.Property(item => item.LockToken).HasMaxLength(100);
        builder.HasIndex(item => new
        {
            item.EnvelopeId,
            item.HandlerKey
        }).IsUnique();
        builder.HasIndex(item => new
        {
            item.State,
            item.AvailableAtUtc
        });
        builder.HasOne<AutomationEnvelopeRecord>()
            .WithMany()
            .HasForeignKey(item => item.EnvelopeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class AutomationDeadLetterRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EnvelopeId { get; set; }
    public Guid DeliveryId { get; set; }
    public string EnvelopeType { get; set; } = string.Empty;
    public string HandlerKey { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "{}";
    public string ErrorMessage { get; set; } = string.Empty;
    public int AttemptCount { get; set; }
    public string? DedupeKey { get; set; }
    public Guid? CorrelationId { get; set; }
    public Guid? CausationId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset DeadLetteredAtUtc { get; set; }
}

internal sealed class AutomationDeadLetterRecordConfiguration : IEntityTypeConfiguration<AutomationDeadLetterRecord>
{
    public void Configure(EntityTypeBuilder<AutomationDeadLetterRecord> builder)
    {
        builder.ToTable("Automation_DeadLetters");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.EnvelopeType).HasMaxLength(240).IsRequired();
        builder.Property(item => item.HandlerKey).HasMaxLength(240).IsRequired();
        builder.Property(item => item.PayloadJson).HasColumnType("TEXT");
        builder.Property(item => item.ErrorMessage).HasColumnType("TEXT");
        builder.Property(item => item.DedupeKey).HasMaxLength(240);
        builder.HasIndex(item => item.DeliveryId).IsUnique();
        builder.HasIndex(item => new
        {
            item.DeadLetteredAtUtc,
            item.HandlerKey
        });
    }
}

public sealed class AutomationTriggerRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public AutomationTriggerOwnerKind OwnerKind { get; set; }
    public string OwnerKey { get; set; } = string.Empty;
    public string TriggerKey { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public AutomationTriggerKind TriggerKind { get; set; } = AutomationTriggerKind.Cron;
    public string CronExpression { get; set; } = string.Empty;
    public string TimeZoneId { get; set; } = string.Empty;
    public DateTimeOffset? StartAtUtc { get; set; }
    public DateTimeOffset? EndAtUtc { get; set; }
    public AutomationTriggerMisfirePolicy MisfirePolicy { get; set; } = AutomationTriggerMisfirePolicy.FireOnceNow;
    public string PayloadJson { get; set; } = "{}";
    public string DedupeKey { get; set; } = string.Empty;
    public DateTimeOffset? NextPlannedFireAtUtc { get; set; }
    public DateTimeOffset? LastFiredAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

internal sealed class AutomationTriggerRecordConfiguration : IEntityTypeConfiguration<AutomationTriggerRecord>
{
    public void Configure(EntityTypeBuilder<AutomationTriggerRecord> builder)
    {
        builder.ToTable("Automation_Triggers");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.OwnerKey).HasMaxLength(160).IsRequired();
        builder.Property(item => item.TriggerKey).HasMaxLength(160).IsRequired();
        builder.Property(item => item.CronExpression).HasMaxLength(160);
        builder.Property(item => item.TimeZoneId).HasMaxLength(120).IsRequired();
        builder.Property(item => item.PayloadJson).HasColumnType("TEXT");
        builder.Property(item => item.DedupeKey).HasMaxLength(240);
        builder.HasIndex(item => new
        {
            item.OwnerKind,
            item.OwnerKey,
            item.TriggerKey
        }).IsUnique();
    }
}

public sealed class PluginIngressEnvelopeRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string SourceKind { get; set; } = string.Empty;
    public string SourceKey { get; set; } = string.Empty;
    public string ExternalMessageId { get; set; } = string.Empty;
    public string CursorValue { get; set; } = string.Empty;
    public string DedupeKey { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "{}";
    public PluginIngressState State { get; set; } = PluginIngressState.Accepted;
    public Guid? CorrelationId { get; set; }
    public string MaterializerKey { get; set; } = string.Empty;
    public string MaterializationSummary { get; set; } = string.Empty;
    public string LastError { get; set; } = string.Empty;
    public DateTimeOffset ReceivedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public DateTimeOffset? MaterializedAtUtc { get; set; }
}

internal sealed class PluginIngressEnvelopeRecordConfiguration : IEntityTypeConfiguration<PluginIngressEnvelopeRecord>
{
    public void Configure(EntityTypeBuilder<PluginIngressEnvelopeRecord> builder)
    {
        builder.ToTable("Automation_PluginIngressEnvelopes");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.SourceKind).HasMaxLength(160).IsRequired();
        builder.Property(item => item.SourceKey).HasMaxLength(160).IsRequired();
        builder.Property(item => item.ExternalMessageId).HasMaxLength(240).IsRequired();
        builder.Property(item => item.CursorValue).HasMaxLength(240);
        builder.Property(item => item.DedupeKey).HasMaxLength(280).IsRequired();
        builder.Property(item => item.PayloadJson).HasColumnType("TEXT");
        builder.Property(item => item.MaterializerKey).HasMaxLength(200);
        builder.Property(item => item.MaterializationSummary).HasColumnType("TEXT");
        builder.Property(item => item.LastError).HasColumnType("TEXT");
        builder.HasIndex(item => new
        {
            item.SourceKind,
            item.SourceKey,
            item.DedupeKey
        }).IsUnique();
        builder.HasIndex(item => new
        {
            item.State,
            item.ReceivedAtUtc
        });
    }
}

public sealed class PluginIngressCursorRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string SourceKind { get; set; } = string.Empty;
    public string SourceKey { get; set; } = string.Empty;
    public string CursorValue { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

internal sealed class PluginIngressCursorRecordConfiguration : IEntityTypeConfiguration<PluginIngressCursorRecord>
{
    public void Configure(EntityTypeBuilder<PluginIngressCursorRecord> builder)
    {
        builder.ToTable("Automation_PluginIngressCursors");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.SourceKind).HasMaxLength(160).IsRequired();
        builder.Property(item => item.SourceKey).HasMaxLength(160).IsRequired();
        builder.Property(item => item.CursorValue).HasMaxLength(240).IsRequired();
        builder.HasIndex(item => new
        {
            item.SourceKind,
            item.SourceKey
        }).IsUnique();
    }
}

public sealed class AutomationExecutionLogRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public AutomationExecutionLogKind EventKind { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public string SourceId { get; set; } = string.Empty;
    public Guid? CorrelationId { get; set; }
    public Guid? CausationId { get; set; }
    public string Message { get; set; } = string.Empty;
    public string DetailsJson { get; set; } = "{}";
    public DateTimeOffset CreatedAtUtc { get; set; }
}

internal sealed class AutomationExecutionLogRecordConfiguration : IEntityTypeConfiguration<AutomationExecutionLogRecord>
{
    public void Configure(EntityTypeBuilder<AutomationExecutionLogRecord> builder)
    {
        builder.ToTable("Automation_ExecutionLogs");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.SourceType).HasMaxLength(160).IsRequired();
        builder.Property(item => item.SourceId).HasMaxLength(160).IsRequired();
        builder.Property(item => item.Message).HasMaxLength(400).IsRequired();
        builder.Property(item => item.DetailsJson).HasColumnType("TEXT");
        builder.HasIndex(item => new
        {
            item.SourceType,
            item.SourceId,
            item.CreatedAtUtc
        });
    }
}

public sealed class AutomationDeliveryAttemptRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EnvelopeId { get; set; }
    public Guid DeliveryId { get; set; }
    public string HandlerKey { get; set; } = string.Empty;
    public int AttemptNumber { get; set; }
    public AutomationDeliveryAttemptOutcome Outcome { get; set; }
    public Guid? CorrelationId { get; set; }
    public Guid? CausationId { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public DateTimeOffset StartedAtUtc { get; set; }
    public DateTimeOffset CompletedAtUtc { get; set; }
}

internal sealed class AutomationDeliveryAttemptRecordConfiguration : IEntityTypeConfiguration<AutomationDeliveryAttemptRecord>
{
    public void Configure(EntityTypeBuilder<AutomationDeliveryAttemptRecord> builder)
    {
        builder.ToTable("Automation_DeliveryAttempts");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.HandlerKey).HasMaxLength(240).IsRequired();
        builder.Property(item => item.ErrorMessage).HasColumnType("TEXT");
        builder.HasIndex(item => new
        {
            item.DeliveryId,
            item.AttemptNumber
        }).IsUnique();
    }
}
