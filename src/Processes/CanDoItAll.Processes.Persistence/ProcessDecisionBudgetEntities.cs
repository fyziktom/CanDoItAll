using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CanDoItAll.SharedKernel;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Persistence;

public sealed class ProcessDecisionBudgetEntity {
    public Guid BudgetId { get; set; }
    public Guid RootRunId { get; set; }
    public Guid RunId { get; set; }
    public Guid? StepInstanceId { get; set; }
    public Guid? ParentBudgetId { get; set; }
    public Guid? AdmissionCommandId { get; set; }
    public string? RunAdmissionJson { get; set; }
    public long Revision { get; set; }
    public long MaximumWorkTicks { get; set; }
    public int MaximumReplans { get; set; }
    public long MaximumBasisAgeTicks { get; set; }
    public long ConsumedWorkTicks { get; set; }
    public long ReservedWorkTicks { get; set; }
    public long ConsumedReplans { get; set; }
    public long ReservedReplans { get; set; }
    public int AllocationCount { get; set; }
    public bool OverrunDetected { get; set; }
}

public sealed class ProcessDecisionBudgetAllocationEntity {
    public Guid BudgetId { get; set; }
    public Guid AllocationId { get; set; }
    public Guid? FundingBudgetId { get; set; }
    public Guid? FundingAllocationId { get; set; }
    public long GrantedWorkTicks { get; set; }
    public int GrantedReplans { get; set; }
    public long? OwnerWorkRevision { get; set; }
    public long? ConsumedWorkTicks { get; set; }
    public int? ConsumedReplans { get; set; }
}

internal sealed class ProcessDecisionBudgetEntityConfiguration : IEntityTypeConfiguration<ProcessDecisionBudgetEntity> {
    public void Configure(EntityTypeBuilder<ProcessDecisionBudgetEntity> builder) {
        builder.ToTable("process_decision_budgets", table => table.HasCheckConstraint("CK_process_decision_budgets_balance", $"""
            "MaximumWorkTicks" > 0 AND "MaximumReplans" >= 0 AND "MaximumBasisAgeTicks" > 0
            AND "ConsumedWorkTicks" >= 0 AND "ReservedWorkTicks" >= 0 AND "ConsumedReplans" >= 0 AND "ReservedReplans" >= 0
            AND "AllocationCount" BETWEEN 0 AND {DecisionBudgetBalance.MaximumAllocations} AND "Revision" >= "AllocationCount" + 1
            AND ("AllocationCount" > 0 OR ("ConsumedWorkTicks" = 0 AND "ReservedWorkTicks" = 0 AND "ConsumedReplans" = 0 AND "ReservedReplans" = 0 AND NOT "OverrunDetected"))
            AND ("OverrunDetected" OR ("ReservedWorkTicks" <= "MaximumWorkTicks" AND "ConsumedWorkTicks" <= "MaximumWorkTicks" - "ReservedWorkTicks"
            AND "ReservedReplans" <= "MaximumReplans" AND "ConsumedReplans" <= "MaximumReplans" - "ReservedReplans"))
            """));
        builder.HasKey(item => item.BudgetId);
        builder.Property(item => item.RunAdmissionJson).HasColumnType("text");
        builder.ToTable(table => table.HasCheckConstraint("CK_process_decision_budgets_owner", $"""
            "BudgetId" <> '00000000-0000-0000-0000-000000000000'::uuid
            AND "RootRunId" <> '00000000-0000-0000-0000-000000000000'::uuid
            AND "RunId" <> '00000000-0000-0000-0000-000000000000'::uuid
            AND (("StepInstanceId" IS NULL AND "RunId" = "RootRunId" AND "ParentBudgetId" IS NULL
                AND "AdmissionCommandId" IS NOT NULL AND "RunAdmissionJson" IS NOT NULL
                AND octet_length("RunAdmissionJson") BETWEEN 1 AND {ProcessDecisionRunAdmission.MaximumUtf8Bytes})
            OR ("StepInstanceId" IS NOT NULL AND "StepInstanceId" <> '00000000-0000-0000-0000-000000000000'::uuid
                AND "AdmissionCommandId" IS NULL AND "RunAdmissionJson" IS NULL
                AND ("ParentBudgetId" IS NULL OR "ParentBudgetId" <> "BudgetId")))
            """));
        builder.Property(item => item.Revision).IsConcurrencyToken();
        builder.HasIndex(item => new { item.RunId, item.StepInstanceId }).IsUnique().HasFilter("\"StepInstanceId\" IS NOT NULL");
        builder.HasIndex(item => item.RunId).IsUnique().HasFilter("\"StepInstanceId\" IS NULL");
        builder.HasIndex(item => item.RootRunId);
        builder.HasOne<ProcessRuntimeStateEntity>().WithMany().HasForeignKey(item => item.RunId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProcessDecisionBudgetEntity>().WithMany().HasForeignKey(item => item.ParentBudgetId)
            .HasConstraintName("FK_process_decision_budgets_parent").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProcessRuntimeIdempotencyEntity>().WithMany()
            .HasForeignKey(item => new { item.RunId, item.AdmissionCommandId })
            .HasConstraintName("FK_process_decision_budgets_admission").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProcessRuntimeStepAssignmentEntity>().WithMany()
            .HasForeignKey(item => new { item.RunId, item.StepInstanceId }).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ProcessDecisionBudgetAllocationEntityConfiguration : IEntityTypeConfiguration<ProcessDecisionBudgetAllocationEntity> {
    public void Configure(EntityTypeBuilder<ProcessDecisionBudgetAllocationEntity> builder) {
        builder.ToTable("process_decision_budget_allocations", table => table.HasCheckConstraint("CK_process_decision_budget_allocations_amounts", """
            "GrantedWorkTicks" >= 0 AND "GrantedReplans" >= 0 AND ("GrantedWorkTicks" > 0 OR "GrantedReplans" > 0)
            AND (("OwnerWorkRevision" IS NULL AND "ConsumedWorkTicks" IS NULL AND "ConsumedReplans" IS NULL)
            OR ("OwnerWorkRevision" IS NOT NULL AND "OwnerWorkRevision" > 0 AND "ConsumedWorkTicks" IS NOT NULL AND "ConsumedWorkTicks" >= 0
            AND "ConsumedReplans" IS NOT NULL AND "ConsumedReplans" >= 0))
            """));
        builder.HasKey(item => new { item.BudgetId, item.AllocationId });
        builder.ToTable(table => table.HasCheckConstraint("CK_process_decision_budget_allocations_funding", """
            ("FundingBudgetId" IS NULL AND "FundingAllocationId" IS NULL)
            OR ("FundingBudgetId" IS NOT NULL AND "FundingAllocationId" IS NOT NULL AND "FundingBudgetId" <> "BudgetId")
            """));
        builder.HasIndex(item => new { item.FundingBudgetId, item.FundingAllocationId }).IsUnique().HasFilter("\"FundingBudgetId\" IS NOT NULL");
        builder.HasOne<ProcessDecisionBudgetAllocationEntity>().WithMany()
            .HasForeignKey(item => new { item.FundingBudgetId, item.FundingAllocationId })
            .HasConstraintName("FK_process_decision_allocations_parent_funding").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProcessDecisionBudgetEntity>().WithMany().HasForeignKey(item => item.BudgetId).OnDelete(DeleteBehavior.Restrict);
    }
}
