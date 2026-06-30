using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Modules.CognitiveMemory;

internal sealed class CognitiveMemorySourceManifestRecordConfiguration : IEntityTypeConfiguration<CognitiveMemorySourceManifestRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemorySourceManifestRecord> builder)
    {
        builder.ToTable("CognitiveMemory_SourceManifests");
        builder.HasKey(manifest => manifest.Id);
        builder.Property(manifest => manifest.SourceSystem).HasMaxLength(80).IsRequired();
        builder.Property(manifest => manifest.SourceScopeKey).HasMaxLength(240).IsRequired();
        builder.Property(manifest => manifest.SourceSnapshotId).HasMaxLength(240).IsRequired();
        builder.Property(manifest => manifest.SnapshotHash).HasMaxLength(128).IsRequired();
        builder.Property(manifest => manifest.ProviderVersion).HasMaxLength(120).IsRequired();
        builder.Property(manifest => manifest.Cursor).HasMaxLength(1000);
        builder.Property(manifest => manifest.ConcurrencyToken).IsConcurrencyToken();
        builder.HasIndex(manifest => new { manifest.SourceSystem, manifest.SourceScopeKey, manifest.SourceSnapshotId }).IsUnique();
        builder.HasIndex(manifest => new { manifest.ProjectId, manifest.SourceSystem, manifest.ObservedAtUtc });
    }
}

internal sealed class CognitiveMemorySourceItemRecordConfiguration : IEntityTypeConfiguration<CognitiveMemorySourceItemRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemorySourceItemRecord> builder)
    {
        builder.ToTable("CognitiveMemory_SourceItems");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.SourceSystem).HasMaxLength(80).IsRequired();
        builder.Property(item => item.SourceItemKey).HasMaxLength(500).IsRequired();
        builder.Property(item => item.SourceItemType).HasMaxLength(80).IsRequired();
        builder.Property(item => item.Title).HasMaxLength(300).IsRequired();
        builder.Property(item => item.ContentText).HasColumnType("TEXT");
        builder.Property(item => item.Locator).HasMaxLength(1000);
        builder.Property(item => item.ContentHash).HasMaxLength(128).IsRequired();
        builder.Property(item => item.AccessScope).HasMaxLength(240).IsRequired();
        builder.Property(item => item.ProvenanceJson).HasColumnType("TEXT");
        builder.Property(item => item.ConcurrencyToken).IsConcurrencyToken();
        builder.HasIndex(item => new { item.SourceManifestId, item.SourceItemKey }).IsUnique();
        builder.HasIndex(item => new { item.ProjectId, item.SourceSystem, item.SourceItemType });
        builder.HasIndex(item => item.ContentHash);
    }
}

internal sealed class CognitiveMemoryRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryRecord> builder)
    {
        builder.ToTable("CognitiveMemory_Records");
        builder.HasKey(record => record.Id);
        builder.Property(record => record.Title).HasMaxLength(300).IsRequired();
        builder.Property(record => record.TopicKey).HasMaxLength(240).IsRequired();
        builder.Property(record => record.CanonicalText).HasColumnType("TEXT");
        builder.Property(record => record.SummaryText).HasColumnType("TEXT");
        builder.Property(record => record.AlgorithmVersion).HasMaxLength(120).IsRequired();
        builder.Property(record => record.ContentHash).HasMaxLength(128).IsRequired();
        builder.Property(record => record.GeneratedReason).HasMaxLength(500).IsRequired();
        builder.Property(record => record.ConcurrencyToken).IsConcurrencyToken();
        builder
            .HasOne<CognitiveMemoryClaimRecord>()
            .WithMany()
            .HasForeignKey(record => record.PrimaryClaimId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<CognitiveMemoryContextFrameRecord>()
            .WithMany()
            .HasForeignKey(record => record.PrimaryContextFrameId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<CognitiveMemoryScoreEvaluationTraceRecord>()
            .WithMany()
            .HasForeignKey(record => record.ConfidenceScoreEvaluationTraceId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<CognitiveMemoryScoreEvaluationTraceRecord>()
            .WithMany()
            .HasForeignKey(record => record.ActivationScoreEvaluationTraceId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(record => new { record.ProjectId, record.Kind, record.ValidationState });
        builder.HasIndex(record => new { record.ProjectId, record.TopicKey });
        builder.HasIndex(record => new { record.ProjectId, record.StabilityState });
        builder.HasIndex(record => record.PrimaryClaimId);
        builder.HasIndex(record => record.PrimaryContextFrameId);
        builder.HasIndex(record => record.ConfidenceScoreEvaluationTraceId);
        builder.HasIndex(record => record.ActivationScoreEvaluationTraceId);
        builder.HasIndex(record => record.ContentHash);
    }
}

internal sealed class CognitiveMemorySourceLinkRecordConfiguration : IEntityTypeConfiguration<CognitiveMemorySourceLinkRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemorySourceLinkRecord> builder)
    {
        builder.ToTable("CognitiveMemory_SourceLinks");
        builder.HasKey(link => link.Id);
        builder.Property(link => link.Locator).HasMaxLength(1000);
        builder.Property(link => link.QuoteHash).HasMaxLength(128);
        builder.Property(link => link.Summary).HasColumnType("TEXT");
        builder
            .HasOne<CognitiveMemoryRecord>()
            .WithMany()
            .HasForeignKey(link => link.MemoryRecordId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne<CognitiveMemorySourceManifestRecord>()
            .WithMany()
            .HasForeignKey(link => link.SourceManifestId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<CognitiveMemorySourceItemRecord>()
            .WithMany()
            .HasForeignKey(link => link.SourceItemId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(link => new { link.MemoryRecordId, link.SourceItemId, link.EvidenceRole }).IsUnique();
        builder.HasIndex(link => link.SourceManifestId);
        builder.HasIndex(link => link.SourceItemId);
    }
}

internal sealed class CognitiveMemoryRelationRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryRelationRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryRelationRecord> builder)
    {
        builder.ToTable("CognitiveMemory_Relations");
        builder.HasKey(relation => relation.Id);
        builder.Property(relation => relation.Reason).HasColumnType("TEXT");
        builder.Property(relation => relation.AlgorithmVersion).HasMaxLength(120).IsRequired();
        builder.Property(relation => relation.ConcurrencyToken).IsConcurrencyToken();
        builder
            .HasOne<CognitiveMemoryRecord>()
            .WithMany()
            .HasForeignKey(relation => relation.SourceMemoryRecordId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<CognitiveMemoryRecord>()
            .WithMany()
            .HasForeignKey(relation => relation.TargetMemoryRecordId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<CognitiveMemoryScoreEvaluationTraceRecord>()
            .WithMany()
            .HasForeignKey(relation => relation.RelationScoreEvaluationTraceId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(relation => new
        {
            relation.ProjectId,
            relation.SourceMemoryRecordId,
            relation.TargetMemoryRecordId,
            relation.RelationKind
        }).IsUnique();
        builder.HasIndex(relation => new { relation.ProjectId, relation.RelationKind });
        builder.HasIndex(relation => relation.TargetMemoryRecordId);
        builder.HasIndex(relation => relation.RelationScoreEvaluationTraceId);
    }
}

internal sealed class CognitiveMemoryProjectionStateRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryProjectionStateRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryProjectionStateRecord> builder)
    {
        builder.ToTable("CognitiveMemory_ProjectionStates");
        builder.HasKey(projection => projection.Id);
        builder.Property(projection => projection.TargetProvider).HasMaxLength(120).IsRequired();
        builder.Property(projection => projection.ProjectionSchemaVersion).HasMaxLength(80).IsRequired();
        builder.Property(projection => projection.AlgorithmVersion).HasMaxLength(120).IsRequired();
        builder.Property(projection => projection.LastSourceHash).HasMaxLength(128).IsRequired();
        builder.Property(projection => projection.FailureCode).HasMaxLength(120).IsRequired();
        builder.Property(projection => projection.FailureMessage).HasColumnType("TEXT");
        builder.Property(projection => projection.ConcurrencyToken).IsConcurrencyToken();
        builder.HasIndex(projection => new { projection.ProjectId, projection.ProjectionKind, projection.TargetProvider }).IsUnique();
        builder.HasIndex(projection => new { projection.Status, projection.RebuildRequired });
    }
}

internal sealed class CognitiveMemoryRecallTraceRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryRecallTraceRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryRecallTraceRecord> builder)
    {
        builder.ToTable("CognitiveMemory_RecallTraces");
        builder.HasKey(trace => trace.Id);
        builder.Property(trace => trace.RequestedByActorId).HasMaxLength(160).IsRequired();
        builder.Property(trace => trace.PolicyProfileId).HasMaxLength(120).IsRequired();
        builder.Property(trace => trace.RequestHash).HasMaxLength(128).IsRequired();
        builder.Property(trace => trace.AlgorithmVersion).HasMaxLength(120).IsRequired();
        builder.Property(trace => trace.TraceJson).HasColumnType("TEXT");
        builder.Property(trace => trace.ConcurrencyToken).IsConcurrencyToken();
        builder
            .HasOne<CognitiveMemoryWorkspaceFrameRecord>()
            .WithMany()
            .HasForeignKey(trace => trace.WorkspaceFrameId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<CognitiveMemoryAttentionDecisionRecord>()
            .WithMany()
            .HasForeignKey(trace => trace.AttentionDecisionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(trace => new { trace.ProjectId, trace.OperationMode, trace.StartedAtUtc });
        builder.HasIndex(trace => trace.WorkspaceFrameId);
        builder.HasIndex(trace => trace.AttentionDecisionId);
        builder.HasIndex(trace => trace.SelfRegulationAssessmentId);
        builder.HasIndex(trace => trace.AnswerPostureDecisionId);
        builder.HasIndex(trace => trace.AnswerGateDecisionId);
        builder.HasIndex(trace => trace.ContextPackId);
        builder.HasIndex(trace => new { trace.ProjectId, trace.RecallMode, trace.Outcome, trace.StartedAtUtc });
        builder.HasIndex(trace => trace.RequestHash);
    }
}

internal sealed class CognitiveMemoryReviewItemRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryReviewItemRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryReviewItemRecord> builder)
    {
        builder.ToTable("CognitiveMemory_ReviewItems");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.ReasonCode).HasMaxLength(120).IsRequired();
        builder.Property(item => item.ReasonText).HasColumnType("TEXT");
        builder.Property(item => item.DecidedByActorId).HasMaxLength(160).IsRequired();
        builder.Property(item => item.DecisionNotes).HasColumnType("TEXT");
        builder.Property(item => item.ConcurrencyToken).IsConcurrencyToken();
        builder.HasIndex(item => new { item.ProjectId, item.Status, item.RiskLevel });
        builder.HasIndex(item => new { item.SubjectKind, item.SubjectId });
    }
}

internal sealed class CognitiveMemoryRunRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryRunRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryRunRecord> builder)
    {
        builder.ToTable("CognitiveMemory_Runs");
        builder.HasKey(run => run.Id);
        builder.Property(run => run.IdempotencyKey).HasMaxLength(240).IsRequired();
        builder.Property(run => run.InputHash).HasMaxLength(128).IsRequired();
        builder.Property(run => run.AlgorithmVersion).HasMaxLength(120).IsRequired();
        builder.Property(run => run.Cursor).HasMaxLength(1000).IsRequired();
        builder.Property(run => run.FailureCode).HasMaxLength(120).IsRequired();
        builder.Property(run => run.FailureMessage).HasColumnType("TEXT");
        builder.Property(run => run.ConcurrencyToken).IsConcurrencyToken();
        builder.HasIndex(run => new { run.ProjectId, run.RunKind, run.Status });
        builder.HasIndex(run => run.IdempotencyKey).IsUnique();
    }
}
