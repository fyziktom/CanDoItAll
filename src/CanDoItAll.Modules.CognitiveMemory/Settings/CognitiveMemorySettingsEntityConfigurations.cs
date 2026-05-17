using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Modules.CognitiveMemory;

internal sealed class CognitiveMemoryAutomationSettingsRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryAutomationSettingsRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryAutomationSettingsRecord> builder)
    {
        builder.ToTable("CognitiveMemory_AutomationSettings");
        builder.HasKey(settings => settings.Id);
        builder.Property(settings => settings.SettingsKey).HasMaxLength(80).IsRequired();
        builder.Property(settings => settings.NightlyLocalTime).HasMaxLength(16).IsRequired();
        builder.Property(settings => settings.ScheduledLocalTimes).HasColumnType("TEXT");
        builder.Property(settings => settings.UpdatedByActorId).HasMaxLength(160).IsRequired();
        builder.Property(settings => settings.ConcurrencyToken).IsConcurrencyToken();
        builder.HasIndex(settings => settings.SettingsKey).IsUnique();
    }
}

internal sealed class CognitiveMemoryExternalSourceIngestionRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryExternalSourceIngestionRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryExternalSourceIngestionRecord> builder)
    {
        builder.ToTable("CognitiveMemory_ExternalSourceIngestions");
        builder.HasKey(operation => operation.Id);
        builder.Property(operation => operation.Title).HasMaxLength(300).IsRequired();
        builder.Property(operation => operation.Locator).HasMaxLength(1000).IsRequired();
        builder.Property(operation => operation.ContentType).HasMaxLength(120).IsRequired();
        builder.Property(operation => operation.ProgressPercent).IsRequired();
        builder.Property(operation => operation.StatusMessage).HasMaxLength(500).IsRequired();
        builder.Property(operation => operation.FailureMessage).HasColumnType("TEXT");
        builder.Property(operation => operation.ConcurrencyToken).IsConcurrencyToken();
        builder
            .HasOne<CognitiveMemorySourceManifestRecord>()
            .WithMany()
            .HasForeignKey(operation => operation.SourceManifestId)
            .OnDelete(DeleteBehavior.SetNull);
        builder
            .HasOne<CognitiveMemorySourceItemRecord>()
            .WithMany()
            .HasForeignKey(operation => operation.SourceItemId)
            .OnDelete(DeleteBehavior.SetNull);
        builder
            .HasOne<CognitiveMemoryEvidenceAnchorRecord>()
            .WithMany()
            .HasForeignKey(operation => operation.EvidenceAnchorId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(operation => new { operation.ProjectId, operation.SourceKind, operation.CreatedAtUtc });
        builder.HasIndex(operation => new { operation.Status, operation.UpdatedAtUtc });
    }
}
