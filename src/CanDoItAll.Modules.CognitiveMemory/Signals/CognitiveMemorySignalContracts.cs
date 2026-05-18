using System.Text.Json.Serialization;

namespace CanDoItAll.Modules.CognitiveMemory;

public enum CognitiveMemorySignalKind
{
    Unknown = 0,
    Novelty = 1,
    Surprise = 2,
    Risk = 3,
    Usefulness = 4,
    Reward = 5,
    ReworkCost = 6,
    ContradictionPressure = 7,
    UserInterest = 8,
    StrategicAlignment = 9,
    StalenessPressure = 10,
    SourceWeakness = 11,
    CalibrationRisk = 12,
    OverconfidencePressure = 13,
    UnderconfidencePressure = 14,
    KnownFailurePatternMatched = 15,
    ProfessorReviewRequired = 16,
    ProfessorReviewDisagreement = 17,
    SelfModelUpdated = 18,
    CalibrationDrift = 19,
    HumilityTriggerFired = 20,
    ConfidenceReinforced = 21
}

public enum CognitiveMemorySignalSourceKind
{
    Unknown = 0,
    ProbeFeedback = 1,
    WorkflowRun = 2,
    ProcessRun = 3,
    ProcedureExecution = 4,
    QaEvent = 5,
    SourceIngestion = 6,
    Consolidation = 7,
    HumanReview = 8,
    UserCorrection = 9,
    RegressionReplay = 10,
    SelfRegulation = 11,
    ProfessorReview = 12,
    AttentionRouting = 13,
    RecallTrace = 14
}

public enum CognitiveMemorySignalConsumerKind
{
    Unknown = 0,
    ActivationEngine = 1,
    RecallRanking = 2,
    AttentionRouter = 3,
    ReplayScheduler = 4,
    EpistemicDrive = 5,
    LearningProposalService = 6,
    ProcedureMaturityEvaluator = 7,
    ConfidenceCalibration = 8,
    ReviewQueuePriority = 9,
    SelfRegulationAssessment = 10,
    AnswerGate = 11
}

public enum CognitiveMemoryPredictionExpectationKind
{
    Unknown = 0,
    ClaimRecall = 1,
    SourceSufficiency = 2,
    ProcedureOutcome = 3,
    ValidationResult = 4,
    ContextBoundary = 5,
    ConfidenceRange = 6
}

public enum CognitiveMemoryPredictionErrorKind
{
    Unknown = 0,
    MissingKnowledge = 1,
    WrongScope = 2,
    OverconfidentIncorrect = 3,
    UnderconfidentCorrect = 4,
    SourceInsufficient = 5,
    StaleMemory = 6,
    ContradictionObserved = 7,
    ProcedureFailed = 8,
    ToolOutcomeMismatch = 9,
    RedactionLimited = 10
}

public enum CognitiveMemoryPredictionSuggestedActionKind
{
    Unknown = 0,
    SourceAudit = 1,
    Probe = 2,
    HumanReview = 3,
    LearningProposal = 4,
    Replay = 5,
    ProcedureReview = 6,
    CalibrationReview = 7,
    Abstain = 8
}

public readonly record struct CognitiveMemorySignalId
{
    [JsonConstructor]
    public CognitiveMemorySignalId(Guid value)
    {
        Value = CognitiveMemoryGuard.EnsureNonEmpty(value, nameof(value));
    }

    public Guid Value { get; }

    public static CognitiveMemorySignalId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public readonly record struct CognitiveMemoryPredictionExpectationId
{
    [JsonConstructor]
    public CognitiveMemoryPredictionExpectationId(Guid value)
    {
        Value = CognitiveMemoryGuard.EnsureNonEmpty(value, nameof(value));
    }

    public Guid Value { get; }

    public static CognitiveMemoryPredictionExpectationId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public readonly record struct CognitiveMemoryPredictionErrorId
{
    [JsonConstructor]
    public CognitiveMemoryPredictionErrorId(Guid value)
    {
        Value = CognitiveMemoryGuard.EnsureNonEmpty(value, nameof(value));
    }

    public Guid Value { get; }

    public static CognitiveMemoryPredictionErrorId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public sealed record CognitiveMemorySignalComponentDraft
{
    public CognitiveMemorySignalComponentDraft(
        CognitiveMemoryScoreDimensionKind dimensionKind,
        double normalizedValue,
        double confidence = 1)
    {
        if (dimensionKind == CognitiveMemoryScoreDimensionKind.Unknown)
        {
            throw new ArgumentException("Signal score dimension must be explicit.", nameof(dimensionKind));
        }

        CognitiveMemoryScoreGuard.EnsureUnitInterval(normalizedValue, nameof(normalizedValue));
        CognitiveMemoryScoreGuard.EnsureUnitInterval(confidence, nameof(confidence));
        DimensionKind = dimensionKind;
        NormalizedValue = normalizedValue;
        Confidence = confidence;
    }

    public CognitiveMemoryScoreDimensionKind DimensionKind { get; }

    public double NormalizedValue { get; }

    public double Confidence { get; }
}

public sealed record CognitiveMemorySignalPublicationRequest(
    Guid ProjectId,
    CognitiveMemorySignalKind SignalKind,
    CognitiveMemorySignalSourceKind SourceKind,
    CognitiveMemoryActorKind ActorKind,
    string ActorId,
    CognitiveMemoryPolicyContext PolicyContext,
    string Summary,
    IReadOnlyList<CognitiveMemorySignalComponentDraft> Components,
    IReadOnlyList<CognitiveMemorySignalConsumerKind> ConsumerKinds,
    IReadOnlyList<CognitiveMemoryEvidenceAnchorId> EvidenceAnchorIds,
    DateTimeOffset? ObservedAtUtc = null,
    bool RequiresReview = false,
    CognitiveMemoryAccessLevel AccessLevel = CognitiveMemoryAccessLevel.Project,
    CognitiveMemoryRedactionState RedactionState = CognitiveMemoryRedactionState.Safe,
    CognitiveMemoryRiskLevel RiskLevel = CognitiveMemoryRiskLevel.Low,
    CognitiveMemoryWorkspaceFrameId? WorkspaceFrameId = null,
    CognitiveMemoryAttentionDecisionId? AttentionDecisionId = null,
    CognitiveMemoryPredictionErrorId? PredictionErrorId = null,
    CognitiveMemoryRecordId? MemoryRecordId = null,
    CognitiveMemoryClaimId? ClaimId = null,
    CognitiveMemorySourceItemId? SourceItemId = null,
    Guid? ProcedureSkillId = null,
    Guid? WorkflowRunId = null,
    Guid? ProcessRunId = null,
    Guid? ProbeTurnId = null,
    Guid? ReviewItemId = null,
    IReadOnlyDictionary<string, string>? Metadata = null);

public sealed record CognitiveMemorySignalPublicationDraft(
    CognitiveMemorySignalKind SignalKind,
    CognitiveMemorySignalSourceKind SourceKind,
    string Summary,
    IReadOnlyList<CognitiveMemorySignalComponentDraft> Components,
    IReadOnlyList<CognitiveMemorySignalConsumerKind> ConsumerKinds,
    CognitiveMemoryRiskLevel RiskLevel = CognitiveMemoryRiskLevel.Low,
    bool RequiresReview = false,
    IReadOnlyDictionary<string, string>? Metadata = null);

public sealed record CognitiveMemorySignalPublicationResult(
    CognitiveMemorySignalRecord Signal,
    IReadOnlyList<CognitiveMemorySignalConsumerPolicyRecord> ConsumerPolicies,
    CognitiveMemoryScoreEvaluationTrace ScoreTrace);

public sealed record CognitiveMemorySignalQuery(
    Guid ProjectId,
    CognitiveMemoryPolicyContext PolicyContext,
    CognitiveMemoryPageRequest Page,
    IReadOnlyList<CognitiveMemorySignalKind>? SignalKinds = null,
    IReadOnlyList<CognitiveMemorySignalConsumerKind>? ConsumerKinds = null,
    DateTimeOffset? SinceUtc = null);

public sealed record CognitiveMemorySignalQueryResult(
    IReadOnlyList<CognitiveMemorySignalRecord> Signals);

public sealed record CognitiveMemoryPredictionExpectationRequest(
    Guid ProjectId,
    CognitiveMemoryPredictionExpectationKind ExpectationKind,
    CognitiveMemoryActorKind ActorKind,
    string ActorId,
    CognitiveMemoryPolicyContext PolicyContext,
    string Summary,
    string ExpectedOutcome,
    IReadOnlyList<CognitiveMemoryEvidenceAnchorId> EvidenceAnchorIds,
    CognitiveMemoryWorkspaceFrameId? WorkspaceFrameId = null,
    CognitiveMemoryAttentionDecisionId? AttentionDecisionId = null,
    CognitiveMemoryRecordId? MemoryRecordId = null,
    CognitiveMemoryClaimId? ClaimId = null,
    CognitiveMemorySourceItemId? SourceItemId = null,
    Guid? ProcedureSkillId = null,
    Guid? WorkflowRunId = null,
    Guid? ProcessRunId = null,
    Guid? ProbeSessionId = null,
    string ExpectedContextKey = "",
    CognitiveMemoryWorkspaceSourceSufficiency ExpectedSourceSufficiency = CognitiveMemoryWorkspaceSourceSufficiency.Unknown,
    double? MinimumExpectedConfidence = null,
    double? MaximumExpectedConfidence = null,
    DateTimeOffset? CreatedAtUtc = null,
    IReadOnlyDictionary<string, string>? Metadata = null);

public sealed record CognitiveMemoryPredictionErrorObservationRequest(
    Guid ProjectId,
    CognitiveMemoryPredictionErrorKind ErrorKind,
    CognitiveMemoryActorKind ActorKind,
    string ActorId,
    CognitiveMemoryPolicyContext PolicyContext,
    string ObservationSummary,
    string ExpectedSummary,
    string ObservedSummary,
    string CauseHypothesis,
    CognitiveMemoryPredictionSuggestedActionKind SuggestedActionKind,
    string SuggestedAction,
    IReadOnlyList<CognitiveMemorySignalComponentDraft> SeverityComponents,
    IReadOnlyList<CognitiveMemoryEvidenceAnchorId> EvidenceAnchorIds,
    CognitiveMemoryPredictionExpectationId? ExpectationId = null,
    CognitiveMemoryWorkspaceFrameId? WorkspaceFrameId = null,
    CognitiveMemoryAttentionDecisionId? AttentionDecisionId = null,
    CognitiveMemoryRecordId? MemoryRecordId = null,
    CognitiveMemoryClaimId? ClaimId = null,
    CognitiveMemorySourceItemId? SourceItemId = null,
    Guid? ProcedureSkillId = null,
    Guid? WorkflowRunId = null,
    Guid? ProcessRunId = null,
    Guid? ProbeTurnId = null,
    bool RequiresReview = false,
    IReadOnlyList<CognitiveMemorySignalPublicationDraft>? SignalsToPublish = null,
    DateTimeOffset? ObservedAtUtc = null,
    IReadOnlyDictionary<string, string>? Metadata = null);

public sealed record CognitiveMemoryPredictionErrorObservationResult(
    CognitiveMemoryPredictionErrorRecord PredictionError,
    IReadOnlyList<CognitiveMemorySignalPublicationResult> PublishedSignals,
    CognitiveMemoryScoreEvaluationTrace SeverityTrace);

public interface ICognitiveMemorySignalLedger
{
    ValueTask<CognitiveMemorySignalPublicationResult> PublishAsync(
        CognitiveMemorySignalPublicationRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<CognitiveMemorySignalQueryResult> QueryAsync(
        CognitiveMemorySignalQuery query,
        CancellationToken cancellationToken = default);
}

public interface ICognitiveMemoryPredictionErrorEngine
{
    ValueTask<CognitiveMemoryPredictionExpectationRecord> RecordExpectationAsync(
        CognitiveMemoryPredictionExpectationRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<CognitiveMemoryPredictionErrorObservationResult> ObserveAsync(
        CognitiveMemoryPredictionErrorObservationRequest request,
        CancellationToken cancellationToken = default);
}
