using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CanDoItAll.CognitiveMemory.Abstractions;

public enum SelfRegulationStateKind
{
    Unknown = 0,
    Calibrated = 1,
    Exploratory = 2,
    Overconfident = 3,
    Underconfident = 4,
    Defensive = 5,
    Fragmented = 6,
    SourcePoor = 7,
    HighRiskUnverified = 8,
    ProfessorReviewNeeded = 9
}

public enum AnswerPostureKind
{
    Unknown = 0,
    DirectConfident = 1,
    DirectWithCaveats = 2,
    PreliminaryReaction = 3,
    Hypothesis = 4,
    ClarifyingQuestion = 5,
    SourceAuditRequest = 6,
    ProbeQuestion = 7,
    ReviewRequired = 8,
    ProfessorReviewRequired = 9,
    Abstain = 10
}

public enum HumilityTriggerKind
{
    Unknown = 0,
    SourcePoorHighRisk = 1,
    ContradictionPressureHigh = 2,
    WrongScopePatternMatched = 3,
    SimilarRecentCorrection = 4,
    GeneratedSummaryPrimarySupport = 5,
    DomainOutsideCompetence = 6,
    HighImpactProcedureUnvalidated = 7,
    RedactionPreventsProof = 8,
    StaleVolatileSource = 9,
    ModelEvidenceDisagreement = 10,
    CognitiveLoadSaturated = 11,
    AmbiguousContextBoundary = 12
}

public enum ConfidenceReinforcementKind
{
    Unknown = 0,
    ProbeConfirmed = 1,
    RegressionPassed = 2,
    HumanReviewApproved = 3,
    WorkflowValidationSucceeded = 4,
    MultipleIndependentSources = 5,
    StableProjectDecisionRecord = 6,
    NoContradictionDuringObservationWindow = 7
}

public enum ProfessorReviewModeKind
{
    Unknown = 0,
    SocraticChallenge = 1,
    ContradictionHunt = 2,
    ArchitectureReview = 3,
    CalibrationReview = 4,
    SourceSufficiencyReview = 5,
    AlternativeHypothesisReview = 6,
    FailureModeReview = 7,
    LearningExpansion = 8
}

public enum SelfRegulationOutcomeKind
{
    Unknown = 0,
    CorrectConfirmed = 1,
    PartiallyCorrect = 2,
    Incorrect = 3,
    WrongScope = 4,
    SourceInsufficient = 5,
    OverconfidentIncorrect = 6,
    UnderconfidentCorrect = 7,
    AbstentionAppropriate = 8,
    AbstentionTooConservative = 9,
    ReviewDisagreed = 10,
    ReviewConfirmed = 11
}

public sealed record CognitiveSelfModelRecord(
    Guid Id,
    Guid ProjectId,
    string ModelProfileKey,
    string RoleKey,
    string Purpose,
    IReadOnlyList<string> OperatingPrinciples,
    IReadOnlyList<string> AllowedTaskCategories,
    IReadOnlyList<string> RestrictedTaskCategories,
    IReadOnlyList<Guid> StrongDomainProfileIds,
    IReadOnlyList<Guid> WeakDomainProfileIds,
    IReadOnlyList<Guid> KnownFailurePatternIds,
    Guid DefaultPolicyProfileId,
    string AlgorithmVersion,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record DomainCompetenceProfileRecord(
    Guid Id,
    Guid ProjectId,
    Guid? KnowledgeRegionId,
    string DomainKey,
    string TaskTypeKey,
    ScoreVectorSnapshot CompetenceVector,
    ScoreScalarProjection? DisplayCompetence,
    Guid? CalibrationAggregateId,
    IReadOnlyList<Guid> EvidenceAnchorIds,
    string ScopeLimitations,
    DateTimeOffset CalculatedAtUtc,
    string AlgorithmVersion,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record KnownFailurePatternRecord(
    Guid Id,
    Guid ProjectId,
    string PatternKey,
    string Title,
    string Description,
    IReadOnlyList<HumilityTriggerKind> TriggerKinds,
    ScoreShapeSnapshot PatternShape,
    AnswerPostureKind RequiredPostureFloor,
    IReadOnlyList<string> MitigationSteps,
    IReadOnlyList<Guid> ExampleProbeTurnIds,
    IReadOnlyList<Guid> RegressionTestIds,
    DateTimeOffset FirstObservedAtUtc,
    DateTimeOffset LastObservedAtUtc,
    bool RequiresHumanReview,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record SelfRegulationPolicyProfileRecord(
    Guid Id,
    Guid ProjectId,
    string PolicyKey,
    string Description,
    IReadOnlyList<AnswerPostureKind> AllowedPostures,
    IReadOnlyList<HumilityTriggerKind> EnabledHumilityTriggers,
    double MinimumSourceSufficiencyLowRisk,
    double MinimumSourceSufficiencyMediumRisk,
    double MinimumSourceSufficiencyHighRisk,
    bool RequireProfessorReviewForHighImpactNovelty,
    bool RequireHumanReviewForHighRiskMutation,
    bool AllowPreliminaryReaction,
    string SchemaVersion,
    string AlgorithmVersion,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record HumilityTriggerRecord(
    Guid Id,
    Guid ProjectId,
    HumilityTriggerKind TriggerKind,
    string Explanation,
    ScoreEvaluationTrace TriggerTrace,
    AnswerPostureKind RecommendedPosture,
    IReadOnlyList<Guid> RelatedClaimIds,
    IReadOnlyList<Guid> RelatedEvidenceAnchorIds,
    DateTimeOffset FiredAtUtc,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record ConfidenceReinforcementRecord(
    Guid Id,
    Guid ProjectId,
    ConfidenceReinforcementKind ReinforcementKind,
    string Explanation,
    ScoreEvaluationTrace ReinforcementTrace,
    IReadOnlyList<Guid> EvidenceAnchorIds,
    DateTimeOffset ObservedAtUtc,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record SelfRegulationAssessmentRequest(
    Guid ProjectId,
    Guid WorkspaceFrameId,
    string RequestText,
    Guid? RecallTraceId,
    IReadOnlyList<Guid> CandidateClaimIds,
    IReadOnlyList<Guid> CandidateMemoryItemIds,
    MemoryAccessContext AccessContext,
    string TaskTypeKey,
    string RiskCategoryKey,
    IReadOnlyDictionary<string, string> Options);

public sealed record SelfRegulationAssessment(
    Guid Id,
    Guid ProjectId,
    Guid WorkspaceFrameId,
    Guid SelfModelId,
    SelfRegulationStateKind StateKind,
    ScoreEvaluationTrace AssessmentTrace,
    ScoreScalarProjection? DisplayConfidence,
    ScoreScalarProjection? DisplayRisk,
    IReadOnlyList<Guid> MatchedDomainCompetenceProfileIds,
    IReadOnlyList<Guid> MatchedKnownFailurePatternIds,
    IReadOnlyList<HumilityTriggerRecord> HumilityTriggers,
    IReadOnlyList<ConfidenceReinforcementRecord> ConfidenceReinforcements,
    IReadOnlyList<string> RequiredOperations,
    IReadOnlyList<string> Warnings,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record AnswerPostureDecision(
    Guid Id,
    Guid ProjectId,
    Guid WorkspaceFrameId,
    Guid SelfRegulationAssessmentId,
    AnswerPostureKind PostureKind,
    string Explanation,
    ScoreEvaluationTrace PostureTrace,
    ScoreScalarProjection? DisplayConfidence,
    IReadOnlyList<string> RequiredNextActions,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record CalibrationBinRecord(
    Guid Id,
    Guid CalibrationAggregateId,
    double LowerBoundInclusive,
    double UpperBoundExclusive,
    int SampleCount,
    double MeanPredictedConfidence,
    double ActualCorrectnessRate,
    double SignedBias,
    double BrierLoss,
    DateTimeOffset CalculatedAtUtc);

public sealed record CalibrationAggregateRecord(
    Guid Id,
    Guid ProjectId,
    string DomainKey,
    string TaskTypeKey,
    string ModelProfileKey,
    string RiskCategoryKey,
    string FeaturePatternKey,
    int SampleCount,
    double ExpectedCalibrationError,
    double BrierScore,
    double SignedConfidenceBias,
    double OverconfidenceRate,
    double UnderconfidenceRate,
    double AbstentionPrecision,
    double WrongScopeRate,
    double SourceInsufficientRate,
    IReadOnlyList<CalibrationBinRecord> Bins,
    string ProfileVersion,
    DateTimeOffset CalculatedAtUtc,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record SelfRegulationOutcomeRecord(
    Guid Id,
    Guid ProjectId,
    Guid SelfRegulationAssessmentId,
    Guid? AnswerPostureDecisionId,
    SelfRegulationOutcomeKind OutcomeKind,
    double? ActualCorrectnessScore,
    string OutcomeSummary,
    IReadOnlyList<Guid> EvidenceAnchorIds,
    IReadOnlyList<Guid> CreatedPredictionErrorIds,
    IReadOnlyList<Guid> CreatedCognitiveSignalIds,
    IReadOnlyList<Guid> CreatedRegressionTestIds,
    IReadOnlyList<Guid> CreatedReviewItemIds,
    DateTimeOffset ObservedAtUtc,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record ProfessorReviewRequest(
    Guid ProjectId,
    Guid WorkspaceFrameId,
    Guid SelfRegulationAssessmentId,
    ProfessorReviewModeKind ReviewMode,
    string ReviewQuestion,
    IReadOnlyList<Guid> InputClaimIds,
    IReadOnlyList<Guid> InputEvidenceAnchorIds,
    IReadOnlyList<Guid> InputMemoryItemIds,
    MemoryAccessContext AccessContext,
    string ModelProfileKey,
    IReadOnlyDictionary<string, string> Options);

public sealed record ProfessorReviewResult(
    Guid Id,
    Guid ProjectId,
    Guid WorkspaceFrameId,
    Guid SelfRegulationAssessmentId,
    ProfessorReviewModeKind ReviewMode,
    string Summary,
    string Critique,
    IReadOnlyList<string> MissingEvidenceRequests,
    IReadOnlyList<string> AlternativeHypotheses,
    IReadOnlyList<string> SuggestedProbeQuestions,
    IReadOnlyList<string> SuggestedRegressionTests,
    AnswerPostureKind RecommendedPosture,
    ScoreEvaluationTrace ReviewTrace,
    string ModelProfileKey,
    string PromptProfileVersion,
    string OutputHash,
    bool RequiresHumanReview,
    DateTimeOffset CompletedAtUtc,
    IReadOnlyDictionary<string, string> Metadata);

public interface ICognitiveSelfModelStore
{
    Task<CognitiveSelfModelRecord> GetActiveSelfModelAsync(
        Guid projectId,
        string modelProfileKey,
        string roleKey,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DomainCompetenceProfileRecord>> GetCompetenceProfilesAsync(
        Guid projectId,
        IReadOnlyList<Guid> knowledgeRegionIds,
        string taskTypeKey,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<KnownFailurePatternRecord>> GetKnownFailurePatternsAsync(
        Guid projectId,
        string taskTypeKey,
        CancellationToken cancellationToken = default);
}

public interface ISelfRegulationOrchestrator
{
    Task<SelfRegulationAssessment> AssessAsync(
        SelfRegulationAssessmentRequest request,
        CancellationToken cancellationToken = default);

    Task<AnswerPostureDecision> SelectAnswerPostureAsync(
        SelfRegulationAssessment assessment,
        CancellationToken cancellationToken = default);
}

public interface IHumilityTriggerEngine
{
    Task<IReadOnlyList<HumilityTriggerRecord>> EvaluateAsync(
        SelfRegulationAssessmentRequest request,
        CognitiveSelfModelRecord selfModel,
        IReadOnlyList<KnownFailurePatternRecord> knownFailurePatterns,
        CancellationToken cancellationToken = default);
}

public interface ICalibrationHealthService
{
    Task<CalibrationAggregateRecord?> GetAggregateAsync(
        Guid projectId,
        string domainKey,
        string taskTypeKey,
        string modelProfileKey,
        string riskCategoryKey,
        string featurePatternKey,
        CancellationToken cancellationToken = default);

    Task<SelfRegulationOutcomeRecord> ObserveOutcomeAsync(
        SelfRegulationOutcomeRecord outcome,
        CancellationToken cancellationToken = default);
}

public interface IProfessorReviewService
{
    Task<ProfessorReviewResult> ReviewAsync(
        ProfessorReviewRequest request,
        CancellationToken cancellationToken = default);
}
