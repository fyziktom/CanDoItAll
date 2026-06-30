using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Modules.CognitiveMemory;

internal sealed class CognitiveMemoryRecordEvidenceAnchorRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryRecordEvidenceAnchorRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryRecordEvidenceAnchorRecord> builder)
    {
        builder.ToTable("CognitiveMemory_RecordEvidenceAnchors");
        builder.HasKey(link => link.Id);
        builder.Property(link => link.Summary).HasColumnType("TEXT");
        builder
            .HasOne<CognitiveMemoryRecord>()
            .WithMany()
            .HasForeignKey(link => link.MemoryRecordId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne<CognitiveMemoryEvidenceAnchorRecord>()
            .WithMany()
            .HasForeignKey(link => link.EvidenceAnchorId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(link => new { link.MemoryRecordId, link.EvidenceAnchorId, link.EvidenceRole }).IsUnique();
        builder.HasIndex(link => new { link.EvidenceAnchorId, link.EvidenceRole });
    }
}

internal sealed class CognitiveMemoryRelationEvidenceRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryRelationEvidenceRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryRelationEvidenceRecord> builder)
    {
        builder.ToTable("CognitiveMemory_RelationEvidence");
        builder.HasKey(evidence => evidence.Id);
        builder.Property(evidence => evidence.Summary).HasColumnType("TEXT");
        builder
            .HasOne<CognitiveMemoryRelationRecord>()
            .WithMany()
            .HasForeignKey(evidence => evidence.RelationId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne<CognitiveMemoryEvidenceAnchorRecord>()
            .WithMany()
            .HasForeignKey(evidence => evidence.EvidenceAnchorId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(evidence => new { evidence.RelationId, evidence.EvidenceAnchorId, evidence.Direction }).IsUnique();
        builder.HasIndex(evidence => new { evidence.EvidenceAnchorId, evidence.Direction });
    }
}

internal sealed class CognitiveMemoryProjectionRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryProjectionRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryProjectionRecord> builder)
    {
        builder.ToTable("CognitiveMemory_Projections");
        builder.HasKey(projection => projection.Id);
        builder.Property(projection => projection.TargetProviderName).HasMaxLength(120).IsRequired();
        builder.Property(projection => projection.CollectionName).HasMaxLength(240).IsRequired();
        builder.Property(projection => projection.PointId).HasMaxLength(500).IsRequired();
        builder.Property(projection => projection.ProjectionProfileId).HasMaxLength(160).IsRequired();
        builder.Property(projection => projection.EmbeddingProfileId).HasMaxLength(160).IsRequired();
        builder.Property(projection => projection.ProjectionSchemaVersion).HasMaxLength(80).IsRequired();
        builder.Property(projection => projection.AlgorithmVersion).HasMaxLength(120).IsRequired();
        builder.Property(projection => projection.SourceHash).HasMaxLength(128).IsRequired();
        builder.Property(projection => projection.PayloadHash).HasMaxLength(128).IsRequired();
        builder.Property(projection => projection.FailureCode).HasMaxLength(120).IsRequired();
        builder.Property(projection => projection.FailureMessage).HasColumnType("TEXT");
        builder.Property(projection => projection.ConcurrencyToken).IsConcurrencyToken();
        builder
            .HasOne<CognitiveMemoryRecord>()
            .WithMany()
            .HasForeignKey(projection => projection.MemoryRecordId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(projection => new
        {
            projection.MemoryRecordId,
            projection.ProjectionStoreKind,
            projection.ProjectionKind,
            projection.ProjectionProfileId,
            projection.EmbeddingProfileId
        }).IsUnique();
        builder.HasIndex(projection => new { projection.ProjectId, projection.CollectionName, projection.Status });
        builder.HasIndex(projection => new { projection.ProjectId, projection.RebuildRequired, projection.StaleReason });
        builder.HasIndex(projection => projection.SourceHash);
        builder.HasIndex(projection => projection.PayloadHash);
        builder.HasIndex(projection => projection.PointId).IsUnique();
    }
}
