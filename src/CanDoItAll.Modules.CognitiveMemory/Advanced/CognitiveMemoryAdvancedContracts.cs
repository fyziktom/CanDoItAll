using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using System.Text.Json.Serialization;

namespace CanDoItAll.Modules.CognitiveMemory;

public enum CognitiveMemoryProbeSessionStatus
{
    Draft = 0,
    Active = 1,
    Closed = 2,
    Abandoned = 3
}

public enum CognitiveMemoryProbeTurnStatus
{
    Asked = 0,
    Answered = 1,
    FeedbackRecorded = 2,
    Failed = 3
}

public enum CognitiveMemoryProbeFeedbackAction
{
    MarkCorrect = 0,
    MarkIncorrect = 1,
    NeedsSource = 2,
    WrongScope = 3,
    AddCorrection = 4,
    CreateRegression = 5,
    RequestReview = 6
}

public enum CognitiveMemoryProbeFindingKind
{
    Unknown = 0,
    MissingSource = 1,
    WrongScope = 2,
    Contradiction = 3,
    RedactionLimited = 4,
    Overconfident = 5,
    Underconfident = 6
}

public enum CognitiveMemoryCuratorRuntimeMode
{
    DirectLlm = 0,
    Agent = 1
}

public enum CognitiveMemoryCuratorConversationDepth
{
    Short = 0,
    Medium = 1,
    Long = 2
}

public enum CognitiveMemoryCuratorSessionStatus
{
    Active = 0,
    Closed = 1
}

public enum CognitiveMemoryCuratorCaptureKind
{
    NewKnowledge = 0,
    Correction = 1,
    WrongScope = 2
}

public enum CognitiveMemoryCuratorCaptureStatus
{
    Captured = 0,
    Applied = 1
}

public enum CognitiveMemoryCuratorTargetingStatus
{
    Untargeted = 0,
    ExplicitTarget = 1,
    InferredSingleTarget = 2,
    AmbiguousNeedsReview = 3
}

public enum CognitiveMemoryProfessorAnchorState
{
    Active = 0,
    Comparing = 1,
    Assimilated = 2,
    Faded = 3,
    Rejected = 4
}

public enum CognitiveMemoryProbeRegressionStatus
{
    Draft = 0,
    Active = 1,
    Retired = 2
}

public enum CognitiveMemoryProbeRegressionRunOutcome
{
    Unknown = 0,
    Passed = 1,
    Failed = 2,
    Blocked = 3
}

public enum CognitiveMemoryCalibrationOutcomeKind
{
    Unknown = 0,
    CorrectHighConfidence = 1,
    IncorrectHighConfidence = 2,
    CorrectLowConfidence = 3,
    IncorrectLowConfidence = 4,
    WrongScope = 5,
    SourceInsufficient = 6,
    AbstentionAppropriate = 7,
    AbstentionUnnecessary = 8,
    HumanReviewRejected = 9,
    ProfessorDisagreed = 10
}

public enum CognitiveMemorySelfModelStatus
{
    Draft = 0,
    Active = 1,
    Superseded = 2,
    Retired = 3
}

public enum CognitiveMemoryCompetenceLevel
{
    Unknown = 0,
    Weak = 1,
    Developing = 2,
    Reliable = 3,
    Strong = 4
}

public enum CognitiveMemoryKnownFailurePatternKind
{
    Unknown = 0,
    WrongScope = 1,
    GeneratedSummaryOverweight = 2,
    SourceInsufficientAnswer = 3,
    RedactionBlindSpot = 4,
    StaleVolatileSource = 5,
    HighRiskProcedureOverreach = 6
}

public enum CognitiveMemorySelfModelUpdateProposalStatus
{
    Draft = 0,
    PendingReview = 1,
    Approved = 2,
    Rejected = 3
}

public enum CognitiveMemorySelfRegulationStateKind
{
    Unknown = 0,
    Calibrated = 1,
    Exploratory = 2,
    Overconfident = 3,
    Underconfident = 4,
    Fragmented = 5,
    SourcePoor = 6,
    HighRiskUnverified = 7,
    ProfessorReviewNeeded = 8,
    AccessLimited = 9
}

public enum CognitiveMemoryHumilityTriggerKind
{
    SourcePoorHighRisk = 0,
    ContradictionPressure = 1,
    WrongScopePattern = 2,
    RecentCorrection = 3,
    GeneratedSummaryPrimarySupport = 4,
    WeakDomain = 5,
    HighImpactUnvalidatedProcedure = 6,
    RedactionPreventsProof = 7,
    StaleVolatileSource = 8,
    CognitiveLoadSaturation = 9
}

public enum CognitiveMemoryConfidenceReinforcementKind
{
    IndependentSourcesAgree = 0,
    RegressionPassed = 1,
    HumanReviewApproved = 2,
    ProcedureValidated = 3,
    StableProjectDecision = 4,
    ProbeAnsweredCorrectly = 5
}

public enum CognitiveMemoryAnswerPostureKind
{
    Direct = 0,
    Caveated = 1,
    ClarifyFirst = 2,
    SourceAuditRequired = 3,
    ProbeRequired = 4,
    HumanReviewRequired = 5,
    ProfessorReviewRequired = 6,
    LearningRequired = 7,
    Abstain = 8
}

public enum CognitiveMemoryRequiredOperationKind
{
    None = 0,
    Clarify = 1,
    SourceAudit = 2,
    Probe = 3,
    HumanReview = 4,
    ProfessorReview = 5,
    LearningProposal = 6,
    Abstain = 7
}

public enum CognitiveMemoryProfessorReviewMode
{
    SocraticChallenge = 0,
    ContradictionHunt = 1,
    ArchitectureReview = 2,
    CalibrationReview = 3,
    SourceSufficiencyReview = 4,
    AlternativeHypothesis = 5,
    FailureModeReview = 6,
    LearningExpansion = 7
}

public enum CognitiveMemoryProfessorReviewStatus
{
    Requested = 0,
    Completed = 1,
    Routed = 2,
    RejectedByPolicy = 3
}

public enum CognitiveMemoryProfessorSuggestionKind
{
    Probe = 0,
    SourceAudit = 1,
    Regression = 2,
    ReviewItem = 3,
    LearningProposal = 4,
    MutationCandidate = 5,
    NoAction = 6
}

public enum CognitiveMemoryAnswerGateDecisionKind
{
    Answer = 0,
    Warn = 1,
    Clarify = 2,
    SourceAudit = 3,
    Probe = 4,
    Review = 5,
    ProfessorReview = 6,
    LearningRequest = 7,
    Abstain = 8
}

public enum CognitiveMemoryKnowledgeRegionKind
{
    Domain = 0,
    ProjectDirection = 1,
    ProcedureArea = 2,
    SourceCoverage = 3,
    RiskArea = 4
}

public enum CognitiveMemoryCoverageState
{
    Unknown = 0,
    Covered = 1,
    Thin = 2,
    Stale = 3,
    Contradicted = 4,
    Missing = 5
}

public enum CognitiveMemoryKnowledgeGapKind
{
    MissingSource = 0,
    StaleSource = 1,
    Contradiction = 2,
    WrongScope = 3,
    RepeatedAbstention = 4,
    PoorCalibration = 5,
    ProfessorSuggestedExpansion = 6,
    ProcedureUnvalidated = 7
}

public enum CognitiveMemoryLearningProposalStatus
{
    Draft = 0,
    PendingApproval = 1,
    Approved = 2,
    Rejected = 3,
    Snoozed = 4,
    Completed = 5
}

public enum CognitiveMemoryLearningTaskStatus
{
    Planned = 0,
    Running = 1,
    Completed = 2,
    Failed = 3,
    Cancelled = 4
}

public enum CognitiveMemoryLearningOutcomeKind
{
    DraftMemory = 0,
    DraftProcedure = 1,
    SourceAudit = 2,
    QaFinding = 3,
    NoChange = 4
}

public enum CognitiveMemoryCrossProjectPromotionStatus
{
    Candidate = 0,
    PendingReview = 1,
    Approved = 2,
    Rejected = 3,
    Demoted = 4
}

public enum CognitiveMemoryDistributedWorkerStatus
{
    Active = 0,
    Suspended = 1,
    Retired = 2
}

public enum CognitiveMemoryDistributedJobKind
{
    ProjectionRebuild = 0,
    ReplayAnalysis = 1,
    ProcedureSimulation = 2,
    EpistemicScan = 3
}

public enum CognitiveMemoryDistributedJobState
{
    Queued = 0,
    Leased = 1,
    Completed = 2,
    Rejected = 3,
    Expired = 4
}

public enum CognitiveMemoryDistributedResultStatus
{
    Submitted = 0,
    Accepted = 1,
    Rejected = 2
}

public readonly record struct CognitiveMemoryModelProfileId
{
    public CognitiveMemoryModelProfileId(string value)
    {
        Value = CognitiveMemoryGuard.EnsureText(value, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public readonly record struct CognitiveMemoryRoleKey
{
    public CognitiveMemoryRoleKey(string value)
    {
        Value = CognitiveMemoryGuard.EnsureText(value, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public readonly record struct CognitiveMemoryRiskKey
{
    public CognitiveMemoryRiskKey(string value)
    {
        Value = CognitiveMemoryGuard.EnsureText(value, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public readonly record struct CognitiveMemoryRiskNotes
{
    public CognitiveMemoryRiskNotes(string value)
    {
        Value = CognitiveMemoryGuard.EnsureText(value, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public sealed record CognitiveMemoryProbeStartRequest(
    Guid ProjectId,
    string Title,
    CognitiveMemoryPolicyContext PolicyContext,
    CognitiveMemoryRecallMode RecallMode = CognitiveMemoryRecallMode.FocusedTaskContext,
    CognitiveMemoryWorkspaceFrameId? WorkspaceFrameId = null,
    CognitiveMemoryProjectionCollectionName? ProjectionCollectionName = null,
    CognitiveMemoryProjectionProfileId? ProjectionProfileId = null,
    CognitiveMemoryEmbeddingProfileId? EmbeddingProfileId = null);

public sealed record CognitiveMemoryProbeAskRequest(
    Guid SessionId,
    string Question,
    CognitiveMemoryRecallIntentKind Intent,
    CognitiveMemoryRecallBudget Budget,
    IReadOnlyDictionary<string, string>? Metadata = null,
    CognitiveMemoryProjectionCollectionName? ProjectionCollectionName = null,
    CognitiveMemoryProjectionProfileId? ProjectionProfileId = null,
    CognitiveMemoryEmbeddingProfileId? EmbeddingProfileId = null);

public sealed record CognitiveMemoryProbeFeedbackRequest(
    Guid TurnId,
    CognitiveMemoryProbeFeedbackAction Action,
    string Notes,
    string CorrectionText,
    CognitiveMemoryRiskLevel RiskLevel,
    bool CreateRegressionTest,
    bool RequestHumanReview,
    CognitiveMemoryCalibrationOutcomeKind CalibrationOutcome);

public sealed record CognitiveMemoryProbeReplayRequest(
    Guid RegressionTestCaseId,
    CognitiveMemoryPolicyContext PolicyContext,
    CognitiveMemoryRecallBudget Budget);

public sealed record CognitiveMemoryProbeAskResult(
    CognitiveMemoryProbeSessionRecord Session,
    CognitiveMemoryProbeTurnRecord Turn,
    CognitiveMemoryRecallResult RecallResult);

public sealed record CognitiveMemoryCuratorSessionStartRequest(
    Guid ProjectId,
    string Title,
    CognitiveMemoryPolicyContext PolicyContext,
    CognitiveMemoryCuratorRuntimeMode RuntimeMode,
    CognitiveMemoryCuratorConversationDepth ConversationDepth = CognitiveMemoryCuratorConversationDepth.Medium,
    Guid? AgentId = null,
    Guid? ProviderProfileId = null,
    CognitiveMemoryExecutionModelId? ModelId = null);

public sealed record CognitiveMemoryCuratorSendRequest(
    Guid SessionId,
    string Message,
    CognitiveMemoryRecallIntentKind Intent = CognitiveMemoryRecallIntentKind.Implementation,
    CognitiveMemoryRecallBudget? Budget = null,
    CognitiveMemoryCuratorCaptureKind? ExplicitCaptureKind = null,
    CognitiveMemoryCuratorConversationDepth? ConversationDepth = null,
    IReadOnlyList<CognitiveMemoryRecordId>? ExplicitTargetMemoryRecordIds = null,
    IReadOnlyList<CognitiveMemoryClaimId>? ExplicitTargetClaimIds = null,
    double? TargetConfidenceScore = null,
    string? CaptureScope = null);

public sealed record CognitiveMemoryCuratorTurnCaptureRequest(
    Guid SessionId,
    string UserMessage,
    string CuratorResponse,
    CognitiveMemoryCuratorRuntimeMode RuntimeMode,
    CognitiveMemoryCuratorConversationDepth? ConversationDepth = null,
    Guid? RecallTraceId = null,
    Guid? ContextPackId = null,
    IReadOnlyList<CognitiveMemoryRecordId>? AffectedMemoryRecordIds = null,
    CognitiveMemoryCuratorCaptureKind? ExplicitCaptureKind = null,
    Guid? AgentId = null,
    Guid? ProviderProfileId = null,
    CognitiveMemoryExecutionModelId? ModelId = null,
    IReadOnlyList<CognitiveMemoryRecordId>? ExplicitTargetMemoryRecordIds = null,
    IReadOnlyList<CognitiveMemoryClaimId>? ExplicitTargetClaimIds = null,
    double? TargetConfidenceScore = null,
    string? CaptureScope = null);

public sealed record CognitiveMemoryCuratorTurnCaptureResult(
    CognitiveMemoryCuratorSessionRecord Session,
    CognitiveMemoryCuratorTurnRecord Turn,
    IReadOnlyList<CognitiveMemoryCuratorCapturedImprovementRecord> CapturedImprovements);

public sealed record CognitiveMemoryCuratorSendResult(
    CognitiveMemoryCuratorSessionRecord Session,
    CognitiveMemoryCuratorTurnRecord Turn,
    CognitiveMemoryCuratorRuntimeMode RuntimeMode,
    string ResponseText,
    Guid? AgentId,
    Guid? ProviderProfileId,
    CognitiveMemoryExecutionModelId? ModelId,
    Guid RecallTraceId,
    Guid ContextPackId,
    IReadOnlyList<CognitiveMemoryRecordId> IncludedMemoryRecordIds,
    IReadOnlyList<CognitiveMemoryCuratorCapturedImprovementRecord> CapturedImprovements,
    IReadOnlyList<string> Warnings);

public sealed record CognitiveMemoryProfessorAnchorAssimilationRequest(
    Guid CaptureId,
    CognitiveMemoryRecordId DerivedMemoryRecordId,
    bool FadeAnchor = false);

public sealed record CognitiveMemoryProfessorAnchorAssimilationEvaluationRequest(
    Guid CaptureId,
    CognitiveMemoryRecordId DerivedMemoryRecordId,
    bool RequireUsageAndIntegration = false);

public sealed record CognitiveMemoryProfessorAnchorAssimilationEvaluationResult(
    bool CanAssimilate,
    string Reason,
    int IndependentSupportCount,
    int RepeatedUseCount,
    bool HasIntegrationEvidence);

public sealed record CognitiveMemoryProfessorAnchorAssimilationScanRequest(
    Guid ProjectId,
    bool FadeAnchor = true,
    int MaxAnchors = 50);

public sealed record CognitiveMemoryProfessorAnchorResult(
    Guid CaptureId,
    CognitiveMemoryProfessorAnchorState AnchorState,
    CognitiveMemoryRecordId? DerivedMemoryRecordId);

public interface ICognitiveMemoryProfessorAssimilationEvaluator
{
    ValueTask<CognitiveMemoryProfessorAnchorAssimilationEvaluationResult> EvaluateAsync(
        CognitiveMemoryProfessorAnchorAssimilationEvaluationRequest request,
        CancellationToken cancellationToken = default);
}

public interface ICognitiveMemoryProbeService
{
    ValueTask<CognitiveMemoryProbeSessionRecord> StartAsync(
        CognitiveMemoryProbeStartRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<CognitiveMemoryProbeAskResult> AskAsync(
        CognitiveMemoryProbeAskRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<CognitiveMemoryProbeFeedbackRecord> RecordFeedbackAsync(
        CognitiveMemoryProbeFeedbackRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<CognitiveMemoryProbeRegressionRunRecord> ReplayRegressionAsync(
        CognitiveMemoryProbeReplayRequest request,
        CancellationToken cancellationToken = default);
}

public interface ICognitiveMemoryCuratorConversationService
{
    ValueTask<CognitiveMemoryCuratorSessionRecord> StartAsync(
        CognitiveMemoryCuratorSessionStartRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<CognitiveMemoryCuratorSendResult> SendAsync(
        CognitiveMemoryCuratorSendRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<CognitiveMemoryCuratorTurnCaptureResult> RecordTurnAsync(
        CognitiveMemoryCuratorTurnCaptureRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyList<CognitiveMemoryCuratorTurnRecord>> GetRecentTurnsAsync(
        Guid sessionId,
        int take = 50,
        CancellationToken cancellationToken = default);
}

public interface ICognitiveMemoryProfessorAnchorService
{
    ValueTask<CognitiveMemoryProfessorAnchorResult> MarkAssimilatedAsync(
        CognitiveMemoryProfessorAnchorAssimilationRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<CognitiveMemoryProfessorAnchorResult> FadeAsync(
        Guid captureId,
        CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyList<CognitiveMemoryProfessorAnchorResult>> ScanAssimilationAsync(
        CognitiveMemoryProfessorAnchorAssimilationScanRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record CognitiveMemorySelfModelQuery(
    Guid? ProjectId,
    CognitiveMemoryModelProfileId ModelProfileId,
    CognitiveMemoryRoleKey RoleKey,
    string DomainKey,
    string TaskTypeKey);

public sealed record CognitiveMemorySelfModelSnapshot(
    CognitiveMemorySelfModelProfileRecord SelfModel,
    CognitiveMemoryDomainCompetenceProfileRecord? Competence,
    IReadOnlyList<CognitiveMemoryKnownFailurePatternRecord> FailurePatterns,
    CognitiveMemorySelfRegulationPolicyProfileRecord? PolicyProfile);

public sealed record CognitiveMemorySelfModelUpdateProposalRequest(
    Guid? ProjectId,
    CognitiveMemoryModelProfileId ModelProfileId,
    string DomainKey,
    string ProposedChange,
    IReadOnlyList<CognitiveMemoryScoreEvidenceRef> EvidenceRefs,
    string RequestedByActorId);

public interface ICognitiveMemorySelfModelStore
{
    ValueTask<CognitiveMemorySelfModelProfileRecord> EnsureSeedProfileAsync(
        CognitiveMemorySelfModelQuery query,
        CancellationToken cancellationToken = default);

    ValueTask<CognitiveMemorySelfModelSnapshot> LoadAsync(
        CognitiveMemorySelfModelQuery query,
        CancellationToken cancellationToken = default);

    ValueTask<CognitiveMemorySelfModelUpdateProposalRecord> ProposeUpdateAsync(
        CognitiveMemorySelfModelUpdateProposalRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record CognitiveMemoryCalibrationOutcomeRequest(
    Guid? ProjectId,
    string DomainKey,
    string TaskTypeKey,
    CognitiveMemoryModelProfileId ModelProfileId,
    CognitiveMemoryRiskKey RiskKey,
    string FeaturePatternKey,
    string ProfileVersion,
    double PredictedConfidence,
    bool ActualCorrect,
    CognitiveMemoryCalibrationOutcomeKind OutcomeKind,
    Guid? ProbeTurnId = null,
    Guid? RecallTraceId = null,
    Guid? ReviewItemId = null,
    Guid? ProfessorReviewId = null);

public sealed record CognitiveMemoryCalibrationHealthSnapshot(
    CognitiveMemoryCalibrationAggregateRecord Aggregate,
    IReadOnlyList<CognitiveMemoryCalibrationBinRecord> Bins);

public interface ICognitiveMemoryCalibrationHealthService
{
    ValueTask<CognitiveMemoryCalibrationEventRecord> RecordOutcomeAsync(
        CognitiveMemoryCalibrationOutcomeRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<CognitiveMemoryCalibrationHealthSnapshot?> GetAggregateAsync(
        Guid? projectId,
        string domainKey,
        string taskTypeKey,
        string modelProfileId,
        string riskKey,
        string featurePatternKey,
        string profileVersion,
        CancellationToken cancellationToken = default);
}

public sealed record CognitiveMemorySelfRegulationAssessmentRequest(
    Guid? ProjectId,
    string ActorId,
    CognitiveMemoryModelProfileId ModelProfileId,
    CognitiveMemoryRoleKey RoleKey,
    string DomainKey,
    string TaskTypeKey,
    CognitiveMemoryRiskLevel RiskLevel,
    CognitiveMemoryPolicyContext PolicyContext,
    double SourceSufficiency,
    double EvidenceCoverage,
    double ContextFit,
    double ContradictionPressure,
    double RedactionPressure,
    double CognitiveLoad,
    bool HighImpact,
    bool RecentCorrection,
    Guid? RecallTraceId = null,
    Guid? WorkspaceFrameId = null,
    Guid? AttentionDecisionId = null);

public sealed record CognitiveMemorySelfRegulationAssessmentResult(
    CognitiveMemorySelfRegulationAssessmentRecord Assessment,
    CognitiveMemoryAnswerPostureDecisionRecord Posture,
    IReadOnlyList<CognitiveMemoryHumilityTriggerRecord> HumilityTriggers,
    IReadOnlyList<CognitiveMemoryConfidenceReinforcementRecord> ConfidenceReinforcements);

public interface ICognitiveMemorySelfRegulationOrchestrator
{
    ValueTask<CognitiveMemorySelfRegulationAssessmentResult> AssessAsync(
        CognitiveMemorySelfRegulationAssessmentRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record CognitiveMemoryProfessorReviewRequest(
    Guid? ProjectId,
    CognitiveMemoryProfessorReviewMode ReviewMode,
    string RequestedByActorId,
    CognitiveMemoryModelProfileId ModelProfileId,
    string PromptProfileVersion,
    CognitiveMemoryPolicyContext PolicyContext,
    Guid? SelfRegulationAssessmentId,
    Guid? AnswerPostureDecisionId,
    string InputSummary,
    string ContextSummary,
    IReadOnlyList<CognitiveMemoryProfessorSuggestionKind> RequestedSuggestionKinds);

public interface ICognitiveMemoryProfessorReviewService
{
    ValueTask<CognitiveMemoryProfessorReviewRecord> RequestReviewAsync(
        CognitiveMemoryProfessorReviewRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<CognitiveMemoryProfessorReviewRecord> CompleteReviewAsync(
        Guid reviewId,
        string critique,
        string missingEvidence,
        CognitiveMemoryAnswerPostureKind recommendedPosture,
        IReadOnlyList<CognitiveMemoryProfessorSuggestionKind> suggestionKinds,
        CancellationToken cancellationToken = default);
}

public sealed record CognitiveMemoryAnswerGateRequest(
    Guid ProjectId,
    string ActorId,
    CognitiveMemoryPolicyContext PolicyContext,
    Guid? RecallTraceId,
    Guid? SelfRegulationAssessmentId,
    Guid? AnswerPostureDecisionId,
    Guid? ProfessorReviewId,
    double SourceSufficiency,
    double ContextFit,
    double EvidenceSupport,
    double ContradictionPressure,
    double StalenessPressure,
    double RedactionPressure,
    double CalibrationRisk,
    CognitiveMemoryRiskLevel RiskLevel,
    bool ProcedureUnvalidated,
    bool ProfessorReviewRequired,
    string DraftAnswerSummary);

public interface ICognitiveMemoryAnswerGateService
{
    ValueTask<CognitiveMemoryAnswerGateDecisionRecord> DecideAsync(
        CognitiveMemoryAnswerGateRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record CognitiveMemoryEpistemicScanRequest(
    Guid ProjectId,
    CognitiveMemoryPolicyContext PolicyContext,
    string RequestedByActorId,
    IReadOnlyList<Guid>? EvidenceTraceIds = null);

public interface ICognitiveMemoryEpistemicDriveService
{
    ValueTask<IReadOnlyList<CognitiveMemoryLearningProposalRecord>> ScanAsync(
        CognitiveMemoryEpistemicScanRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<CognitiveMemoryLearningProposalRecord> DecideProposalAsync(
        Guid proposalId,
        CognitiveMemoryLearningProposalStatus decision,
        string actorId,
        string notes,
        CancellationToken cancellationToken = default);
}

public sealed record CognitiveMemoryCrossProjectPromotionRequest(
    Guid SourceMemoryRecordId,
    Guid SourceProjectId,
    string RequestedByActorId,
    CognitiveMemoryPolicyContext PolicyContext,
    double SemanticSimilarity,
    double EntityEquivalence,
    double ContextSeparation,
    double SourceReusePermission,
    double PolicyCompatibility,
    string Reason);

public interface ICognitiveMemoryCrossProjectMemoryService
{
    ValueTask<CognitiveMemoryCrossProjectPromotionCandidateRecord> CreateCandidateAsync(
        CognitiveMemoryCrossProjectPromotionRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<CognitiveMemoryCrossProjectPromotionCandidateRecord> DecideAsync(
        Guid candidateId,
        CognitiveMemoryCrossProjectPromotionStatus decision,
        string actorId,
        string notes,
        CancellationToken cancellationToken = default);
}

public sealed record CognitiveMemoryDistributedJobEnqueueRequest(
    Guid ProjectId,
    CognitiveMemoryDistributedJobKind JobKind,
    string SourceScopeKey,
    string InputPayloadJson,
    string ExpectedOutputSchema,
    string AlgorithmVersion,
    string PolicyProfileId);

public sealed record CognitiveMemoryDistributedLeaseClaim(
    Guid JobId,
    string LeaseToken,
    DateTimeOffset LeaseExpiresAtUtc,
    string InputPayloadJson,
    string InputHash);

public interface ICognitiveMemoryDistributedComputeCoordinator
{
    ValueTask<CognitiveMemoryDistributedWorkerRecord> RegisterWorkerAsync(
        string workerId,
        string machineName,
        IReadOnlyList<CognitiveMemoryDistributedJobKind> capabilities,
        CancellationToken cancellationToken = default);

    ValueTask<CognitiveMemoryDistributedJobRecord> EnqueueAsync(
        CognitiveMemoryDistributedJobEnqueueRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<CognitiveMemoryDistributedLeaseClaim?> ClaimAsync(
        string workerId,
        IReadOnlyList<CognitiveMemoryDistributedJobKind> capabilities,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default);

    ValueTask<CognitiveMemoryDistributedWorkerResultRecord> SubmitResultAsync(
        Guid jobId,
        string workerId,
        string leaseToken,
        string inputHash,
        string outputPayloadJson,
        string algorithmVersion,
        string outputSchema,
        CancellationToken cancellationToken = default);
}

public static class CognitiveMemoryWorkflowExecutorIds
{
    public static WorkflowExecutorId Recall { get; } = new("cognitive-memory.recall");

    public static WorkflowExecutorId Probe { get; } = new("cognitive-memory.probe");

    public static WorkflowExecutorId LearningProposal { get; } = new("cognitive-memory.learning-proposal");

    public static WorkflowExecutorId ReviewItem { get; } = new("cognitive-memory.review-item");
}

public sealed record CognitiveMemoryRecallWorkflowExecutorSettings
{
    public Guid ProjectId { get; init; }

    public string Query { get; init; } = string.Empty;

    public CognitiveMemoryRecallIntentKind Intent { get; init; } = CognitiveMemoryRecallIntentKind.Implementation;

    public CognitiveMemoryRecallMode Mode { get; init; } = CognitiveMemoryRecallMode.FocusedTaskContext;

    public int ContextCharacterBudget { get; init; } = 4000;
}

public sealed record CognitiveMemoryProbeWorkflowExecutorSettings
{
    public Guid ProjectId { get; init; }

    public string Question { get; init; } = string.Empty;

    public string SessionTitle { get; init; } = "Workflow probe";
}

public sealed record CognitiveMemoryLearningWorkflowExecutorSettings
{
    public Guid ProjectId { get; init; }
}
