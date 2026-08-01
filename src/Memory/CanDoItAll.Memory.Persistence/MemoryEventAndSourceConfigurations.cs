using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Memory.Persistence;

internal sealed class MemoryEventInboxLedgerEntityConfiguration : IEntityTypeConfiguration<MemoryEventInboxLedgerEntity>
{
    public void Configure(EntityTypeBuilder<MemoryEventInboxLedgerEntity> builder)
    {
        builder.ToTable("Memory_EventInbox");
        builder.HasKey(item => item.InboxRecordId);
        builder.Property(item => item.ProviderInstanceId).HasMaxLength(180).IsRequired();
        builder.Property(item => item.DedupeKey).HasMaxLength(260).IsRequired();
        builder.Property(item => item.RecordJson).HasColumnType("TEXT").IsRequired();
        builder.HasIndex(item => item.DedupeKey).IsUnique();
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

internal sealed class MemoryEventOutboxLedgerEntityConfiguration : IEntityTypeConfiguration<MemoryEventOutboxLedgerEntity>
{
    public void Configure(EntityTypeBuilder<MemoryEventOutboxLedgerEntity> builder)
    {
        builder.ToTable("Memory_EventOutbox");
        builder.HasKey(item => item.OutboxRecordId);
        builder.Property(item => item.ProviderInstanceId).HasMaxLength(180).IsRequired();
        builder.Property(item => item.DedupeKey).HasMaxLength(260).IsRequired();
        builder.Property(item => item.PayloadKind).HasMaxLength(120).IsRequired();
        builder.Property(item => item.RecordJson).HasColumnType("TEXT").IsRequired();
        builder.HasIndex(item => new
        {
            item.ProviderInstanceId,
            item.Status,
            item.UpdatedAtUtc
        });
    }
}

internal sealed class MemorySourceRequestLedgerEntityConfiguration : IEntityTypeConfiguration<MemorySourceRequestLedgerEntity>
{
    public void Configure(EntityTypeBuilder<MemorySourceRequestLedgerEntity> builder)
    {
        builder.ToTable("Memory_SourceRequests");
        builder.HasKey(item => item.JobId);
        builder.Property(item => item.ProviderInstanceId).HasMaxLength(180).IsRequired();
        builder.Property(item => item.RecordJson).HasColumnType("TEXT").IsRequired();
        builder.HasIndex(item => new
        {
            item.ProviderInstanceId,
            item.Status,
            item.UpdatedAtUtc
        });
    }
}
