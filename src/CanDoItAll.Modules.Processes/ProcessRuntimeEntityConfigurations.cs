using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessRunConfiguration : IEntityTypeConfiguration<ProcessRun>
{
    public void Configure(EntityTypeBuilder<ProcessRun> builder)
    {
        builder.ToTable("Processes_Runs");
        builder.HasKey(run => run.Id);
        builder.Property(run => run.Name).HasMaxLength(200).IsRequired();
        builder.Property(run => run.Status).HasConversion<string>().HasMaxLength(48);
        builder.Property(run => run.OperatingMode).HasConversion<string>().HasMaxLength(48);
        builder.Property(run => run.TriggerReason).HasColumnType("TEXT");
        builder.Property(run => run.GovernanceSnapshot).HasColumnType("TEXT");
        builder.Property(run => run.PolicySnapshot).HasColumnType("TEXT");
        builder.Property(run => run.ExecutorSnapshotSummary).HasColumnType("TEXT");
        builder.Property(run => run.ReplayPackageKey).HasMaxLength(200);
        builder.HasIndex(run => run.ProcessDefinitionId);
        builder.HasIndex(run => run.ProjectId);
        builder.HasIndex(run => run.Status);
    }
}

internal sealed class ProcessStepRunConfiguration : IEntityTypeConfiguration<ProcessStepRun>
{
    public void Configure(EntityTypeBuilder<ProcessStepRun> builder)
    {
        builder.ToTable("Processes_StepRuns");
        builder.HasKey(step => step.Id);
        builder.Property(step => step.Title).HasMaxLength(200).IsRequired();
        builder.Property(step => step.StepKind).HasConversion<string>().HasMaxLength(48);
        builder.Property(step => step.Status).HasConversion<string>().HasMaxLength(48);
        builder.Property(step => step.RoleSnapshotSummary).HasColumnType("TEXT");
        builder.Property(step => step.CurrentExecutorName).HasMaxLength(200);
        builder.Property(step => step.DecisionSummary).HasColumnType("TEXT");
        builder.Property(step => step.BlockedReason).HasColumnType("TEXT");
        builder.Property(step => step.RefusalReason).HasColumnType("TEXT");
        builder.Property(step => step.ExceptionSummary).HasColumnType("TEXT");
        builder.Property(step => step.InputQualitySummary).HasColumnType("TEXT");
        builder.Property(step => step.SelectedBranchOutcomeTitle).HasMaxLength(200);
        builder.Property(step => step.CapabilityGapSeverity).HasConversion<string>().HasMaxLength(48);
        builder.HasIndex(step => new { step.ProcessRunId, step.Sequence }).IsUnique();
        builder.HasIndex(step => new { step.ProcessRunId, step.Status });
        builder.HasIndex(step => step.StepDefinitionId);
        builder.HasIndex(step => step.SelectedBranchOutcomeId);
    }
}

internal sealed class ProcessRunAssignmentConfiguration : IEntityTypeConfiguration<ProcessRunAssignment>
{
    public void Configure(EntityTypeBuilder<ProcessRunAssignment> builder)
    {
        builder.ToTable("Processes_RunAssignments");
        builder.HasKey(assignment => assignment.Id);
        builder.Property(assignment => assignment.DisplayName).HasMaxLength(200);
        builder.Property(assignment => assignment.ExecutorKind).HasMaxLength(80);
        builder.Property(assignment => assignment.BindingReason).HasColumnType("TEXT");
        builder.Property(assignment => assignment.SourceRegistryKey).HasMaxLength(160);
        builder.Property(assignment => assignment.SnapshotSummary).HasColumnType("TEXT");
        builder.HasIndex(assignment => new { assignment.ProcessRunId, assignment.RoleRequirementId, assignment.StepDefinitionId });
        builder.HasIndex(assignment => assignment.PartyId);
    }
}

internal sealed class ProcessWorkBriefConfiguration : IEntityTypeConfiguration<ProcessWorkBrief>
{
    public void Configure(EntityTypeBuilder<ProcessWorkBrief> builder)
    {
        builder.ToTable("Processes_WorkBriefs");
        builder.HasKey(brief => brief.Id);
        builder.Property(brief => brief.Title).HasMaxLength(200).IsRequired();
        builder.Property(brief => brief.WorkBriefText).HasColumnType("TEXT");
        builder.Property(brief => brief.HandoffSummary).HasColumnType("TEXT");
        builder.Property(brief => brief.AssignmentReason).HasColumnType("TEXT");
        builder.Property(brief => brief.ExpectedOutcome).HasColumnType("TEXT");
        builder.Property(brief => brief.EvidenceExpectationSummary).HasColumnType("TEXT");
        builder.HasIndex(brief => brief.ProcessRunId);
        builder.HasIndex(brief => brief.StepRunId);
    }
}

internal sealed class ProcessDecisionRecordConfiguration : IEntityTypeConfiguration<ProcessDecisionRecord>
{
    public void Configure(EntityTypeBuilder<ProcessDecisionRecord> builder)
    {
        builder.ToTable("Processes_DecisionRecords");
        builder.HasKey(record => record.Id);
        builder.Property(record => record.DecisionKind).HasConversion<string>().HasMaxLength(48);
        builder.Property(record => record.Outcome).HasConversion<string>().HasMaxLength(48);
        builder.Property(record => record.Title).HasMaxLength(200).IsRequired();
        builder.Property(record => record.Reason).HasColumnType("TEXT");
        builder.Property(record => record.PolicyEvaluation).HasColumnType("TEXT");
        builder.Property(record => record.BranchOutcomeTitle).HasMaxLength(200);
        builder.Property(record => record.DecidedBy).HasMaxLength(160);
        builder.Property(record => record.OperatingMode).HasConversion<string>().HasMaxLength(48);
        builder.HasIndex(record => new { record.ProcessRunId, record.CreatedAtUtc });
        builder.HasIndex(record => record.StepRunId);
        builder.HasIndex(record => record.BranchOutcomeId);
    }
}

internal sealed class ProcessArtifactRecordConfiguration : IEntityTypeConfiguration<ProcessArtifactRecord>
{
    public void Configure(EntityTypeBuilder<ProcessArtifactRecord> builder)
    {
        builder.ToTable("Processes_ArtifactRecords");
        builder.HasKey(record => record.Id);
        builder.Property(record => record.ArtifactKind).HasConversion<string>().HasMaxLength(48);
        builder.Property(record => record.Title).HasMaxLength(200).IsRequired();
        builder.Property(record => record.TrustStatus).HasConversion<string>().HasMaxLength(48);
        builder.Property(record => record.SensitivityLevel).HasConversion<string>().HasMaxLength(48);
        builder.Property(record => record.ProvenanceSummary).HasColumnType("TEXT");
        builder.Property(record => record.AllowedFutureUsageSummary).HasColumnType("TEXT");
        builder.Property(record => record.ReviewSummary).HasColumnType("TEXT");
        builder.Property(record => record.ManagedStoragePath).HasMaxLength(500);
        builder.Property(record => record.ExternalReferenceKey).HasMaxLength(200);
        builder.HasIndex(record => record.ProcessRunId);
        builder.HasIndex(record => record.StepRunId);
    }
}

internal sealed class ProcessJournalEntryConfiguration : IEntityTypeConfiguration<ProcessJournalEntry>
{
    public void Configure(EntityTypeBuilder<ProcessJournalEntry> builder)
    {
        builder.ToTable("Processes_JournalEntries");
        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.EventType).HasMaxLength(120).IsRequired();
        builder.Property(entry => entry.Title).HasMaxLength(200).IsRequired();
        builder.Property(entry => entry.Description).HasColumnType("TEXT");
        builder.Property(entry => entry.CorrelationId).HasMaxLength(120);
        builder.Property(entry => entry.OperatingMode).HasConversion<string>().HasMaxLength(48);
        builder.Property(entry => entry.PolicyVersion).HasMaxLength(120);
        builder.Property(entry => entry.EnvironmentMode).HasMaxLength(120);
        builder.Property(entry => entry.ReplayContextJson).HasColumnType("TEXT");
        builder.HasIndex(entry => new { entry.ProcessRunId, entry.OccurredAtUtc });
        builder.HasIndex(entry => entry.StepRunId);
    }
}

internal sealed class ProcessConformanceObservationConfiguration : IEntityTypeConfiguration<ProcessConformanceObservation>
{
    public void Configure(EntityTypeBuilder<ProcessConformanceObservation> builder)
    {
        builder.ToTable("Processes_ConformanceObservations");
        builder.HasKey(observation => observation.Id);
        builder.Property(observation => observation.Severity).HasConversion<string>().HasMaxLength(48);
        builder.Property(observation => observation.Category).HasMaxLength(120).IsRequired();
        builder.Property(observation => observation.Observation).HasColumnType("TEXT");
        builder.Property(observation => observation.DeviationReason).HasColumnType("TEXT");
        builder.HasIndex(observation => observation.ProcessRunId);
        builder.HasIndex(observation => observation.StepRunId);
    }
}

internal sealed class ProcessImprovementCandidateConfiguration : IEntityTypeConfiguration<ProcessImprovementCandidate>
{
    public void Configure(EntityTypeBuilder<ProcessImprovementCandidate> builder)
    {
        builder.ToTable("Processes_ImprovementCandidates");
        builder.HasKey(candidate => candidate.Id);
        builder.Property(candidate => candidate.Title).HasMaxLength(200).IsRequired();
        builder.Property(candidate => candidate.Category).HasMaxLength(120);
        builder.Property(candidate => candidate.ProblemSummary).HasColumnType("TEXT");
        builder.Property(candidate => candidate.EvidenceSummary).HasColumnType("TEXT");
        builder.Property(candidate => candidate.Status).HasConversion<string>().HasMaxLength(48);
        builder.HasIndex(candidate => candidate.ProcessDefinitionId);
        builder.HasIndex(candidate => candidate.ProcessRunId);
        builder.HasIndex(candidate => candidate.Status);
    }
}
