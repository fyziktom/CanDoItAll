using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Processes.Persistence;

public sealed class ProcessAcceptedAssignmentSourceEntity {
    public Guid AdmissionId { get; set; }
    public Guid StepInstanceId { get; set; }
    public string PayloadJson { get; set; } = string.Empty;
    public string Fingerprint { get; set; } = string.Empty;
    public ProcessAcceptedExecutionInputVersion InputVersion { get; set; }
    public string? InputJson { get; set; }
    public string? InputFingerprint { get; set; }
}

internal sealed class ProcessAcceptedAssignmentSourceEntityConfiguration : IEntityTypeConfiguration<ProcessAcceptedAssignmentSourceEntity> {
    public void Configure(EntityTypeBuilder<ProcessAcceptedAssignmentSourceEntity> builder) {
        builder.ToTable("process_accepted_assignment_sources", table => {
            table.HasCheckConstraint("CK_process_accepted_assignment_source", $$"""
                "StepInstanceId" <> '00000000-0000-0000-0000-000000000000'::uuid
                AND octet_length("PayloadJson") BETWEEN 2 AND {{ProcessAcceptedSourceCodec.MaximumPayloadUtf8Bytes}}
                AND "Fingerprint" ~ '^sha256:[0-9a-f]{64}$'
                """);
            table.HasCheckConstraint("CK_process_accepted_execution_input", $$"""
                ("InputVersion" = 0 AND "InputJson" IS NULL AND "InputFingerprint" IS NULL)
                OR ("InputVersion" IN (1, 2) AND "InputJson" IS NOT NULL
                    AND octet_length("InputJson") BETWEEN 2 AND {{ProcessAcceptedExecutionInputCodec.MaximumPayloadUtf8Bytes}}
                    AND "InputFingerprint" IS NOT NULL AND "InputFingerprint" ~ '^sha256:[0-9a-f]{64}$')
                """);
        });
        builder.HasKey(item => new { item.AdmissionId, item.StepInstanceId });
        builder.Property(item => item.PayloadJson).IsRequired();
        builder.Property(item => item.Fingerprint).HasMaxLength(71).IsRequired();
        builder.Property(item => item.InputFingerprint).HasMaxLength(71);
        builder.HasOne<ProcessPreparedLaunchEntity>().WithMany().HasForeignKey(item => item.AdmissionId).OnDelete(DeleteBehavior.Cascade);
    }
}
