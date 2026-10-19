using CanDoItAll.Processes.Runtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Processes.Persistence;

public sealed class ProcessDecisionHeadEntity {
    public Guid DecisionId { get; set; }
    public Guid RootRunId { get; set; }
    public Guid RunId { get; set; }
    public Guid StepInstanceId { get; set; }
    public Guid BudgetId { get; set; }
    public int CurrentGeneration { get; set; }
    public long Revision { get; set; }
    public Guid? SharedRunScopeId { get; set; }
}

public sealed class ProcessDecisionGenerationEntity {
    public Guid DecisionId { get; set; }
    public int Generation { get; set; }
    public long Revision { get; set; }
    public ProcessDecisionPhase Phase { get; set; }
    public Guid MaterialId { get; set; }
    public Guid? CanonicalMaterialId { get; set; }
    public Guid BudgetId { get; set; }
    public Guid CaptureAllocationId { get; set; }
    public int ChildCount { get; set; }
    public int UnclosedChildCount { get; set; }
}

public sealed class ProcessDecisionChildEntity {
    public Guid DecisionId { get; set; }
    public int Generation { get; set; }
    public Guid ChildId { get; set; }
    public ProcessDecisionChildOwner Owner { get; set; }
    public Guid BudgetId { get; set; }
    public Guid AllocationId { get; set; }
    public long? OwnerRevision { get; set; }
    public ProcessDecisionChildOutcome? Outcome { get; set; }
    public Guid? AcceptedReceiptId { get; set; }
}

internal sealed class ProcessDecisionHeadConfiguration : IEntityTypeConfiguration<ProcessDecisionHeadEntity> {
    public void Configure(EntityTypeBuilder<ProcessDecisionHeadEntity> builder) {
        builder.ToTable("process_decision_heads", table => table.HasCheckConstraint("CK_process_decision_heads_version",
            $"\"CurrentGeneration\" BETWEEN 1 AND {ProcessDecisionGeneration.MaximumGenerations} AND \"Revision\" >= 2 * \"CurrentGeneration\" - 1"));
        builder.HasKey(item => item.DecisionId);
        builder.Property(item => item.Revision).IsConcurrencyToken();
        builder.HasIndex(item => new { item.RunId, item.StepInstanceId }).IsUnique();
        builder.HasIndex(item => item.BudgetId).IsUnique();
        builder.HasIndex(item => item.RootRunId);
        builder.HasOne<ProcessDecisionRunScopeEntity>().WithMany().HasForeignKey(item => item.SharedRunScopeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProcessRuntimeStepAssignmentEntity>().WithMany()
            .HasForeignKey(item => new { item.RunId, item.StepInstanceId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProcessDecisionBudgetEntity>().WithMany().HasForeignKey(item => item.BudgetId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ProcessDecisionGenerationConfiguration : IEntityTypeConfiguration<ProcessDecisionGenerationEntity> {
    public void Configure(EntityTypeBuilder<ProcessDecisionGenerationEntity> builder) {
        builder.ToTable("process_decision_generations", table => table.HasCheckConstraint("CK_process_decision_generations_state", $"""
            "Generation" BETWEEN 1 AND {ProcessDecisionGeneration.MaximumGenerations} AND "Revision" >= 2 * "Generation" - 1
            AND "Phase" BETWEEN 0 AND 5 AND "ChildCount" BETWEEN 0 AND {ProcessDecisionGeneration.MaximumChildren}
            AND "UnclosedChildCount" BETWEEN 0 AND "ChildCount" AND "Revision" >= 1 + 2 * "ChildCount" - "UnclosedChildCount"
            AND ("Phase" < 3 OR "UnclosedChildCount" = 0)
            AND ("Phase" <> 5 OR "Generation" < {ProcessDecisionGeneration.MaximumGenerations})
            """));
        builder.HasKey(item => new { item.DecisionId, item.Generation });
        builder.ToTable(table => table.HasCheckConstraint("CK_process_decision_generations_material",
            "\"CanonicalMaterialId\" IS NULL OR \"CanonicalMaterialId\" = \"MaterialId\""));
        builder.HasOne<ProcessDecisionMaterialEntity>().WithMany().HasForeignKey(item => item.CanonicalMaterialId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(item => item.Revision).IsConcurrencyToken();
        builder.HasIndex(item => new { item.BudgetId, item.CaptureAllocationId }).IsUnique();
        builder.HasOne<ProcessDecisionHeadEntity>().WithMany().HasForeignKey(item => item.DecisionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProcessDecisionBudgetAllocationEntity>().WithMany()
            .HasForeignKey(item => new { item.BudgetId, AllocationId = item.CaptureAllocationId }).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ProcessDecisionChildConfiguration : IEntityTypeConfiguration<ProcessDecisionChildEntity> {
    public void Configure(EntityTypeBuilder<ProcessDecisionChildEntity> builder) {
        builder.ToTable("process_decision_children", table => table.HasCheckConstraint("CK_process_decision_children_closure", """
            "Owner" BETWEEN 0 AND 1
            AND (("OwnerRevision" IS NULL AND "Outcome" IS NULL AND "AcceptedReceiptId" IS NULL)
            OR ("OwnerRevision" IS NOT NULL AND "OwnerRevision" > 0 AND "Outcome" IS NOT NULL
            AND (("Outcome" = 0 AND "AcceptedReceiptId" IS NOT NULL) OR ("Outcome" = 1 AND "AcceptedReceiptId" IS NULL))))
            """));
        builder.HasKey(item => new { item.DecisionId, item.Generation, item.ChildId });
        builder.HasIndex(item => new { item.DecisionId, item.ChildId }).IsUnique();
        builder.HasIndex(item => new { item.BudgetId, item.AllocationId }).IsUnique();
        builder.HasOne<ProcessDecisionGenerationEntity>().WithMany()
            .HasForeignKey(item => new { item.DecisionId, item.Generation }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProcessDecisionBudgetAllocationEntity>().WithMany()
            .HasForeignKey(item => new { item.BudgetId, item.AllocationId }).OnDelete(DeleteBehavior.Restrict);
    }
}
