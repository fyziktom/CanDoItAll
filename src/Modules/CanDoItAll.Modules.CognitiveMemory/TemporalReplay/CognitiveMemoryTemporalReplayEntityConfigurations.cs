using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Modules.CognitiveMemory;

internal sealed class CognitiveMemoryTemporalEpisodeRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryTemporalEpisodeRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryTemporalEpisodeRecord> builder)
    {
        builder.ToTable("CognitiveMemory_TemporalEpisodes");
        builder.HasKey(episode => episode.Id);
        builder.Property(episode => episode.Goal).HasColumnType("TEXT");
        builder.Property(episode => episode.ExpectedOutcome).HasColumnType("TEXT");
        builder.Property(episode => episode.ActualOutcome).HasColumnType("TEXT");
        builder.Property(episode => episode.OutcomeSummary).HasColumnType("TEXT");
        builder.Property(episode => episode.AlgorithmVersion).HasMaxLength(120).IsRequired();
        builder.Property(episode => episode.MetadataJson).HasColumnType("TEXT");
        builder.Property(episode => episode.ConcurrencyToken).IsConcurrencyToken();
        builder.HasIndex(episode => new { episode.ProjectId, episode.EpisodeKind, episode.StartedAtUtc });
        builder.HasIndex(episode => new { episode.ProjectId, episode.EndedAtUtc });
    }
}

internal sealed class CognitiveMemoryEpisodeStepRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryEpisodeStepRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryEpisodeStepRecord> builder)
    {
        builder.ToTable("CognitiveMemory_EpisodeSteps");
        builder.HasKey(step => step.Id);
        builder.Property(step => step.ActorId).HasMaxLength(200).IsRequired();
        builder.Property(step => step.Summary).HasColumnType("TEXT");
        builder.Property(step => step.ToolOrPluginKey).HasMaxLength(200).IsRequired();
        builder.Property(step => step.ErrorCode).HasMaxLength(120).IsRequired();
        builder.Property(step => step.ErrorSummary).HasColumnType("TEXT");
        builder.Property(step => step.MetadataJson).HasColumnType("TEXT");
        builder.Property(step => step.ConcurrencyToken).IsConcurrencyToken();
        builder
            .HasOne<CognitiveMemoryTemporalEpisodeRecord>()
            .WithMany()
            .HasForeignKey(step => step.EpisodeId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(step => new { step.EpisodeId, step.SequenceIndex }).IsUnique();
        builder.HasIndex(step => new { step.ProjectId, step.OccurredAtUtc });
        builder.HasIndex(step => new { step.ProjectId, step.ActorKind, step.ActorId });
    }
}

internal sealed class CognitiveMemoryTemporalEpisodeLinkRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryTemporalEpisodeLinkRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryTemporalEpisodeLinkRecord> builder)
    {
        builder.ToTable("CognitiveMemory_TemporalEpisodeLinks");
        builder.HasKey(link => link.Id);
        builder.Property(link => link.TargetKey).HasMaxLength(300).IsRequired();
        builder.Property(link => link.Summary).HasColumnType("TEXT");
        builder
            .HasOne<CognitiveMemoryTemporalEpisodeRecord>()
            .WithMany()
            .HasForeignKey(link => link.EpisodeId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(link => new { link.EpisodeId, link.LinkKind, link.TargetId, link.TargetKey }).IsUnique();
        builder.HasIndex(link => new { link.ProjectId, link.LinkKind, link.TargetId });
    }
}

internal sealed class CognitiveMemoryEpisodeStepEvidenceRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryEpisodeStepEvidenceRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryEpisodeStepEvidenceRecord> builder)
    {
        builder.ToTable("CognitiveMemory_EpisodeStepEvidence");
        builder.HasKey(evidence => evidence.Id);
        builder
            .HasOne<CognitiveMemoryEpisodeStepRecord>()
            .WithMany()
            .HasForeignKey(evidence => evidence.StepId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne<CognitiveMemoryEvidenceAnchorRecord>()
            .WithMany()
            .HasForeignKey(evidence => evidence.EvidenceAnchorId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(evidence => new { evidence.StepId, evidence.EvidenceRole, evidence.EvidenceAnchorId }).IsUnique();
        builder.HasIndex(evidence => new { evidence.ProjectId, evidence.EvidenceAnchorId });
    }
}

internal sealed class CognitiveMemoryEpisodeCausalLinkRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryEpisodeCausalLinkRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryEpisodeCausalLinkRecord> builder)
    {
        builder.ToTable("CognitiveMemory_EpisodeCausalLinks");
        builder.HasKey(link => link.Id);
        builder.Property(link => link.Summary).HasColumnType("TEXT");
        builder
            .HasOne<CognitiveMemoryTemporalEpisodeRecord>()
            .WithMany()
            .HasForeignKey(link => link.EpisodeId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne<CognitiveMemoryEpisodeStepRecord>()
            .WithMany()
            .HasForeignKey(link => link.FromStepId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<CognitiveMemoryEpisodeStepRecord>()
            .WithMany()
            .HasForeignKey(link => link.ToStepId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<CognitiveMemoryEvidenceAnchorRecord>()
            .WithMany()
            .HasForeignKey(link => link.EvidenceAnchorId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<CognitiveMemoryClaimRecord>()
            .WithMany()
            .HasForeignKey(link => link.ClaimId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<CognitiveMemoryPredictionErrorRecord>()
            .WithMany()
            .HasForeignKey(link => link.PredictionErrorId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(link => new { link.EpisodeId, link.LinkKind, link.FromStepId, link.ToStepId });
        builder.HasIndex(link => new { link.ProjectId, link.PredictionErrorId });
        builder.HasIndex(link => new { link.ProjectId, link.ClaimId });
    }
}

internal sealed class CognitiveMemoryReplayJobRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryReplayJobRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryReplayJobRecord> builder)
    {
        builder.ToTable("CognitiveMemory_ReplayJobs");
        builder.HasKey(job => job.Id);
        builder.Property(job => job.Reason).HasColumnType("TEXT");
        builder.Property(job => job.InputHash).HasMaxLength(128).IsRequired();
        builder.Property(job => job.ExpectedOutputSchema).HasMaxLength(200).IsRequired();
        builder.Property(job => job.AlgorithmVersion).HasMaxLength(120).IsRequired();
        builder.Property(job => job.PolicyProfileId).HasMaxLength(160).IsRequired();
        builder.Property(job => job.SourceScopeKey).HasMaxLength(300).IsRequired();
        builder.Property(job => job.LeaseToken).HasMaxLength(160).IsRequired();
        builder.Property(job => job.FailureCode).HasMaxLength(120).IsRequired();
        builder.Property(job => job.FailureMessage).HasColumnType("TEXT");
        builder.Property(job => job.MetadataJson).HasColumnType("TEXT");
        builder.Property(job => job.ConcurrencyToken).IsConcurrencyToken();
        builder
            .HasOne<CognitiveMemoryScoreEvaluationTraceRecord>()
            .WithMany()
            .HasForeignKey(job => job.PriorityScoreEvaluationTraceId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(job => new { job.ProjectId, job.State, job.ScheduledAtUtc });
        builder.HasIndex(job => new { job.ProjectId, job.JobKind, job.QueuePriority });
        builder.HasIndex(job => new { job.ProjectId, job.JobKind, job.InputHash }).IsUnique();
        builder.HasIndex(job => job.PriorityScoreEvaluationTraceId);
    }
}

internal sealed class CognitiveMemoryReplayJobTargetRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryReplayJobTargetRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryReplayJobTargetRecord> builder)
    {
        builder.ToTable("CognitiveMemory_ReplayJobTargets");
        builder.HasKey(target => target.Id);
        builder.Property(target => target.TargetKey).HasMaxLength(300).IsRequired();
        builder.Property(target => target.RequiredInputHash).HasMaxLength(128).IsRequired();
        builder.Property(target => target.Summary).HasColumnType("TEXT");
        builder
            .HasOne<CognitiveMemoryReplayJobRecord>()
            .WithMany()
            .HasForeignKey(target => target.ReplayJobId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(target => new { target.ReplayJobId, target.TargetKind, target.TargetId, target.TargetKey }).IsUnique();
        builder.HasIndex(target => new { target.ProjectId, target.TargetKind, target.TargetId });
    }
}

internal sealed class CognitiveMemoryReplayJobSignalRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryReplayJobSignalRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryReplayJobSignalRecord> builder)
    {
        builder.ToTable("CognitiveMemory_ReplayJobSignals");
        builder.HasKey(link => link.Id);
        builder
            .HasOne<CognitiveMemoryReplayJobRecord>()
            .WithMany()
            .HasForeignKey(link => link.ReplayJobId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne<CognitiveMemorySignalRecord>()
            .WithMany()
            .HasForeignKey(link => link.CognitiveSignalId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(link => new { link.ReplayJobId, link.CognitiveSignalId }).IsUnique();
        builder.HasIndex(link => new { link.ProjectId, link.CognitiveSignalId });
    }
}

internal sealed class CognitiveMemoryReplayJobPredictionErrorRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryReplayJobPredictionErrorRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryReplayJobPredictionErrorRecord> builder)
    {
        builder.ToTable("CognitiveMemory_ReplayJobPredictionErrors");
        builder.HasKey(link => link.Id);
        builder
            .HasOne<CognitiveMemoryReplayJobRecord>()
            .WithMany()
            .HasForeignKey(link => link.ReplayJobId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne<CognitiveMemoryPredictionErrorRecord>()
            .WithMany()
            .HasForeignKey(link => link.PredictionErrorId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(link => new { link.ReplayJobId, link.PredictionErrorId }).IsUnique();
        builder.HasIndex(link => new { link.ProjectId, link.PredictionErrorId });
    }
}

internal sealed class CognitiveMemoryReplayOutputRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryReplayOutputRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryReplayOutputRecord> builder)
    {
        builder.ToTable("CognitiveMemory_ReplayOutputs");
        builder.HasKey(output => output.Id);
        builder.Property(output => output.Summary).HasColumnType("TEXT");
        builder.Property(output => output.PayloadHash).HasMaxLength(128).IsRequired();
        builder.Property(output => output.PayloadJson).HasColumnType("TEXT");
        builder
            .HasOne<CognitiveMemoryReplayJobRecord>()
            .WithMany()
            .HasForeignKey(output => output.ReplayJobId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne<CognitiveMemoryReviewItemRecord>()
            .WithMany()
            .HasForeignKey(output => output.ReviewItemId)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<CognitiveMemoryMutationCommandRecord>()
            .WithMany()
            .HasForeignKey(output => output.MutationCommandId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(output => new { output.ProjectId, output.OutputKind, output.Status });
        builder.HasIndex(output => output.ReplayJobId);
        builder.HasIndex(output => output.ReviewItemId);
        builder.HasIndex(output => output.MutationCommandId);
    }
}

internal sealed class CognitiveMemoryReplayWorkerResultRecordConfiguration : IEntityTypeConfiguration<CognitiveMemoryReplayWorkerResultRecord>
{
    public void Configure(EntityTypeBuilder<CognitiveMemoryReplayWorkerResultRecord> builder)
    {
        builder.ToTable("CognitiveMemory_ReplayWorkerResults");
        builder.HasKey(result => result.Id);
        builder.Property(result => result.WorkerId).HasMaxLength(200).IsRequired();
        builder.Property(result => result.InputHash).HasMaxLength(128).IsRequired();
        builder.Property(result => result.OutputHash).HasMaxLength(128).IsRequired();
        builder.Property(result => result.AlgorithmVersion).HasMaxLength(120).IsRequired();
        builder.Property(result => result.SourceScopeKey).HasMaxLength(300).IsRequired();
        builder.Property(result => result.PolicyProfileId).HasMaxLength(160).IsRequired();
        builder.Property(result => result.OutputSchema).HasMaxLength(200).IsRequired();
        builder.Property(result => result.ResultStorageReference).HasColumnType("TEXT");
        builder.Property(result => result.RejectionReason).HasColumnType("TEXT");
        builder.Property(result => result.WarningsJson).HasColumnType("TEXT");
        builder
            .HasOne<CognitiveMemoryReplayJobRecord>()
            .WithMany()
            .HasForeignKey(result => result.ReplayJobId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(result => new { result.ReplayJobId, result.WorkerId, result.SubmittedAtUtc });
        builder.HasIndex(result => new { result.ProjectId, result.Status, result.SubmittedAtUtc });
    }
}
