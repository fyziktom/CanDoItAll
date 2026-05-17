using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Modules.CognitiveMemory;

internal sealed class CognitiveMemoryEvidenceAnchorRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryEvidenceAnchorRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryEvidenceAnchorRecord> builder)
    {
        builder.ToTable("CognitiveMemory_EvidenceAnchors");
        builder.HasKey(anchor => anchor.Id);
        builder.Property(anchor => anchor.SourceSystem).HasMaxLength(80).IsRequired();
        builder.Property(anchor => anchor.Locator).HasMaxLength(1000).IsRequired();
        builder.Property(anchor => anchor.StructuredPath).HasMaxLength(1000).IsRequired();
        builder.Property(anchor => anchor.QuoteHash).HasMaxLength(128).IsRequired();
        builder.Property(anchor => anchor.SourceHash).HasMaxLength(128).IsRequired();
        builder.Property(anchor => anchor.ConcurrencyToken).IsConcurrencyToken();
        builder
            .HasOne<CognitiveMemorySourceManifestRecord>()
            .WithMany()
            .HasForeignKey(anchor => anchor.SourceManifestId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<CognitiveMemorySourceItemRecord>()
            .WithMany()
            .HasForeignKey(anchor => anchor.SourceItemId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(anchor => new { anchor.ProjectId, anchor.SourceManifestId, anchor.SourceItemId });
        builder.HasIndex(anchor => new { anchor.ProjectId, anchor.AnchorKind, anchor.ObservedAtUtc });
        builder.HasIndex(anchor => anchor.QuoteHash);
        builder.HasIndex(anchor => anchor.SourceHash);
    }
}

internal sealed class CognitiveMemoryClaimRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryClaimRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryClaimRecord> builder)
    {
        builder.ToTable("CognitiveMemory_Claims");
        builder.HasKey(claim => claim.Id);
        builder.Property(claim => claim.ClaimText).HasColumnType("TEXT");
        builder.Property(claim => claim.SubjectKey).HasMaxLength(240).IsRequired();
        builder.Property(claim => claim.PredicateKey).HasMaxLength(160).IsRequired();
        builder.Property(claim => claim.ObjectKey).HasMaxLength(240).IsRequired();
        builder.Property(claim => claim.AlgorithmVersion).HasMaxLength(120).IsRequired();
        builder.Property(claim => claim.ConcurrencyToken).IsConcurrencyToken();
        builder
            .HasOne<CognitiveMemoryRecord>()
            .WithMany()
            .HasForeignKey(claim => claim.MemoryRecordId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<CognitiveMemoryContextFrameRecord>()
            .WithMany()
            .HasForeignKey(claim => claim.PrimaryContextFrameId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<CognitiveMemoryScoreEvaluationTraceRecord>()
            .WithMany()
            .HasForeignKey(claim => claim.CurrentBeliefScoreEvaluationTraceId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(claim => new { claim.ProjectId, claim.ClaimKind, claim.CurrentBeliefState, claim.ValidationState });
        builder.HasIndex(claim => new { claim.ProjectId, claim.SubjectKey, claim.PredicateKey, claim.ObjectKey });
        builder.HasIndex(claim => claim.PrimaryContextFrameId);
        builder.HasIndex(claim => claim.CurrentBeliefScoreEvaluationTraceId);
        builder.HasIndex(claim => claim.MemoryRecordId);
    }
}

internal sealed class CognitiveMemoryClaimEvidenceLinkRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryClaimEvidenceLinkRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryClaimEvidenceLinkRecord> builder)
    {
        builder.ToTable("CognitiveMemory_ClaimEvidenceLinks");
        builder.HasKey(link => link.Id);
        builder.Property(link => link.Explanation).HasColumnType("TEXT");
        builder
            .HasOne<CognitiveMemoryClaimRecord>()
            .WithMany()
            .HasForeignKey(link => link.ClaimId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne<CognitiveMemoryEvidenceAnchorRecord>()
            .WithMany()
            .HasForeignKey(link => link.EvidenceAnchorId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(link => new { link.ClaimId, link.EvidenceAnchorId, link.Direction }).IsUnique();
        builder.HasIndex(link => new { link.EvidenceAnchorId, link.Direction });
    }
}

internal sealed class CognitiveMemoryBeliefStateRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryBeliefStateRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryBeliefStateRecord> builder)
    {
        builder.ToTable("CognitiveMemory_BeliefStates");
        builder.HasKey(belief => belief.Id);
        builder.Property(belief => belief.Explanation).HasColumnType("TEXT");
        builder.Property(belief => belief.AlgorithmVersion).HasMaxLength(120).IsRequired();
        builder
            .HasOne<CognitiveMemoryClaimRecord>()
            .WithMany()
            .HasForeignKey(belief => belief.ClaimId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne<CognitiveMemoryScoreEvaluationTraceRecord>()
            .WithMany()
            .HasForeignKey(belief => belief.ScoreEvaluationTraceId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(belief => new { belief.ClaimId, belief.CalculatedAtUtc });
        builder.HasIndex(belief => new { belief.StateKind, belief.ProjectionBucket });
        builder.HasIndex(belief => belief.ScoreEvaluationTraceId);
    }
}

internal sealed class CognitiveMemoryEntityRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryEntityRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryEntityRecord> builder)
    {
        builder.ToTable("CognitiveMemory_Entities");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.CanonicalName).HasMaxLength(300).IsRequired();
        builder.Property(entity => entity.CanonicalNameKey).HasMaxLength(300).IsRequired();
        builder.Property(entity => entity.ConcurrencyToken).IsConcurrencyToken();
        builder
            .HasOne<CognitiveMemoryContextFrameRecord>()
            .WithMany()
            .HasForeignKey(entity => entity.PrimaryContextFrameId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<CognitiveMemoryScoreEvaluationTraceRecord>()
            .WithMany()
            .HasForeignKey(entity => entity.ConfidenceScoreEvaluationTraceId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(entity => new { entity.ProjectId, entity.EntityKind, entity.CanonicalNameKey }).IsUnique();
        builder.HasIndex(entity => entity.PrimaryContextFrameId);
        builder.HasIndex(entity => entity.ConfidenceScoreEvaluationTraceId);
    }
}

internal sealed class CognitiveMemoryEntityAliasRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryEntityAliasRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryEntityAliasRecord> builder)
    {
        builder.ToTable("CognitiveMemory_EntityAliases");
        builder.HasKey(alias => alias.Id);
        builder.Property(alias => alias.Alias).HasMaxLength(300).IsRequired();
        builder.Property(alias => alias.AliasKey).HasMaxLength(300).IsRequired();
        builder
            .HasOne<CognitiveMemoryEntityRecord>()
            .WithMany()
            .HasForeignKey(alias => alias.EntityId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(alias => new { alias.ProjectId, alias.EntityKind, alias.AliasKey }).IsUnique();
        builder.HasIndex(alias => alias.EntityId);
    }
}

internal sealed class CognitiveMemoryContextFrameRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryContextFrameRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryContextFrameRecord> builder)
    {
        builder.ToTable("CognitiveMemory_ContextFrames");
        builder.HasKey(frame => frame.Id);
        builder.Property(frame => frame.DisplayName).HasMaxLength(300).IsRequired();
        builder.Property(frame => frame.ConcurrencyToken).IsConcurrencyToken();
        builder
            .HasOne<CognitiveMemoryScoreEvaluationTraceRecord>()
            .WithMany()
            .HasForeignKey(frame => frame.ConfidenceScoreEvaluationTraceId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(frame => new { frame.ProjectId, frame.FrameKind, frame.DisplayName });
        builder.HasIndex(frame => frame.ConfidenceScoreEvaluationTraceId);
    }
}

internal sealed class CognitiveMemoryContextFrameDimensionRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryContextFrameDimensionRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryContextFrameDimensionRecord> builder)
    {
        builder.ToTable("CognitiveMemory_ContextFrameDimensions");
        builder.HasKey(dimension => dimension.Id);
        builder.Property(dimension => dimension.Value).HasMaxLength(300).IsRequired();
        builder.Property(dimension => dimension.ValueKey).HasMaxLength(300).IsRequired();
        builder
            .HasOne<CognitiveMemoryContextFrameRecord>()
            .WithMany()
            .HasForeignKey(dimension => dimension.ContextFrameId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(dimension => new { dimension.ContextFrameId, dimension.DimensionKind, dimension.ValueKey }).IsUnique();
        builder.HasIndex(dimension => new { dimension.ProjectId, dimension.DimensionKind, dimension.ValueKey });
    }
}

internal sealed class CognitiveMemoryContextBoundaryRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryContextBoundaryRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryContextBoundaryRecord> builder)
    {
        builder.ToTable("CognitiveMemory_ContextBoundaries");
        builder.HasKey(boundary => boundary.Id);
        builder.Property(boundary => boundary.Explanation).HasColumnType("TEXT");
        builder
            .HasOne<CognitiveMemoryContextFrameRecord>()
            .WithMany()
            .HasForeignKey(boundary => boundary.SourceContextFrameId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<CognitiveMemoryContextFrameRecord>()
            .WithMany()
            .HasForeignKey(boundary => boundary.TargetContextFrameId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<CognitiveMemoryScoreEvaluationTraceRecord>()
            .WithMany()
            .HasForeignKey(boundary => boundary.ScoreEvaluationTraceId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(boundary => new
        {
            boundary.ProjectId,
            boundary.SourceContextFrameId,
            boundary.TargetContextFrameId,
            boundary.BoundaryKind
        }).IsUnique();
        builder.HasIndex(boundary => new { boundary.ProjectId, boundary.BoundaryPolicy });
        builder.HasIndex(boundary => boundary.ScoreEvaluationTraceId);
    }
}

internal sealed class CognitiveMemoryMutationCommandRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryMutationCommandRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryMutationCommandRecord> builder)
    {
        builder.ToTable("CognitiveMemory_MutationCommands");
        builder.HasKey(command => command.Id);
        builder.Property(command => command.ActorId).HasMaxLength(160).IsRequired();
        builder.Property(command => command.IdempotencyKey).HasMaxLength(240).IsRequired();
        builder.Property(command => command.AffectedMemoryRecordIdsJson).HasColumnType("TEXT");
        builder.Property(command => command.AffectedClaimIdsJson).HasColumnType("TEXT");
        builder.Property(command => command.EvidenceAnchorIdsJson).HasColumnType("TEXT");
        builder.Property(command => command.PayloadJson).HasColumnType("TEXT");
        builder.Property(command => command.ExpectedVersionToken).HasMaxLength(120).IsRequired();
        builder.Property(command => command.ReviewReason).HasColumnType("TEXT");
        builder.Property(command => command.ResultVersionToken).HasMaxLength(120).IsRequired();
        builder.Property(command => command.ConcurrencyToken).IsConcurrencyToken();
        builder.HasIndex(command => new { command.ProjectId, command.IdempotencyKey }).IsUnique();
        builder.HasIndex(command => new { command.ProjectId, command.CommandKind, command.Status, command.CreatedAtUtc });
        builder.HasIndex(command => new { command.ActorKind, command.ActorId });
    }
}

internal sealed class CognitiveMemoryMutationAuditEventRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryMutationAuditEventRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryMutationAuditEventRecord> builder)
    {
        builder.ToTable("CognitiveMemory_MutationAuditEvents");
        builder.HasKey(audit => audit.Id);
        builder.Property(audit => audit.Message).HasColumnType("TEXT");
        builder
            .HasOne<CognitiveMemoryMutationCommandRecord>()
            .WithMany()
            .HasForeignKey(audit => audit.MutationCommandId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(audit => new { audit.MutationCommandId, audit.Sequence }).IsUnique();
        builder.HasIndex(audit => new { audit.ProjectId, audit.EventKind, audit.CreatedAtUtc });
    }
}
