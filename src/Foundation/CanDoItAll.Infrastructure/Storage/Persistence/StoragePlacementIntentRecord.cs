using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Infrastructure.Storage;

public sealed class StoragePlacementIntentRecord {
    public Guid Id { get; set; }
    public Guid StorageId { get; set; }
    public Guid? ProjectId { get; set; }
    public string RequestFingerprint { get; set; } = string.Empty;
    public string PlanJson { get; set; } = string.Empty;
    public StorageStablePlacementState State { get; set; }
    public bool DeletionRequested { get; set; }
    public bool WriteAcknowledged { get; set; }
    public bool ExternalDispatchConfirmedStopped { get; set; }
    public string ReceiptJson { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

internal sealed class StoragePlacementIntentRecordConfiguration : IEntityTypeConfiguration<StoragePlacementIntentRecord> {
    public void Configure(EntityTypeBuilder<StoragePlacementIntentRecord> builder) {
        builder.ToTable("Storage_PlacementIntents");
        builder.HasKey(row => row.Id);
        builder.Property(row => row.RequestFingerprint).HasMaxLength(64).IsRequired();
        builder.Property(row => row.PlanJson).HasColumnType("TEXT").IsRequired();
        builder.Property(row => row.ReceiptJson).HasColumnType("TEXT").IsRequired();
        builder.HasIndex(row => new { row.ProjectId, row.State, row.Id });
        builder.HasIndex(row => new { row.StorageId, row.State, row.Id });
    }
}
