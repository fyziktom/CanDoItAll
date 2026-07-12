using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Memory.Persistence;

internal sealed class MemoryWorkerLeaseEntity
{
    public int Phase { get; set; }

    public string OwnerId { get; set; } = string.Empty;

    public Guid LeaseToken { get; set; }

    public DateTimeOffset LeaseExpiresAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}

internal sealed class MemoryWorkerLeaseEntityConfiguration :
    IEntityTypeConfiguration<MemoryWorkerLeaseEntity>
{
    public void Configure(EntityTypeBuilder<MemoryWorkerLeaseEntity> builder)
    {
        builder.ToTable("Memory_WorkerLeases");
        builder.HasKey(item => item.Phase);
        builder.Property(item => item.OwnerId)
            .HasMaxLength(Hosting.MemoryWorkerLeaseOwnerId.MaxLength)
            .IsRequired();
        builder.HasIndex(item => item.LeaseExpiresAtUtc);
    }
}
