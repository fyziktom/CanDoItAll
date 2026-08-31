using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.AgentFramework.ProviderHistory.Persistence;

internal sealed class HistoryPartitionConfiguration : IEntityTypeConfiguration<HistoryPartitionRow> {
    public void Configure(EntityTypeBuilder<HistoryPartitionRow> b) {
        b.ToTable("ProviderHistory_Partitions");
        b.HasKey(x => x.Id);
        b.Property(x => x.SecurityPartition).HasMaxLength(128);
    }
}

internal sealed class HistoryStorageIdentityConfiguration : IEntityTypeConfiguration<HistoryStorageIdentity> {
    public void Configure(EntityTypeBuilder<HistoryStorageIdentity> b) {
        b.ToTable("ProviderHistory_StorageIdentity");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.HasOne<HistoryPartitionRow>().WithMany().HasForeignKey(x => x.PartitionId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class HistoryPolicyConfiguration : IEntityTypeConfiguration<HistoryPolicyRow> {
    public void Configure(EntityTypeBuilder<HistoryPolicyRow> b) {
        b.ToTable("ProviderHistory_Policies", table => table.HasCheckConstraint("CK_ProviderHistory_Quota",
            """
            "UsedDetailBytes" >= 0 AND "DetailQuotaBytes" > 0 AND "MetadataRetentionDays" BETWEEN 1 AND 3650 AND "DetailRetentionDays" BETWEEN 1 AND "MetadataRetentionDays" AND "MaximumTextBytes" BETWEEN 1 AND 131072 AND "BatchSize" BETWEEN 1 AND 1000
            """));
        b.HasKey(x => x.PartitionId);
        b.Property(x => x.ConcurrencyToken).IsConcurrencyToken();
        b.HasOne<HistoryPartitionRow>().WithMany().HasForeignKey(x => x.PartitionId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class HistoryPolicyAuditConfiguration : IEntityTypeConfiguration<HistoryPolicyAuditRow> {
    public void Configure(EntityTypeBuilder<HistoryPolicyAuditRow> b) {
        b.ToTable("ProviderHistory_PolicyAudit");
        b.HasKey(x => x.Id);
        b.Property(x => x.Policy).HasColumnType("jsonb").HasConversion(
            value => JsonSerializer.Serialize(value, (JsonSerializerOptions?)null),
            value => JsonSerializer.Deserialize<HistoryPolicy>(value, (JsonSerializerOptions?)null)!);
        b.Property(x => x.Caller).HasColumnType("jsonb").HasConversion(
            value => JsonSerializer.Serialize(value, (JsonSerializerOptions?)null),
            value => JsonSerializer.Deserialize<HistoryCaller>(value, (JsonSerializerOptions?)null)!);
        b.HasIndex(x => new { x.PartitionId, x.Version }).IsUnique();
        b.HasOne<HistoryPartitionRow>().WithMany().HasForeignKey(x => x.PartitionId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class HistoryCheckpointConfiguration : IEntityTypeConfiguration<HistoryCheckpointRow> {
    public void Configure(EntityTypeBuilder<HistoryCheckpointRow> b) {
        b.ToTable("ProviderHistory_Checkpoints");
        b.HasKey(x => new { x.PartitionId, x.SourceKind });
        b.Property(x => x.ConcurrencyToken).IsConcurrencyToken();
        b.Property(x => x.Cursor).HasMaxLength(4096);
        b.Property(x => x.FailureCode).HasMaxLength(128);
        b.HasOne<HistoryPartitionRow>().WithMany().HasForeignKey(x => x.PartitionId).OnDelete(DeleteBehavior.Restrict);
    }
}
