using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.AgentFramework.ProviderHistory.Persistence;

public sealed class HistoryHostLeaseRow {
    public Guid Id { get; set; }
    public Guid PartitionId { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
}

internal sealed class HistoryHostLeaseConfiguration : IEntityTypeConfiguration<HistoryHostLeaseRow> {
    public void Configure(EntityTypeBuilder<HistoryHostLeaseRow> b) {
        b.ToTable("ProviderHistory_HostLeases");
        b.HasKey(row => row.Id);
        b.HasAlternateKey(row => new { row.Id, row.PartitionId });
        b.HasOne<HistoryPartitionRow>().WithMany().HasForeignKey(row => row.PartitionId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(row => new { row.PartitionId, row.ExpiresAtUtc });
    }
}
