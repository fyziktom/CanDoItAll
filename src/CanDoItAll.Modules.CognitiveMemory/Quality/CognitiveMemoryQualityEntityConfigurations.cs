using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Modules.CognitiveMemory;

internal sealed class CognitiveMemoryQualityClusterRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryQualityClusterRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryQualityClusterRecord> builder)
    {
        builder.ToTable("CognitiveMemory_QualityClusters");
        builder.HasKey(cluster => cluster.Id);
        builder.Property(cluster => cluster.ClusterHash).HasMaxLength(128).IsRequired();
        builder.Property(cluster => cluster.PolicyProfileId).HasMaxLength(120).IsRequired();
        builder.Property(cluster => cluster.AlgorithmVersion).HasMaxLength(120).IsRequired();
        builder.Property(cluster => cluster.EligibilityReason).HasMaxLength(500).IsRequired();
        builder.Property(cluster => cluster.ConcurrencyToken).IsConcurrencyToken();
        builder.HasIndex(cluster => new { cluster.ProjectId, cluster.ClusterHash }).IsUnique();
        builder.HasIndex(cluster => new { cluster.ProjectId, cluster.PrimaryKeyFamily, cluster.Readiness });
        builder.HasIndex(cluster => new { cluster.ProjectId, cluster.AccessLevel, cluster.RiskLevel });
        builder.HasIndex(cluster => new { cluster.ProjectId, cluster.AggregateEligible, cluster.CompositeScore });
    }
}

internal sealed class CognitiveMemoryQualityClusterKeyRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryQualityClusterKeyRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryQualityClusterKeyRecord> builder)
    {
        builder.ToTable("CognitiveMemory_QualityClusterKeys");
        builder.HasKey(key => key.Id);
        builder.Property(key => key.Key).HasMaxLength(500).IsRequired();
        builder.Property(key => key.DisplayText).HasMaxLength(500).IsRequired();
        builder
            .HasOne<CognitiveMemoryQualityClusterRecord>()
            .WithMany()
            .HasForeignKey(key => key.ClusterId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(key => new { key.ClusterId, key.KeyFamily, key.Key }).IsUnique();
        builder.HasIndex(key => new { key.ProjectId, key.KeyFamily, key.Key });
    }
}

internal sealed class CognitiveMemoryQualityClusterMemberRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryQualityClusterMemberRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryQualityClusterMemberRecord> builder)
    {
        builder.ToTable("CognitiveMemory_QualityClusterMembers");
        builder.HasKey(member => member.Id);
        builder
            .HasOne<CognitiveMemoryQualityClusterRecord>()
            .WithMany()
            .HasForeignKey(member => member.ClusterId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne<CognitiveMemoryRecord>()
            .WithMany()
            .HasForeignKey(member => member.MemoryRecordId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne<CognitiveMemorySourceItemRecord>()
            .WithMany()
            .HasForeignKey(member => member.SourceItemId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<CognitiveMemoryEvidenceAnchorRecord>()
            .WithMany()
            .HasForeignKey(member => member.EvidenceAnchorId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(member => new { member.ClusterId, member.MemberKind, member.MemoryRecordId, member.SourceItemId }).IsUnique();
        builder.HasIndex(member => new { member.ProjectId, member.MemberKind });
        builder.HasIndex(member => member.SourceItemId);
        builder.HasIndex(member => member.EvidenceAnchorId);
    }
}

internal sealed class CognitiveMemoryDreamRunRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryDreamRunRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryDreamRunRecord> builder)
    {
        builder.ToTable("CognitiveMemory_DreamRuns");
        builder.HasKey(run => run.Id);
        builder.Property(run => run.IdempotencyKey).HasMaxLength(240).IsRequired();
        builder.Property(run => run.PolicyProfileId).HasMaxLength(120).IsRequired();
        builder.Property(run => run.AlgorithmVersion).HasMaxLength(120).IsRequired();
        builder.Property(run => run.WarningsJson).HasColumnType("TEXT");
        builder.Property(run => run.FailureCode).HasMaxLength(120).IsRequired();
        builder.Property(run => run.FailureMessage).HasColumnType("TEXT");
        builder.Property(run => run.ConcurrencyToken).IsConcurrencyToken();
        builder.HasIndex(run => new { run.ProjectId, run.IdempotencyKey }).IsUnique();
        builder.HasIndex(run => new { run.ProjectId, run.Mode, run.Status, run.StartedAtUtc });
    }
}

internal sealed class CognitiveMemoryDreamRunClusterRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryDreamRunClusterRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryDreamRunClusterRecord> builder)
    {
        builder.ToTable("CognitiveMemory_DreamRunClusters");
        builder.HasKey(runCluster => runCluster.Id);
        builder.Property(runCluster => runCluster.SelectionReasonCode).HasMaxLength(120).IsRequired();
        builder
            .HasOne<CognitiveMemoryDreamRunRecord>()
            .WithMany()
            .HasForeignKey(runCluster => runCluster.DreamRunId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne<CognitiveMemoryQualityClusterRecord>()
            .WithMany()
            .HasForeignKey(runCluster => runCluster.ClusterId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(runCluster => new { runCluster.DreamRunId, runCluster.ClusterId }).IsUnique();
        builder.HasIndex(runCluster => new { runCluster.ProjectId, runCluster.Readiness });
    }
}

internal sealed class CognitiveMemoryDreamAggregateCandidateRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryDreamAggregateCandidateRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryDreamAggregateCandidateRecord> builder)
    {
        builder.ToTable("CognitiveMemory_DreamAggregateCandidates");
        builder.HasKey(candidate => candidate.Id);
        builder.Property(candidate => candidate.Title).HasMaxLength(300).IsRequired();
        builder.Property(candidate => candidate.SummaryText).HasColumnType("TEXT");
        builder.Property(candidate => candidate.CanonicalText).HasColumnType("TEXT");
        builder.Property(candidate => candidate.AlgorithmVersion).HasMaxLength(120).IsRequired();
        builder.Property(candidate => candidate.PayloadHash).HasMaxLength(128).IsRequired();
        builder.Property(candidate => candidate.ConcurrencyToken).IsConcurrencyToken();
        builder
            .HasOne<CognitiveMemoryDreamRunRecord>()
            .WithMany()
            .HasForeignKey(candidate => candidate.DreamRunId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne<CognitiveMemoryQualityClusterRecord>()
            .WithMany()
            .HasForeignKey(candidate => candidate.ClusterId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<CognitiveMemoryDreamValidationRecord>()
            .WithMany()
            .HasForeignKey(candidate => candidate.ValidationRecordId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<CognitiveMemoryReviewItemRecord>()
            .WithMany()
            .HasForeignKey(candidate => candidate.ReviewItemId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<CognitiveMemoryRecord>()
            .WithMany()
            .HasForeignKey(candidate => candidate.MemoryRecordId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(candidate => new { candidate.ProjectId, candidate.Mode, candidate.Status });
        builder.HasIndex(candidate => new { candidate.DreamRunId, candidate.ClusterId }).IsUnique();
        builder.HasIndex(candidate => candidate.PayloadHash);
        builder.HasIndex(candidate => candidate.ValidationRecordId);
        builder.HasIndex(candidate => candidate.ReviewItemId);
        builder.HasIndex(candidate => candidate.MemoryRecordId);
    }
}

internal sealed class CognitiveMemoryDreamAggregateClaimRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryDreamAggregateClaimRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryDreamAggregateClaimRecord> builder)
    {
        builder.ToTable("CognitiveMemory_DreamAggregateClaims");
        builder.HasKey(claim => claim.Id);
        builder.Property(claim => claim.ClaimText).HasColumnType("TEXT");
        builder.Property(claim => claim.SubjectKey).HasMaxLength(240).IsRequired();
        builder.Property(claim => claim.PredicateKey).HasMaxLength(160).IsRequired();
        builder.Property(claim => claim.ObjectKey).HasMaxLength(240).IsRequired();
        builder
            .HasOne<CognitiveMemoryDreamAggregateCandidateRecord>()
            .WithMany()
            .HasForeignKey(claim => claim.AggregateCandidateId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(claim => new { claim.AggregateCandidateId, claim.Sequence }).IsUnique();
        builder.HasIndex(claim => new { claim.ProjectId, claim.SubjectKey, claim.PredicateKey, claim.ObjectKey });
    }
}

internal sealed class CognitiveMemoryDreamAggregateClaimSourceMapRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryDreamAggregateClaimSourceMapRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryDreamAggregateClaimSourceMapRecord> builder)
    {
        builder.ToTable("CognitiveMemory_DreamAggregateClaimSourceMaps");
        builder.HasKey(sourceMap => sourceMap.Id);
        builder.Property(sourceMap => sourceMap.Summary).HasColumnType("TEXT");
        builder
            .HasOne<CognitiveMemoryDreamAggregateCandidateRecord>()
            .WithMany()
            .HasForeignKey(sourceMap => sourceMap.AggregateCandidateId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne<CognitiveMemoryDreamAggregateClaimRecord>()
            .WithMany()
            .HasForeignKey(sourceMap => sourceMap.AggregateClaimId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne<CognitiveMemoryRecord>()
            .WithMany()
            .HasForeignKey(sourceMap => sourceMap.SourceMemoryRecordId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<CognitiveMemorySourceItemRecord>()
            .WithMany()
            .HasForeignKey(sourceMap => sourceMap.SourceItemId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<CognitiveMemoryEvidenceAnchorRecord>()
            .WithMany()
            .HasForeignKey(sourceMap => sourceMap.EvidenceAnchorId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(sourceMap => new { sourceMap.AggregateClaimId, sourceMap.SourceMemoryRecordId, sourceMap.EvidenceAnchorId, sourceMap.Direction }).IsUnique();
        builder.HasIndex(sourceMap => new { sourceMap.ProjectId, sourceMap.Direction });
        builder.HasIndex(sourceMap => sourceMap.SourceItemId);
        builder.HasIndex(sourceMap => sourceMap.EvidenceAnchorId);
    }
}

internal sealed class CognitiveMemoryDreamValidationRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryDreamValidationRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryDreamValidationRecord> builder)
    {
        builder.ToTable("CognitiveMemory_DreamValidations");
        builder.HasKey(validation => validation.Id);
        builder.Property(validation => validation.PolicyProfileId).HasMaxLength(120).IsRequired();
        builder.Property(validation => validation.IssuesJson).HasColumnType("TEXT");
        builder.Property(validation => validation.ConcurrencyToken).IsConcurrencyToken();
        builder
            .HasOne<CognitiveMemoryDreamAggregateCandidateRecord>()
            .WithMany()
            .HasForeignKey(validation => validation.AggregateCandidateId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(validation => new { validation.AggregateCandidateId, validation.Decision });
        builder.HasIndex(validation => new { validation.ProjectId, validation.Decision, validation.CreatedAtUtc });
    }
}

internal sealed class CognitiveMemorySynthesizedRecallRecordConfiguration : IEntityTypeConfiguration<CognitiveMemorySynthesizedRecallRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemorySynthesizedRecallRecord> builder)
    {
        builder.ToTable("CognitiveMemory_SynthesizedRecalls");
        builder.HasKey(synthesis => synthesis.Id);
        builder.Property(synthesis => synthesis.Brief).HasColumnType("TEXT");
        builder.Property(synthesis => synthesis.ConcurrencyToken).IsConcurrencyToken();
        builder
            .HasOne<CognitiveMemoryRecallTraceRecord>()
            .WithMany()
            .HasForeignKey(synthesis => synthesis.RecallTraceId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(synthesis => new { synthesis.ProjectId, synthesis.RecallTraceId });
        builder.HasIndex(synthesis => synthesis.CreatedAtUtc);
    }
}

internal sealed class CognitiveMemorySynthesizedStatementRecordConfiguration : IEntityTypeConfiguration<CognitiveMemorySynthesizedStatementRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemorySynthesizedStatementRecord> builder)
    {
        builder.ToTable("CognitiveMemory_SynthesizedStatements");
        builder.HasKey(statement => statement.Id);
        builder.Property(statement => statement.Text).HasColumnType("TEXT");
        builder
            .HasOne<CognitiveMemorySynthesizedRecallRecord>()
            .WithMany()
            .HasForeignKey(statement => statement.SynthesisId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(statement => new { statement.SynthesisId, statement.Sequence }).IsUnique();
        builder.HasIndex(statement => new { statement.ProjectId, statement.CreatedAtUtc });
    }
}

internal sealed class CognitiveMemorySynthesizedStatementSourceMapRecordConfiguration : IEntityTypeConfiguration<CognitiveMemorySynthesizedStatementSourceMapRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemorySynthesizedStatementSourceMapRecord> builder)
    {
        builder.ToTable("CognitiveMemory_SynthesizedStatementSourceMaps");
        builder.HasKey(sourceMap => sourceMap.Id);
        builder.Property(sourceMap => sourceMap.SourceSystem).HasMaxLength(80).IsRequired();
        builder.Property(sourceMap => sourceMap.Locator).HasMaxLength(1000).IsRequired();
        builder.Property(sourceMap => sourceMap.Summary).HasColumnType("TEXT");
        builder
            .HasOne<CognitiveMemorySynthesizedRecallRecord>()
            .WithMany()
            .HasForeignKey(sourceMap => sourceMap.SynthesisId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne<CognitiveMemorySynthesizedStatementRecord>()
            .WithMany()
            .HasForeignKey(sourceMap => sourceMap.StatementId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne<CognitiveMemoryRecord>()
            .WithMany()
            .HasForeignKey(sourceMap => sourceMap.MemoryRecordId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<CognitiveMemorySourceItemRecord>()
            .WithMany()
            .HasForeignKey(sourceMap => sourceMap.SourceItemId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<CognitiveMemoryEvidenceAnchorRecord>()
            .WithMany()
            .HasForeignKey(sourceMap => sourceMap.EvidenceAnchorId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(sourceMap => new { sourceMap.StatementId, sourceMap.MemoryRecordId, sourceMap.SourceItemId, sourceMap.EvidenceAnchorId }).IsUnique();
        builder.HasIndex(sourceMap => new { sourceMap.ProjectId, sourceMap.AccessLevel, sourceMap.RedactionState });
    }
}
