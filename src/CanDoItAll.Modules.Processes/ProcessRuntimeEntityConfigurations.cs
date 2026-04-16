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
        builder.Property(run => run.ConcurrencyToken).IsConcurrencyToken();
        builder.HasIndex(run => run.ProcessDefinitionId);
        builder.HasIndex(run => run.ProjectId);
        builder.HasIndex(run => run.Status);
        builder.HasOne<ProcessDefinition>()
            .WithMany()
            .HasForeignKey(run => run.ProcessDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ProcessDefinitionVersion>()
            .WithMany()
            .HasForeignKey(run => new { run.ProcessDefinitionId, run.ProcessDefinitionVersionId })
            .HasPrincipalKey(version => new { version.ProcessDefinitionId, version.Id })
            .OnDelete(DeleteBehavior.Restrict);
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
        builder.Property(step => step.ConcurrencyToken).IsConcurrencyToken();
        builder.HasIndex(step => new { step.ProcessRunId, step.Sequence }).IsUnique();
        builder.HasIndex(step => new { step.ProcessRunId, step.StepDefinitionId })
            .IsUnique()
            .HasDatabaseName(ProcessPersistenceConstraintNames.StepRunPerDefinitionUniqueIndex);
        builder.HasIndex(step => new { step.ProcessRunId, step.Status });
        builder.HasIndex(step => step.StepDefinitionId);
        builder.HasIndex(step => step.SelectedBranchOutcomeId);
        builder.HasOne<ProcessRun>()
            .WithMany()
            .HasForeignKey(step => step.ProcessRunId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ProcessStepDefinition>()
            .WithMany()
            .HasForeignKey(step => step.StepDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProcessStepBranchOutcomeDefinition>()
            .WithMany()
            .HasForeignKey(step => step.SelectedBranchOutcomeId)
            .OnDelete(DeleteBehavior.SetNull);
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
        builder.Property(assignment => assignment.AllowsDirectMessaging).HasDefaultValue(false);
        builder.HasIndex(assignment => new { assignment.ProcessRunId, assignment.RoleRequirementId })
            .IsUnique()
            .HasDatabaseName(ProcessPersistenceConstraintNames.RunAssignmentRunScopedUniqueIndex)
            .HasFilter("\"StepDefinitionId\" IS NULL");
        builder.HasIndex(assignment => new { assignment.ProcessRunId, assignment.RoleRequirementId, assignment.StepDefinitionId })
            .IsUnique()
            .HasDatabaseName(ProcessPersistenceConstraintNames.RunAssignmentStepScopedUniqueIndex)
            .HasFilter("\"StepDefinitionId\" IS NOT NULL");
        builder.HasIndex(assignment => assignment.PartyId);
        builder.HasOne<ProcessRun>()
            .WithMany()
            .HasForeignKey(assignment => assignment.ProcessRunId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ProcessRoleRequirement>()
            .WithMany()
            .HasForeignKey(assignment => assignment.RoleRequirementId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProcessStepDefinition>()
            .WithMany()
            .HasForeignKey(assignment => assignment.StepDefinitionId)
            .OnDelete(DeleteBehavior.SetNull);
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
        builder.HasOne<ProcessRun>()
            .WithMany()
            .HasForeignKey(brief => brief.ProcessRunId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ProcessStepRun>()
            .WithMany()
            .HasForeignKey(brief => brief.StepRunId)
            .OnDelete(DeleteBehavior.SetNull);
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
        builder.HasOne<ProcessRun>()
            .WithMany()
            .HasForeignKey(record => record.ProcessRunId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ProcessStepRun>()
            .WithMany()
            .HasForeignKey(record => record.StepRunId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<ProcessStepBranchOutcomeDefinition>()
            .WithMany()
            .HasForeignKey(record => record.BranchOutcomeId)
            .OnDelete(DeleteBehavior.SetNull);
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
        builder.HasIndex(record => record.ArtifactExpectationId);
        builder.HasOne<ProcessRun>()
            .WithMany()
            .HasForeignKey(record => record.ProcessRunId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ProcessStepRun>()
            .WithMany()
            .HasForeignKey(record => record.StepRunId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<ProcessArtifactExpectation>()
            .WithMany()
            .HasForeignKey(record => record.ArtifactExpectationId)
            .OnDelete(DeleteBehavior.SetNull);
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
        builder.HasOne<ProcessRun>()
            .WithMany()
            .HasForeignKey(entry => entry.ProcessRunId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ProcessStepRun>()
            .WithMany()
            .HasForeignKey(entry => entry.StepRunId)
            .OnDelete(DeleteBehavior.SetNull);
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
        builder.HasOne<ProcessRun>()
            .WithMany()
            .HasForeignKey(observation => observation.ProcessRunId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ProcessStepRun>()
            .WithMany()
            .HasForeignKey(observation => observation.StepRunId)
            .OnDelete(DeleteBehavior.SetNull);
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
        builder.HasOne<ProcessDefinition>()
            .WithMany()
            .HasForeignKey(candidate => candidate.ProcessDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ProcessRun>()
            .WithMany()
            .HasForeignKey(candidate => candidate.ProcessRunId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class ProcessLaunchPlanConfiguration : IEntityTypeConfiguration<ProcessLaunchPlan>
{
    public void Configure(EntityTypeBuilder<ProcessLaunchPlan> builder)
    {
        builder.ToTable("Processes_LaunchPlans");
        builder.HasKey(plan => plan.Id);
        builder.Property(plan => plan.Name).HasMaxLength(200).IsRequired();
        builder.Property(plan => plan.OperatingMode).HasConversion<string>().HasMaxLength(48);
        builder.Property(plan => plan.TriggerReason).HasColumnType("TEXT");
        builder.Property(plan => plan.Status).HasConversion<string>().HasMaxLength(48);
        builder.Property(plan => plan.RecommendationStrategy).HasColumnType("TEXT");
        builder.Property(plan => plan.FallbackStrategy).HasColumnType("TEXT");
        builder.Property(plan => plan.Summary).HasColumnType("TEXT");
        builder.Property(plan => plan.RequestedBy).HasMaxLength(160);
        builder.Property(plan => plan.ConcurrencyToken).IsConcurrencyToken();
        builder.HasIndex(plan => new { plan.ProcessDefinitionId, plan.CreatedAtUtc });
        builder.HasIndex(plan => new { plan.ProjectId, plan.CreatedAtUtc });
        builder.HasIndex(plan => plan.Status);
        builder.HasIndex(plan => plan.GeneratedRunId);
        builder.HasOne<ProcessDefinition>()
            .WithMany()
            .HasForeignKey(plan => plan.ProcessDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ProcessDefinitionVersion>()
            .WithMany()
            .HasForeignKey(plan => new { plan.ProcessDefinitionId, plan.ProcessDefinitionVersionId })
            .HasPrincipalKey(version => new { version.ProcessDefinitionId, version.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProcessRun>()
            .WithMany()
            .HasForeignKey(plan => plan.GeneratedRunId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class ProcessLaunchPlanRoleConfiguration : IEntityTypeConfiguration<ProcessLaunchPlanRole>
{
    public void Configure(EntityTypeBuilder<ProcessLaunchPlanRole> builder)
    {
        builder.ToTable("Processes_LaunchPlanRoles");
        builder.HasKey(role => role.Id);
        builder.Property(role => role.RoleKey).HasMaxLength(120).IsRequired();
        builder.Property(role => role.DisplayName).HasMaxLength(200).IsRequired();
        builder.Property(role => role.PreferredExecutorKind).HasMaxLength(80);
        builder.Property(role => role.RequiredSkillIdsJson).HasColumnType("TEXT");
        builder.Property(role => role.RecommendationSummary).HasColumnType("TEXT");
        builder.Property(role => role.SelectionSummary).HasColumnType("TEXT");
        builder.Property(role => role.ReadinessSummary).HasColumnType("TEXT");
        builder.HasIndex(role => new { role.LaunchPlanId, role.DisplayOrder });
        builder.HasIndex(role => new { role.LaunchPlanId, role.RoleRequirementId })
            .IsUnique()
            .HasDatabaseName(ProcessPersistenceConstraintNames.LaunchPlanRoleUniqueIndex);
        builder.HasIndex(role => role.SelectedCandidateId);
        builder.HasOne<ProcessLaunchPlan>()
            .WithMany()
            .HasForeignKey(role => role.LaunchPlanId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ProcessRoleRequirement>()
            .WithMany()
            .HasForeignKey(role => role.RoleRequirementId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ProcessLaunchCandidateConfiguration : IEntityTypeConfiguration<ProcessLaunchCandidate>
{
    public void Configure(EntityTypeBuilder<ProcessLaunchCandidate> builder)
    {
        builder.ToTable("Processes_LaunchCandidates");
        builder.HasKey(candidate => candidate.Id);
        builder.Property(candidate => candidate.CandidateKind).HasConversion<string>().HasMaxLength(48);
        builder.Property(candidate => candidate.DisplayName).HasMaxLength(200).IsRequired();
        builder.Property(candidate => candidate.ExecutorKind).HasMaxLength(80);
        builder.Property(candidate => candidate.RecommendationSummary).HasColumnType("TEXT");
        builder.Property(candidate => candidate.AvailabilitySummary).HasColumnType("TEXT");
        builder.Property(candidate => candidate.SourceRegistryKey).HasMaxLength(160);
        builder.Property(candidate => candidate.MetadataJson).HasColumnType("TEXT");
        builder.HasIndex(candidate => new { candidate.LaunchPlanRoleId, candidate.Score });
        builder.HasIndex(candidate => candidate.PartyId);
        builder.HasIndex(candidate => candidate.TechnicalAgentId);
        builder.HasOne<ProcessLaunchPlanRole>()
            .WithMany()
            .HasForeignKey(candidate => candidate.LaunchPlanRoleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ProcessLaunchApprovalRecordConfiguration : IEntityTypeConfiguration<ProcessLaunchApprovalRecord>
{
    public void Configure(EntityTypeBuilder<ProcessLaunchApprovalRecord> builder)
    {
        builder.ToTable("Processes_LaunchApprovals");
        builder.HasKey(approval => approval.Id);
        builder.Property(approval => approval.Status).HasConversion<string>().HasMaxLength(48);
        builder.Property(approval => approval.ApproverDisplayName).HasMaxLength(200).IsRequired();
        builder.Property(approval => approval.ApproverKind).HasMaxLength(80);
        builder.Property(approval => approval.HumanSubstituteName).HasMaxLength(200);
        builder.Property(approval => approval.RequestMessage).HasColumnType("TEXT");
        builder.Property(approval => approval.ResolutionSummary).HasColumnType("TEXT");
        builder.Property(approval => approval.DecidedBy).HasMaxLength(160);
        builder.HasIndex(approval => new { approval.LaunchPlanId, approval.CreatedAtUtc });
        builder.HasIndex(approval => approval.Status);
        builder.HasIndex(approval => approval.CollaborationThreadId);
        builder.HasOne<ProcessLaunchPlan>()
            .WithMany()
            .HasForeignKey(approval => approval.LaunchPlanId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ProcessLaunchProvisioningRequestConfiguration : IEntityTypeConfiguration<ProcessLaunchProvisioningRequest>
{
    public void Configure(EntityTypeBuilder<ProcessLaunchProvisioningRequest> builder)
    {
        builder.ToTable("Processes_LaunchProvisioningRequests");
        builder.HasKey(request => request.Id);
        builder.Property(request => request.Status).HasConversion<string>().HasMaxLength(48);
        builder.Property(request => request.RequestKind).HasMaxLength(80);
        builder.Property(request => request.Title).HasMaxLength(200).IsRequired();
        builder.Property(request => request.RequestPayloadJson).HasColumnType("TEXT");
        builder.Property(request => request.ResultSummary).HasColumnType("TEXT");
        builder.HasIndex(request => new { request.LaunchPlanId, request.Status });
        builder.HasIndex(request => request.SelectedCandidateId);
        builder.HasIndex(request => new { request.LaunchPlanId, request.LaunchPlanRoleId })
            .IsUnique()
            .HasDatabaseName(ProcessPersistenceConstraintNames.LaunchPlanProvisioningRoleUniqueIndex);
        builder.HasOne<ProcessLaunchPlan>()
            .WithMany()
            .HasForeignKey(request => request.LaunchPlanId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ProcessLaunchPlanRole>()
            .WithMany()
            .HasForeignKey(request => request.LaunchPlanRoleId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProcessLaunchCandidate>()
            .WithMany()
            .HasForeignKey(request => request.SelectedCandidateId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
