using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.AgentFramework.ProviderHistory.Persistence;

internal sealed class HistoryDetailConfiguration : IEntityTypeConfiguration<HistoryDetailRow> {
    public void Configure(EntityTypeBuilder<HistoryDetailRow> b) {
        b.ToTable("ProviderHistory_Details", table => table.HasCheckConstraint("CK_ProviderHistory_DetailPart",
            """("Part" = 0 AND "EntryId" IS NULL) OR ("Part" = 1 AND "EntryId" IS NOT NULL)"""));
        b.HasKey(x => x.Id);
        b.HasAlternateKey(x => new { x.Id, x.PartitionId });
        b.Property(x => x.ProtectedText).HasColumnType("text");
        b.HasIndex(x => new { x.PartitionId, x.RequestId, x.InputRevision, x.Part })
            .IsUnique().HasFilter("\"Part\" = 0");
        b.HasIndex(x => new { x.PartitionId, x.EntryId, x.Part }).IsUnique().HasFilter("\"Part\" = 1");
        b.HasIndex(x => new { x.PartitionId, x.ExpiresAtUtc, x.Id });
        b.HasOne<HistoryPartitionRow>().WithMany().HasForeignKey(x => x.PartitionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<HistoryEntryRow>().WithMany().HasForeignKey(x => new { x.EntryId, x.PartitionId })
            .HasPrincipalKey(x => new { x.Id, x.PartitionId }).OnDelete(DeleteBehavior.Restrict);
    }
}
