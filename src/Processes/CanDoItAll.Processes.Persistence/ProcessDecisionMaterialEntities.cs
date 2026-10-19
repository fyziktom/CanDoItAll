using CanDoItAll.Processes.Runtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Processes.Persistence;

public sealed class ProcessDecisionMaterialScopeEntity {
    public Guid ScopeId { get; set; }
    public Guid DatabaseProfileId { get; set; }
    public Guid WorkspaceNamespaceId { get; set; }
    public Guid RootRunId { get; set; }
    public Guid OwnerRunId { get; set; }
    public Guid? StepInstanceId { get; set; }
    public Guid ContractId { get; set; }
    public int ContractVersion { get; set; }
    public ProcessDecisionChildOwner Executor { get; set; }
}

public sealed class ProcessDecisionMaterialEntity {
    public Guid MaterialId { get; set; }
    public Guid ScopeId { get; set; }
    public int Generation { get; set; }
    public long CapturedAtUtcTicks { get; set; }
    public string PayloadJson { get; set; } = string.Empty;
    public int PayloadUtf8Bytes { get; set; }
    public string Fingerprint { get; set; } = string.Empty;
}

internal sealed class ProcessDecisionMaterialScopeConfiguration : IEntityTypeConfiguration<ProcessDecisionMaterialScopeEntity> {
    public void Configure(EntityTypeBuilder<ProcessDecisionMaterialScopeEntity> builder) {
        builder.ToTable("process_decision_material_scopes", table => table.HasCheckConstraint("CK_process_decision_material_scopes_identity", """
            "ScopeId" <> '00000000-0000-0000-0000-000000000000'::uuid
            AND "DatabaseProfileId" <> '00000000-0000-0000-0000-000000000000'::uuid
            AND "WorkspaceNamespaceId" <> '00000000-0000-0000-0000-000000000000'::uuid
            AND "RootRunId" <> '00000000-0000-0000-0000-000000000000'::uuid
            AND "OwnerRunId" <> '00000000-0000-0000-0000-000000000000'::uuid
            AND ("StepInstanceId" IS NULL OR "StepInstanceId" <> '00000000-0000-0000-0000-000000000000'::uuid)
            AND "ContractId" <> '00000000-0000-0000-0000-000000000000'::uuid
            AND "ContractVersion" > 0 AND "Executor" BETWEEN 0 AND 1
            """));
        builder.HasKey(item => item.ScopeId);
        builder.HasIndex(item => item.OwnerRunId).IsUnique().HasFilter("\"StepInstanceId\" IS NULL");
        builder.HasIndex(item => new { item.OwnerRunId, item.StepInstanceId }).IsUnique().HasFilter("\"StepInstanceId\" IS NOT NULL");
        builder.HasIndex(item => item.RootRunId);
        builder.HasOne<ProcessRuntimeStateEntity>().WithMany().HasForeignKey(item => item.OwnerRunId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProcessRuntimeStepAssignmentEntity>().WithMany()
            .HasForeignKey(item => new { RunId = item.OwnerRunId, item.StepInstanceId }).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ProcessDecisionMaterialConfiguration : IEntityTypeConfiguration<ProcessDecisionMaterialEntity> {
    public void Configure(EntityTypeBuilder<ProcessDecisionMaterialEntity> builder) {
        builder.ToTable("process_decision_materials", table => table.HasCheckConstraint("CK_process_decision_materials_content", $$"""
            "MaterialId" <> '00000000-0000-0000-0000-000000000000'::uuid
            AND "Generation" BETWEEN 1 AND {{ProcessDecisionGeneration.MaximumGenerations}}
            AND "CapturedAtUtcTicks" BETWEEN 1 AND {{DateTimeOffset.MaxValue.UtcTicks}}
            AND "PayloadUtf8Bytes" BETWEEN 2 AND {{ProcessDecisionMaterial.MaximumPayloadUtf8Bytes}}
            AND octet_length("PayloadJson") = "PayloadUtf8Bytes"
            AND "Fingerprint" ~ '^[0-9A-F]{64}$'
            """));
        builder.HasKey(item => item.MaterialId);
        builder.HasIndex(item => new { item.ScopeId, item.Generation }).IsUnique();
        builder.Property(item => item.PayloadJson).HasColumnType("text").IsRequired();
        builder.Property(item => item.Fingerprint).HasMaxLength(64).IsRequired();
        builder.HasOne<ProcessDecisionMaterialScopeEntity>().WithMany().HasForeignKey(item => item.ScopeId).OnDelete(DeleteBehavior.Restrict);
    }
}
