using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Processes.Persistence;

internal sealed class ProcessInstancePlanEntityConfiguration : IEntityTypeConfiguration<ProcessInstancePlanEntity>
{
    public void Configure(EntityTypeBuilder<ProcessInstancePlanEntity> builder)
    {
        builder.ToTable("process_instance_plans");
        builder.HasKey(plan => plan.PlanId);
        builder.Property(plan => plan.PlanHash).HasMaxLength(128).IsRequired();
        builder.Property(plan => plan.PlanSchemaVersion).HasMaxLength(64).IsRequired();
        builder.Property(plan => plan.DefinitionContentHash).HasMaxLength(128).IsRequired();
        builder.Property(plan => plan.PayloadJson).IsRequired();
        builder.HasIndex(plan => plan.RootPlanId);
        builder.HasIndex(plan => new { plan.DefinitionId, plan.DefinitionVersionId });
        builder.HasIndex(plan => plan.CreatedAtUtc);
    }
}

internal sealed class ProcessRuntimeStateEntityConfiguration : IEntityTypeConfiguration<ProcessRuntimeStateEntity>
{
    public void Configure(EntityTypeBuilder<ProcessRuntimeStateEntity> builder)
    {
        builder.ToTable("process_runtime_states");
        builder.HasKey(state => state.RunId);
        builder.Property(state => state.PlanHash).HasMaxLength(128).IsRequired();
        builder.Property(state => state.Status).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(state => state.ConcurrencyToken).IsConcurrencyToken();
        builder.HasIndex(state => state.RootRunId);
        builder.HasIndex(state => state.Status);

        builder.HasMany(state => state.Steps)
            .WithOne(step => step.RuntimeState)
            .HasForeignKey(step => step.RunId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(state => state.Claims)
            .WithOne(claim => claim.RuntimeState)
            .HasForeignKey(claim => claim.RunId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(state => state.ResultReceipts)
            .WithOne(receipt => receipt.RuntimeState)
            .HasForeignKey(receipt => receipt.RunId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(state => state.AvailableArtifactSlots)
            .WithOne(slot => slot.RuntimeState)
            .HasForeignKey(slot => slot.RunId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ProcessRuntimeStepAssignmentEntityConfiguration : IEntityTypeConfiguration<ProcessRuntimeStepAssignmentEntity>
{
    public void Configure(EntityTypeBuilder<ProcessRuntimeStepAssignmentEntity> builder)
    {
        builder.ToTable("process_runtime_step_assignments");
        builder.HasKey(assignment => new { assignment.RunId, assignment.StepInstanceId });
        builder.Property(assignment => assignment.StepKey).HasMaxLength(256).IsRequired();
        builder.Property(assignment => assignment.RoleKey).HasMaxLength(256).IsRequired();
        builder.Property(assignment => assignment.ExecutorKind).HasMaxLength(128).IsRequired();
        builder.Property(assignment => assignment.ExecutorId).HasMaxLength(256).IsRequired();
        builder.Property(assignment => assignment.ExecutorDisplayName).HasMaxLength(512).IsRequired();
        builder.Property(assignment => assignment.Prompt).IsRequired();
        builder.Property(assignment => assignment.ReadinessHash).HasMaxLength(128).IsRequired();
        builder.Property(assignment => assignment.AssignmentReason).HasMaxLength(2048).IsRequired();
        builder.Property(assignment => assignment.ProducedArtifactSlotIds).IsRequired();
        builder.Property(assignment => assignment.RequiredArtifactSlotIds).IsRequired();
        builder.Property(assignment => assignment.AllowedOperations).IsRequired();
        builder.Property(assignment => assignment.OperationTargetScope).HasMaxLength(128).IsRequired();
        builder.Property(assignment => assignment.LaunchVariablesJson).IsRequired();
        builder.Property(assignment => assignment.BranchGateSourceStepKey).HasMaxLength(256);
        builder.Property(assignment => assignment.BranchGateRequiredOutcomeKey).HasMaxLength(256);
        builder.HasIndex(assignment => assignment.PlanId);
        builder.HasIndex(assignment => new { assignment.RunId, assignment.StepKey }).IsUnique();
        builder.HasIndex(assignment => new { assignment.ExecutorKind, assignment.ExecutorId });
    }
}

internal sealed class ProcessRuntimeStepEntityConfiguration : IEntityTypeConfiguration<ProcessRuntimeStepEntity>
{
    public void Configure(EntityTypeBuilder<ProcessRuntimeStepEntity> builder)
    {
        builder.ToTable("process_runtime_steps");
        builder.HasKey(step => new { step.RunId, step.StepInstanceId });
        builder.Property(step => step.Status).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(step => step.DependencyStepIds).IsRequired();
        builder.Property(step => step.RequiredArtifactSlotIds).IsRequired();
        builder.HasIndex(step => new { step.RunId, step.Status });
        builder.HasIndex(step => new { step.RunId, step.ActiveClaimToken });
    }
}

internal sealed class ProcessDispatchClaimEntityConfiguration : IEntityTypeConfiguration<ProcessDispatchClaimEntity>
{
    public void Configure(EntityTypeBuilder<ProcessDispatchClaimEntity> builder)
    {
        builder.ToTable("process_dispatch_claims");
        builder.HasKey(claim => new { claim.RunId, claim.ClaimToken });
        builder.Property(claim => claim.OwnerId).HasMaxLength(256).IsRequired();
        builder.Property(claim => claim.Status).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.HasIndex(claim => new { claim.StepInstanceId, claim.ClaimToken }).IsUnique();
        builder.HasIndex(claim => new { claim.RunId, claim.Status, claim.ExpiresAtUtc });
    }
}

internal sealed class ProcessStrategyResultReceiptEntityConfiguration : IEntityTypeConfiguration<ProcessStrategyResultReceiptEntity>
{
    public void Configure(EntityTypeBuilder<ProcessStrategyResultReceiptEntity> builder)
    {
        builder.ToTable("process_strategy_result_receipts");
        builder.HasKey(receipt => new { receipt.RunId, receipt.StepInstanceId, receipt.StrategyId, receipt.IdempotencyKey });
        builder.Property(receipt => receipt.StrategyId).HasMaxLength(256).IsRequired();
        builder.Property(receipt => receipt.Outcome).HasMaxLength(64).IsRequired();
        builder.Property(receipt => receipt.AppliedStepStatus).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(receipt => receipt.ResultHash).HasMaxLength(128).IsRequired();
        builder.HasIndex(receipt => new { receipt.StepInstanceId, receipt.StrategyId, receipt.IdempotencyKey }).IsUnique();
    }
}

internal sealed class ProcessRuntimeAvailableArtifactSlotEntityConfiguration : IEntityTypeConfiguration<ProcessRuntimeAvailableArtifactSlotEntity>
{
    public void Configure(EntityTypeBuilder<ProcessRuntimeAvailableArtifactSlotEntity> builder)
    {
        builder.ToTable("process_runtime_available_artifact_slots");
        builder.HasKey(slot => new { slot.RunId, slot.SlotId });
    }
}

internal sealed class ProcessRuntimeEventEntityConfiguration : IEntityTypeConfiguration<ProcessRuntimeEventEntity>
{
    public void Configure(EntityTypeBuilder<ProcessRuntimeEventEntity> builder)
    {
        builder.ToTable("process_runtime_events");
        builder.HasKey(runtimeEvent => runtimeEvent.GlobalSequence);
        builder.Property(runtimeEvent => runtimeEvent.EventType).HasMaxLength(256).IsRequired();
        builder.Property(runtimeEvent => runtimeEvent.CorrelationId).HasMaxLength(256).IsRequired();
        builder.Property(runtimeEvent => runtimeEvent.ActorKind).HasMaxLength(64).IsRequired();
        builder.Property(runtimeEvent => runtimeEvent.ActorId).HasMaxLength(256).IsRequired();
        builder.Property(runtimeEvent => runtimeEvent.SchemaVersion).HasMaxLength(64).IsRequired();
        builder.Property(runtimeEvent => runtimeEvent.Sensitivity).HasMaxLength(64).IsRequired();
        builder.Property(runtimeEvent => runtimeEvent.PayloadHash).HasMaxLength(128).IsRequired();
        builder.HasIndex(runtimeEvent => runtimeEvent.EventId).IsUnique();
        builder.HasIndex(runtimeEvent => new { runtimeEvent.RootRunId, runtimeEvent.RootSequence }).IsUnique();
        builder.HasIndex(runtimeEvent => new { runtimeEvent.RunId, runtimeEvent.OccurredAtUtc });
    }
}

internal sealed class ProcessOutboxMessageEntityConfiguration : IEntityTypeConfiguration<ProcessOutboxMessageEntity>
{
    public void Configure(EntityTypeBuilder<ProcessOutboxMessageEntity> builder)
    {
        builder.ToTable("process_outbox_messages");
        builder.HasKey(message => message.MessageId);
        builder.Property(message => message.SubscriberKind).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(message => message.PayloadHash).HasMaxLength(128).IsRequired();
        builder.Property(message => message.Status).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(message => message.LockId).HasMaxLength(128);
        builder.Property(message => message.LastErrorClass).HasMaxLength(256);
        builder.HasIndex(message => new { message.EventId, message.SubscriberKind }).IsUnique();
        builder.HasIndex(message => new { message.Status, message.AvailableAtUtc, message.LockedAtUtc });
    }
}

internal sealed class ProcessArtifactLedgerEventEntityConfiguration : IEntityTypeConfiguration<ProcessArtifactLedgerEventEntity>
{
    public void Configure(EntityTypeBuilder<ProcessArtifactLedgerEventEntity> builder)
    {
        builder.ToTable("process_artifact_ledger_events");
        builder.HasKey(ledgerEvent => ledgerEvent.LedgerEventId);
        builder.Property(ledgerEvent => ledgerEvent.ContentHash).HasMaxLength(128).IsRequired();
        builder.HasIndex(ledgerEvent => new { ledgerEvent.SlotId, ledgerEvent.LedgerEventId }).IsUnique();
        builder.HasIndex(ledgerEvent => ledgerEvent.EventId);
    }
}

internal sealed class ProcessRuntimeIdempotencyEntityConfiguration : IEntityTypeConfiguration<ProcessRuntimeIdempotencyEntity>
{
    public void Configure(EntityTypeBuilder<ProcessRuntimeIdempotencyEntity> builder)
    {
        builder.ToTable("process_runtime_idempotency_keys");
        builder.HasKey(key => new { key.RunId, key.CommandId });
        builder.Property(key => key.Outcome).HasConversion<string>().HasMaxLength(64).IsRequired();
    }
}

internal sealed class ProcessProjectionSnapshotEntityConfiguration : IEntityTypeConfiguration<ProcessProjectionSnapshotEntity>
{
    public void Configure(EntityTypeBuilder<ProcessProjectionSnapshotEntity> builder)
    {
        builder.ToTable("process_projection_snapshots");
        builder.HasKey(snapshot => new { snapshot.ProjectorName, snapshot.ProjectionKey });
        builder.Property(snapshot => snapshot.ProjectorName).HasMaxLength(256).IsRequired();
        builder.Property(snapshot => snapshot.ProjectionKey).HasMaxLength(512).IsRequired();
        builder.Property(snapshot => snapshot.SchemaVersion).HasMaxLength(64).IsRequired();
        builder.Property(snapshot => snapshot.PayloadJson).IsRequired();
        builder.Property(snapshot => snapshot.PayloadHash).HasMaxLength(128).IsRequired();
        builder.HasIndex(snapshot => snapshot.UpdatedAtUtc);
    }
}

internal sealed class ProcessProjectorOffsetEntityConfiguration : IEntityTypeConfiguration<ProcessProjectorOffsetEntity>
{
    public void Configure(EntityTypeBuilder<ProcessProjectorOffsetEntity> builder)
    {
        builder.ToTable("process_projector_offsets");
        builder.HasKey(offset => new { offset.ProjectorName, offset.ShardKey });
        builder.Property(offset => offset.ProjectorName).HasMaxLength(256).IsRequired();
        builder.Property(offset => offset.ShardKey).HasMaxLength(256).IsRequired();
        builder.HasIndex(offset => offset.GlobalSequence);
    }
}

internal sealed class ProcessProjectionHistoryEntityConfiguration : IEntityTypeConfiguration<ProcessProjectionHistoryEntity>
{
    public void Configure(EntityTypeBuilder<ProcessProjectionHistoryEntity> builder)
    {
        builder.ToTable("process_projection_history");
        builder.HasKey(history => new { history.ProjectorName, history.ProjectionKey, history.GlobalSequence });
        builder.Property(history => history.ProjectorName).HasMaxLength(256).IsRequired();
        builder.Property(history => history.ProjectionKey).HasMaxLength(512).IsRequired();
        builder.Property(history => history.EventType).HasMaxLength(256).IsRequired();
        builder.Property(history => history.SchemaVersion).HasMaxLength(64).IsRequired();
        builder.Property(history => history.PayloadJson).IsRequired();
        builder.Property(history => history.PayloadHash).HasMaxLength(128).IsRequired();
        builder.Property(history => history.Sensitivity).HasMaxLength(64).IsRequired();
        builder.HasIndex(history => new { history.ProjectorName, history.RootRunId, history.OccurredAtUtc });
        builder.HasIndex(history => new { history.ProjectorName, history.RunId, history.GlobalSequence });
    }
}

internal sealed class ProcessProjectionDeadLetterEntityConfiguration : IEntityTypeConfiguration<ProcessProjectionDeadLetterEntity>
{
    public void Configure(EntityTypeBuilder<ProcessProjectionDeadLetterEntity> builder)
    {
        builder.ToTable("process_projection_dead_letters");
        builder.HasKey(deadLetter => deadLetter.DeadLetterId);
        builder.Property(deadLetter => deadLetter.ProjectorName).HasMaxLength(256).IsRequired();
        builder.Property(deadLetter => deadLetter.ShardKey).HasMaxLength(256).IsRequired();
        builder.Property(deadLetter => deadLetter.ErrorClass).HasMaxLength(256).IsRequired();
        builder.Property(deadLetter => deadLetter.DiagnosticReference).HasMaxLength(512).IsRequired();
        builder.Property(deadLetter => deadLetter.RetryPolicy).HasMaxLength(128).IsRequired();
        builder.HasIndex(deadLetter => new { deadLetter.ProjectorName, deadLetter.ShardKey, deadLetter.GlobalSequence });
    }
}
