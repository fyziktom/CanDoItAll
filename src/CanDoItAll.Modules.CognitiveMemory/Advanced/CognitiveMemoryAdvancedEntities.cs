using CanDoItAll.Infrastructure.Persistence;

namespace CanDoItAll.Modules.CognitiveMemory;

public sealed class CognitiveMemoryProbeSessionRecord : IHasConcurrencyToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProjectId { get; set; }

    public CognitiveMemoryProbeSessionStatus Status { get; set; } = CognitiveMemoryProbeSessionStatus.Active;

    public CognitiveMemoryRecallMode RecallMode { get; set; } = CognitiveMemoryRecallMode.FocusedTaskContext;

    public Guid? WorkspaceFrameId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string ActorId { get; set; } = string.Empty;

    public string PolicyProfileId { get; set; } = string.Empty;

    public CognitiveMemoryAccessLevel AccessLevel { get; set; } = CognitiveMemoryAccessLevel.Project;

    public CognitiveMemoryRiskLevel RiskLevel { get; set; } = CognitiveMemoryRiskLevel.Low;

    public bool AllowRestrictedContent { get; set; }

    public string ProjectionCollectionName { get; set; } = string.Empty;

    public string ProjectionProfileId { get; set; } = string.Empty;

    public string EmbeddingProfileId { get; set; } = string.Empty;

    public string AlgorithmVersion { get; set; } = string.Empty;

    public int TurnCount { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public DateTimeOffset? ClosedAtUtc { get; set; }

    public Guid ConcurrencyToken { get; set; }
}

public sealed class CognitiveMemoryProbeTurnRecord : IHasConcurrencyToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProbeSessionId { get; set; }

    public Guid ProjectId { get; set; }

    public int Sequence { get; set; }

    public CognitiveMemoryProbeTurnStatus Status { get; set; } = CognitiveMemoryProbeTurnStatus.Asked;

    public CognitiveMemoryRecallIntentKind Intent { get; set; } = CognitiveMemoryRecallIntentKind.Implementation;

    public string Question { get; set; } = string.Empty;

    public string AnswerSummary { get; set; } = string.Empty;

    public Guid RecallTraceId { get; set; }

    public Guid? ContextPackId { get; set; }

    public Guid? SelfRegulationAssessmentId { get; set; }

    public Guid? AnswerPostureDecisionId { get; set; }

    public Guid? AnswerGateDecisionId { get; set; }

    public Guid ProbeScoreEvaluationTraceId { get; set; }

    public CognitiveMemoryScoreProjectionBucket ProbeScoreBucket { get; set; } = CognitiveMemoryScoreProjectionBucket.Unknown;

    public double? DisplayProbeScore { get; set; }

    public int WarningCount { get; set; }

    public string WarningsJson { get; set; } = "[]";

    public string MetadataJson { get; set; } = "{}";

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public Guid ConcurrencyToken { get; set; }
}

public sealed class CognitiveMemoryProbeFeedbackRecord : IHasConcurrencyToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProbeTurnId { get; set; }

    public Guid ProbeSessionId { get; set; }

    public Guid ProjectId { get; set; }

    public CognitiveMemoryProbeFeedbackAction Action { get; set; } = CognitiveMemoryProbeFeedbackAction.MarkCorrect;

    public CognitiveMemoryCalibrationOutcomeKind CalibrationOutcome { get; set; } = CognitiveMemoryCalibrationOutcomeKind.Unknown;

    public CognitiveMemoryRiskLevel RiskLevel { get; set; } = CognitiveMemoryRiskLevel.Low;

    public string Notes { get; set; } = string.Empty;

    public string CorrectionText { get; set; } = string.Empty;

    public Guid? ReviewItemId { get; set; }

    public Guid? RegressionTestCaseId { get; set; }

    public Guid? CalibrationEventId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public Guid ConcurrencyToken { get; set; }
}

public sealed class CognitiveMemoryCuratorSessionRecord : IHasConcurrencyToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProjectId { get; set; }

    public CognitiveMemoryCuratorSessionStatus Status { get; set; } = CognitiveMemoryCuratorSessionStatus.Active;

    public CognitiveMemoryCuratorRuntimeMode RuntimeMode { get; set; } = CognitiveMemoryCuratorRuntimeMode.DirectLlm;

    public CognitiveMemoryCuratorConversationDepth ConversationDepth { get; set; } = CognitiveMemoryCuratorConversationDepth.Medium;

    public string Title { get; set; } = string.Empty;

    public string ActorId { get; set; } = string.Empty;

    public string PolicyProfileId { get; set; } = string.Empty;

    public CognitiveMemoryAccessLevel AccessLevel { get; set; } = CognitiveMemoryAccessLevel.Project;

    public CognitiveMemoryRiskLevel RiskLevel { get; set; } = CognitiveMemoryRiskLevel.Low;

    public bool AllowRestrictedContent { get; set; }

    public Guid? AgentId { get; set; }

    public Guid? ProviderProfileId { get; set; }

    public CognitiveMemoryExecutionModelId? ModelId { get; set; }

    public Guid? AgentChatSessionId { get; set; }

    public string AlgorithmVersion { get; set; } = string.Empty;

    public int TurnCount { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public DateTimeOffset? ClosedAtUtc { get; set; }

    public Guid ConcurrencyToken { get; set; }
}

public sealed class CognitiveMemoryCuratorTurnRecord : IHasConcurrencyToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CuratorSessionId { get; set; }

    public Guid ProjectId { get; set; }

    public int Sequence { get; set; }

    public CognitiveMemoryCuratorRuntimeMode RuntimeMode { get; set; } = CognitiveMemoryCuratorRuntimeMode.DirectLlm;

    public CognitiveMemoryCuratorConversationDepth ConversationDepth { get; set; } = CognitiveMemoryCuratorConversationDepth.Medium;

    public string UserMessage { get; set; } = string.Empty;

    public string CuratorResponse { get; set; } = string.Empty;

    public Guid? RecallTraceId { get; set; }

    public Guid? ContextPackId { get; set; }

    public string IncludedMemoryRecordIdsJson { get; set; } = "[]";

    public Guid? AgentId { get; set; }

    public Guid? ProviderProfileId { get; set; }

    public CognitiveMemoryExecutionModelId? ModelId { get; set; }

    public int CaptureCount { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public Guid ConcurrencyToken { get; set; }
}

public sealed class CognitiveMemoryCuratorCapturedImprovementRecord : IHasConcurrencyToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CuratorSessionId { get; set; }

    public Guid CuratorTurnId { get; set; }

    public Guid ProjectId { get; set; }

    public CognitiveMemoryCuratorCaptureKind CaptureKind { get; set; } = CognitiveMemoryCuratorCaptureKind.NewKnowledge;

    public CognitiveMemoryCuratorConversationDepth ConversationDepth { get; set; } = CognitiveMemoryCuratorConversationDepth.Medium;

    public CognitiveMemoryCuratorCaptureStatus Status { get; set; } = CognitiveMemoryCuratorCaptureStatus.Captured;

    public Guid? RecallTraceId { get; set; }

    public Guid? ContextPackId { get; set; }

    public string AffectedMemoryRecordIdsJson { get; set; } = "[]";

    public string TargetClaimIdsJson { get; set; } = "[]";

    public CognitiveMemoryCuratorTargetingStatus TargetingStatus { get; set; } = CognitiveMemoryCuratorTargetingStatus.Untargeted;

    public CognitiveMemoryProfessorAnchorState AnchorState { get; set; } = CognitiveMemoryProfessorAnchorState.Active;

    public Guid? SourceItemId { get; set; }

    public Guid? EvidenceAnchorId { get; set; }

    public Guid? MutationCommandId { get; set; }

    public Guid? ConsolidationCandidateId { get; set; }

    public Guid? AppliedMemoryRecordId { get; set; }

    public Guid? AssimilatedMemoryRecordId { get; set; }

    public Guid? ReviewItemId { get; set; }

    public string ActorId { get; set; } = string.Empty;

    public double ConfidenceScore { get; set; }

    public double PriorityScore { get; set; }

    public double TargetConfidenceScore { get; set; }

    public string CaptureLanguage { get; set; } = string.Empty;

    public string CaptureScope { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string CorrectionText { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? AnchorRetiredAtUtc { get; set; }

    public Guid ConcurrencyToken { get; set; }
}

public sealed class CognitiveMemoryProbeFindingRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProbeTurnId { get; set; }

    public Guid ProjectId { get; set; }

    public CognitiveMemoryProbeFindingKind FindingKind { get; set; } = CognitiveMemoryProbeFindingKind.Unknown;

    public CognitiveMemoryRiskLevel RiskLevel { get; set; } = CognitiveMemoryRiskLevel.Low;

    public string Summary { get; set; } = string.Empty;

    public Guid? ReviewItemId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class CognitiveMemoryProbeRegressionTestCaseRecord : IHasConcurrencyToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProjectId { get; set; }

    public Guid ProbeTurnId { get; set; }

    public CognitiveMemoryProbeRegressionStatus Status { get; set; } = CognitiveMemoryProbeRegressionStatus.Draft;

    public string Question { get; set; } = string.Empty;

    public string ExpectedEvidenceText { get; set; } = string.Empty;

    public string ExpectedContextKey { get; set; } = string.Empty;

    public string AccessPolicyProfileId { get; set; } = string.Empty;

    public string EvaluatorProfileVersion { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public Guid ConcurrencyToken { get; set; }
}

public sealed class CognitiveMemoryProbeRegressionRunRecord : IHasConcurrencyToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProjectId { get; set; }

    public Guid RegressionTestCaseId { get; set; }

    public CognitiveMemoryProbeRegressionRunOutcome Outcome { get; set; } = CognitiveMemoryProbeRegressionRunOutcome.Unknown;

    public Guid? RecallTraceId { get; set; }

    public string FailureReason { get; set; } = string.Empty;

    public string EvaluatorProfileVersion { get; set; } = string.Empty;

    public DateTimeOffset StartedAtUtc { get; set; }

    public DateTimeOffset? CompletedAtUtc { get; set; }

    public Guid ConcurrencyToken { get; set; }
}

public sealed class CognitiveMemorySelfModelProfileRecord : IHasConcurrencyToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? ProjectId { get; set; }

    public CognitiveMemorySelfModelStatus Status { get; set; } = CognitiveMemorySelfModelStatus.Active;

    public CognitiveMemoryModelProfileId ModelProfileId { get; set; }

    public CognitiveMemoryRoleKey RoleKey { get; set; }

    public string ProfileVersion { get; set; } = string.Empty;

    public string OperatingPrinciples { get; set; } = string.Empty;

    public string AllowedTaskCategoriesJson { get; set; } = "[]";

    public string RestrictedTaskCategoriesJson { get; set; } = "[]";

    public string AlgorithmVersion { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public Guid ConcurrencyToken { get; set; }
}

public sealed class CognitiveMemoryDomainCompetenceProfileRecord : IHasConcurrencyToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? ProjectId { get; set; }

    public Guid SelfModelProfileId { get; set; }

    public string DomainKey { get; set; } = string.Empty;

    public string TaskTypeKey { get; set; } = string.Empty;

    public CognitiveMemoryModelProfileId ModelProfileId { get; set; }

    public string ProfileVersion { get; set; } = string.Empty;

    public CognitiveMemoryCompetenceLevel CompetenceLevel { get; set; } = CognitiveMemoryCompetenceLevel.Unknown;

    public Guid? CompetenceScoreEvaluationTraceId { get; set; }

    public int EvidenceCount { get; set; }

    public string EvidenceRefsJson { get; set; } = "[]";

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public Guid ConcurrencyToken { get; set; }
}

public sealed class CognitiveMemoryKnownFailurePatternRecord : IHasConcurrencyToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? ProjectId { get; set; }

    public Guid SelfModelProfileId { get; set; }

    public CognitiveMemoryKnownFailurePatternKind PatternKind { get; set; } = CognitiveMemoryKnownFailurePatternKind.Unknown;

    public string DomainKey { get; set; } = string.Empty;

    public string TaskTypeKey { get; set; } = string.Empty;

    public string TriggerSummary { get; set; } = string.Empty;

    public string Mitigation { get; set; } = string.Empty;

    public bool RequiresReview { get; set; }

    public Guid? PatternScoreEvaluationTraceId { get; set; }

    public string EvidenceRefsJson { get; set; } = "[]";

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public Guid ConcurrencyToken { get; set; }
}

public sealed class CognitiveMemorySelfRegulationPolicyProfileRecord : IHasConcurrencyToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? ProjectId { get; set; }

    public Guid SelfModelProfileId { get; set; }

    public string PolicyKey { get; set; } = string.Empty;

    public string ProfileVersion { get; set; } = string.Empty;

    public string AllowedPosturesJson { get; set; } = "[]";

    public string RequiredOperationsJson { get; set; } = "[]";

    public double ReviewThreshold { get; set; }

    public double AbstentionThreshold { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public Guid ConcurrencyToken { get; set; }
}

public sealed class CognitiveMemorySelfModelUpdateProposalRecord : IHasConcurrencyToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? ProjectId { get; set; }

    public CognitiveMemorySelfModelUpdateProposalStatus Status { get; set; } = CognitiveMemorySelfModelUpdateProposalStatus.PendingReview;

    public CognitiveMemoryModelProfileId ModelProfileId { get; set; }

    public string DomainKey { get; set; } = string.Empty;

    public string ProposedChange { get; set; } = string.Empty;

    public string EvidenceRefsJson { get; set; } = "[]";

    public string RequestedByActorId { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public Guid ConcurrencyToken { get; set; }
}

public sealed class CognitiveMemoryCalibrationEventRecord : IHasConcurrencyToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? ProjectId { get; set; }

    public string DomainKey { get; set; } = string.Empty;

    public string TaskTypeKey { get; set; } = string.Empty;

    public CognitiveMemoryModelProfileId ModelProfileId { get; set; }

    public CognitiveMemoryRiskKey RiskKey { get; set; }

    public string FeaturePatternKey { get; set; } = string.Empty;

    public string ProfileVersion { get; set; } = string.Empty;

    public double PredictedConfidence { get; set; }

    public bool ActualCorrect { get; set; }

    public CognitiveMemoryCalibrationOutcomeKind OutcomeKind { get; set; } = CognitiveMemoryCalibrationOutcomeKind.Unknown;

    public Guid? ProbeTurnId { get; set; }

    public Guid? RecallTraceId { get; set; }

    public Guid? ReviewItemId { get; set; }

    public Guid? ProfessorReviewId { get; set; }

    public DateTimeOffset ObservedAtUtc { get; set; }

    public Guid ConcurrencyToken { get; set; }
}

public sealed class CognitiveMemoryCalibrationAggregateRecord : IHasConcurrencyToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? ProjectId { get; set; }

    public string DomainKey { get; set; } = string.Empty;

    public string TaskTypeKey { get; set; } = string.Empty;

    public CognitiveMemoryModelProfileId ModelProfileId { get; set; }

    public CognitiveMemoryRiskKey RiskKey { get; set; }

    public string FeaturePatternKey { get; set; } = string.Empty;

    public string ProfileVersion { get; set; } = string.Empty;

    public int ObservationCount { get; set; }

    public double ExpectedCalibrationError { get; set; }

    public double BrierScore { get; set; }

    public double SignedBias { get; set; }

    public double OverconfidenceRate { get; set; }

    public double UnderconfidenceRate { get; set; }

    public double AbstentionQualityRate { get; set; }

    public double WrongScopeRate { get; set; }

    public double SourceInsufficientRate { get; set; }

    public Guid? CalibrationScoreEvaluationTraceId { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public Guid ConcurrencyToken { get; set; }
}

public sealed class CognitiveMemoryCalibrationBinRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CalibrationAggregateId { get; set; }

    public Guid? ProjectId { get; set; }

    public int BinIndex { get; set; }

    public double LowerBound { get; set; }

    public double UpperBound { get; set; }

    public int ObservationCount { get; set; }

    public double AveragePredictedConfidence { get; set; }

    public double ActualAccuracy { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed class CognitiveMemorySelfRegulationAssessmentRecord : IHasConcurrencyToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? ProjectId { get; set; }

    public Guid? SelfModelProfileId { get; set; }

    public Guid? DomainCompetenceProfileId { get; set; }

    public Guid? CalibrationAggregateId { get; set; }

    public Guid? RecallTraceId { get; set; }

    public Guid? WorkspaceFrameId { get; set; }

    public Guid? AttentionDecisionId { get; set; }

    public string ActorId { get; set; } = string.Empty;

    public CognitiveMemoryModelProfileId ModelProfileId { get; set; }

    public string DomainKey { get; set; } = string.Empty;

    public string TaskTypeKey { get; set; } = string.Empty;

    public CognitiveMemorySelfRegulationStateKind State { get; set; } = CognitiveMemorySelfRegulationStateKind.Unknown;

    public Guid AssessmentScoreEvaluationTraceId { get; set; }

    public CognitiveMemoryScoreProjectionBucket AssessmentBucket { get; set; } = CognitiveMemoryScoreProjectionBucket.Unknown;

    public double? DisplayAssessmentScore { get; set; }

    public string WarningsJson { get; set; } = "[]";

    public string RequiredOperationsJson { get; set; } = "[]";

    public string AlgorithmVersion { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public Guid ConcurrencyToken { get; set; }
}

public sealed class CognitiveMemoryHumilityTriggerRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid SelfRegulationAssessmentId { get; set; }

    public Guid? ProjectId { get; set; }

    public CognitiveMemoryHumilityTriggerKind TriggerKind { get; set; } = CognitiveMemoryHumilityTriggerKind.SourcePoorHighRisk;

    public string Reason { get; set; } = string.Empty;

    public Guid? ScoreEvaluationTraceId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class CognitiveMemoryConfidenceReinforcementRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid SelfRegulationAssessmentId { get; set; }

    public Guid? ProjectId { get; set; }

    public CognitiveMemoryConfidenceReinforcementKind ReinforcementKind { get; set; } = CognitiveMemoryConfidenceReinforcementKind.IndependentSourcesAgree;

    public string Reason { get; set; } = string.Empty;

    public Guid? EvidenceId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class CognitiveMemoryAnswerPostureDecisionRecord : IHasConcurrencyToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? ProjectId { get; set; }

    public Guid SelfRegulationAssessmentId { get; set; }

    public CognitiveMemoryAnswerPostureKind Posture { get; set; } = CognitiveMemoryAnswerPostureKind.Caveated;

    public Guid PostureScoreEvaluationTraceId { get; set; }

    public CognitiveMemoryScoreProjectionBucket PostureBucket { get; set; } = CognitiveMemoryScoreProjectionBucket.Unknown;

    public string RequiredOperationsJson { get; set; } = "[]";

    public string WarningsJson { get; set; } = "[]";

    public string Reason { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public Guid ConcurrencyToken { get; set; }
}

public sealed class CognitiveMemoryProfessorReviewRecord : IHasConcurrencyToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? ProjectId { get; set; }

    public CognitiveMemoryProfessorReviewMode ReviewMode { get; set; } = CognitiveMemoryProfessorReviewMode.SocraticChallenge;

    public CognitiveMemoryProfessorReviewStatus Status { get; set; } = CognitiveMemoryProfessorReviewStatus.Requested;

    public string RequestedByActorId { get; set; } = string.Empty;

    public CognitiveMemoryModelProfileId ModelProfileId { get; set; }

    public string PromptProfileVersion { get; set; } = string.Empty;

    public string PolicyProfileId { get; set; } = string.Empty;

    public Guid? SelfRegulationAssessmentId { get; set; }

    public Guid? AnswerPostureDecisionId { get; set; }

    public Guid RoutingScoreEvaluationTraceId { get; set; }

    public string InputSummary { get; set; } = string.Empty;

    public string ContextSummary { get; set; } = string.Empty;

    public string Critique { get; set; } = string.Empty;

    public string MissingEvidence { get; set; } = string.Empty;

    public CognitiveMemoryAnswerPostureKind RecommendedPosture { get; set; } = CognitiveMemoryAnswerPostureKind.Caveated;

    public CognitiveMemoryHashAlgorithm OutputHashAlgorithm { get; set; } = CognitiveMemoryHashAlgorithm.Sha256;

    public string OutputHash { get; set; } = string.Empty;

    public bool RequiresHumanReview { get; set; } = true;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? CompletedAtUtc { get; set; }

    public Guid ConcurrencyToken { get; set; }
}

public sealed class CognitiveMemoryProfessorReviewActionRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProfessorReviewId { get; set; }

    public Guid? ProjectId { get; set; }

    public CognitiveMemoryProfessorSuggestionKind SuggestionKind { get; set; } = CognitiveMemoryProfessorSuggestionKind.NoAction;

    public Guid? CreatedReviewItemId { get; set; }

    public Guid? CreatedLearningProposalId { get; set; }

    public Guid? CreatedRegressionTestCaseId { get; set; }

    public string Summary { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class CognitiveMemoryAnswerGateDecisionRecord : IHasConcurrencyToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProjectId { get; set; }

    public Guid? RecallTraceId { get; set; }

    public Guid? SelfRegulationAssessmentId { get; set; }

    public Guid? AnswerPostureDecisionId { get; set; }

    public Guid? ProfessorReviewId { get; set; }

    public CognitiveMemoryAnswerGateDecisionKind DecisionKind { get; set; } = CognitiveMemoryAnswerGateDecisionKind.Warn;

    public Guid ScoreEvaluationTraceId { get; set; }

    public CognitiveMemoryScoreProjectionBucket DecisionBucket { get; set; } = CognitiveMemoryScoreProjectionBucket.Unknown;

    public double? DisplayConfidenceProjection { get; set; }

    public string WarningsJson { get; set; } = "[]";

    public string RequiredOperationsJson { get; set; } = "[]";

    public string Reason { get; set; } = string.Empty;

    public string DraftAnswerSummary { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public Guid ConcurrencyToken { get; set; }
}

public sealed class CognitiveMemoryKnowledgeRegionRecord : IHasConcurrencyToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProjectId { get; set; }

    public CognitiveMemoryKnowledgeRegionKind RegionKind { get; set; } = CognitiveMemoryKnowledgeRegionKind.Domain;

    public string RegionKey { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public Guid ConcurrencyToken { get; set; }
}

public sealed class CognitiveMemoryCoverageMapRecord : IHasConcurrencyToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProjectId { get; set; }

    public Guid KnowledgeRegionId { get; set; }

    public CognitiveMemoryCoverageState CoverageState { get; set; } = CognitiveMemoryCoverageState.Unknown;

    public int SourceEvidenceCount { get; set; }

    public int RecallFailureCount { get; set; }

    public int ProbeFailureCount { get; set; }

    public int AbstentionCount { get; set; }

    public DateTimeOffset RefreshedAtUtc { get; set; }

    public Guid ConcurrencyToken { get; set; }
}

public sealed class CognitiveMemoryKnowledgeGapRecord : IHasConcurrencyToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProjectId { get; set; }

    public Guid KnowledgeRegionId { get; set; }

    public CognitiveMemoryKnowledgeGapKind GapKind { get; set; } = CognitiveMemoryKnowledgeGapKind.MissingSource;

    public string Summary { get; set; } = string.Empty;

    public string EvidenceRefsJson { get; set; } = "[]";

    public DateTimeOffset CreatedAtUtc { get; set; }

    public Guid ConcurrencyToken { get; set; }
}

public sealed class CognitiveMemoryLearningProposalRecord : IHasConcurrencyToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProjectId { get; set; }

    public Guid KnowledgeGapId { get; set; }

    public CognitiveMemoryLearningProposalStatus Status { get; set; } = CognitiveMemoryLearningProposalStatus.PendingApproval;

    public string Title { get; set; } = string.Empty;

    public string Explanation { get; set; } = string.Empty;

    public string EvidenceRefsJson { get; set; } = "[]";

    public CognitiveMemoryRiskNotes Risks { get; set; }

    public string AcceptanceCriteria { get; set; } = string.Empty;

    public Guid NeedScoreEvaluationTraceId { get; set; }

    public CognitiveMemoryScoreProjectionBucket NeedBucket { get; set; } = CognitiveMemoryScoreProjectionBucket.Unknown;

    public double? DisplayPriorityProjection { get; set; }

    public string DecidedByActorId { get; set; } = string.Empty;

    public string DecisionNotes { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? DecidedAtUtc { get; set; }

    public Guid ConcurrencyToken { get; set; }
}

public sealed class CognitiveMemoryLearningTaskRecord : IHasConcurrencyToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProjectId { get; set; }

    public Guid LearningProposalId { get; set; }

    public CognitiveMemoryLearningTaskStatus Status { get; set; } = CognitiveMemoryLearningTaskStatus.Planned;

    public string WorkflowExecutorKey { get; set; } = string.Empty;

    public string ApprovalActorId { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public Guid ConcurrencyToken { get; set; }
}

public sealed class CognitiveMemoryLearningOutcomeRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProjectId { get; set; }

    public Guid LearningTaskId { get; set; }

    public CognitiveMemoryLearningOutcomeKind OutcomeKind { get; set; } = CognitiveMemoryLearningOutcomeKind.NoChange;

    public string Summary { get; set; } = string.Empty;

    public string SourceRefsJson { get; set; } = "[]";

    public Guid? ReviewItemId { get; set; }

    public Guid? MutationCommandId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class CognitiveMemoryCrossProjectPromotionCandidateRecord : IHasConcurrencyToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid SourceProjectId { get; set; }

    public Guid SourceMemoryRecordId { get; set; }

    public CognitiveMemoryCrossProjectPromotionStatus Status { get; set; } = CognitiveMemoryCrossProjectPromotionStatus.Candidate;

    public Guid PromotionScoreEvaluationTraceId { get; set; }

    public CognitiveMemoryScoreProjectionBucket PromotionBucket { get; set; } = CognitiveMemoryScoreProjectionBucket.Unknown;

    public string RequestedByActorId { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    public Guid? ReviewItemId { get; set; }

    public string DecidedByActorId { get; set; } = string.Empty;

    public string DecisionNotes { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? DecidedAtUtc { get; set; }

    public Guid ConcurrencyToken { get; set; }
}

public sealed class CognitiveMemoryDistributedWorkerRecord : IHasConcurrencyToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string WorkerId { get; set; } = string.Empty;

    public string MachineName { get; set; } = string.Empty;

    public CognitiveMemoryDistributedWorkerStatus Status { get; set; } = CognitiveMemoryDistributedWorkerStatus.Active;

    public string CapabilitiesJson { get; set; } = "[]";

    public DateTimeOffset LastSeenAtUtc { get; set; }

    public Guid ConcurrencyToken { get; set; }
}

public sealed class CognitiveMemoryDistributedJobRecord : IHasConcurrencyToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProjectId { get; set; }

    public CognitiveMemoryDistributedJobKind JobKind { get; set; } = CognitiveMemoryDistributedJobKind.ProjectionRebuild;

    public CognitiveMemoryDistributedJobState State { get; set; } = CognitiveMemoryDistributedJobState.Queued;

    public string SourceScopeKey { get; set; } = string.Empty;

    public string InputPayloadJson { get; set; } = "{}";

    public CognitiveMemoryHashAlgorithm InputHashAlgorithm { get; set; } = CognitiveMemoryHashAlgorithm.Sha256;

    public string InputHash { get; set; } = string.Empty;

    public string ExpectedOutputSchema { get; set; } = string.Empty;

    public string AlgorithmVersion { get; set; } = string.Empty;

    public string PolicyProfileId { get; set; } = string.Empty;

    public string LeaseToken { get; set; } = string.Empty;

    public string LeasedWorkerId { get; set; } = string.Empty;

    public DateTimeOffset? LeaseExpiresAtUtc { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public Guid ConcurrencyToken { get; set; }
}

public sealed class CognitiveMemoryDistributedWorkerResultRecord : IHasConcurrencyToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid DistributedJobId { get; set; }

    public Guid ProjectId { get; set; }

    public CognitiveMemoryDistributedResultStatus Status { get; set; } = CognitiveMemoryDistributedResultStatus.Submitted;

    public string WorkerId { get; set; } = string.Empty;

    public string InputHash { get; set; } = string.Empty;

    public string OutputHash { get; set; } = string.Empty;

    public string AlgorithmVersion { get; set; } = string.Empty;

    public string OutputSchema { get; set; } = string.Empty;

    public string OutputPayloadJson { get; set; } = "{}";

    public string RejectionReason { get; set; } = string.Empty;

    public DateTimeOffset SubmittedAtUtc { get; set; }

    public DateTimeOffset? AcceptedAtUtc { get; set; }

    public Guid ConcurrencyToken { get; set; }
}
