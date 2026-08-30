using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.AgentFramework.ProviderHistory.Persistence;

internal sealed class HistoryEntryConfiguration : IEntityTypeConfiguration<HistoryEntryRow> {
    public void Configure(EntityTypeBuilder<HistoryEntryRow> b) {
        b.ToTable("ProviderHistory_Entries", table => {
            table.HasCheckConstraint("CK_ProviderHistory_Granularity",
                """("Granularity" = 0 AND "AttemptId" IS NOT NULL AND "StartedAtUtc" IS NOT NULL) OR "Granularity" = 1""");
            table.HasCheckConstraint("CK_ProviderHistory_Tokens",
                """("InputTokens" IS NULL OR "InputTokens" >= 0) AND ("OutputTokens" IS NULL OR "OutputTokens" >= 0) AND ("Amount" IS NULL OR "Amount" >= 0)""");
        });
        b.HasKey(x => x.Id);
        b.HasAlternateKey(x => new { x.Id, x.PartitionId });
        b.Property(x => x.ConcurrencyToken).IsConcurrencyToken();
        b.Property(x => x.ProviderName).HasMaxLength(200);
        b.Property(x => x.ProviderKind).HasMaxLength(128);
        b.Property(x => x.RequestedModel).HasMaxLength(512);
        b.Property(x => x.ResolvedModel).HasMaxLength(512);
        b.Property(x => x.Issuer).HasMaxLength(512);
        b.Property(x => x.Subject).HasMaxLength(512);
        b.Property(x => x.CallerName).HasMaxLength(200);
        b.Property(x => x.CorrelationId).HasMaxLength(256);
        b.Property(x => x.ExternalReferenceType).HasMaxLength(HistoryExternalReference.MaximumTypeLength);
        b.Property(x => x.ExternalReferenceValue).HasMaxLength(HistoryExternalReference.MaximumValueLength);
        b.Property(x => x.Currency).HasMaxLength(3);
        b.Property(x => x.PriceHash).HasMaxLength(128);
        b.Property(x => x.PriceVersion).HasMaxLength(128);
        b.Property(x => x.PriceSourceRevision).HasMaxLength(256);
        b.Property(x => x.RemoteRequestId).HasMaxLength(256);
        b.Property(x => x.Amount).HasColumnType("numeric");
        b.HasIndex(x => new { x.PartitionId, x.AttemptId }).IsUnique().HasFilter("\"AttemptId\" IS NOT NULL");
        b.HasIndex(x => new { x.PartitionId, x.SortAtUtc, x.Id }).IsDescending(false, true, true);
        b.HasIndex(x => new { x.PartitionId, x.ProviderId, x.SortAtUtc, x.Id }).IsDescending(false, false, true, true);
        b.HasIndex(x => new { x.PartitionId, x.CredentialId, x.SortAtUtc, x.Id }).IsDescending(false, false, true, true);
        b.HasIndex(x => new { x.PartitionId, x.ExternalReferenceValue, x.ExternalReferenceType, x.SortAtUtc, x.Id })
            .IsDescending(false, false, false, true, true)
            .HasFilter("\"ExternalReferenceValue\" IS NOT NULL");
        b.HasIndex(x => new { x.PartitionId, x.Outcome, x.ExpiresAtUtc });
        b.HasOne<HistoryPartitionRow>().WithMany().HasForeignKey(x => x.PartitionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<HistoryHostLeaseRow>().WithMany().HasForeignKey(row => new { row.CaptureHostId, row.PartitionId })
            .HasPrincipalKey(row => new { row.Id, row.PartitionId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<HistoryDetailRow>().WithMany().HasForeignKey(x => new { x.InputDetailId, x.PartitionId })
            .HasPrincipalKey(x => new { x.Id, x.PartitionId }).OnDelete(DeleteBehavior.Restrict);
    }
}
