using CanDoItAll.Processes.Runtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Processes.Persistence;

public sealed class ProcessDecisionRunScopeEntity {
    public Guid ScopeId { get; set; }
    public Guid BudgetId { get; set; }
    public long Revision { get; set; }
    public ProcessDecisionRunScopePhase Phase { get; set; }
    public Guid? CurrentMaterialId { get; set; }
    public int TotalMemberships { get; set; }
    public int CurrentMemberships { get; set; }
    public Guid? CaptureAllocationId { get; set; }
    public Guid? CaptureMaterialId { get; set; }
    public ProcessDecisionRunCaptureKind? CaptureKind { get; set; }
}

public sealed class ProcessDecisionRunScopeMemberEntity {
    public Guid ScopeId { get; set; }
    public Guid MaterialId { get; set; }
    public Guid RunId { get; set; }
    public Guid StepInstanceId { get; set; }
    public Guid DecisionId { get; set; }
    public int Generation { get; set; }
}

internal sealed class ProcessDecisionRunScopeConfiguration : IEntityTypeConfiguration<ProcessDecisionRunScopeEntity> {
    public void Configure(EntityTypeBuilder<ProcessDecisionRunScopeEntity> builder) {
        builder.ToTable("process_decision_run_scopes", table => table.HasCheckConstraint("CK_process_decision_run_scopes_state", $$"""
            "Revision" >= 1 + "TotalMemberships" AND "Phase" BETWEEN 0 AND 5
            AND "TotalMemberships" BETWEEN 0 AND {{ProcessDecisionRunScopeState.MaximumMemberships}}
            AND "CurrentMemberships" BETWEEN 0 AND "TotalMemberships"
            AND ("CurrentMaterialId" IS NOT NULL OR ("TotalMemberships" = 0 AND "Phase" NOT IN (2, 4)))
            AND ("Phase" <> 0 OR "CurrentMaterialId" IS NULL)
            AND (("Phase" = 1 AND "CaptureAllocationId" IS NOT NULL AND "CaptureMaterialId" IS NOT NULL
                AND "CaptureKind" IS NOT NULL AND "CaptureKind" BETWEEN 0 AND 2
                AND (("CurrentMaterialId" IS NULL) = ("CaptureKind" = 0)))
                OR ("Phase" <> 1 AND "CaptureAllocationId" IS NULL AND "CaptureMaterialId" IS NULL AND "CaptureKind" IS NULL))
            """));
        builder.HasKey(item => item.ScopeId);
        builder.Property(item => item.Revision).IsConcurrencyToken();
        builder.HasIndex(item => item.BudgetId).IsUnique();
        builder.HasOne<ProcessDecisionMaterialScopeEntity>().WithMany().HasForeignKey(item => item.ScopeId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_process_run_scope_material_scope");
        builder.HasOne<ProcessDecisionBudgetEntity>().WithMany().HasForeignKey(item => item.BudgetId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_process_run_scope_root_budget");
        builder.HasOne<ProcessDecisionMaterialEntity>().WithMany().HasForeignKey(item => item.CurrentMaterialId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_process_run_scope_current_material");
        builder.HasOne<ProcessDecisionBudgetAllocationEntity>().WithMany().HasForeignKey(item => new { item.BudgetId, AllocationId = item.CaptureAllocationId })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_process_run_scope_capture_allocation");
    }
}

internal sealed class ProcessDecisionRunScopeMemberConfiguration : IEntityTypeConfiguration<ProcessDecisionRunScopeMemberEntity> {
    public void Configure(EntityTypeBuilder<ProcessDecisionRunScopeMemberEntity> builder) {
        builder.ToTable("process_decision_run_scope_members");
        builder.HasKey(item => new { item.DecisionId, item.Generation });
        builder.HasIndex(item => new { item.ScopeId, item.MaterialId });
        builder.HasOne<ProcessDecisionRunScopeEntity>().WithMany().HasForeignKey(item => item.ScopeId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_process_run_scope_member_scope");
        builder.HasOne<ProcessDecisionMaterialEntity>().WithMany().HasForeignKey(item => item.MaterialId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_process_run_scope_member_material");
        builder.HasOne<ProcessDecisionGenerationEntity>().WithMany().HasForeignKey(item => new { item.DecisionId, item.Generation })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_process_run_scope_member_decision");
        builder.HasOne<ProcessRuntimeStepAssignmentEntity>().WithMany().HasForeignKey(item => new { item.RunId, item.StepInstanceId })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_process_run_scope_member_assignment");
    }
}
