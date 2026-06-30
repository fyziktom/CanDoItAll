using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Modules.CognitiveMemory;

internal sealed class CognitiveMemoryRecallTraceStageRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryRecallTraceStageRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryRecallTraceStageRecord> builder)
    {
        builder.ToTable("CognitiveMemory_RecallTraceStages");
        builder.HasKey(stage => stage.Id);
        builder.Property(stage => stage.ProviderTrace).HasMaxLength(500).IsRequired();
        builder.Property(stage => stage.FailureCode).HasMaxLength(120).IsRequired();
        builder.Property(stage => stage.FailureMessage).HasColumnType("TEXT");
        builder
            .HasOne<CognitiveMemoryRecallTraceRecord>()
            .WithMany()
            .HasForeignKey(stage => stage.RecallTraceId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(stage => new { stage.RecallTraceId, stage.StageKind, stage.ChannelKind });
        builder.HasIndex(stage => new { stage.ProjectId, stage.StageKind, stage.Status, stage.StartedAtUtc });
    }
}

internal sealed class CognitiveMemoryRecallCandidateRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryRecallCandidateRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryRecallCandidateRecord> builder)
    {
        builder.ToTable("CognitiveMemory_RecallCandidates");
        builder.HasKey(candidate => candidate.Id);
        builder.Property(candidate => candidate.Title).HasMaxLength(300).IsRequired();
        builder.Property(candidate => candidate.Summary).HasColumnType("TEXT");
        builder.Property(candidate => candidate.Reason).HasColumnType("TEXT");
        builder.Property(candidate => candidate.ChannelTraceJson).HasColumnType("TEXT");
        builder
            .HasOne<CognitiveMemoryRecallTraceRecord>()
            .WithMany()
            .HasForeignKey(candidate => candidate.RecallTraceId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne<CognitiveMemoryRecord>()
            .WithMany()
            .HasForeignKey(candidate => candidate.MemoryRecordId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<CognitiveMemoryClaimRecord>()
            .WithMany()
            .HasForeignKey(candidate => candidate.ClaimId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<CognitiveMemorySourceItemRecord>()
            .WithMany()
            .HasForeignKey(candidate => candidate.SourceItemId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<CognitiveMemoryEvidenceAnchorRecord>()
            .WithMany()
            .HasForeignKey(candidate => candidate.EvidenceAnchorId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<CognitiveMemoryWorkspaceFrameRecord>()
            .WithMany()
            .HasForeignKey(candidate => candidate.WorkspaceFrameId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<CognitiveMemoryContextFrameRecord>()
            .WithMany()
            .HasForeignKey(candidate => candidate.ContextFrameId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<CognitiveMemoryScoreEvaluationTraceRecord>()
            .WithMany()
            .HasForeignKey(candidate => candidate.ScoreEvaluationTraceId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(candidate => new { candidate.RecallTraceId, candidate.DecisionKind, candidate.PrimaryChannelKind });
        builder.HasIndex(candidate => new { candidate.ProjectId, candidate.MemoryRecordId, candidate.DecisionKind });
        builder.HasIndex(candidate => new { candidate.ProjectId, candidate.PrimaryChannelKind, candidate.CreatedAtUtc });
        builder.HasIndex(candidate => candidate.ScoreEvaluationTraceId);
        builder.HasIndex(candidate => candidate.ClaimId);
        builder.HasIndex(candidate => candidate.SourceItemId);
        builder.HasIndex(candidate => candidate.EvidenceAnchorId);
    }
}

internal sealed class CognitiveMemoryRecallContextPackRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryRecallContextPackRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryRecallContextPackRecord> builder)
    {
        builder.ToTable("CognitiveMemory_RecallContextPacks");
        builder.HasKey(pack => pack.Id);
        builder.Property(pack => pack.Title).HasMaxLength(300).IsRequired();
        builder.Property(pack => pack.Summary).HasColumnType("TEXT");
        builder.Property(pack => pack.MetadataJson).HasColumnType("TEXT");
        builder
            .HasOne<CognitiveMemoryRecallTraceRecord>()
            .WithMany()
            .HasForeignKey(pack => pack.RecallTraceId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne<CognitiveMemoryWorkspaceFrameRecord>()
            .WithMany()
            .HasForeignKey(pack => pack.WorkspaceFrameId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(pack => pack.RecallTraceId).IsUnique();
        builder.HasIndex(pack => new { pack.ProjectId, pack.CreatedAtUtc });
        builder.HasIndex(pack => pack.WorkspaceFrameId);
    }
}

internal sealed class CognitiveMemoryRecallContextSectionRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryRecallContextSectionRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryRecallContextSectionRecord> builder)
    {
        builder.ToTable("CognitiveMemory_RecallContextSections");
        builder.HasKey(section => section.Id);
        builder.Property(section => section.SectionKey).HasMaxLength(120).IsRequired();
        builder.Property(section => section.Title).HasMaxLength(300).IsRequired();
        builder.Property(section => section.Content).HasColumnType("TEXT");
        builder
            .HasOne<CognitiveMemoryRecallContextPackRecord>()
            .WithMany()
            .HasForeignKey(section => section.ContextPackId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne<CognitiveMemoryRecallTraceRecord>()
            .WithMany()
            .HasForeignKey(section => section.RecallTraceId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne<CognitiveMemoryRecord>()
            .WithMany()
            .HasForeignKey(section => section.MemoryRecordId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<CognitiveMemoryClaimRecord>()
            .WithMany()
            .HasForeignKey(section => section.ClaimId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<CognitiveMemorySourceItemRecord>()
            .WithMany()
            .HasForeignKey(section => section.SourceItemId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(section => new { section.ContextPackId, section.Sequence }).IsUnique();
        builder.HasIndex(section => new { section.RecallTraceId, section.SectionKind });
        builder.HasIndex(section => new { section.ProjectId, section.SectionKind, section.CreatedAtUtc });
    }
}

internal sealed class CognitiveMemoryRecallSourceRefRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryRecallSourceRefRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryRecallSourceRefRecord> builder)
    {
        builder.ToTable("CognitiveMemory_RecallSourceRefs");
        builder.HasKey(sourceRef => sourceRef.Id);
        builder.Property(sourceRef => sourceRef.SourceSystem).HasMaxLength(80).IsRequired();
        builder.Property(sourceRef => sourceRef.Locator).HasMaxLength(1000).IsRequired();
        builder.Property(sourceRef => sourceRef.QuoteHash).HasMaxLength(128).IsRequired();
        builder.Property(sourceRef => sourceRef.Summary).HasColumnType("TEXT");
        builder
            .HasOne<CognitiveMemoryRecallTraceRecord>()
            .WithMany()
            .HasForeignKey(sourceRef => sourceRef.RecallTraceId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne<CognitiveMemoryRecallContextPackRecord>()
            .WithMany()
            .HasForeignKey(sourceRef => sourceRef.ContextPackId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne<CognitiveMemoryRecord>()
            .WithMany()
            .HasForeignKey(sourceRef => sourceRef.MemoryRecordId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<CognitiveMemoryClaimRecord>()
            .WithMany()
            .HasForeignKey(sourceRef => sourceRef.ClaimId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<CognitiveMemorySourceItemRecord>()
            .WithMany()
            .HasForeignKey(sourceRef => sourceRef.SourceItemId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<CognitiveMemoryEvidenceAnchorRecord>()
            .WithMany()
            .HasForeignKey(sourceRef => sourceRef.EvidenceAnchorId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(sourceRef => new { sourceRef.RecallTraceId, sourceRef.MemoryRecordId, sourceRef.IncludedInContext });
        builder.HasIndex(sourceRef => new { sourceRef.ContextPackId, sourceRef.IncludedInContext });
        builder.HasIndex(sourceRef => new { sourceRef.ProjectId, sourceRef.SourceSystem, sourceRef.IncludedInContext });
        builder.HasIndex(sourceRef => sourceRef.SourceItemId);
        builder.HasIndex(sourceRef => sourceRef.EvidenceAnchorId);
    }
}
