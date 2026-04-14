namespace CanDoItAll.Modules.Processes;

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

public sealed record ProcessStepRunResponsibilityPortViewModel(
    ProcessResponsibilityKind ResponsibilityKind,
    bool IsRequired,
    int AssignmentCount);

public sealed record ProcessStepRunArtifactPortViewModel(
    Guid ArtifactExpectationId,
    string Title,
    bool IsRequired);

public sealed record ProcessStepRunViewModel(
    Guid Id,
    Guid StepDefinitionId,
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
    public Guid StepRunConcurrencyToken { get; init; }

    public IReadOnlyList<ProcessStepDependencyViewModel> Dependencies { get; init; } = [];

    public string DecisionRoleTitle { get; init; } = string.Empty;

    public IReadOnlyList<ProcessStepRunResponsibilityPortViewModel> ResponsibilityPorts { get; init; } = [];

    public int ArtifactInputCount { get; init; }

    public IReadOnlyList<ProcessStepRunArtifactPortViewModel> ArtifactOutputs { get; init; } = [];
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

public sealed class ProcessRunStartRequest
{
    public Guid ProcessDefinitionId { get; set; }

    public Guid? ProjectId { get; set; }

    public string RunName { get; set; } = string.Empty;

    public ProcessOperatingMode OperatingMode { get; set; } = ProcessOperatingMode.AssistedExecution;

    public string TriggerReason { get; set; } = string.Empty;
}

public sealed class ProcessStepTransitionRequest
{
    public Guid StepRunId { get; set; }

    public Guid? StepRunConcurrencyToken { get; set; }

    public ProcessStepRunStatus TargetStatus { get; set; } = ProcessStepRunStatus.InProgress;

    public string Reason { get; set; } = string.Empty;

    public Guid? SelectedBranchOutcomeId { get; set; }

    public string DecidedBy { get; set; } = string.Empty;
}

public sealed class ProcessAssignmentResolutionRequest
{
    public Guid ProcessRunId { get; set; }

    public Guid RoleRequirementId { get; set; }

    public Guid? StepDefinitionId { get; set; }

    public Guid? PartyId { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public string ExecutorKind { get; set; } = string.Empty;

    public string BindingReason { get; set; } = string.Empty;

    public bool IsFallback { get; set; }
}

public sealed class ProcessArtifactRecordRequest
{
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

