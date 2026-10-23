using CanDoItAll.Processes.Runtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Processes.Persistence;

public sealed class ProcessDecisionRefreshCaptureEntity {
    public Guid RunId { get; set; }
    public Guid ReservationCommandId { get; set; }
    public Guid RootRunId { get; set; }
    public Guid AdmissionCommandId { get; set; }
    public Guid BudgetId { get; set; }
    public Guid AllocationId { get; set; }
    public Guid DecisionId { get; set; }
    public int DecisionGeneration { get; set; }
    public long OriginalBudgetRevision { get; set; }
    public long GrantedWorkTicks { get; set; }
    public Guid ScopeId { get; set; }
    public Guid MaterialId { get; set; }
    public int MaterialGeneration { get; set; }
    public string InputFingerprint { get; set; } = string.Empty;
    public int ProtocolVersion { get; set; } = 1;
    public long Revision { get; set; }
    public ProcessDecisionRefreshCapturePhase Phase { get; set; }
    public Guid? InvocationId { get; set; }
    public ProcessDecisionRefreshCaptureOutcomeKind? Outcome { get; set; }
    public long? ConsumedWorkTicks { get; set; }
    public string? MaterialFingerprint { get; set; }
    public long? CapturedAtUtcTicks { get; set; }
    public ProcessDecisionRefreshCaptureDisposition? Disposition { get; set; }
    public Guid? SettlementCommandId { get; set; }
}

public sealed class ProcessDecisionRefreshCapturePayloadEntity {
    public Guid RunId { get; set; }
    public Guid ReservationCommandId { get; set; }
    public string PayloadJson { get; set; } = string.Empty;
}

internal sealed class ProcessDecisionRefreshCaptureConfiguration : IEntityTypeConfiguration<ProcessDecisionRefreshCaptureEntity> {
    public void Configure(EntityTypeBuilder<ProcessDecisionRefreshCaptureEntity> builder) {
        builder.ToTable("process_decision_refresh_captures", table => {
            table.HasCheckConstraint("CK_process_refresh_capture_identity", $$"""
                "ProtocolVersion" = 1 AND "RunId" <> '00000000-0000-0000-0000-000000000000'::uuid
                AND "RootRunId" <> '00000000-0000-0000-0000-000000000000'::uuid
                AND "ReservationCommandId" <> '00000000-0000-0000-0000-000000000000'::uuid
                AND "AdmissionCommandId" <> '00000000-0000-0000-0000-000000000000'::uuid
                AND "AdmissionCommandId" <> "ReservationCommandId"
                AND "MaterialId" <> '00000000-0000-0000-0000-000000000000'::uuid
                AND "MaterialGeneration" BETWEEN 2 AND {{ProcessDecisionGeneration.MaximumGenerations}}
                AND "OriginalBudgetRevision" > 0 AND "GrantedWorkTicks" BETWEEN 1 AND {{ProcessDecisionRefreshCaptureReservation.MaximumWork.Ticks}}
                AND "InputFingerprint" ~ '^sha256:[0-9a-f]{64}$'
                """);
            table.HasCheckConstraint("CK_process_refresh_capture_phase", """
                ("InvocationId" IS NULL OR "InvocationId" <> '00000000-0000-0000-0000-000000000000'::uuid)
                AND (("Phase" = 1 AND "Revision" = 1 AND "InvocationId" IS NULL AND "Outcome" IS NULL)
                    OR ("Phase" = 2 AND "Revision" = 2 AND "InvocationId" IS NOT NULL AND "Outcome" IS NULL)
                    OR ("Phase" IN (3, 4) AND "Outcome" IS NOT NULL
                        AND (("InvocationId" IS NULL AND "Outcome" = 7 AND "Revision" = "Phase" - 1)
                            OR ("InvocationId" IS NOT NULL AND "Outcome" BETWEEN 1 AND 6 AND "Revision" = "Phase"))))
                AND (("Phase" = 4 AND "Disposition" IS NOT NULL AND "Disposition" IN (1, 2) AND "SettlementCommandId" IS NOT NULL
                        AND "SettlementCommandId" <> '00000000-0000-0000-0000-000000000000'::uuid)
                    OR ("Phase" <> 4 AND "Disposition" IS NULL AND "SettlementCommandId" IS NULL))
                AND ("Disposition" IS NULL OR "Disposition" <> 1 OR "Outcome" = 1)
                """);
            table.HasCheckConstraint("CK_process_refresh_capture_outcome", $$"""
                (("Outcome" IS NULL AND "ConsumedWorkTicks" IS NULL)
                    OR ("Outcome" IS NOT NULL AND "Outcome" BETWEEN 1 AND 7 AND "ConsumedWorkTicks" IS NOT NULL AND "ConsumedWorkTicks" >= 0))
                AND ("Outcome" IS NULL OR "Outcome" <> 7 OR "ConsumedWorkTicks" = 0)
                AND (("Outcome" IS NOT NULL AND "Outcome" = 1 AND "ConsumedWorkTicks" <= "GrantedWorkTicks"
                        AND "MaterialFingerprint" IS NOT NULL AND "MaterialFingerprint" ~ '^[0-9A-F]{64}$'
                        AND "CapturedAtUtcTicks" IS NOT NULL AND "CapturedAtUtcTicks" BETWEEN 1 AND {{DateTimeOffset.MaxValue.UtcTicks}})
                    OR (("Outcome" IS NULL OR "Outcome" <> 1) AND "MaterialFingerprint" IS NULL AND "CapturedAtUtcTicks" IS NULL))
                """);
        });
        builder.HasKey(row => new { row.RunId, row.ReservationCommandId });
        builder.Property(row => row.Revision).IsConcurrencyToken();
        builder.Property(row => row.InputFingerprint).HasMaxLength(71).IsRequired();
        builder.Property(row => row.MaterialFingerprint).HasMaxLength(64);
        builder.HasIndex(row => new { row.RunId, row.AdmissionCommandId });
        builder.HasIndex(row => new { row.BudgetId, row.AllocationId }).IsUnique();
        builder.HasOne<ProcessRuntimeStateEntity>().WithMany().HasForeignKey(row => row.RootRunId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProcessRuntimeIdempotencyEntity>().WithMany().HasForeignKey(row => new { row.RunId, CommandId = row.ReservationCommandId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProcessRuntimeIdempotencyEntity>().WithMany().HasForeignKey(row => new { row.RunId, CommandId = row.AdmissionCommandId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProcessRuntimeIdempotencyEntity>().WithMany().HasForeignKey(row => new { row.RunId, CommandId = row.SettlementCommandId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProcessDecisionBudgetAllocationEntity>().WithMany().HasForeignKey(row => new { row.BudgetId, row.AllocationId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProcessDecisionGenerationEntity>().WithMany().HasForeignKey(row => new { row.DecisionId, Generation = row.DecisionGeneration }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProcessDecisionMaterialScopeEntity>().WithMany().HasForeignKey(row => row.ScopeId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ProcessDecisionRefreshCapturePayloadConfiguration : IEntityTypeConfiguration<ProcessDecisionRefreshCapturePayloadEntity> {
    public void Configure(EntityTypeBuilder<ProcessDecisionRefreshCapturePayloadEntity> builder) {
        builder.ToTable("process_decision_refresh_capture_payloads", table => table.HasCheckConstraint("CK_process_refresh_capture_payload", $$"""
            octet_length("PayloadJson") BETWEEN 2 AND {{ProcessDecisionMaterial.MaximumPayloadUtf8Bytes}}
            """));
        builder.HasKey(row => new { row.RunId, row.ReservationCommandId });
        builder.Property(row => row.PayloadJson).HasColumnType("text").IsRequired();
        builder.HasOne<ProcessDecisionRefreshCaptureEntity>().WithMany().HasForeignKey(row => new { row.RunId, row.ReservationCommandId }).OnDelete(DeleteBehavior.Restrict);
    }
}
