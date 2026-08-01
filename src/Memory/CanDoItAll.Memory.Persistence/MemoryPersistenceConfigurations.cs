using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Memory.Persistence;

internal sealed class MemoryProviderProfileEntityConfiguration : IEntityTypeConfiguration<MemoryProviderProfileEntity>
{
    public void Configure(EntityTypeBuilder<MemoryProviderProfileEntity> builder)
    {
        builder.ToTable("Memory_ProviderProfiles");
        builder.HasKey(item => item.InstanceId);
        builder.Property(item => item.InstanceId).HasMaxLength(180).IsRequired();
        builder.Property(item => item.DisplayName).HasMaxLength(240).IsRequired();
        builder.Property(item => item.SelectionTagsJson).HasColumnType("TEXT").IsRequired();
        builder.Property(item => item.ManifestJson).HasColumnType("TEXT").IsRequired();
        builder.HasIndex(item => new
        {
            item.DriverKind,
            item.IsEnabled
        });
    }
}

internal sealed class MemoryOperationLedgerEntityConfiguration : IEntityTypeConfiguration<MemoryOperationLedgerEntity>
{
    public void Configure(EntityTypeBuilder<MemoryOperationLedgerEntity> builder)
    {
        builder.ToTable("Memory_OperationLedger");
        builder.HasKey(item => item.RecordId);
        builder.HasIndex(item => item.OperationId).IsUnique();
        builder.Property(item => item.ProviderInstanceId).HasMaxLength(180).IsRequired();
        builder.Property(item => item.CapabilityId).HasMaxLength(180).IsRequired();
        builder.Property(item => item.RecordJson).HasColumnType("TEXT").IsRequired();
        builder.HasIndex(item => new
        {
            item.ProviderInstanceId,
            item.Status,
            item.UpdatedAtUtc
        });
        builder.HasIndex(item => new
        {
            item.Status,
            item.ExpiresAtUtc,
            item.ForgetAtUtc
        });
    }
}

internal sealed class MemoryFeedbackLedgerEntityConfiguration : IEntityTypeConfiguration<MemoryFeedbackLedgerEntity>
{
    public void Configure(EntityTypeBuilder<MemoryFeedbackLedgerEntity> builder)
    {
        builder.ToTable("Memory_FeedbackLedger");
        builder.HasKey(item => item.FeedbackRecordId);
        builder.Property(item => item.ProviderInstanceId).HasMaxLength(180).IsRequired();
        builder.Property(item => item.RecordJson).HasColumnType("TEXT").IsRequired();
        builder.HasIndex(item => new
        {
            item.ProviderInstanceId,
            item.Status,
            item.UpdatedAtUtc
        });
        builder.HasIndex(item => new
        {
            item.Status,
            item.ExpiresAtUtc,
            item.ForgetAtUtc
        });
    }
}
