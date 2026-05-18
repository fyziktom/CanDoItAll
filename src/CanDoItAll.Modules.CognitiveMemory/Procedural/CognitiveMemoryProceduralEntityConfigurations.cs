using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Modules.CognitiveMemory;

internal sealed class CognitiveMemoryProcedureSkillRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryProcedureSkillRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryProcedureSkillRecord> builder)
    {
        builder.ToTable("CognitiveMemory_ProcedureSkills");
        builder.HasKey(skill => skill.Id);
        builder.Property(skill => skill.Title).HasMaxLength(300).IsRequired();
        builder.Property(skill => skill.Purpose).HasColumnType("TEXT");
        builder.Property(skill => skill.PreconditionsJson).HasColumnType("TEXT");
        builder.Property(skill => skill.PostconditionsJson).HasColumnType("TEXT");
        builder.Property(skill => skill.RequiredParticipantsJson).HasColumnType("TEXT");
        builder.Property(skill => skill.RequiredToolKeysJson).HasColumnType("TEXT");
        builder.Property(skill => skill.InputSchemaJson).HasColumnType("TEXT");
        builder.Property(skill => skill.OutputSchemaJson).HasColumnType("TEXT");
        builder.Property(skill => skill.AlgorithmVersion).HasMaxLength(120).IsRequired();
        builder.Property(skill => skill.MetadataJson).HasColumnType("TEXT");
        builder.Property(skill => skill.ConcurrencyToken).IsConcurrencyToken();
        builder
            .HasOne<CognitiveMemoryScoreEvaluationTraceRecord>()
            .WithMany()
            .HasForeignKey(skill => skill.MaturityScoreEvaluationTraceId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<CognitiveMemoryConsolidationCandidateRecord>()
            .WithMany()
            .HasForeignKey(skill => skill.SourceConsolidationCandidateId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<CognitiveMemoryTemporalEpisodeRecord>()
            .WithMany()
            .HasForeignKey(skill => skill.LastSuccessfulEpisodeId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(skill => new { skill.ProjectId, skill.Maturity, skill.ValidationState });
        builder.HasIndex(skill => new { skill.ProjectId, skill.RiskLevel, skill.Maturity });
        builder.HasIndex(skill => skill.MaturityScoreEvaluationTraceId);
        builder.HasIndex(skill => skill.SourceConsolidationCandidateId);
        builder.HasIndex(skill => skill.LastSuccessfulEpisodeId);
    }
}

internal sealed class CognitiveMemoryProcedureStepRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryProcedureStepRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryProcedureStepRecord> builder)
    {
        builder.ToTable("CognitiveMemory_ProcedureSteps");
        builder.HasKey(step => step.Id);
        builder.Property(step => step.StepKey).HasMaxLength(160).IsRequired();
        builder.Property(step => step.Action).HasColumnType("TEXT");
        builder.Property(step => step.RequiredInput).HasColumnType("TEXT");
        builder.Property(step => step.ExpectedOutput).HasColumnType("TEXT");
        builder.Property(step => step.ValidationCheck).HasColumnType("TEXT");
        builder.Property(step => step.FailureHandling).HasColumnType("TEXT");
        builder.Property(step => step.ToolBindingKey).HasMaxLength(200).IsRequired();
        builder.Property(step => step.MetadataJson).HasColumnType("TEXT");
        builder.Property(step => step.ConcurrencyToken).IsConcurrencyToken();
        builder
            .HasOne<CognitiveMemoryProcedureSkillRecord>()
            .WithMany()
            .HasForeignKey(step => step.ProcedureSkillId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(step => new { step.ProcedureSkillId, step.SequenceIndex }).IsUnique();
        builder.HasIndex(step => new { step.ProcedureSkillId, step.StepKey }).IsUnique();
        builder.HasIndex(step => new { step.ProjectId, step.ToolBindingKey });
    }
}

internal sealed class CognitiveMemoryProcedureStepEvidenceRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryProcedureStepEvidenceRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryProcedureStepEvidenceRecord> builder)
    {
        builder.ToTable("CognitiveMemory_ProcedureStepEvidence");
        builder.HasKey(evidence => evidence.Id);
        builder
            .HasOne<CognitiveMemoryProcedureStepRecord>()
            .WithMany()
            .HasForeignKey(evidence => evidence.ProcedureStepId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne<CognitiveMemoryEvidenceAnchorRecord>()
            .WithMany()
            .HasForeignKey(evidence => evidence.EvidenceAnchorId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(evidence => new { evidence.ProcedureStepId, evidence.EvidenceAnchorId }).IsUnique();
        builder.HasIndex(evidence => new { evidence.ProjectId, evidence.EvidenceAnchorId });
    }
}

internal sealed class CognitiveMemoryProcedureFailureModeRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryProcedureFailureModeRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryProcedureFailureModeRecord> builder)
    {
        builder.ToTable("CognitiveMemory_ProcedureFailureModes");
        builder.HasKey(failure => failure.Id);
        builder.Property(failure => failure.FailureKey).HasMaxLength(160).IsRequired();
        builder.Property(failure => failure.Condition).HasColumnType("TEXT");
        builder.Property(failure => failure.DetectionSignal).HasColumnType("TEXT");
        builder.Property(failure => failure.LikelyCause).HasColumnType("TEXT");
        builder.Property(failure => failure.Mitigation).HasColumnType("TEXT");
        builder.Property(failure => failure.RollbackOrCompensation).HasColumnType("TEXT");
        builder.Property(failure => failure.MetadataJson).HasColumnType("TEXT");
        builder.Property(failure => failure.ConcurrencyToken).IsConcurrencyToken();
        builder
            .HasOne<CognitiveMemoryProcedureSkillRecord>()
            .WithMany()
            .HasForeignKey(failure => failure.ProcedureSkillId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(failure => new { failure.ProcedureSkillId, failure.FailureKey }).IsUnique();
        builder.HasIndex(failure => new { failure.ProjectId, failure.CreatedAtUtc });
    }
}

internal sealed class CognitiveMemoryProcedureFailureModePredictionErrorRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryProcedureFailureModePredictionErrorRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryProcedureFailureModePredictionErrorRecord> builder)
    {
        builder.ToTable("CognitiveMemory_ProcedureFailureModePredictionErrors");
        builder.HasKey(link => link.Id);
        builder
            .HasOne<CognitiveMemoryProcedureFailureModeRecord>()
            .WithMany()
            .HasForeignKey(link => link.ProcedureFailureModeId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne<CognitiveMemoryPredictionErrorRecord>()
            .WithMany()
            .HasForeignKey(link => link.PredictionErrorId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(link => new { link.ProcedureFailureModeId, link.PredictionErrorId }).IsUnique();
        builder.HasIndex(link => new { link.ProjectId, link.PredictionErrorId });
    }
}

internal sealed class CognitiveMemoryProcedureFailureModeEpisodeRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryProcedureFailureModeEpisodeRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryProcedureFailureModeEpisodeRecord> builder)
    {
        builder.ToTable("CognitiveMemory_ProcedureFailureModeEpisodes");
        builder.HasKey(link => link.Id);
        builder
            .HasOne<CognitiveMemoryProcedureFailureModeRecord>()
            .WithMany()
            .HasForeignKey(link => link.ProcedureFailureModeId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne<CognitiveMemoryTemporalEpisodeRecord>()
            .WithMany()
            .HasForeignKey(link => link.EpisodeId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(link => new { link.ProcedureFailureModeId, link.EpisodeId }).IsUnique();
        builder.HasIndex(link => new { link.ProjectId, link.EpisodeId });
    }
}

internal sealed class CognitiveMemoryProcedureValidationEvidenceRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryProcedureValidationEvidenceRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryProcedureValidationEvidenceRecord> builder)
    {
        builder.ToTable("CognitiveMemory_ProcedureValidationEvidence");
        builder.HasKey(evidence => evidence.Id);
        builder.Property(evidence => evidence.Summary).HasColumnType("TEXT");
        builder
            .HasOne<CognitiveMemoryProcedureSkillRecord>()
            .WithMany()
            .HasForeignKey(evidence => evidence.ProcedureSkillId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne<CognitiveMemoryEvidenceAnchorRecord>()
            .WithMany()
            .HasForeignKey(evidence => evidence.EvidenceAnchorId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne<CognitiveMemoryTemporalEpisodeRecord>()
            .WithMany()
            .HasForeignKey(evidence => evidence.EpisodeId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<CognitiveMemoryReviewItemRecord>()
            .WithMany()
            .HasForeignKey(evidence => evidence.ReviewItemId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(evidence => new { evidence.ProcedureSkillId, evidence.EvidenceRole, evidence.EvidenceAnchorId }).IsUnique();
        builder.HasIndex(evidence => new { evidence.ProjectId, evidence.EvidenceAnchorId });
        builder.HasIndex(evidence => evidence.EpisodeId);
        builder.HasIndex(evidence => evidence.ReviewItemId);
    }
}

internal sealed class CognitiveMemoryProcedureAutomationBindingRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryProcedureAutomationBindingRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryProcedureAutomationBindingRecord> builder)
    {
        builder.ToTable("CognitiveMemory_ProcedureAutomationBindings");
        builder.HasKey(binding => binding.Id);
        builder.Property(binding => binding.BindingKey).HasMaxLength(300).IsRequired();
        builder.Property(binding => binding.RejectionCode).HasMaxLength(120).IsRequired();
        builder.Property(binding => binding.RejectionReason).HasColumnType("TEXT");
        builder.Property(binding => binding.ConcurrencyToken).IsConcurrencyToken();
        builder
            .HasOne<CognitiveMemoryProcedureSkillRecord>()
            .WithMany()
            .HasForeignKey(binding => binding.ProcedureSkillId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne<CognitiveMemoryReviewItemRecord>()
            .WithMany()
            .HasForeignKey(binding => binding.ReviewItemId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(binding => new { binding.ProcedureSkillId, binding.BindingKind, binding.BindingKey }).IsUnique();
        builder.HasIndex(binding => new { binding.ProjectId, binding.State, binding.BindingKind });
        builder.HasIndex(binding => binding.ReviewItemId);
    }
}

internal sealed class CognitiveMemoryProcedureSimulationRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryProcedureSimulationRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryProcedureSimulationRecord> builder)
    {
        builder.ToTable("CognitiveMemory_ProcedureSimulations");
        builder.HasKey(simulation => simulation.Id);
        builder.Property(simulation => simulation.Summary).HasColumnType("TEXT");
        builder.Property(simulation => simulation.SpeculationLabel).HasMaxLength(160).IsRequired();
        builder.Property(simulation => simulation.PolicyProfileId).HasMaxLength(160).IsRequired();
        builder.Property(simulation => simulation.SourceScopeKey).HasMaxLength(300).IsRequired();
        builder.Property(simulation => simulation.RequiredValidationStepsJson).HasColumnType("TEXT");
        builder.Property(simulation => simulation.MetadataJson).HasColumnType("TEXT");
        builder.Property(simulation => simulation.ConcurrencyToken).IsConcurrencyToken();
        builder
            .HasOne<CognitiveMemoryScoreEvaluationTraceRecord>()
            .WithMany()
            .HasForeignKey(simulation => simulation.RiskScoreEvaluationTraceId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(simulation => new { simulation.ProjectId, simulation.Status, simulation.CreatedAtUtc });
        builder.HasIndex(simulation => new { simulation.ProjectId, simulation.OutputKind, simulation.RiskLevel });
        builder.HasIndex(simulation => simulation.RiskScoreEvaluationTraceId);
    }
}

internal sealed class CognitiveMemoryProcedureSimulationSkillRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryProcedureSimulationSkillRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryProcedureSimulationSkillRecord> builder)
    {
        builder.ToTable("CognitiveMemory_ProcedureSimulationSkills");
        builder.HasKey(link => link.Id);
        builder
            .HasOne<CognitiveMemoryProcedureSimulationRecord>()
            .WithMany()
            .HasForeignKey(link => link.SimulationId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne<CognitiveMemoryProcedureSkillRecord>()
            .WithMany()
            .HasForeignKey(link => link.ProcedureSkillId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(link => new { link.SimulationId, link.ProcedureSkillId }).IsUnique();
        builder.HasIndex(link => new { link.ProjectId, link.ProcedureSkillId });
    }
}

internal sealed class CognitiveMemoryProcedureSimulationEvidenceRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryProcedureSimulationEvidenceRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryProcedureSimulationEvidenceRecord> builder)
    {
        builder.ToTable("CognitiveMemory_ProcedureSimulationEvidence");
        builder.HasKey(evidence => evidence.Id);
        builder
            .HasOne<CognitiveMemoryProcedureSimulationRecord>()
            .WithMany()
            .HasForeignKey(evidence => evidence.SimulationId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne<CognitiveMemoryEvidenceAnchorRecord>()
            .WithMany()
            .HasForeignKey(evidence => evidence.EvidenceAnchorId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(evidence => new { evidence.SimulationId, evidence.EvidenceAnchorId }).IsUnique();
        builder.HasIndex(evidence => new { evidence.ProjectId, evidence.EvidenceAnchorId });
    }
}
