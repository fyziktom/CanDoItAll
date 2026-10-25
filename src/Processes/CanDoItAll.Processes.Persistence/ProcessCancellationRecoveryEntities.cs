using CanDoItAll.Processes.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Processes.Persistence;

public sealed class ProcessCancellationRecoveryEntity {
    public Guid RootRunId { get; set; }
    public ProcessCancellationRecoveryOrigin Origin { get; set; }
    public Guid? BarrierCommandId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public DateTimeOffset DueAtUtc { get; set; }
    public Guid? LeaseToken { get; set; }
    public long LeaseGeneration { get; set; }
    public DateTimeOffset? LeaseExpiresAtUtc { get; set; }
    public long ProgressRevision { get; set; }
    public int Cycle { get; set; }
    public string PositionJson { get; set; } = string.Empty;
    public string? IntentJson { get; set; }
    public ProcessCancellationRecoveryPending Pending { get; set; }
    public bool Completed { get; set; }
}

public sealed class ProcessCancellationRecoverySeedEntity {
    public const int SingletonId = 1;
    public int Id { get; set; }
    public Guid? LastRootRunId { get; set; }
    public long Revision { get; set; }
    public bool Completed { get; set; }
}

internal sealed class ProcessCancellationRecoveryConfiguration : IEntityTypeConfiguration<ProcessCancellationRecoveryEntity> {
    public void Configure(EntityTypeBuilder<ProcessCancellationRecoveryEntity> builder) {
        builder.ToTable("process_cancellation_recovery", table => {
            table.HasCheckConstraint("CK_process_cancellation_recovery_identity", """
                "RootRunId" <> '00000000-0000-0000-0000-000000000000'::uuid
                AND (("Origin" = 0 AND "BarrierCommandId" IS NOT NULL AND "BarrierCommandId" <> '00000000-0000-0000-0000-000000000000'::uuid)
                    OR ("Origin" = 1 AND "BarrierCommandId" IS NULL))
                """);
            table.HasCheckConstraint("CK_process_cancellation_recovery_lease", """
                "LeaseGeneration" >= 0 AND "ProgressRevision" >= 0 AND "Cycle" >= 0
                AND (("LeaseToken" IS NULL AND "LeaseExpiresAtUtc" IS NULL)
                    OR ("LeaseToken" IS NOT NULL AND "LeaseToken" <> '00000000-0000-0000-0000-000000000000'::uuid
                        AND "LeaseGeneration" > 0 AND "LeaseExpiresAtUtc" IS NOT NULL AND "DueAtUtc" = "LeaseExpiresAtUtc" AND NOT "Completed"))
                """);
            table.HasCheckConstraint("CK_process_cancellation_recovery_payload", $$"""
                octet_length("PositionJson") BETWEEN 2 AND {{ProcessCancellationRecoveryCodec.MaximumPositionBytes}}
                AND ("IntentJson" IS NULL OR octet_length("IntentJson") BETWEEN 2 AND {{ProcessCancellationRecoveryCodec.MaximumIntentBytes}})
                AND "Pending" BETWEEN 0 AND {{(int)ProcessCancellationRecoveryPending.InvalidEvidence}}
                AND (NOT "Completed" OR ("IntentJson" IS NULL AND "Pending" = 0))
                """);
        });
        builder.HasKey(item => item.RootRunId);
        builder.HasOne<ProcessRuntimeStateEntity>().WithMany().HasForeignKey(item => item.RootRunId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(item => item.Origin).HasConversion<int>();
        builder.Property(item => item.Pending).HasConversion<int>();
        builder.Property(item => item.CreatedAtUtc).HasDefaultValueSql("clock_timestamp()");
        builder.Property(item => item.UpdatedAtUtc).HasDefaultValueSql("clock_timestamp()");
        builder.Property(item => item.DueAtUtc).HasDefaultValueSql("clock_timestamp()");
        builder.Property(item => item.PositionJson).IsRequired();
        builder.HasIndex(item => new { item.Origin, item.DueAtUtc, item.RootRunId })
            .HasDatabaseName("IX_process_cancellation_recovery_due").HasFilter("NOT \"Completed\"");
    }
}

internal sealed class ProcessCancellationRecoverySeedConfiguration : IEntityTypeConfiguration<ProcessCancellationRecoverySeedEntity> {
    public void Configure(EntityTypeBuilder<ProcessCancellationRecoverySeedEntity> builder) {
        builder.ToTable("process_cancellation_recovery_seed", table => table.HasCheckConstraint("CK_process_cancellation_recovery_seed", """
            "Id" = 1 AND "Revision" >= 0
            AND ("LastRootRunId" IS NULL OR ("LastRootRunId" <> '00000000-0000-0000-0000-000000000000'::uuid AND "Revision" > 0))
            AND (NOT "Completed" OR "Revision" > 0)
            """));
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.HasData(new ProcessCancellationRecoverySeedEntity { Id = ProcessCancellationRecoverySeedEntity.SingletonId });
    }
}
