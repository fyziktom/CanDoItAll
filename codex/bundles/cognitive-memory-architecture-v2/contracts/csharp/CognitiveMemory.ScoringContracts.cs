using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CanDoItAll.CognitiveMemory.Abstractions;

public enum ScoreSpaceKind
{
    Unknown = 0,
    RecallCandidate = 1,
    AttentionRouting = 2,
    BeliefState = 3,
    SalienceSignal = 4,
    ReplayPriority = 5,
    ProbeAssessment = 6,
    AnswerGate = 7,
    EpistemicNeed = 8,
    CrossProjectPromotion = 9,
    ProcedureMaturity = 10,
    MindMapSimilarity = 11,
    MemoryActivation = 12,
    SelfRegulationAssessment = 13,
    SelfModelCompetence = 14,
    CalibrationHealth = 15,
    ProfessorReviewRouting = 16,
    AnswerPosture = 17
}

public enum ScoreDimensionKind
{
    Unknown = 0,
    SemanticSimilarity = 1,
    LexicalMatch = 2,
    GraphProximity = 3,
    SpatialProximity = 4,
    MetadataFit = 5,
    TemporalRecency = 6,
    ContextFit = 7,
    SourceSufficiency = 8,
    SourceQuality = 9,
    EvidenceSupport = 10,
    EvidenceAttack = 11,
    ContradictionPressure = 12,
    StalenessPressure = 13,
    CalibrationRisk = 14,
    AccessPolicyRisk = 15,
    RedactionPressure = 16,
    RiskImpact = 17,
    Usefulness = 18,
    Reward = 19,
    ReworkCost = 20,
    UserInterest = 21,
    StrategicAlignment = 22,
    ProcedureMaturity = 23,
    ExpectedEffort = 24,
    BusinessValue = 25,
    ExpectedReuse = 26,
    CognitiveLoad = 27,
    QuestionDensity = 28,
    FailureRecurrence = 29,
    Volatility = 30,
    SourceAvailability = 31,
    EntityEquivalence = 32,
    ContextSeparation = 33,
    PrivacyRisk = 34,
    WorkspaceFocusFit = 35,
    MissingKnowledgePressure = 36,
    ActionCost = 37,
    OutcomeMismatch = 38,
    EvidenceStrength = 39,
    EvidenceCoverage = 40,
    SourceReliability = 41,
    RecencyFit = 42,
    NoveltyRisk = 43,
    ConsequenceRisk = 44,
    ModelUncertainty = 45,
    HistoricalCalibrationFit = 46,
    DomainCompetenceFit = 47,
    KnownFailurePatternSimilarity = 48,
    ScopeAmbiguity = 49,
    UserCorrectionPressure = 50,
    SelfModelStability = 51,
    ProfessorReviewValue = 52,
    EscalationCost = 53,
    AbstentionCost = 54,
    ConfidenceBias = 55,
    OverconfidenceRate = 56,
    UnderconfidenceRate = 57,
    HumanReviewAgreement = 58,
    ProfessorReviewAgreement = 59,
    HumilityTriggerPressure = 60,
    ConfidenceReinforcementPressure = 61
}

public enum ScoreShapeKind
{
    Unknown = 0,
    PointVector = 1,
    WeightedRegion = 2,
    CentroidRadius = 3,
    ThresholdEnvelope = 4,
    BoundaryPlane = 5,
    ParetoFrontier = 6,
    TimeDecayedTrajectory = 7
}

public enum ScoreMissingDimensionPolicy
{
    Unknown = 0,
    RejectEvaluation = 1,
    MarkUnavailable = 2,
    TreatAsNotApplicable = 3,
    UseProfileDefaultWithWarning = 4
}

public enum ScoreScalarProjectionKind
{
    None = 0,
    DisplayOnly = 1,
    QueueOrdering = 2,
    UiSorting = 3,
    TieBreaker = 4
}

public enum ScoreProjectionBucket
{
    Unknown = 0,
    StrongAccept = 1,
    WeakAccept = 2,
    NeedsClarification = 3,
    NeedsReview = 4,
    Inhibit = 5,
    Reject = 6,
    Abstain = 7
}

public enum ScoreEvidenceKind
{
    Unknown = 0,
    SourceItem = 1,
    EvidenceAnchor = 2,
    MemoryItem = 3,
    Claim = 4,
    RecallTrace = 5,
    ProbeTurn = 6,
    WorkflowRun = 7,
    ProcessRun = 8,
    ReviewDecision = 9,
    PredictionError = 10,
    CognitiveSignal = 11,
    ReplayJob = 12,
    ProcedureSkill = 13,
    AnswerGateDecision = 14,
    ProjectDirection = 15,
    CoverageMap = 16,
    SelfModel = 17,
    DomainCompetenceProfile = 18,
    KnownFailurePattern = 19,
    CalibrationAggregate = 20,
    SelfRegulationAssessment = 21,
    AnswerPostureDecision = 22,
    ProfessorReview = 23,
    HumilityTrigger = 24,
    ConfidenceReinforcement = 25
}

public sealed record ScoreDimensionDefinition(
    ScoreDimensionKind Kind,
    double Minimum,
    double Maximum,
    bool HigherIsBetter,
    bool Required,
    double DefaultWeight,
    string Description);

public sealed record ScoreSpaceDefinition(
    ScoreSpaceKind Kind,
    string SchemaVersion,
    string NormalizationProfile,
    ScoreMissingDimensionPolicy MissingDimensionPolicy,
    ScoreScalarProjectionKind ScalarProjectionKind,
    IReadOnlyList<ScoreDimensionDefinition> Dimensions,
    string AlgorithmVersion);

public sealed record ScoreEvidenceRef(
    ScoreEvidenceKind EvidenceKind,
    Guid EvidenceId,
    double Confidence,
    DateTimeOffset ObservedAtUtc);

public sealed record ScoreComponent(
    ScoreDimensionKind DimensionKind,
    double NormalizedValue,
    double Confidence,
    IReadOnlyList<ScoreEvidenceRef> EvidenceRefs,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record ScoreVectorSnapshot(
    ScoreSpaceKind SpaceKind,
    string SchemaVersion,
    string NormalizationProfile,
    IReadOnlyList<ScoreComponent> Components,
    string AlgorithmVersion,
    DateTimeOffset CalculatedAtUtc,
    string InputHash);

public sealed record ScoreShapeComponent(
    ScoreDimensionKind DimensionKind,
    double Center,
    double? LowerBound,
    double? UpperBound,
    double Weight);

public sealed record ScoreShapeSnapshot(
    ScoreShapeKind ShapeKind,
    ScoreSpaceKind SpaceKind,
    string SchemaVersion,
    IReadOnlyList<ScoreShapeComponent> Components,
    double? Radius,
    string Explanation,
    IReadOnlyList<ScoreEvidenceRef> EvidenceRefs,
    string AlgorithmVersion);

public sealed record ScoreScalarProjection(
    ScoreScalarProjectionKind ProjectionKind,
    ScoreProjectionBucket Bucket,
    double? DisplayScore,
    int? ParetoRank,
    string Explanation);

public sealed record ScoreEvaluationRequest(
    Guid ProjectId,
    ScoreSpaceKind SpaceKind,
    string SchemaVersion,
    IReadOnlyList<ScoreVectorSnapshot> InputVectors,
    IReadOnlyList<ScoreShapeSnapshot> CandidateShapes,
    IReadOnlyDictionary<string, string> Options);

public sealed record ScoreEvaluationTrace(
    Guid Id,
    Guid ProjectId,
    ScoreSpaceKind SpaceKind,
    string SchemaVersion,
    IReadOnlyList<ScoreVectorSnapshot> InputVectors,
    IReadOnlyList<ScoreShapeSnapshot> MatchedShapes,
    IReadOnlyList<ScoreDimensionKind> MissingRequiredDimensions,
    ScoreScalarProjection? ScalarProjection,
    string DecisionExplanation,
    string AlgorithmVersion,
    DateTimeOffset CalculatedAtUtc);

public interface IScoreSpaceRegistry
{
    Task<ScoreSpaceDefinition> GetDefinitionAsync(
        ScoreSpaceKind kind,
        string schemaVersion,
        CancellationToken cancellationToken = default);
}

public interface IScoreGeometryDriver
{
    Task<ScoreEvaluationTrace> EvaluateAsync(
        ScoreEvaluationRequest request,
        CancellationToken cancellationToken = default);
}
