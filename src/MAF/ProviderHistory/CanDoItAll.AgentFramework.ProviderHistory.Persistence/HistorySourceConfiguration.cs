using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.AgentFramework.ProviderHistory.Persistence;

internal sealed class HistorySourceConfiguration : IEntityTypeConfiguration<HistorySourceRow> {
    public void Configure(EntityTypeBuilder<HistorySourceRow> b) {
        b.ToTable("ProviderHistory_Sources");
        b.HasKey(x => x.Id);
        b.HasAlternateKey(x => new { x.Id, x.PartitionId });
        b.Property(x => x.ConcurrencyToken).IsConcurrencyToken();
        b.Property(x => x.OwnerId).HasMaxLength(256);
        b.Property(x => x.EvidenceId).HasMaxLength(256);
        b.HasIndex(x => new { x.PartitionId, x.Kind, x.OwnerId, x.EvidenceId }).IsUnique();
        b.HasOne<HistoryPartitionRow>().WithMany().HasForeignKey(x => x.PartitionId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class HistoryOwnerConfiguration : IEntityTypeConfiguration<HistoryOwnerRow> {
    public void Configure(EntityTypeBuilder<HistoryOwnerRow> b) {
        b.ToTable("ProviderHistory_Owners");
        b.HasKey(x => new { x.SourceId, x.EntryId });
        b.HasOne<HistorySourceRow>().WithMany().HasForeignKey(x => new { x.SourceId, x.PartitionId })
            .HasPrincipalKey(x => new { x.Id, x.PartitionId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<HistoryEntryRow>().WithMany().HasForeignKey(x => new { x.EntryId, x.PartitionId })
            .HasPrincipalKey(x => new { x.Id, x.PartitionId }).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class HistoryOutboxConfiguration : IEntityTypeConfiguration<HistoryOutboxRow> {
    public void Configure(EntityTypeBuilder<HistoryOutboxRow> b) {
        b.ToTable("ProviderHistory_Outbox");
        b.HasKey(x => x.Id);
        b.Property(x => x.FailureCode).HasMaxLength(128);
        b.Property(x => x.Mutation).HasColumnType("jsonb").HasConversion(
            value => JsonSerializer.Serialize(value, (JsonSerializerOptions?)null),
            value => JsonSerializer.Deserialize<HistorySourceMutation>(value, (JsonSerializerOptions?)null)!);
        b.HasIndex(x => new { x.PartitionId, x.RetryAfterUtc, x.CreatedAtUtc, x.Id });
        b.HasOne<HistoryPartitionRow>().WithMany().HasForeignKey(x => x.PartitionId).OnDelete(DeleteBehavior.Restrict);
    }
}
