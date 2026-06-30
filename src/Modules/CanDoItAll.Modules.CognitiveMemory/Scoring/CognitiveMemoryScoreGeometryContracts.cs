using System.Text.Json.Serialization;

namespace CanDoItAll.Modules.CognitiveMemory;

public enum CognitiveMemoryScoreSpaceKind
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
    AnswerPosture = 17,
    PredictionErrorSeverity = 18,
    ConsolidationCandidate = 19,
    SimulationRisk = 20
}

public enum CognitiveMemoryScoreDimensionKind
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
    ConfidenceReinforcementPressure = 61,
    MemoryActivation = 62,
    ContextAmbiguity = 63,
    AvailableWorkspaceEvidence = 64,
    ExpectedValue = 65,
    TemporalValidity = 66,
    HumanValidation = 67,
    PredictionErrorMagnitude = 68,
    RegressionFailure = 69,
    WrongScopePressure = 70,
    RegressionValue = 71,
    SourceReusePermission = 72,
    PolicyCompatibility = 73,
    GlobalReuseValue = 74,
    SourceWeakness = 75,
    AbstentionQuality = 76,
    WrongScopeRecurrence = 77,
    SourceInsufficientRecurrence = 78,
    CalibrationDrift = 79,
    ExpectedLearningValue = 80
}

public enum CognitiveMemoryScoreShapeKind
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

public enum CognitiveMemoryScoreMissingDimensionPolicy
{
    RejectEvaluation = 0,
    MarkUnavailable = 1,
    TreatAsNotApplicable = 2,
    UseProfileDefaultWithWarning = 3
}

public enum CognitiveMemoryScoreMissingDimensionReason
{
    Unavailable = 0,
    NotApplicable = 1,
    BlockedByPolicy = 2,
    NotObserved = 3
}

public enum CognitiveMemoryScoreScalarProjectionKind
{
    None = 0,
    DisplayOnly = 1,
    QueueOrdering = 2,
    UiSorting = 3,
    TieBreaker = 4
}

public enum CognitiveMemoryScoreProjectionBucket
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

public enum CognitiveMemoryScoreEvidenceKind
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
    ConfidenceReinforcement = 25,
    WorkspaceFrame = 26,
    AttentionDecision = 27,
    InhibitedCandidate = 28,
    OpenQuestion = 29,
    ProcedureSimulation = 30
}

public enum CognitiveMemoryScoreOwnerKind
{
    Unknown = 0,
    MemoryRecord = 1,
    MemoryRelation = 2,
    RecallTrace = 3,
    ReviewItem = 4,
    Run = 5,
    SourceItem = 6,
    ProbeTurn = 7,
    AnswerGateDecision = 8,
    LearningProposal = 9,
    CrossProjectCandidate = 10,
    ProcedureSkill = 11,
    SelfRegulationAssessment = 12,
    WorkspaceFrame = 13,
    AttentionDecision = 14,
    PredictionError = 15,
    CognitiveSignal = 16,
    RecallCandidate = 17,
    ReplayJob = 18,
    TemporalEpisode = 19,
    EpisodeStep = 20,
    ProcedureSimulation = 21,
    ProcedureFailureMode = 22,
    ProfessorReview = 23,
    CalibrationAggregate = 24,
    DistributedJob = 25
}

public readonly record struct CognitiveMemoryScoreEvaluationId
{
    [JsonConstructor]
    public CognitiveMemoryScoreEvaluationId(Guid value)
    {
        Value = CognitiveMemoryGuard.EnsureNonEmpty(value, nameof(value));
    }

    public Guid Value { get; }

    public static CognitiveMemoryScoreEvaluationId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public readonly record struct CognitiveMemoryScoreSchemaVersion
{
    [JsonConstructor]
    public CognitiveMemoryScoreSchemaVersion(string value)
    {
        Value = CognitiveMemoryGuard.EnsureText(value, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public readonly record struct CognitiveMemoryScoreNormalizationProfileId
{
    [JsonConstructor]
    public CognitiveMemoryScoreNormalizationProfileId(string value)
    {
        Value = CognitiveMemoryGuard.EnsureText(value, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public sealed record CognitiveMemoryScoreDimensionDefinition
{
    public CognitiveMemoryScoreDimensionDefinition(
        CognitiveMemoryScoreDimensionKind kind,
        double minimum,
        double maximum,
        bool higherIsBetter,
        bool required,
        double defaultWeight,
        string description)
    {
        if (kind == CognitiveMemoryScoreDimensionKind.Unknown)
        {
            throw new ArgumentException("Score dimension kind must be explicit.", nameof(kind));
        }

        if (double.IsNaN(minimum) || double.IsNaN(maximum) || minimum >= maximum)
        {
            throw new ArgumentOutOfRangeException(nameof(maximum), "Score dimension range must be finite and increasing.");
        }

        if (double.IsNaN(defaultWeight) || defaultWeight < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(defaultWeight), "Score dimension weight must not be negative.");
        }

        Kind = kind;
        Minimum = minimum;
        Maximum = maximum;
        HigherIsBetter = higherIsBetter;
        Required = required;
        DefaultWeight = defaultWeight;
        Description = CognitiveMemoryGuard.EnsureText(description, nameof(description));
    }

    public CognitiveMemoryScoreDimensionKind Kind { get; }

    public double Minimum { get; }

    public double Maximum { get; }

    public bool HigherIsBetter { get; }

    public bool Required { get; }

    public double DefaultWeight { get; }

    public string Description { get; }
}

public sealed record CognitiveMemoryScoreSpaceDefinition
{
    public CognitiveMemoryScoreSpaceDefinition(
        CognitiveMemoryScoreSpaceKind kind,
        CognitiveMemoryScoreSchemaVersion schemaVersion,
        CognitiveMemoryScoreNormalizationProfileId normalizationProfileId,
        CognitiveMemoryScoreMissingDimensionPolicy missingDimensionPolicy,
        CognitiveMemoryScoreScalarProjectionKind scalarProjectionKind,
        IReadOnlyList<CognitiveMemoryScoreDimensionDefinition> dimensions,
        CognitiveMemoryAlgorithmVersion algorithmVersion)
    {
        if (kind == CognitiveMemoryScoreSpaceKind.Unknown)
        {
            throw new ArgumentException("Score space kind must be explicit.", nameof(kind));
        }

        ArgumentNullException.ThrowIfNull(dimensions);
        if (dimensions.Count == 0)
        {
            throw new ArgumentException("Score spaces must define at least one dimension.", nameof(dimensions));
        }

        if (dimensions.Select(dimension => dimension.Kind).Distinct().Count() != dimensions.Count)
        {
            throw new ArgumentException("Score space dimensions must be unique by kind.", nameof(dimensions));
        }

        Kind = kind;
        SchemaVersion = schemaVersion;
        NormalizationProfileId = normalizationProfileId;
        MissingDimensionPolicy = missingDimensionPolicy;
        ScalarProjectionKind = scalarProjectionKind;
        Dimensions = dimensions;
        AlgorithmVersion = algorithmVersion;
    }

    public CognitiveMemoryScoreSpaceKind Kind { get; }

    public CognitiveMemoryScoreSchemaVersion SchemaVersion { get; }

    public CognitiveMemoryScoreNormalizationProfileId NormalizationProfileId { get; }

    public CognitiveMemoryScoreMissingDimensionPolicy MissingDimensionPolicy { get; }

    public CognitiveMemoryScoreScalarProjectionKind ScalarProjectionKind { get; }

    public IReadOnlyList<CognitiveMemoryScoreDimensionDefinition> Dimensions { get; }

    public CognitiveMemoryAlgorithmVersion AlgorithmVersion { get; }
}

public sealed record CognitiveMemoryScoreEvidenceRef(
    CognitiveMemoryScoreEvidenceKind EvidenceKind,
    Guid EvidenceId,
    double Confidence,
    DateTimeOffset ObservedAtUtc);

public sealed record CognitiveMemoryScoreComponent
{
    public CognitiveMemoryScoreComponent(
        CognitiveMemoryScoreDimensionKind dimensionKind,
        double normalizedValue,
        double confidence,
        IReadOnlyList<CognitiveMemoryScoreEvidenceRef>? evidenceRefs = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        if (dimensionKind == CognitiveMemoryScoreDimensionKind.Unknown)
        {
            throw new ArgumentException("Score component dimension kind must be explicit.", nameof(dimensionKind));
        }

        CognitiveMemoryScoreGuard.EnsureUnitInterval(normalizedValue, nameof(normalizedValue));
        CognitiveMemoryScoreGuard.EnsureUnitInterval(confidence, nameof(confidence));
        DimensionKind = dimensionKind;
        NormalizedValue = normalizedValue;
        Confidence = confidence;
        EvidenceRefs = evidenceRefs ?? [];
        Metadata = metadata ?? new Dictionary<string, string>();
    }

    public CognitiveMemoryScoreDimensionKind DimensionKind { get; }

    public double NormalizedValue { get; }

    public double Confidence { get; }

    public IReadOnlyList<CognitiveMemoryScoreEvidenceRef> EvidenceRefs { get; }

    public IReadOnlyDictionary<string, string> Metadata { get; }
}

public sealed record CognitiveMemoryScoreVectorSnapshot
{
    public CognitiveMemoryScoreVectorSnapshot(
        CognitiveMemoryScoreSpaceKind spaceKind,
        CognitiveMemoryScoreSchemaVersion schemaVersion,
        CognitiveMemoryScoreNormalizationProfileId normalizationProfileId,
        IReadOnlyList<CognitiveMemoryScoreComponent> components,
        CognitiveMemoryAlgorithmVersion algorithmVersion,
        DateTimeOffset calculatedAtUtc,
        CognitiveMemoryHash inputHash)
    {
        if (spaceKind == CognitiveMemoryScoreSpaceKind.Unknown)
        {
            throw new ArgumentException("Score vector space kind must be explicit.", nameof(spaceKind));
        }

        ArgumentNullException.ThrowIfNull(components);
        if (components.Count == 0)
        {
            throw new ArgumentException("Score vectors must contain at least one component.", nameof(components));
        }

        if (components.Select(component => component.DimensionKind).Distinct().Count() != components.Count)
        {
            throw new ArgumentException("Score vector components must be unique by dimension.", nameof(components));
        }

        SpaceKind = spaceKind;
        SchemaVersion = schemaVersion;
        NormalizationProfileId = normalizationProfileId;
        Components = components;
        AlgorithmVersion = algorithmVersion;
        CalculatedAtUtc = calculatedAtUtc;
        InputHash = inputHash;
    }

    public CognitiveMemoryScoreSpaceKind SpaceKind { get; }

    public CognitiveMemoryScoreSchemaVersion SchemaVersion { get; }

    public CognitiveMemoryScoreNormalizationProfileId NormalizationProfileId { get; }

    public IReadOnlyList<CognitiveMemoryScoreComponent> Components { get; }

    public CognitiveMemoryAlgorithmVersion AlgorithmVersion { get; }

    public DateTimeOffset CalculatedAtUtc { get; }

    public CognitiveMemoryHash InputHash { get; }
}

public sealed record CognitiveMemoryScoreShapeComponent
{
    public CognitiveMemoryScoreShapeComponent(
        CognitiveMemoryScoreDimensionKind dimensionKind,
        double center,
        double? lowerBound,
        double? upperBound,
        double weight)
    {
        if (dimensionKind == CognitiveMemoryScoreDimensionKind.Unknown)
        {
            throw new ArgumentException("Score shape dimension kind must be explicit.", nameof(dimensionKind));
        }

        CognitiveMemoryScoreGuard.EnsureUnitInterval(center, nameof(center));
        if (lowerBound is not null)
        {
            CognitiveMemoryScoreGuard.EnsureUnitInterval(lowerBound.Value, nameof(lowerBound));
        }

        if (upperBound is not null)
        {
            CognitiveMemoryScoreGuard.EnsureUnitInterval(upperBound.Value, nameof(upperBound));
        }

        if (lowerBound is not null && upperBound is not null && lowerBound > upperBound)
        {
            throw new ArgumentException("Shape lower bound must not exceed upper bound.", nameof(lowerBound));
        }

        if (double.IsNaN(weight) || weight < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(weight), "Score shape weight must not be negative.");
        }

        DimensionKind = dimensionKind;
        Center = center;
        LowerBound = lowerBound;
        UpperBound = upperBound;
        Weight = weight;
    }

    public CognitiveMemoryScoreDimensionKind DimensionKind { get; }

    public double Center { get; }

    public double? LowerBound { get; }

    public double? UpperBound { get; }

    public double Weight { get; }
}

public sealed record CognitiveMemoryScoreShapeSnapshot
{
    public CognitiveMemoryScoreShapeSnapshot(
        CognitiveMemoryScoreShapeKind shapeKind,
        CognitiveMemoryScoreSpaceKind spaceKind,
        CognitiveMemoryScoreSchemaVersion schemaVersion,
        IReadOnlyList<CognitiveMemoryScoreShapeComponent> components,
        double? radius,
        CognitiveMemoryScoreProjectionBucket projectionBucket,
        string explanation,
        IReadOnlyList<CognitiveMemoryScoreEvidenceRef>? evidenceRefs,
        CognitiveMemoryAlgorithmVersion algorithmVersion)
    {
        if (shapeKind == CognitiveMemoryScoreShapeKind.Unknown)
        {
            throw new ArgumentException("Score shape kind must be explicit.", nameof(shapeKind));
        }

        if (spaceKind == CognitiveMemoryScoreSpaceKind.Unknown)
        {
            throw new ArgumentException("Score shape space kind must be explicit.", nameof(spaceKind));
        }

        ArgumentNullException.ThrowIfNull(components);
        if (components.Count == 0)
        {
            throw new ArgumentException("Score shapes must contain at least one component.", nameof(components));
        }

        if (radius is not null)
        {
            CognitiveMemoryScoreGuard.EnsureUnitInterval(radius.Value, nameof(radius));
        }

        ShapeKind = shapeKind;
        SpaceKind = spaceKind;
        SchemaVersion = schemaVersion;
        Components = components;
        Radius = radius;
        ProjectionBucket = projectionBucket;
        Explanation = CognitiveMemoryGuard.EnsureText(explanation, nameof(explanation));
        EvidenceRefs = evidenceRefs ?? [];
        AlgorithmVersion = algorithmVersion;
    }

    public CognitiveMemoryScoreShapeKind ShapeKind { get; }

    public CognitiveMemoryScoreSpaceKind SpaceKind { get; }

    public CognitiveMemoryScoreSchemaVersion SchemaVersion { get; }

    public IReadOnlyList<CognitiveMemoryScoreShapeComponent> Components { get; }

    public double? Radius { get; }

    public CognitiveMemoryScoreProjectionBucket ProjectionBucket { get; }

    public string Explanation { get; }

    public IReadOnlyList<CognitiveMemoryScoreEvidenceRef> EvidenceRefs { get; }

    public CognitiveMemoryAlgorithmVersion AlgorithmVersion { get; }
}

public sealed record CognitiveMemoryScoreScalarProjection(
    CognitiveMemoryScoreScalarProjectionKind ProjectionKind,
    CognitiveMemoryScoreProjectionBucket Bucket,
    double? DisplayScore,
    int? ParetoRank,
    string Explanation);

public sealed record CognitiveMemoryMissingScoreDimension(
    CognitiveMemoryScoreDimensionKind DimensionKind,
    CognitiveMemoryScoreMissingDimensionReason Reason);

public sealed record CognitiveMemoryScoreEvaluationRequest(
    Guid? ProjectId,
    CognitiveMemoryScoreOwnerKind OwnerKind,
    Guid? OwnerId,
    CognitiveMemoryScoreSpaceKind SpaceKind,
    CognitiveMemoryScoreSchemaVersion SchemaVersion,
    IReadOnlyList<CognitiveMemoryScoreVectorSnapshot> InputVectors,
    IReadOnlyList<CognitiveMemoryScoreShapeSnapshot> CandidateShapes,
    IReadOnlyDictionary<string, string>? Options = null);

public sealed record CognitiveMemoryScoreEvaluationTrace(
    CognitiveMemoryScoreEvaluationId Id,
    Guid? ProjectId,
    CognitiveMemoryScoreOwnerKind OwnerKind,
    Guid? OwnerId,
    CognitiveMemoryScoreSpaceKind SpaceKind,
    CognitiveMemoryScoreSchemaVersion SchemaVersion,
    IReadOnlyList<CognitiveMemoryScoreVectorSnapshot> InputVectors,
    IReadOnlyList<CognitiveMemoryScoreShapeSnapshot> MatchedShapes,
    IReadOnlyList<CognitiveMemoryMissingScoreDimension> MissingRequiredDimensions,
    CognitiveMemoryScoreScalarProjection? ScalarProjection,
    string DecisionExplanation,
    CognitiveMemoryAlgorithmVersion AlgorithmVersion,
    DateTimeOffset CalculatedAtUtc);

public interface ICognitiveMemoryScoreSpaceRegistry
{
    ValueTask<CognitiveMemoryScoreSpaceDefinition> GetDefinitionAsync(
        CognitiveMemoryScoreSpaceKind kind,
        CognitiveMemoryScoreSchemaVersion schemaVersion,
        CancellationToken cancellationToken = default);
}

public interface ICognitiveMemoryScoreGeometryDriver
{
    ValueTask<CognitiveMemoryScoreEvaluationTrace> EvaluateAsync(
        CognitiveMemoryScoreEvaluationRequest request,
        CancellationToken cancellationToken = default);
}

internal static class CognitiveMemoryScoreGuard
{
    public static void EnsureUnitInterval(double value, string parameterName)
    {
        if (double.IsNaN(value) || value < 0 || value > 1)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Score values must be in the 0..1 interval.");
        }
    }
}
