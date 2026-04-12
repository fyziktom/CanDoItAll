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

