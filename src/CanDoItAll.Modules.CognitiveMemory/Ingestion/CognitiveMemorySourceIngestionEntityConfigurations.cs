using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Modules.CognitiveMemory;

internal sealed class CognitiveMemorySourceItemLayoutRecordConfiguration : IEntityTypeConfiguration<CognitiveMemorySourceItemLayoutRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemorySourceItemLayoutRecord> builder)
    {
        builder.ToTable("CognitiveMemory_SourceItemLayouts");
        builder.HasKey(layout => layout.Id);
        builder.Property(layout => layout.SurfaceKind)
            .HasConversion(
                kind => kind.Value,
                value => new CognitiveMemorySourceSurfaceKind(value))
            .HasMaxLength(120)
            .IsRequired();
        builder.Property(layout => layout.MetadataJson).HasColumnType("TEXT");
        builder
            .HasOne<CognitiveMemorySourceItemRecord>()
            .WithMany()
            .HasForeignKey(layout => layout.SourceItemId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(layout => layout.SourceItemId).IsUnique();
        builder.HasIndex(layout => new { layout.ProjectId, layout.SurfaceKind });
    }
}

internal sealed class CognitiveMemorySourceItemGraphLinkRecordConfiguration : IEntityTypeConfiguration<CognitiveMemorySourceItemGraphLinkRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemorySourceItemGraphLinkRecord> builder)
    {
        builder.ToTable("CognitiveMemory_SourceItemGraphLinks");
        builder.HasKey(link => link.Id);
        builder.Property(link => link.SourceItemKey).HasMaxLength(500).IsRequired();
        builder.Property(link => link.TargetSourceItemKey).HasMaxLength(500).IsRequired();
        builder.Property(link => link.LinkKind)
            .HasConversion(
                kind => kind.Value,
                value => new CognitiveMemorySourceLinkKind(value))
            .HasMaxLength(120)
            .IsRequired();
        builder
            .HasOne<CognitiveMemorySourceManifestRecord>()
            .WithMany()
            .HasForeignKey(link => link.SourceManifestId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne<CognitiveMemorySourceItemRecord>()
            .WithMany()
            .HasForeignKey(link => link.SourceItemId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(link => new { link.SourceManifestId, link.SourceItemKey, link.TargetSourceItemKey, link.LinkKind }).IsUnique();
        builder.HasIndex(link => new { link.ProjectId, link.LinkKind });
    }
}

internal sealed class CognitiveMemorySourceItemContextHintRecordConfiguration : IEntityTypeConfiguration<CognitiveMemorySourceItemContextHintRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemorySourceItemContextHintRecord> builder)
    {
        builder.ToTable("CognitiveMemory_SourceItemContextHints");
        builder.HasKey(hint => hint.Id);
        builder.Property(hint => hint.ValueKey).HasMaxLength(300).IsRequired();
        builder
            .HasOne<CognitiveMemorySourceItemRecord>()
            .WithMany()
            .HasForeignKey(hint => hint.SourceItemId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne<CognitiveMemoryContextFrameRecord>()
            .WithMany()
            .HasForeignKey(hint => hint.ContextFrameId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(hint => new { hint.SourceItemId, hint.ContextFrameId }).IsUnique();
        builder.HasIndex(hint => new { hint.ProjectId, hint.DimensionKind, hint.ValueKey });
    }
}

internal sealed class CognitiveMemorySourceTombstoneRecordConfiguration : IEntityTypeConfiguration<CognitiveMemorySourceTombstoneRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemorySourceTombstoneRecord> builder)
    {
        builder.ToTable("CognitiveMemory_SourceTombstones");
        builder.HasKey(tombstone => tombstone.Id);
        builder.Property(tombstone => tombstone.SourceSystem).HasMaxLength(80).IsRequired();
        builder.Property(tombstone => tombstone.SourceScopeKey).HasMaxLength(240).IsRequired();
        builder.Property(tombstone => tombstone.SourceItemKey).HasMaxLength(500).IsRequired();
        builder.Property(tombstone => tombstone.Reason).HasMaxLength(500).IsRequired();
        builder.Property(tombstone => tombstone.ConcurrencyToken).IsConcurrencyToken();
        builder
            .HasOne<CognitiveMemorySourceItemRecord>()
            .WithMany()
            .HasForeignKey(tombstone => tombstone.PreviousSourceItemId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<CognitiveMemorySourceManifestRecord>()
            .WithMany()
            .HasForeignKey(tombstone => tombstone.DetectedInManifestId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(tombstone => new { tombstone.SourceSystem, tombstone.SourceScopeKey, tombstone.SourceItemKey, tombstone.DetectedInManifestId }).IsUnique();
        builder.HasIndex(tombstone => new { tombstone.ProjectId, tombstone.SourceSystem, tombstone.TombstonedAtUtc });
    }
}

internal sealed class CognitiveMemorySourceScanFailureRecordConfiguration : IEntityTypeConfiguration<CognitiveMemorySourceScanFailureRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemorySourceScanFailureRecord> builder)
    {
        builder.ToTable("CognitiveMemory_SourceScanFailures");
        builder.HasKey(failure => failure.Id);
        builder.Property(failure => failure.SourceSystem).HasMaxLength(80).IsRequired();
        builder.Property(failure => failure.SourceScopeKey).HasMaxLength(240).IsRequired();
        builder.Property(failure => failure.CursorHash).HasMaxLength(128).IsRequired();
        builder.Property(failure => failure.ExceptionCategory).HasMaxLength(160).IsRequired();
        builder.Property(failure => failure.Message).HasColumnType("TEXT");
        builder
            .HasOne<CognitiveMemoryRunRecord>()
            .WithMany()
            .HasForeignKey(failure => failure.RunId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(failure => failure.RunId);
        builder.HasIndex(failure => new { failure.ProjectId, failure.SourceSystem, failure.CreatedAtUtc });
        builder.HasIndex(failure => new { failure.SourceSystem, failure.SourceScopeKey, failure.ExceptionCategory });
    }
}
