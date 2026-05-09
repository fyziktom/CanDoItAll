using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Collaboration;

namespace CanDoItAll.Modules.Processes;

public sealed record ProcessRunListItem(
    Guid Id,
    Guid ProcessDefinitionId,
    Guid ProcessDefinitionVersionId,
    Guid? ParentRunId,
    Guid? ParentStepRunId,
    Guid RootRunId,
    int HierarchyDepth,
    Guid? ProjectId,
    string Name,
    ProcessRunStatus Status,
    ProcessOperatingMode OperatingMode,
    Guid? ManagerAgentId,
    string ManagerAgentName,
    int CompletedStepCount,
    int TotalStepCount,
    int BlockedStepCount,
    int CapabilityGapCount,
    decimal EstimatedCost,
    decimal ActualCost,
    DateTimeOffset UpdatedAtUtc);

public enum ProcessArtifactExpectationSatisfactionStatus {
    Expected,
    Satisfied,
    AutoProjected,
    Missing,
    ProjectionFailed,
    NotApplicable
}

public enum ProcessArtifactExpectationSourceKind {
    None,
    ProcessArtifactRecord,
    AgentExecutionArtifact,
    AssistantResponse,
    ProcessMockArtifact,
    CompletedDecision,
    ProviderNativeBrowserArtifact
}

public enum ProcessRecoveryClassification {
    None,
    AutomaticRetry,
    CrashRecovery,
    ContextResetRetry,
    ProviderRepairRetry,
    ManualRerun,
    MissingArtifact,
    OutboxDeadLetter
}

public enum ProcessOutboxHealthStatus {
    Pending,
    Leased,
    WaitingToRetry,
    Completed,
    DeadLettered
}

public sealed record ProcessArtifactExpectationSatisfactionViewModel(
    Guid StepRunId,
    Guid ArtifactExpectationId,
    ProcessArtifactKind ArtifactKind,
    string Title,
    bool IsRequired,
    ProcessArtifactExpectationSatisfactionStatus Status,
    ProcessArtifactExpectationSourceKind SourceKind,
    Guid? ProcessArtifactRecordId,
    string SatisfiedByTitle,
    string ManagedStoragePath,
    string Diagnostic);

public sealed record ProcessStepExecutionAttemptViewModel(
    Guid ExecutionRunId,
    string StatusBadgeText,
    string StatusTone,
    string RawStatusBadgeText,
    ExecutionState State,
    RunOutcome? Outcome,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    bool IsLatest);

public sealed record ProcessStepRunHealthViewModel(
    int AttemptCount,
    string LatestAttemptStatus,
    string LatestAttemptTone,
    int PendingApprovalCount,
    ProcessRecoveryClassification RecoveryClassification,
    string ActionableReason,
    bool CanManualRerun,
    int PendingOutboxCount,
    int DeadLetteredOutboxCount,
    IReadOnlyList<ProcessStepExecutionAttemptViewModel> Attempts)
{
    public static ProcessStepRunHealthViewModel Empty { get; } = new(
        0,
        string.Empty,
        "neutral",
        0,
        ProcessRecoveryClassification.None,
        string.Empty,
        false,
        0,
        0,
        []);
}

public sealed record ProcessOutboxRecordViewModel(
    Guid Id,
    Guid? StepRunId,
    string CommandKey,
    ProcessOutboxRecordStatus Status,
    ProcessOutboxHealthStatus HealthStatus,
    int AttemptCount,
    DateTimeOffset? LastAttemptAtUtc,
    DateTimeOffset? NextAttemptAtUtc,
    DateTimeOffset? LeaseExpiresAtUtc,
    DateTimeOffset? CompletedAtUtc,
    string LastError,
    string Trigger,
    DateTimeOffset UpdatedAtUtc);

public sealed record ProcessRunHealthSummaryViewModel(
    int ActiveExecutionCount,
    int LatestAttemptCount,
    int PendingApprovalCount,
    int BlockedStepCount,
    int FailedStepCount,
    int WaitingApprovalStepCount,
    int MissingArtifactCount,
    int PendingOutboxCount,
    int DeadLetteredOutboxCount,
    ProcessRecoveryClassification RecoveryClassification,
    string ActionableReason)
{
    public static ProcessRunHealthSummaryViewModel Empty { get; } = new(
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        ProcessRecoveryClassification.None,
        string.Empty);
}

public sealed record ProcessLaunchPlanListItem(
    Guid Id,
    Guid ProcessDefinitionId,
    Guid ProcessDefinitionVersionId,
    Guid? ProjectId,
    string Name,
    ProcessOperatingMode OperatingMode,
    ProcessLaunchPlanStatus Status,
    int ResolvedRoleCount,
    int TotalRoleCount,
    int PendingProvisioningCount,
    DateTimeOffset UpdatedAtUtc)
{
    public Guid? GeneratedRunId { get; init; }

    public string StatusBadgeText { get; init; } = string.Empty;

    public string StatusTone { get; init; } = "neutral";

    public string PlanningStatusBadgeText { get; init; } = string.Empty;

    public string StatusDetail { get; init; } = string.Empty;
}

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

public sealed record ProcessSubprocessRunSummaryViewModel(
    Guid RunId,
    Guid ProcessDefinitionId,
    Guid? ProjectId,
    string RunName,
    ProcessRunStatus Status,
    int CompletedStepCount,
    int TotalStepCount,
    int BlockedStepCount,
    DateTimeOffset UpdatedAtUtc);

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

    public string ExceptionSummary { get; init; } = string.Empty;

    public IReadOnlyList<ProcessArtifactExpectationSatisfactionViewModel> ArtifactExpectations { get; init; } = [];

    public ProcessStepRunHealthViewModel Health { get; init; } = ProcessStepRunHealthViewModel.Empty;

    public ProcessSubprocessRunSummaryViewModel? SubprocessRun { get; init; }
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
    Guid? StepRunId,
    Guid? ArtifactExpectationId,
    ProcessArtifactKind ArtifactKind,
    string Title,
    ProcessArtifactTrustStatus TrustStatus,
    ProcessSensitivityLevel SensitivityLevel,
    string ProvenanceSummary,
    string AllowedFutureUsageSummary,
    string ManagedStoragePath,
    string ExternalReferenceKey,
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
    bool IsCapabilityGap,
    bool AllowsDirectMessaging)
{
    public string RoleDisplayName { get; init; } = string.Empty;
}

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

public sealed record ProcessDirectMessageEntryViewModel(
    Guid MessageId,
    CollaborationMessageKind MessageKind,
    string AuthorName,
    string Body,
    DateTimeOffset CreatedAtUtc);

public sealed record ProcessDirectMessageThreadViewModel(
    Guid ThreadId,
    string Subject,
    string Route,
    string ParticipantSummary,
    int MessageCount,
    int UnreadCount,
    DateTimeOffset LastActivityAtUtc,
    IReadOnlyList<ProcessDirectMessageEntryViewModel> Messages);

public sealed record ProcessExecutionApprovalViewModel(
    string ApprovalId,
    string ToolName,
    string ToolKind,
    ExecutionApprovalStatus Status,
    string Details,
    DateTimeOffset RequestedAtUtc,
    DateTimeOffset? DecidedAtUtc,
    string DecisionNotes);

public sealed record ProcessExecutionArtifactViewModel(
    Guid Id,
    string ArtifactKind,
    string DisplayName,
    string RelativePath,
    string ContentType,
    string ProducedBy,
    string Summary,
    DateTimeOffset CreatedAtUtc);

public sealed record ProcessExecutionCheckpointViewModel(
    Guid Id,
    string CheckpointKind,
    ExecutionState RunState,
    int PendingApprovalCount,
    DateTimeOffset CapturedAtUtc,
    DateTimeOffset? ResumedAtUtc);

public sealed record ProcessExecutionToolReceiptViewModel(
    Guid Id,
    string ToolFamily,
    string ToolName,
    string RiskClass,
    string ApprovalMode,
    string IsolationGuarantee,
    string RequestSummary,
    string WorkingDirectory,
    string ExitSummary,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc);

public sealed record ProcessExecutionRunViewModel(
    Guid Id,
    Guid AgentId,
    Guid? StepRunId,
    string StepTitle,
    string AgentName,
    string AgentRoleTitle,
    string Title,
    string ProviderName,
    string Model,
    ExecutionState State,
    RunOutcome? Outcome,
    string InputSummary,
    string ResultSummary,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    int LogEntryCount)
{
    public string StatusBadgeText { get; init; } = string.Empty;

    public string StatusTone { get; init; } = "neutral";

    public string RawStatusBadgeText { get; init; } = string.Empty;

    public string StatusDetail { get; init; } = string.Empty;

    public bool HasBrowserEvidenceToolInvocation { get; init; }

    public IReadOnlyList<ProcessExecutionApprovalViewModel> Approvals { get; init; } = [];

    public IReadOnlyList<ProcessExecutionArtifactViewModel> Artifacts { get; init; } = [];

    public IReadOnlyList<ProcessExecutionCheckpointViewModel> Checkpoints { get; init; } = [];

    public IReadOnlyList<ProcessExecutionToolReceiptViewModel> ToolReceipts { get; init; } = [];
}

public sealed record ProcessActiveAgentViewModel(
    Guid ExecutionRunId,
    Guid AgentId,
    string AgentName,
    string AgentRoleTitle,
    string StepTitle,
    ExecutionState State,
    RunOutcome? Outcome,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset UpdatedAtUtc)
{
    public string StatusBadgeText { get; init; } = string.Empty;

    public string StatusTone { get; init; } = "neutral";
}

public sealed record ProcessActiveRunHealthMetrics(
    Guid RunId,
    int PendingOutboxCount,
    int DeadLetteredOutboxCount,
    int BlockedOrFailedStepCount,
    IReadOnlyDictionary<Guid, string> StepTitlesByStepRunId)
{
    public static ProcessActiveRunHealthMetrics Empty(Guid runId)
    {
        return new ProcessActiveRunHealthMetrics(
            runId,
            0,
            0,
            0,
            new Dictionary<Guid, string>());
    }
}

public sealed record ProcessActiveRunSummaryViewModel(
    Guid RunId,
    string RunName,
    ProcessRunStatus RunStatus,
    DateTimeOffset UpdatedAtUtc,
    int ActiveExecutionCount,
    int PendingApprovalCount)
{
    public IReadOnlyList<ProcessActiveAgentViewModel> Agents { get; init; } = [];

    public int PendingOutboxCount { get; init; }

    public int DeadLetteredOutboxCount { get; init; }

    public int BlockedOrFailedStepCount { get; init; }

    public string HealthSummary { get; init; } = string.Empty;
}

public sealed record ProcessImprovementViewModel(
    Guid Id,
    string Title,
    string Category,
    string ProblemSummary,
    ProcessImprovementStatus Status,
    bool IsTrainingOpportunity,
    bool RequiresGovernanceReview);

public sealed record ProcessLaunchCandidateViewModel(
    Guid Id,
    ProcessLaunchCandidateKind CandidateKind,
    Guid? PartyId,
    Guid? TechnicalAgentId,
    string DisplayName,
    string ExecutorKind,
    decimal Score,
    bool IsRecommended,
    bool AllowsDirectMessaging,
    bool RequiresProvisioning,
    string RecommendationSummary,
    string AvailabilitySummary,
    string SourceRegistryKey);

public sealed record ProcessLaunchRoleViewModel(
    Guid Id,
    Guid RoleRequirementId,
    string RoleKey,
    string DisplayName,
    string PreferredExecutorKind,
    bool IsRequired,
    bool RequiresExplicitApproval,
    bool RequiresProvisioning,
    bool IsResolved,
    Guid? SelectedCandidateId,
    string RecommendationSummary,
    string SelectionSummary,
    string ReadinessSummary,
    IReadOnlyList<Guid> RequiredSkillIds,
    IReadOnlyList<ProcessLaunchCandidateViewModel> Candidates);

public sealed record ProcessLaunchApprovalViewModel(
    Guid Id,
    ProcessLaunchApprovalStatus Status,
    Guid? ApproverPartyId,
    string ApproverDisplayName,
    string ApproverKind,
    Guid? HumanSubstitutePartyId,
    string HumanSubstituteName,
    Guid? CollaborationThreadId,
    string RequestMessage,
    string ResolutionSummary,
    string DecidedBy,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? DecidedAtUtc);

public sealed record ProcessLaunchProvisioningViewModel(
    Guid Id,
    Guid LaunchPlanRoleId,
    Guid SelectedCandidateId,
    ProcessLaunchProvisioningStatus Status,
    string RequestKind,
    string Title,
    Guid? ResultPartyId,
    Guid? ResultTechnicalAgentId,
    string ResultSummary,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? CompletedAtUtc);

public sealed record ProcessLaunchPlanDetails(
    Guid Id,
    Guid ProcessDefinitionId,
    Guid ProcessDefinitionVersionId,
    Guid? ProjectId,
    string Name,
    ProcessOperatingMode OperatingMode,
    string TriggerReason,
    ProcessLaunchPlanStatus Status,
    string RecommendationStrategy,
    string FallbackStrategy,
    string Summary,
    Guid? ApprovalThreadId,
    Guid? GeneratedRunId,
    string RequestedBy,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? SubmittedAtUtc,
    DateTimeOffset? ApprovedAtUtc,
    DateTimeOffset? ExecutedAtUtc,
    IReadOnlyList<ProcessLaunchRoleViewModel> Roles,
    IReadOnlyList<ProcessLaunchApprovalViewModel> Approvals,
    IReadOnlyList<ProcessLaunchProvisioningViewModel> ProvisioningRequests)
{
    public string StatusBadgeText { get; init; } = string.Empty;

    public string StatusTone { get; init; } = "neutral";

    public string PlanningStatusBadgeText { get; init; } = string.Empty;

    public string StatusDetail { get; init; } = string.Empty;
}

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

    public ProcessProjectStructureContext? ProjectStructureContext { get; set; }

    public Guid? LaunchPlanId { get; set; }

    public Guid? ParentRunId { get; set; }

    public Guid? ParentStepRunId { get; set; }
}

public sealed class ProcessRunStopRequest
{
    public Guid ProcessRunId { get; set; }

    public string Reason { get; set; } = string.Empty;

    public string StoppedBy { get; set; } = "process-workspace";
}

public sealed class ProcessManagerDirectiveRequest
{
    public Guid ProcessRunId { get; set; }

    public string Directive { get; set; } = string.Empty;

    public string InstructedBy { get; set; } = "process-workspace";
}

public sealed class ProcessLaunchCreateRequest
{
    public Guid ProcessDefinitionId { get; set; }

    public Guid? ProjectId { get; set; }

    public string LaunchName { get; set; } = string.Empty;

    public ProcessOperatingMode OperatingMode { get; set; } = ProcessOperatingMode.AssistedExecution;

    public string TriggerReason { get; set; } = string.Empty;

    public ProcessProjectStructureContext? ProjectStructureContext { get; set; }

    public string RequestedBy { get; set; } = "process-workspace";
}

public sealed class ProcessLaunchCandidateSelectionRequest
{
    public Guid LaunchPlanId { get; set; }

    public Guid LaunchPlanRoleId { get; set; }

    public Guid CandidateId { get; set; }
}

public sealed class ProcessLaunchApprovalDecisionRequest
{
    public Guid LaunchPlanId { get; set; }

    public ProcessLaunchApprovalStatus Status { get; set; } = ProcessLaunchApprovalStatus.Approved;

    public string ResolutionSummary { get; set; } = string.Empty;

    public string DecidedBy { get; set; } = string.Empty;
}

public sealed class ProcessLaunchExecutionRequest
{
    public Guid LaunchPlanId { get; set; }

    public string RequestedBy { get; set; } = "process-workspace";
}

public sealed class ProcessStepTransitionRequest
{
    public Guid StepRunId { get; set; }

    public Guid? StepRunConcurrencyToken { get; set; }

    public ProcessStepRunStatus TargetStatus { get; set; } = ProcessStepRunStatus.InProgress;

    public string Reason { get; set; } = string.Empty;

    public Guid? SelectedBranchOutcomeId { get; set; }

    public string DecidedBy { get; set; } = string.Empty;

    public bool SuppressAutomationDispatch { get; set; }

    public bool AllowCompletedAgentRerun { get; internal set; }
}

public sealed class ProcessAgentStepRerunRequest
{
    public Guid StepRunId { get; set; }

    public Guid? StepRunConcurrencyToken { get; set; }

    public string OperatorReason { get; set; } = string.Empty;
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

    public bool AllowsDirectMessaging { get; set; } = true;
}

public sealed class ProcessArtifactRecordRequest
{
    public Guid ProcessRunId { get; set; }

    public Guid? StepRunId { get; set; }

    public Guid? ArtifactExpectationId { get; set; }

    public ProcessArtifactKind ArtifactKind { get; set; } = ProcessArtifactKind.Evidence;

    public string Title { get; set; } = string.Empty;

    public ProcessArtifactTrustStatus TrustStatus { get; set; } = ProcessArtifactTrustStatus.ReviewRequired;

    public ProcessSensitivityLevel SensitivityLevel { get; set; } = ProcessSensitivityLevel.Internal;

    public string ProvenanceSummary { get; set; } = string.Empty;

    public string AllowedFutureUsageSummary { get; set; } = string.Empty;

    public string ReviewSummary { get; set; } = string.Empty;

    public string ManagedStoragePath { get; set; } = string.Empty;

    public string ExternalReferenceKey { get; set; } = string.Empty;
}

public sealed class ProcessDirectMessageRequest
{
    public Guid ProcessRunId { get; set; }

    public Guid SourceRoleRequirementId { get; set; }

    public Guid TargetRoleRequirementId { get; set; }

    public string MessageBody { get; set; } = string.Empty;
}

