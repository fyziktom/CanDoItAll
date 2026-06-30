using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Modules.CognitiveMemory;

internal sealed class CognitiveMemoryPredictionExpectationRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryPredictionExpectationRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryPredictionExpectationRecord> builder)
    {
        builder.ToTable("CognitiveMemory_PredictionExpectations");
        builder.HasKey(expectation => expectation.Id);
        builder.Property(expectation => expectation.ActorId).HasMaxLength(200).IsRequired();
        builder.Property(expectation => expectation.PolicyProfileId).HasMaxLength(160).IsRequired();
        builder.Property(expectation => expectation.ExpectedContextKey).HasMaxLength(300).IsRequired();
        builder.Property(expectation => expectation.Summary).HasColumnType("TEXT");
        builder.Property(expectation => expectation.ExpectedOutcome).HasColumnType("TEXT");
        builder.Property(expectation => expectation.AlgorithmVersion).HasMaxLength(120).IsRequired();
        builder.Property(expectation => expectation.MetadataJson).HasColumnType("TEXT");
        builder.Property(expectation => expectation.ConcurrencyToken).IsConcurrencyToken();
        builder
            .HasOne<CognitiveMemoryWorkspaceFrameRecord>()
            .WithMany()
            .HasForeignKey(expectation => expectation.WorkspaceFrameId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<CognitiveMemoryAttentionDecisionRecord>()
            .WithMany()
            .HasForeignKey(expectation => expectation.AttentionDecisionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<CognitiveMemoryRecord>()
            .WithMany()
            .HasForeignKey(expectation => expectation.MemoryRecordId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<CognitiveMemoryClaimRecord>()
            .WithMany()
            .HasForeignKey(expectation => expectation.ClaimId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<CognitiveMemorySourceItemRecord>()
            .WithMany()
            .HasForeignKey(expectation => expectation.SourceItemId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(expectation => new { expectation.ProjectId, expectation.ExpectationKind, expectation.CreatedAtUtc });
        builder.HasIndex(expectation => new { expectation.ProjectId, expectation.ActorKind, expectation.ActorId });
        builder.HasIndex(expectation => expectation.WorkspaceFrameId);
        builder.HasIndex(expectation => expectation.AttentionDecisionId);
        builder.HasIndex(expectation => expectation.MemoryRecordId);
        builder.HasIndex(expectation => expectation.ClaimId);
        builder.HasIndex(expectation => expectation.SourceItemId);
    }
}

internal sealed class CognitiveMemoryPredictionExpectationEvidenceAnchorRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryPredictionExpectationEvidenceAnchorRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryPredictionExpectationEvidenceAnchorRecord> builder)
    {
        builder.ToTable("CognitiveMemory_PredictionExpectationEvidenceAnchors");
        builder.HasKey(anchor => anchor.Id);
        builder
            .HasOne<CognitiveMemoryPredictionExpectationRecord>()
            .WithMany()
            .HasForeignKey(anchor => anchor.PredictionExpectationId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne<CognitiveMemoryEvidenceAnchorRecord>()
            .WithMany()
            .HasForeignKey(anchor => anchor.EvidenceAnchorId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(anchor => new { anchor.PredictionExpectationId, anchor.EvidenceAnchorId }).IsUnique();
        builder.HasIndex(anchor => new { anchor.ProjectId, anchor.EvidenceAnchorId });
    }
}

internal sealed class CognitiveMemoryPredictionErrorRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryPredictionErrorRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryPredictionErrorRecord> builder)
    {
        builder.ToTable("CognitiveMemory_PredictionErrors");
        builder.HasKey(error => error.Id);
        builder.Property(error => error.ActorId).HasMaxLength(200).IsRequired();
        builder.Property(error => error.PolicyProfileId).HasMaxLength(160).IsRequired();
        builder.Property(error => error.ObservationSummary).HasColumnType("TEXT");
        builder.Property(error => error.ExpectedSummary).HasColumnType("TEXT");
        builder.Property(error => error.ObservedSummary).HasColumnType("TEXT");
        builder.Property(error => error.CauseHypothesis).HasColumnType("TEXT");
        builder.Property(error => error.SuggestedAction).HasColumnType("TEXT");
        builder.Property(error => error.AlgorithmVersion).HasMaxLength(120).IsRequired();
        builder.Property(error => error.MetadataJson).HasColumnType("TEXT");
        builder.Property(error => error.ConcurrencyToken).IsConcurrencyToken();
        builder
            .HasOne<CognitiveMemoryPredictionExpectationRecord>()
            .WithMany()
            .HasForeignKey(error => error.PredictionExpectationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<CognitiveMemoryWorkspaceFrameRecord>()
            .WithMany()
            .HasForeignKey(error => error.WorkspaceFrameId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<CognitiveMemoryAttentionDecisionRecord>()
            .WithMany()
            .HasForeignKey(error => error.AttentionDecisionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<CognitiveMemoryRecord>()
            .WithMany()
            .HasForeignKey(error => error.MemoryRecordId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<CognitiveMemoryClaimRecord>()
            .WithMany()
            .HasForeignKey(error => error.ClaimId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<CognitiveMemorySourceItemRecord>()
            .WithMany()
            .HasForeignKey(error => error.SourceItemId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<CognitiveMemoryScoreEvaluationTraceRecord>()
            .WithMany()
            .HasForeignKey(error => error.SeverityScoreEvaluationTraceId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(error => new { error.ProjectId, error.ErrorKind, error.ObservedAtUtc });
        builder.HasIndex(error => new { error.ProjectId, error.RequiresReview, error.ObservedAtUtc });
        builder.HasIndex(error => error.PredictionExpectationId);
        builder.HasIndex(error => error.SeverityScoreEvaluationTraceId);
        builder.HasIndex(error => error.WorkspaceFrameId);
        builder.HasIndex(error => error.AttentionDecisionId);
        builder.HasIndex(error => error.MemoryRecordId);
        builder.HasIndex(error => error.ClaimId);
        builder.HasIndex(error => error.SourceItemId);
    }
}

internal sealed class CognitiveMemoryPredictionErrorEvidenceAnchorRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryPredictionErrorEvidenceAnchorRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryPredictionErrorEvidenceAnchorRecord> builder)
    {
        builder.ToTable("CognitiveMemory_PredictionErrorEvidenceAnchors");
        builder.HasKey(anchor => anchor.Id);
        builder
            .HasOne<CognitiveMemoryPredictionErrorRecord>()
            .WithMany()
            .HasForeignKey(anchor => anchor.PredictionErrorId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne<CognitiveMemoryEvidenceAnchorRecord>()
            .WithMany()
            .HasForeignKey(anchor => anchor.EvidenceAnchorId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(anchor => new { anchor.PredictionErrorId, anchor.EvidenceAnchorId }).IsUnique();
        builder.HasIndex(anchor => new { anchor.ProjectId, anchor.EvidenceAnchorId });
    }
}

internal sealed class CognitiveMemoryPredictionErrorSignalRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryPredictionErrorSignalRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryPredictionErrorSignalRecord> builder)
    {
        builder.ToTable("CognitiveMemory_PredictionErrorSignals");
        builder.HasKey(link => link.Id);
        builder
            .HasOne<CognitiveMemoryPredictionErrorRecord>()
            .WithMany()
            .HasForeignKey(link => link.PredictionErrorId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne<CognitiveMemorySignalRecord>()
            .WithMany()
            .HasForeignKey(link => link.CognitiveSignalId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(link => new { link.PredictionErrorId, link.CognitiveSignalId }).IsUnique();
        builder.HasIndex(link => new { link.ProjectId, link.CognitiveSignalId });
    }
}

internal sealed class CognitiveMemorySignalRecordConfiguration : IEntityTypeConfiguration<CognitiveMemorySignalRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemorySignalRecord> builder)
    {
        builder.ToTable("CognitiveMemory_Signals");
        builder.HasKey(signal => signal.Id);
        builder.Property(signal => signal.ActorId).HasMaxLength(200).IsRequired();
        builder.Property(signal => signal.PolicyProfileId).HasMaxLength(160).IsRequired();
        builder.Property(signal => signal.ScoreSchemaVersion).HasMaxLength(80).IsRequired();
        builder.Property(signal => signal.NormalizationProfileId).HasMaxLength(120).IsRequired();
        builder.Property(signal => signal.AlgorithmVersion).HasMaxLength(120).IsRequired();
        builder.Property(signal => signal.Summary).HasColumnType("TEXT");
        builder.Property(signal => signal.MetadataJson).HasColumnType("TEXT");
        builder.Property(signal => signal.ConcurrencyToken).IsConcurrencyToken();
        builder
            .HasOne<CognitiveMemoryWorkspaceFrameRecord>()
            .WithMany()
            .HasForeignKey(signal => signal.WorkspaceFrameId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<CognitiveMemoryAttentionDecisionRecord>()
            .WithMany()
            .HasForeignKey(signal => signal.AttentionDecisionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<CognitiveMemoryPredictionErrorRecord>()
            .WithMany()
            .HasForeignKey(signal => signal.PredictionErrorId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<CognitiveMemoryRecord>()
            .WithMany()
            .HasForeignKey(signal => signal.MemoryRecordId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<CognitiveMemoryClaimRecord>()
            .WithMany()
            .HasForeignKey(signal => signal.ClaimId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<CognitiveMemorySourceItemRecord>()
            .WithMany()
            .HasForeignKey(signal => signal.SourceItemId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<CognitiveMemoryScoreEvaluationTraceRecord>()
            .WithMany()
            .HasForeignKey(signal => signal.SignalScoreEvaluationTraceId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(signal => new { signal.ProjectId, signal.SignalKind, signal.ObservedAtUtc });
        builder.HasIndex(signal => new { signal.ProjectId, signal.SourceKind, signal.ObservedAtUtc });
        builder.HasIndex(signal => new { signal.ProjectId, signal.RequiresReview, signal.ObservedAtUtc });
        builder.HasIndex(signal => new { signal.ProjectId, signal.ActorKind, signal.ActorId });
        builder.HasIndex(signal => new { signal.ProjectId, signal.WorkspaceFrameId, signal.ObservedAtUtc });
        builder.HasIndex(signal => signal.AttentionDecisionId);
        builder.HasIndex(signal => signal.PredictionErrorId);
        builder.HasIndex(signal => signal.MemoryRecordId);
        builder.HasIndex(signal => signal.ClaimId);
        builder.HasIndex(signal => signal.SourceItemId);
        builder.HasIndex(signal => signal.SignalScoreEvaluationTraceId);
    }
}

internal sealed class CognitiveMemorySignalEvidenceAnchorRecordConfiguration : IEntityTypeConfiguration<CognitiveMemorySignalEvidenceAnchorRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemorySignalEvidenceAnchorRecord> builder)
    {
        builder.ToTable("CognitiveMemory_SignalEvidenceAnchors");
        builder.HasKey(anchor => anchor.Id);
        builder
            .HasOne<CognitiveMemorySignalRecord>()
            .WithMany()
            .HasForeignKey(anchor => anchor.CognitiveSignalId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne<CognitiveMemoryEvidenceAnchorRecord>()
            .WithMany()
            .HasForeignKey(anchor => anchor.EvidenceAnchorId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(anchor => new { anchor.CognitiveSignalId, anchor.EvidenceAnchorId }).IsUnique();
        builder.HasIndex(anchor => new { anchor.ProjectId, anchor.EvidenceAnchorId });
    }
}

internal sealed class CognitiveMemorySignalConsumerPolicyRecordConfiguration : IEntityTypeConfiguration<CognitiveMemorySignalConsumerPolicyRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemorySignalConsumerPolicyRecord> builder)
    {
        builder.ToTable("CognitiveMemory_SignalConsumerPolicies");
        builder.HasKey(policy => policy.Id);
        builder
            .HasOne<CognitiveMemorySignalRecord>()
            .WithMany()
            .HasForeignKey(policy => policy.CognitiveSignalId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(policy => new { policy.CognitiveSignalId, policy.ConsumerKind }).IsUnique();
        builder.HasIndex(policy => new { policy.ProjectId, policy.ConsumerKind, policy.CreatedAtUtc });
    }
}
