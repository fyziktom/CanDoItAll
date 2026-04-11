using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Modules.Processes;

public enum ProcessRunStatus {
    Draft,
    Active,
    Blocked,
    Completed,
    Cancelled,
    Failed
}

public enum ProcessStepRunStatus {
    Pending,
    Ready,
    InProgress,
    WaitingApproval,
    Blocked,
    Completed,
    Refused,
    Skipped,
    Failed
}

public enum ProcessOperatingMode {
    Simulation,
    Development,
    AssistedExecution,
    GovernedLive,
    Emergency
}

public enum ProcessDecisionKind {
    Assignment,
    Approval,
    Escalation,
    Exception,
    Refusal,
    Autonomy,
    Variant,
    ImportWarning,
    ImprovementCandidate
}

public enum ProcessDecisionOutcome {
    Proposed,
    Approved,
    Rejected,
    Escalated,
    Refused,
    Accepted,
    Recorded
}

public enum ProcessArtifactTrustStatus {
    Draft,
    ReviewRequired,
    Approved,
    Rejected,
    TrustedSource
}

public enum ProcessCapabilityGapSeverity {
    None,
    Attention,
    Critical
}

public enum ProcessConformanceSeverity {
    Low,
    Moderate,
    High,
    Critical
}

public enum ProcessImprovementStatus {
    Open,
    Planned,
    Accepted,
    Rejected,
    Closed
}

public sealed class ProcessRun {
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProcessDefinitionId { get; set; }

    public Guid ProcessDefinitionVersionId { get; set; }

    public Guid? ProjectId { get; set; }

    public string Name { get; set; } = string.Empty;

    public ProcessRunStatus Status { get; set; } = ProcessRunStatus.Draft;

    public ProcessOperatingMode OperatingMode { get; set; } = ProcessOperatingMode.AssistedExecution;

    public string TriggerReason { get; set; } = string.Empty;

    public string GovernanceSnapshot { get; set; } = string.Empty;

    public string PolicySnapshot { get; set; } = string.Empty;

    public string ExecutorSnapshotSummary { get; set; } = string.Empty;

    public string ReplayPackageKey { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public DateTimeOffset? StartedAtUtc { get; set; }

    public DateTimeOffset? CompletedAtUtc { get; set; }

    public decimal EstimatedCost { get; set; }

    public decimal ActualCost { get; set; }

    public int FirstTimeRightPercent { get; set; } = 100;

    public int SlaAttainmentPercent { get; set; } = 100;
}

public sealed class ProcessStepRun {
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProcessRunId { get; set; }

    public Guid StepDefinitionId { get; set; }

    public int Sequence { get; set; }

    public string Title { get; set; } = string.Empty;

    public ProcessStepKind StepKind { get; set; } = ProcessStepKind.Work;

    public ProcessStepRunStatus Status { get; set; } = ProcessStepRunStatus.Pending;

    public string RoleSnapshotSummary { get; set; } = string.Empty;

    public string CurrentExecutorName { get; set; } = string.Empty;

    public Guid? CurrentExecutorPartyId { get; set; }

    public string DecisionSummary { get; set; } = string.Empty;

    public string BlockedReason { get; set; } = string.Empty;

    public string RefusalReason { get; set; } = string.Empty;

    public string ExceptionSummary { get; set; } = string.Empty;

    public string InputQualitySummary { get; set; } = string.Empty;

    public Guid? SelectedBranchOutcomeId { get; set; }

    public string SelectedBranchOutcomeTitle { get; set; } = string.Empty;

    public DateTimeOffset? ReadyAtUtc { get; set; }

    public DateTimeOffset? StartedAtUtc { get; set; }

    public DateTimeOffset? CompletedAtUtc { get; set; }

    public int WaitMinutes { get; set; }

    public int TouchMinutes { get; set; }

    public int BlockedMinutes { get; set; }

    public int ReworkCount { get; set; }

    public ProcessCapabilityGapSeverity CapabilityGapSeverity { get; set; } = ProcessCapabilityGapSeverity.None;
}

public sealed class ProcessRunAssignment {
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProcessRunId { get; set; }

    public Guid RoleRequirementId { get; set; }

    public Guid? StepDefinitionId { get; set; }

    public Guid? PartyId { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public string ExecutorKind { get; set; } = string.Empty;

    public string BindingReason { get; set; } = string.Empty;

    public string SourceRegistryKey { get; set; } = string.Empty;

    public string SnapshotSummary { get; set; } = string.Empty;

    public bool IsFallback { get; set; }

    public bool IsCapabilityGap { get; set; }
}

public sealed class ProcessWorkBrief {
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProcessRunId { get; set; }

    public Guid? StepRunId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string WorkBriefText { get; set; } = string.Empty;

    public string HandoffSummary { get; set; } = string.Empty;

    public string AssignmentReason { get; set; } = string.Empty;

    public string ExpectedOutcome { get; set; } = string.Empty;

    public string EvidenceExpectationSummary { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class ProcessDecisionRecord {
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProcessRunId { get; set; }

    public Guid? StepRunId { get; set; }

    public ProcessDecisionKind DecisionKind { get; set; } = ProcessDecisionKind.Assignment;

    public ProcessDecisionOutcome Outcome { get; set; } = ProcessDecisionOutcome.Recorded;

    public string Title { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    public string PolicyEvaluation { get; set; } = string.Empty;

    public Guid? BranchOutcomeId { get; set; }

    public string BranchOutcomeTitle { get; set; } = string.Empty;

    public string DecidedBy { get; set; } = string.Empty;

    public ProcessOperatingMode OperatingMode { get; set; } = ProcessOperatingMode.AssistedExecution;

    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class ProcessArtifactRecord {
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProcessRunId { get; set; }

    public Guid? StepRunId { get; set; }

    public ProcessArtifactKind ArtifactKind { get; set; } = ProcessArtifactKind.Evidence;

    public string Title { get; set; } = string.Empty;

    public ProcessArtifactTrustStatus TrustStatus { get; set; } = ProcessArtifactTrustStatus.ReviewRequired;

    public ProcessSensitivityLevel SensitivityLevel { get; set; } = ProcessSensitivityLevel.Internal;

    public string ProvenanceSummary { get; set; } = string.Empty;

    public string AllowedFutureUsageSummary { get; set; } = string.Empty;

    public string ReviewSummary { get; set; } = string.Empty;

    public string ManagedStoragePath { get; set; } = string.Empty;

    public string ExternalReferenceKey { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class ProcessJournalEntry {
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProcessRunId { get; set; }

    public Guid? StepRunId { get; set; }

    public string EventType { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string CorrelationId { get; set; } = string.Empty;

    public ProcessOperatingMode OperatingMode { get; set; } = ProcessOperatingMode.AssistedExecution;

    public string PolicyVersion { get; set; } = string.Empty;

    public string EnvironmentMode { get; set; } = string.Empty;

    public string ReplayContextJson { get; set; } = "{}";

    public DateTimeOffset OccurredAtUtc { get; set; }
}

public sealed class ProcessConformanceObservation {
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProcessRunId { get; set; }

    public Guid? StepRunId { get; set; }

    public ProcessConformanceSeverity Severity { get; set; } = ProcessConformanceSeverity.Moderate;

    public string Category { get; set; } = string.Empty;

    public string Observation { get; set; } = string.Empty;

    public string DeviationReason { get; set; } = string.Empty;

    public bool IsSafeNonAction { get; set; }

    public bool ContainsSensitiveAssessment { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class ProcessImprovementCandidate {
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProcessDefinitionId { get; set; }

    public Guid? ProcessRunId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string ProblemSummary { get; set; } = string.Empty;

    public string EvidenceSummary { get; set; } = string.Empty;

    public ProcessImprovementStatus Status { get; set; } = ProcessImprovementStatus.Open;

    public bool IsTrainingOpportunity { get; set; }

    public bool RequiresGovernanceReview { get; set; } = true;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? ClosedAtUtc { get; set; }
}

internal sealed class ProcessRunConfiguration : IEntityTypeConfiguration<ProcessRun> {
    public void Configure(EntityTypeBuilder<ProcessRun> builder) {
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

internal sealed class ProcessStepRunConfiguration : IEntityTypeConfiguration<ProcessStepRun> {
    public void Configure(EntityTypeBuilder<ProcessStepRun> builder) {
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

internal sealed class ProcessRunAssignmentConfiguration : IEntityTypeConfiguration<ProcessRunAssignment> {
    public void Configure(EntityTypeBuilder<ProcessRunAssignment> builder) {
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

internal sealed class ProcessWorkBriefConfiguration : IEntityTypeConfiguration<ProcessWorkBrief> {
    public void Configure(EntityTypeBuilder<ProcessWorkBrief> builder) {
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

internal sealed class ProcessDecisionRecordConfiguration : IEntityTypeConfiguration<ProcessDecisionRecord> {
    public void Configure(EntityTypeBuilder<ProcessDecisionRecord> builder) {
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

internal sealed class ProcessArtifactRecordConfiguration : IEntityTypeConfiguration<ProcessArtifactRecord> {
    public void Configure(EntityTypeBuilder<ProcessArtifactRecord> builder) {
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

internal sealed class ProcessJournalEntryConfiguration : IEntityTypeConfiguration<ProcessJournalEntry> {
    public void Configure(EntityTypeBuilder<ProcessJournalEntry> builder) {
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

internal sealed class ProcessConformanceObservationConfiguration : IEntityTypeConfiguration<ProcessConformanceObservation> {
    public void Configure(EntityTypeBuilder<ProcessConformanceObservation> builder) {
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

internal sealed class ProcessImprovementCandidateConfiguration : IEntityTypeConfiguration<ProcessImprovementCandidate> {
    public void Configure(EntityTypeBuilder<ProcessImprovementCandidate> builder) {
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

public sealed record ProcessRunListItem(
    Guid Id,
    Guid ProcessDefinitionId,
    Guid ProcessDefinitionVersionId,
    Guid? ProjectId,
    string Name,
    ProcessRunStatus Status,
    ProcessOperatingMode OperatingMode,
    int CompletedStepCount,
    int TotalStepCount,
    int BlockedStepCount,
    int CapabilityGapCount,
    decimal EstimatedCost,
    decimal ActualCost,
    DateTimeOffset UpdatedAtUtc);

public sealed record ProcessStepBranchOutcomeOptionViewModel(
    Guid Id,
    string Title,
    string Description);

public sealed record ProcessStepDependencyViewModel(
    Guid DependsOnStepDefinitionId,
    Guid? DependsOnBranchOutcomeId);

public sealed record ProcessStepRunViewModel(
    Guid Id,
    Guid StepDefinitionId,
    Guid? DependsOnStepDefinitionId,
    Guid? DependsOnBranchOutcomeId,
    Guid? DecisionRoleRequirementId,
    int Sequence,
    string Title,
    ProcessStepKind StepKind,
    ProcessStepRunStatus Status,
    string CurrentExecutorName,
    string DecisionSummary,
    string BlockedReason,
    string RefusalReason,
    Guid? SelectedBranchOutcomeId,
    string SelectedBranchOutcomeTitle,
    int WaitMinutes,
    int TouchMinutes,
    int BlockedMinutes,
    int ReworkCount,
    ProcessCapabilityGapSeverity CapabilityGapSeverity,
    IReadOnlyList<ProcessStepBranchOutcomeOptionViewModel> AvailableBranchOutcomes)
{
    public IReadOnlyList<ProcessStepDependencyViewModel> Dependencies { get; init; } = [];
}

public sealed record ProcessDecisionViewModel(
    Guid Id,
    ProcessDecisionKind DecisionKind,
    ProcessDecisionOutcome Outcome,
    string Title,
    string Reason,
    string BranchOutcomeTitle,
    string DecidedBy,
    DateTimeOffset CreatedAtUtc);

public sealed record ProcessArtifactViewModel(
    Guid Id,
    ProcessArtifactKind ArtifactKind,
    string Title,
    ProcessArtifactTrustStatus TrustStatus,
    ProcessSensitivityLevel SensitivityLevel,
    string ProvenanceSummary,
    string AllowedFutureUsageSummary,
    DateTimeOffset CreatedAtUtc);

public sealed record ProcessRunAssignmentViewModel(
    Guid Id,
    Guid RoleRequirementId,
    Guid? StepDefinitionId,
    Guid? PartyId,
    string DisplayName,
    string ExecutorKind,
    string BindingReason,
    string SourceRegistryKey,
    string SnapshotSummary,
    bool IsFallback,
    bool IsCapabilityGap);

public sealed record ProcessWorkBriefViewModel(
    Guid Id,
    Guid? StepRunId,
    string Title,
    string WorkBriefText,
    string HandoffSummary,
    string AssignmentReason,
    string ExpectedOutcome,
    string EvidenceExpectationSummary,
    DateTimeOffset CreatedAtUtc);

public sealed record ProcessConformanceObservationViewModel(
    Guid Id,
    Guid? StepRunId,
    ProcessConformanceSeverity Severity,
    string Category,
    string Observation,
    string DeviationReason,
    bool IsSafeNonAction,
    bool ContainsSensitiveAssessment,
    DateTimeOffset CreatedAtUtc);

public sealed record ProcessImprovementViewModel(
    Guid Id,
    string Title,
    string Category,
    string ProblemSummary,
    ProcessImprovementStatus Status,
    bool IsTrainingOpportunity,
    bool RequiresGovernanceReview);

public sealed record ProcessAnalyticsSummary(
    int TotalRuns,
    int ActiveRuns,
    int CompletedRuns,
    int BlockedRuns,
    int CapabilityGapCount,
    int ImprovementCandidateCount,
    int ConformanceObservationCount,
    int SafeNonActionCount,
    int AverageLeadMinutes,
    int AverageWaitMinutes,
    int AverageBlockedMinutes,
    decimal EstimatedCost,
    decimal ActualCost);

public sealed class ProcessRunStartRequest {
    public Guid ProcessDefinitionId { get; set; }

    public Guid? ProjectId { get; set; }

    public string RunName { get; set; } = string.Empty;

    public ProcessOperatingMode OperatingMode { get; set; } = ProcessOperatingMode.AssistedExecution;

    public string TriggerReason { get; set; } = string.Empty;
}

public sealed class ProcessStepTransitionRequest {
    public Guid StepRunId { get; set; }

    public ProcessStepRunStatus TargetStatus { get; set; } = ProcessStepRunStatus.InProgress;

    public string Reason { get; set; } = string.Empty;

    public Guid? SelectedBranchOutcomeId { get; set; }

    public string DecidedBy { get; set; } = string.Empty;
}

public sealed class ProcessAssignmentResolutionRequest {
    public Guid ProcessRunId { get; set; }

    public Guid RoleRequirementId { get; set; }

    public Guid? StepDefinitionId { get; set; }

    public Guid? PartyId { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public string ExecutorKind { get; set; } = string.Empty;

    public string BindingReason { get; set; } = string.Empty;

    public bool IsFallback { get; set; }
}

public sealed class ProcessArtifactRecordRequest {
    public Guid ProcessRunId { get; set; }

    public Guid? StepRunId { get; set; }

    public ProcessArtifactKind ArtifactKind { get; set; } = ProcessArtifactKind.Evidence;

    public string Title { get; set; } = string.Empty;

    public ProcessArtifactTrustStatus TrustStatus { get; set; } = ProcessArtifactTrustStatus.ReviewRequired;

    public ProcessSensitivityLevel SensitivityLevel { get; set; } = ProcessSensitivityLevel.Internal;

    public string ProvenanceSummary { get; set; } = string.Empty;

    public string AllowedFutureUsageSummary { get; set; } = string.Empty;

    public string ReviewSummary { get; set; } = string.Empty;

    public string ManagedStoragePath { get; set; } = string.Empty;
}

public sealed class ProcessImportExportEnvelope {
    public ProcessDefinitionEditorModel Definition { get; set; } = new();

    public List<string> Warnings { get; set; } = [];

    public string SourceFormat { get; set; } = string.Empty;
}
