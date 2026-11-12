using CanDoItAll.Processes.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Processes.Persistence;

public sealed class ProcessDecisionRefreshDeliveryEntity {
    public Guid RunId { get; set; }
    public Guid AdmissionCommandId { get; set; }
    public Guid RootRunId { get; set; }
    public int ProtocolVersion { get; set; } = 1;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public DateTimeOffset DueAtUtc { get; set; }
    public Guid? LeaseToken { get; set; }
    public long LeaseGeneration { get; set; }
    public DateTimeOffset? LeaseExpiresAtUtc { get; set; }
    public long ProgressRevision { get; set; }
    public long WakeRevision { get; set; }
    public ProcessDecisionRefreshDeliveryDisposition Disposition { get; set; }
    public ProcessDecisionRefreshDeliveryReason Reason { get; set; }

    internal ProcessDecisionRefreshDeliveryClaim ReadClaim() {
        if (ProtocolVersion != 1 || Disposition != ProcessDecisionRefreshDeliveryDisposition.Ready ||
                new ProcessDecisionRefreshDeliveryCheckpoint(Reason).Disposition != Disposition ||
                LeaseToken is not { } token || LeaseExpiresAtUtc is not { } expiry || DueAtUtc != expiry) {
            throw new InvalidDataException("Refresh delivery contains invalid retained scheduling evidence.");
        }
        ProcessDecisionRefreshDeliveryClaim claim = new(new(RootRunId), new(RunId), new(AdmissionCommandId), token,
            LeaseGeneration, ProgressRevision, WakeRevision, expiry);
        claim.Validate();
        return claim;
    }
}

internal sealed class ProcessDecisionRefreshDeliveryConfiguration : IEntityTypeConfiguration<ProcessDecisionRefreshDeliveryEntity> {
    public void Configure(EntityTypeBuilder<ProcessDecisionRefreshDeliveryEntity> builder) {
        builder.ToTable("process_decision_refresh_delivery", table => {
            table.HasCheckConstraint("CK_process_refresh_delivery_identity", """
                "ProtocolVersion" = 1 AND "RunId" <> '00000000-0000-0000-0000-000000000000'::uuid
                AND "RootRunId" <> '00000000-0000-0000-0000-000000000000'::uuid
                AND "AdmissionCommandId" <> '00000000-0000-0000-0000-000000000000'::uuid
                """);
            table.HasCheckConstraint("CK_process_refresh_delivery_lease", """
                "LeaseGeneration" >= 0 AND "ProgressRevision" >= 0 AND "WakeRevision" >= 0
                AND (("LeaseToken" IS NULL AND "LeaseExpiresAtUtc" IS NULL)
                    OR ("LeaseToken" IS NOT NULL AND "LeaseToken" <> '00000000-0000-0000-0000-000000000000'::uuid
                        AND "LeaseGeneration" > 0 AND "LeaseExpiresAtUtc" IS NOT NULL
                        AND "DueAtUtc" = "LeaseExpiresAtUtc" AND "Disposition" = 0))
                """);
            table.HasCheckConstraint("CK_process_refresh_delivery_outcome", """
                ("Disposition" = 0 AND "Reason" BETWEEN 0 AND 4)
                OR ("Disposition" = 1 AND "Reason" = 5)
                OR ("Disposition" = 2 AND "Reason" BETWEEN 6 AND 9)
                OR ("Disposition" = 3 AND "Reason" BETWEEN 10 AND 12)
                """);
        });
        builder.HasKey(row => new { row.RunId, row.AdmissionCommandId });
        builder.HasOne<ProcessRuntimeIdempotencyEntity>().WithMany().HasForeignKey(row => new { row.RunId, CommandId = row.AdmissionCommandId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProcessRuntimeStateEntity>().WithMany().HasForeignKey(row => row.RootRunId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(row => row.CreatedAtUtc).HasDefaultValueSql("clock_timestamp()");
        builder.Property(row => row.UpdatedAtUtc).HasDefaultValueSql("clock_timestamp()");
        builder.Property(row => row.DueAtUtc).HasDefaultValueSql("clock_timestamp()");
        builder.HasIndex(row => new { row.DueAtUtc, row.RunId, row.AdmissionCommandId })
            .HasDatabaseName("IX_process_refresh_delivery_due").HasFilter("\"Disposition\" = 0");
    }
}
