using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Modules.CognitiveMemory;

internal sealed class CognitiveMemoryWorkspaceFrameRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryWorkspaceFrameRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryWorkspaceFrameRecord> builder)
    {
        builder.ToTable("CognitiveMemory_WorkspaceFrames");
        builder.HasKey(frame => frame.Id);
        builder.Property(frame => frame.OwnerUserId).HasMaxLength(160).IsRequired();
        builder.Property(frame => frame.OwnerAgentId).HasMaxLength(160).IsRequired();
        builder.Property(frame => frame.MetadataJson).HasColumnType("TEXT");
        builder.Property(frame => frame.ConcurrencyToken).IsConcurrencyToken();
        builder
            .HasOne<CognitiveMemoryScoreEvaluationTraceRecord>()
            .WithMany()
            .HasForeignKey(frame => frame.CognitiveLoadScoreEvaluationTraceId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(frame => new { frame.ProjectId, frame.FrameKind, frame.Status, frame.ExpiresAtUtc });
        builder.HasIndex(frame => new { frame.ProjectId, frame.OwnerUserId, frame.Status });
        builder.HasIndex(frame => new { frame.ProjectId, frame.OwnerAgentId, frame.Status });
        builder.HasIndex(frame => new { frame.ProjectId, frame.ProcessRunId, frame.ProcessStepId, frame.Status });
        builder.HasIndex(frame => new { frame.ProjectId, frame.WorkflowRunId, frame.Status });
        builder.HasIndex(frame => new { frame.ProjectId, frame.ProbeSessionId, frame.Status });
        builder.HasIndex(frame => new { frame.ProjectId, frame.ReviewSessionId, frame.Status });
        builder.HasIndex(frame => new { frame.ProjectId, frame.LearningTaskId, frame.Status });
        builder.HasIndex(frame => frame.CognitiveLoadScoreEvaluationTraceId);
        builder.HasIndex(frame => frame.LastAttentionDecisionId);
    }
}

internal sealed class CognitiveMemoryWorkspaceGoalRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryWorkspaceGoalRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryWorkspaceGoalRecord> builder)
    {
        builder.ToTable("CognitiveMemory_WorkspaceGoals");
        builder.HasKey(goal => goal.Id);
        builder.Property(goal => goal.GoalKey).HasMaxLength(240).IsRequired();
        builder.Property(goal => goal.Description).HasColumnType("TEXT");
        builder
            .HasOne<CognitiveMemoryWorkspaceFrameRecord>()
            .WithMany()
            .HasForeignKey(goal => goal.WorkspaceFrameId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(goal => new { goal.WorkspaceFrameId, goal.Sequence }).IsUnique();
        builder.HasIndex(goal => new { goal.ProjectId, goal.GoalKey });
    }
}

internal sealed class CognitiveMemoryWorkingMemorySlotRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryWorkingMemorySlotRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryWorkingMemorySlotRecord> builder)
    {
        builder.ToTable("CognitiveMemory_WorkspaceFocusSlots");
        builder.HasKey(slot => slot.Id);
        builder.Property(slot => slot.ExternalPlaceholderKey).HasMaxLength(300).IsRequired();
        builder.Property(slot => slot.Title).HasMaxLength(300).IsRequired();
        builder.Property(slot => slot.Summary).HasColumnType("TEXT");
        builder.Property(slot => slot.InclusionReason).HasColumnType("TEXT");
        builder.Property(slot => slot.RelationToActiveGoal).HasMaxLength(500).IsRequired();
        builder.Property(slot => slot.CompressionSummary).HasColumnType("TEXT");
        builder
            .HasOne<CognitiveMemoryWorkspaceFrameRecord>()
            .WithMany()
            .HasForeignKey(slot => slot.WorkspaceFrameId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne<CognitiveMemoryRecord>()
            .WithMany()
            .HasForeignKey(slot => slot.MemoryRecordId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<CognitiveMemoryClaimRecord>()
            .WithMany()
            .HasForeignKey(slot => slot.ClaimId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<CognitiveMemorySourceItemRecord>()
            .WithMany()
            .HasForeignKey(slot => slot.SourceItemId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<CognitiveMemoryRecallTraceRecord>()
            .WithMany()
            .HasForeignKey(slot => slot.RecallTraceId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<CognitiveMemoryWorkspaceOpenQuestionRecord>()
            .WithMany()
            .HasForeignKey(slot => slot.OpenQuestionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<CognitiveMemoryScoreEvaluationTraceRecord>()
            .WithMany()
            .HasForeignKey(slot => slot.AttentionScoreEvaluationTraceId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(slot => new { slot.WorkspaceFrameId, slot.SlotKind, slot.CreatedAtUtc });
        builder.HasIndex(slot => slot.MemoryRecordId);
        builder.HasIndex(slot => slot.ClaimId);
        builder.HasIndex(slot => slot.SourceItemId);
        builder.HasIndex(slot => slot.RecallTraceId);
        builder.HasIndex(slot => slot.AttentionScoreEvaluationTraceId);
    }
}

internal sealed class CognitiveMemoryWorkspaceSlotEvidenceAnchorRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryWorkspaceSlotEvidenceAnchorRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryWorkspaceSlotEvidenceAnchorRecord> builder)
    {
        builder.ToTable("CognitiveMemory_WorkspaceSlotEvidenceAnchors");
        builder.HasKey(anchor => anchor.Id);
        builder
            .HasOne<CognitiveMemoryWorkingMemorySlotRecord>()
            .WithMany()
            .HasForeignKey(anchor => anchor.WorkspaceSlotId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne<CognitiveMemoryWorkspaceFrameRecord>()
            .WithMany()
            .HasForeignKey(anchor => anchor.WorkspaceFrameId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne<CognitiveMemoryEvidenceAnchorRecord>()
            .WithMany()
            .HasForeignKey(anchor => anchor.EvidenceAnchorId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(anchor => new { anchor.WorkspaceSlotId, anchor.EvidenceAnchorId }).IsUnique();
        builder.HasIndex(anchor => new { anchor.WorkspaceFrameId, anchor.EvidenceAnchorId });
        builder.HasIndex(anchor => new { anchor.ProjectId, anchor.EvidenceAnchorId });
    }
}

internal sealed class CognitiveMemoryWorkspaceOpenQuestionRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryWorkspaceOpenQuestionRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryWorkspaceOpenQuestionRecord> builder)
    {
        builder.ToTable("CognitiveMemory_WorkspaceOpenQuestions");
        builder.HasKey(question => question.Id);
        builder.Property(question => question.QuestionText).HasColumnType("TEXT");
        builder.Property(question => question.Reason).HasColumnType("TEXT");
        builder
            .HasOne<CognitiveMemoryWorkspaceFrameRecord>()
            .WithMany()
            .HasForeignKey(question => question.WorkspaceFrameId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(question => new { question.WorkspaceFrameId, question.Status, question.CreatedAtUtc });
        builder.HasIndex(question => new { question.ProjectId, question.Status });
    }
}

internal sealed class CognitiveMemoryInhibitedCandidateRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryInhibitedCandidateRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryInhibitedCandidateRecord> builder)
    {
        builder.ToTable("CognitiveMemory_WorkspaceInhibitedCandidates");
        builder.HasKey(candidate => candidate.Id);
        builder.Property(candidate => candidate.ExternalCandidateKey).HasMaxLength(300).IsRequired();
        builder.Property(candidate => candidate.Reason).HasColumnType("TEXT");
        builder
            .HasOne<CognitiveMemoryWorkspaceFrameRecord>()
            .WithMany()
            .HasForeignKey(candidate => candidate.WorkspaceFrameId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne<CognitiveMemoryRecord>()
            .WithMany()
            .HasForeignKey(candidate => candidate.MemoryRecordId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<CognitiveMemoryClaimRecord>()
            .WithMany()
            .HasForeignKey(candidate => candidate.ClaimId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<CognitiveMemorySourceItemRecord>()
            .WithMany()
            .HasForeignKey(candidate => candidate.SourceItemId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<CognitiveMemoryScoreEvaluationTraceRecord>()
            .WithMany()
            .HasForeignKey(candidate => candidate.InhibitionScoreEvaluationTraceId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(candidate => new { candidate.WorkspaceFrameId, candidate.ReasonKind, candidate.CreatedAtUtc });
        builder.HasIndex(candidate => candidate.MemoryRecordId);
        builder.HasIndex(candidate => candidate.ClaimId);
        builder.HasIndex(candidate => candidate.SourceItemId);
        builder.HasIndex(candidate => candidate.InhibitionScoreEvaluationTraceId);
    }
}

internal sealed class CognitiveMemoryAttentionDecisionRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryAttentionDecisionRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryAttentionDecisionRecord> builder)
    {
        builder.ToTable("CognitiveMemory_AttentionDecisions");
        builder.HasKey(decision => decision.Id);
        builder.Property(decision => decision.RequestHash).HasMaxLength(128).IsRequired();
        builder.Property(decision => decision.RequestPreview).HasMaxLength(500).IsRequired();
        builder.Property(decision => decision.Explanation).HasColumnType("TEXT");
        builder.Property(decision => decision.RequiredNextActionsJson).HasColumnType("TEXT");
        builder.Property(decision => decision.MetadataJson).HasColumnType("TEXT");
        builder.Property(decision => decision.ConcurrencyToken).IsConcurrencyToken();
        builder
            .HasOne<CognitiveMemoryWorkspaceFrameRecord>()
            .WithMany()
            .HasForeignKey(decision => decision.WorkspaceFrameId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<CognitiveMemoryScoreEvaluationTraceRecord>()
            .WithMany()
            .HasForeignKey(decision => decision.RoutingScoreEvaluationTraceId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(decision => new { decision.ProjectId, decision.WorkspaceFrameId, decision.CreatedAtUtc });
        builder.HasIndex(decision => new { decision.WorkspaceFrameId, decision.DecisionKind });
        builder.HasIndex(decision => new { decision.ProjectId, decision.DecisionKind, decision.CreatedAtUtc });
        builder.HasIndex(decision => decision.RoutingScoreEvaluationTraceId);
        builder.HasIndex(decision => decision.RequestHash);
    }
}
