using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CanDoItAll.CognitiveMemory.Abstractions;

public static class NeuroCognitivePatchContractMarker
{
}

public enum WorkspaceFrameKind
{
    Unknown = 0,
    UserConversation = 1,
    AgentRun = 2,
    WorkflowRun = 3,
    ProcessStep = 4,
    ProbeSession = 5,
    ReviewSession = 6,
    LearningTask = 7
}

public enum WorkingMemorySlotKind
{
    Unknown = 0,
    MemoryItem = 1,
    Claim = 2,
    SourceItem = 3,
    ProcedureSkill = 4,
    RecallTrace = 5,
    ProbeTurn = 6,
    WorkflowArtifact = 7,
    OpenQuestion = 8,
    Hypothesis = 9
}

public enum AttentionDecisionKind
{
    Unknown = 0,
    Recall = 1,
    AnswerFromWorkspace = 2,
    AskClarification = 3,
    RunSourceAudit = 4,
    StartProbe = 5,
    CreateReviewItem = 6,
    RequestLearningProposal = 7,
    RunReplay = 8,
    Abstain = 9
}

public enum CognitiveSignalKind
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
    CalibrationRisk = 12
}

public enum PredictionErrorKind
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

public enum EvidenceAnchorKind
{
    Unknown = 0,
    TextSpan = 1,
    StructuredPath = 2,
    FilePath = 3,
    RepositoryLocation = 4,
    MindMapNode = 5,
    WorkflowArtifact = 6,
    ProcessEvent = 7,
    ProbeTurn = 8,
    ReviewDecision = 9
}

public enum EvidenceDirection
{
    Unknown = 0,
    Supports = 1,
    Attacks = 2,
    Qualifies = 3,
    Supersedes = 4,
    NarrowsScope = 5,
    BroadensScope = 6,
    Example = 7,
    CounterExample = 8
}

public enum ClaimKind
{
    Unknown = 0,
    Fact = 1,
    Decision = 2,
    Requirement = 3,
    Policy = 4,
    ProcedureConstraint = 5,
    Observation = 6,
    FailureMode = 7,
    Hypothesis = 8
}

public enum MemoryBeliefStateKind
{
    Unknown = 0,
    Unexamined = 1,
    Supported = 2,
    WeaklySupported = 3,
    Contested = 4,
    Contradicted = 5,
    ScopeLimited = 6,
    Stale = 7,
    Superseded = 8,
    Rejected = 9,
    Validated = 10
}

public enum MemoryMutationCommandKind
{
    Unknown = 0,
    ProposeClaim = 1,
    SupportClaim = 2,
    AttackClaim = 3,
    NarrowScope = 4,
    BroadenScope = 5,
    SupersedeClaim = 6,
    RejectClaim = 7,
    ValidateClaim = 8,
    RetireClaim = 9,
    CreateRelation = 10,
    UpdateProcedureSkill = 11,
    InvalidateProjection = 12,
    RecordEvidence = 13
}

public enum CognitiveActorKind
{
    Unknown = 0,
    User = 1,
    Agent = 2,
    WorkflowExecutor = 3,
    ProcessRole = 4,
    DistributedWorker = 5,
    System = 6
}

public enum ContextFrameKind
{
    Unknown = 0,
    Project = 1,
    Environment = 2,
    Runtime = 3,
    Process = 4,
    Role = 5,
    Temporal = 6,
    SourceTrust = 7,
    Risk = 8,
    AccessScope = 9,
    Composite = 10
}

public enum ContextDimensionKind
{
    Unknown = 0,
    Project = 1,
    Environment = 2,
    Runtime = 3,
    Process = 4,
    Role = 5,
    TimeRange = 6,
    SourceTrust = 7,
    Risk = 8,
    AccessScope = 9,
    Version = 10,
    Branch = 11,
    Platform = 12
}

public enum EntityKind
{
    Unknown = 0,
    Project = 1,
    Module = 2,
    Plugin = 3,
    Workflow = 4,
    Process = 5,
    Agent = 6,
    UserRole = 7,
    SourceSystem = 8,
    Environment = 9,
    RepositoryBranch = 10,
    TechnologyTopic = 11,
    ProcedureTarget = 12,
    BusinessObject = 13,
    Artifact = 14
}

public enum TemporalEpisodeKind
{
    Unknown = 0,
    Workflow = 1,
    Process = 2,
    Probe = 3,
    Review = 4,
    Deployment = 5,
    Debugging = 6,
    LearningTask = 7,
    AgentHandoff = 8,
    UserCorrection = 9
}

public enum EpisodeStepActionKind
{
    Unknown = 0,
    Started = 1,
    ToolCalled = 2,
    SourceRead = 3,
    DecisionMade = 4,
    ArtifactCreated = 5,
    ValidationRun = 6,
    FeedbackReceived = 7,
    ErrorObserved = 8,
    ReviewQueued = 9,
    Completed = 10
}

public enum ReplayJobKind
{
    Unknown = 0,
    ConsolidateEpisode = 1,
    RehearseClaim = 2,
    ReplayProbeRegression = 3,
    ValidateProcedure = 4,
    RefreshSourceAnchors = 5,
    ResolveContradiction = 6,
    SpacedRecall = 7,
    ContextBoundaryDrill = 8,
    CrossProjectAnalogyReview = 9
}

public enum ReplayJobState
{
    Draft = 0,
    Ready = 1,
    Running = 2,
    Completed = 3,
    Failed = 4,
    NeedsReview = 5,
    Cancelled = 6
}

public enum ProcedureSkillMaturity
{
    Unknown = 0,
    Draft = 1,
    Observed = 2,
    Reviewed = 3,
    Validated = 4,
    Automatable = 5,
    Deprecated = 6
}

public enum SimulationOutputKind
{
    Unknown = 0,
    CandidatePlan = 1,
    RiskAnalysis = 2,
    MissingPreconditions = 3,
    ExpectedOutcome = 4,
    LikelyFailureModes = 5,
    RequiredSourcesOrTests = 6,
    SuggestedProbeOrRegression = 7,
    ProcedureImprovementProposal = 8
}

public enum MetamemoryAnswerDecisionKind
{
    Unknown = 0,
    Answer = 1,
    AnswerWithWarnings = 2,
    AskClarification = 3,
    RequestSourceAudit = 4,
    StartProbe = 5,
    CreateReviewItem = 6,
    RequestLearningProposal = 7,
    Abstain = 8
}

public sealed record CognitiveWorkspaceFrame(
    Guid Id,
    Guid ProjectId,
    WorkspaceFrameKind FrameKind,
    string? UserId,
    string? AgentId,
    Guid? ProcessRunId,
    Guid? WorkflowRunId,
    Guid? ProbeSessionId,
    IReadOnlyList<string> GoalStack,
    IReadOnlyList<WorkingMemorySlot> FocusSlots,
    IReadOnlyList<InhibitedMemoryCandidate> InhibitedCandidates,
    IReadOnlyList<string> OpenQuestions,
    int ContextBudgetTokens,
    ScoreEvaluationTrace CognitiveLoadTrace,
    Guid? LastAttentionDecisionId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? ExpiresAtUtc,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record WorkingMemorySlot(
    Guid Id,
    WorkingMemorySlotKind Kind,
    Guid? MemoryItemId,
    Guid? ClaimId,
    Guid? SourceItemId,
    Guid? ProcedureSkillId,
    string Title,
    string Summary,
    ScoreVectorSnapshot SlotVector,
    ScoreScalarProjection? DisplayAttention,
    ScoreScalarProjection? DisplayConfidence,
    ScoreScalarProjection? DisplayRisk,
    string InclusionReason,
    IReadOnlyList<Guid> EvidenceAnchorIds,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record InhibitedMemoryCandidate(
    Guid CandidateId,
    WorkingMemorySlotKind CandidateKind,
    string Reason,
    ScoreEvaluationTrace InhibitionTrace,
    ScoreScalarProjection? DisplayRelevance,
    ScoreScalarProjection? DisplayInhibitionStrength,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record AttentionRoutingRequest(
    Guid ProjectId,
    string RequestText,
    Guid WorkspaceFrameId,
    RecallIntent? RequestedIntent,
    MemoryAccessContext AccessContext,
    IReadOnlyDictionary<string, string> Options);

public sealed record AttentionRoutingDecision(
    Guid Id,
    Guid ProjectId,
    Guid WorkspaceFrameId,
    AttentionDecisionKind DecisionKind,
    string Explanation,
    IReadOnlyList<string> RequiredNextActions,
    ScoreEvaluationTrace RoutingTrace,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record MemoryEvidenceAnchor(
    Guid Id,
    Guid ProjectId,
    EvidenceAnchorKind AnchorKind,
    Guid? SourceManifestId,
    Guid? SourceItemId,
    string SourceSystem,
    string? Locator,
    string? StructuredPath,
    int? TextStart,
    int? TextEnd,
    string? QuoteHash,
    SourceTrustLevel TrustLevel,
    bool IsRedacted,
    string ContentHash,
    DateTimeOffset ObservedAtUtc,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record MemoryClaim(
    Guid Id,
    Guid ProjectId,
    string ClaimText,
    ClaimKind ClaimKind,
    string? SubjectKey,
    string? PredicateKey,
    string? ObjectKey,
    IReadOnlyList<Guid> ContextFrameIds,
    DateTimeOffset? ValidFromUtc,
    DateTimeOffset? ValidToUtc,
    ScoreVectorSnapshot ConfidenceVector,
    ScoreScalarProjection? DisplayConfidence,
    MemoryValidationState ValidationState,
    MemoryStabilityState StabilityState,
    IReadOnlyList<Guid> SupportingEvidenceAnchorIds,
    IReadOnlyList<Guid> AttackingEvidenceAnchorIds,
    Guid? SupersedesClaimId,
    string AlgorithmVersion,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record MemoryBeliefState(
    Guid Id,
    Guid ClaimId,
    MemoryBeliefStateKind StateKind,
    ScoreVectorSnapshot BeliefVector,
    ScoreEvaluationTrace BeliefEvaluationTrace,
    ScoreScalarProjection? DisplayBelief,
    string Explanation,
    DateTimeOffset CalculatedAtUtc,
    string AlgorithmVersion,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record MemoryMutationCommand(
    Guid Id,
    Guid ProjectId,
    MemoryMutationCommandKind CommandKind,
    CognitiveActorKind ActorKind,
    string ActorId,
    string IdempotencyKey,
    IReadOnlyList<Guid> AffectedMemoryItemIds,
    IReadOnlyList<Guid> AffectedClaimIds,
    IReadOnlyList<Guid> EvidenceAnchorIds,
    string PayloadJson,
    string? ExpectedVersionToken,
    bool RequiresHumanReview,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record MemoryMutationResult(
    Guid CommandId,
    bool Accepted,
    bool Applied,
    bool ReviewRequired,
    string? ReviewReason,
    string? NewVersionToken,
    IReadOnlyList<Guid> CreatedAuditEventIds,
    IReadOnlyList<Guid> InvalidatedProjectionRecordIds,
    IReadOnlyList<string> Warnings);

public sealed record CognitiveSignalRecord(
    Guid Id,
    Guid ProjectId,
    CognitiveSignalKind SignalKind,
    Guid? MemoryItemId,
    Guid? ClaimId,
    Guid? ProcedureSkillId,
    Guid? EpisodeId,
    Guid? ProbeTurnId,
    Guid? WorkflowRunId,
    ScoreVectorSnapshot SignalVector,
    ScoreScalarProjection? DisplayMagnitude,
    string Summary,
    IReadOnlyList<Guid> EvidenceAnchorIds,
    DateTimeOffset ObservedAtUtc,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record PredictionExpectation(
    Guid Id,
    Guid ProjectId,
    PredictionErrorKind ExpectedErrorKind,
    string Summary,
    IReadOnlyList<Guid> ExpectedClaimIds,
    IReadOnlyList<Guid> ExpectedProcedureSkillIds,
    ScoreShapeSnapshot ExpectedScoreEnvelope,
    IReadOnlyList<ContextDimension> ExpectedContext,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record PredictionErrorRecord(
    Guid Id,
    Guid ProjectId,
    Guid? ExpectationId,
    PredictionErrorKind ErrorKind,
    ScoreVectorSnapshot ErrorVector,
    ScoreScalarProjection? DisplaySeverity,
    string ObservationSummary,
    string CauseHypothesis,
    IReadOnlyList<Guid> RelatedMemoryItemIds,
    IReadOnlyList<Guid> RelatedClaimIds,
    IReadOnlyList<Guid> EvidenceAnchorIds,
    IReadOnlyList<Guid> CreatedSignalIds,
    bool RequiresReview,
    DateTimeOffset ObservedAtUtc,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record ContextDimension(
    ContextDimensionKind Kind,
    string Value,
    IReadOnlyList<Guid> EvidenceAnchorIds);

public sealed record ContextFrameRecord(
    Guid Id,
    Guid ProjectId,
    ContextFrameKind FrameKind,
    string DisplayName,
    IReadOnlyList<ContextDimension> Dimensions,
    IReadOnlyList<Guid> SourceEvidenceAnchorIds,
    ScoreVectorSnapshot ConfidenceVector,
    ScoreScalarProjection? DisplayConfidence,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record EntityRegistryRecord(
    Guid Id,
    Guid ProjectId,
    EntityKind EntityKind,
    string CanonicalName,
    IReadOnlyList<string> Aliases,
    IReadOnlyList<Guid> ContextFrameIds,
    IReadOnlyList<Guid> EvidenceAnchorIds,
    ScoreVectorSnapshot ConfidenceVector,
    ScoreScalarProjection? DisplayConfidence,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record TemporalEpisodeRecord(
    Guid Id,
    Guid ProjectId,
    TemporalEpisodeKind EpisodeKind,
    string Goal,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? EndedAtUtc,
    IReadOnlyList<string> Actors,
    IReadOnlyList<Guid> ContextFrameIds,
    IReadOnlyList<Guid> StepIds,
    IReadOnlyList<Guid> PredictionErrorIds,
    IReadOnlyList<Guid> RelatedClaimIds,
    IReadOnlyList<Guid> RelatedProcedureSkillIds,
    string OutcomeSummary,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record EpisodeStepRecord(
    Guid Id,
    Guid EpisodeId,
    int SequenceIndex,
    DateTimeOffset OccurredAtUtc,
    CognitiveActorKind ActorKind,
    string ActorId,
    EpisodeStepActionKind ActionKind,
    string Summary,
    IReadOnlyList<Guid> InputEvidenceAnchorIds,
    IReadOnlyList<Guid> OutputEvidenceAnchorIds,
    bool Succeeded,
    string? ErrorSummary,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record MemoryReplayJobRecord(
    Guid Id,
    Guid ProjectId,
    ReplayJobKind JobKind,
    string Reason,
    ScoreEvaluationTrace PriorityTrace,
    ScoreScalarProjection? DisplayPriority,
    IReadOnlyList<Guid> TargetMemoryItemIds,
    IReadOnlyList<Guid> TargetClaimIds,
    IReadOnlyList<Guid> TargetProcedureSkillIds,
    IReadOnlyList<Guid> TriggerSignalIds,
    ReplayJobState State,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ScheduledAtUtc,
    DateTimeOffset? CompletedAtUtc,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record ProcedureSkillRecord(
    Guid Id,
    Guid ProjectId,
    string Title,
    string Purpose,
    IReadOnlyList<Guid> ContextFrameIds,
    IReadOnlyList<string> Preconditions,
    IReadOnlyList<ProcedureStepRecord> Steps,
    IReadOnlyList<string> Postconditions,
    IReadOnlyList<ProcedureFailureModeRecord> FailureModes,
    IReadOnlyList<Guid> ValidationEvidenceAnchorIds,
    Guid? LastSuccessfulEpisodeId,
    ProcedureSkillMaturity Maturity,
    ScoreVectorSnapshot SkillVector,
    ScoreScalarProjection? DisplayRisk,
    string? AutomationBindingKey,
    MemoryValidationState ValidationState,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record ProcedureStepRecord(
    string StepKey,
    int Order,
    string Action,
    string? ToolBindingKey,
    string ExpectedOutput,
    string ValidationCheck,
    string FailureHandling,
    IReadOnlyList<Guid> EvidenceAnchorIds,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record ProcedureFailureModeRecord(
    string FailureKey,
    string Condition,
    string LikelyCause,
    string Mitigation,
    string RollbackOrCompensation,
    IReadOnlyList<Guid> RelatedPredictionErrorIds,
    ScoreVectorSnapshot ConfidenceVector,
    ScoreScalarProjection? DisplayConfidence,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record ProcedureSimulationRecord(
    Guid Id,
    Guid ProjectId,
    SimulationOutputKind OutputKind,
    string Summary,
    IReadOnlyList<Guid> RelatedProcedureSkillIds,
    IReadOnlyList<Guid> EvidenceAnchorIds,
    IReadOnlyList<string> RequiredValidationSteps,
    bool IsSpeculative,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record MetamemoryAnswerGateRequest(
    Guid ProjectId,
    Guid WorkspaceFrameId,
    RecallResult RecallResult,
    IReadOnlyList<Guid> SelectedClaimIds,
    MemoryAccessContext AccessContext,
    IReadOnlyDictionary<string, string> Options);

public sealed record MetamemoryAnswerGateDecision(
    Guid Id,
    Guid ProjectId,
    Guid WorkspaceFrameId,
    MetamemoryAnswerDecisionKind DecisionKind,
    string Explanation,
    ScoreEvaluationTrace AnswerGateTrace,
    ScoreScalarProjection? DisplayConfidence,
    bool HasSourceSufficiency,
    bool HasContextAmbiguity,
    bool HasContradictionRisk,
    bool HasStalenessRisk,
    bool HasRedactionLimit,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> RequiredNextActions,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyDictionary<string, string> Metadata);

public interface ICognitiveWorkspaceService
{
    Task<CognitiveWorkspaceFrame> GetOrCreateAsync(
        Guid projectId,
        WorkspaceFrameKind frameKind,
        MemoryAccessContext accessContext,
        IReadOnlyDictionary<string, string> options,
        CancellationToken cancellationToken = default);

    Task<CognitiveWorkspaceFrame> UpdateFocusAsync(
        Guid frameId,
        IReadOnlyList<WorkingMemorySlot> focusSlots,
        IReadOnlyList<InhibitedMemoryCandidate> inhibitedCandidates,
        CancellationToken cancellationToken = default);
}

public interface IAttentionRouter
{
    Task<AttentionRoutingDecision> RouteAsync(
        AttentionRoutingRequest request,
        CancellationToken cancellationToken = default);
}

public interface IClaimEvidenceLedger
{
    Task<MemoryClaim> ProposeClaimAsync(
        MemoryClaim claim,
        CancellationToken cancellationToken = default);

    Task<MemoryBeliefState> CalculateBeliefStateAsync(
        Guid claimId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MemoryClaim>> GetClaimsForMemoryItemAsync(
        Guid memoryItemId,
        CancellationToken cancellationToken = default);
}

public interface IMemoryMutationAuthority
{
    Task<MemoryMutationResult> SubmitAsync(
        MemoryMutationCommand command,
        CancellationToken cancellationToken = default);
}

public interface ICognitiveSignalLedger
{
    Task<IReadOnlyList<CognitiveSignalRecord>> PublishAsync(
        IReadOnlyList<CognitiveSignalRecord> signals,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CognitiveSignalRecord>> QueryAsync(
        Guid projectId,
        IReadOnlyList<CognitiveSignalKind> kinds,
        DateTimeOffset? sinceUtc,
        CancellationToken cancellationToken = default);
}

public interface IPredictionErrorEngine
{
    Task<PredictionErrorRecord> ObserveAsync(
        PredictionExpectation? expectation,
        string observationSummary,
        PredictionErrorKind errorKind,
        IReadOnlyList<Guid> evidenceAnchorIds,
        CancellationToken cancellationToken = default);
}

public interface IEntityContextBindingService
{
    Task<IReadOnlyList<EntityRegistryRecord>> ResolveEntitiesAsync(
        Guid projectId,
        string text,
        IReadOnlyList<Guid> evidenceAnchorIds,
        CancellationToken cancellationToken = default);

    Task<ContextFrameRecord> BuildContextFrameAsync(
        Guid projectId,
        IReadOnlyList<ContextDimension> dimensions,
        IReadOnlyList<Guid> evidenceAnchorIds,
        CancellationToken cancellationToken = default);
}

public interface ITemporalEpisodeService
{
    Task<TemporalEpisodeRecord> CreateEpisodeAsync(
        TemporalEpisodeRecord episode,
        CancellationToken cancellationToken = default);

    Task<EpisodeStepRecord> AppendStepAsync(
        EpisodeStepRecord step,
        CancellationToken cancellationToken = default);
}

public interface IReplayScheduler
{
    Task<IReadOnlyList<MemoryReplayJobRecord>> PlanReplayJobsAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task<MemoryReplayJobRecord> EnqueueAsync(
        MemoryReplayJobRecord job,
        CancellationToken cancellationToken = default);
}

public interface IProcedureSkillMemoryService
{
    Task<ProcedureSkillRecord> ProposeSkillAsync(
        ProcedureSkillRecord skill,
        CancellationToken cancellationToken = default);

    Task<ProcedureSkillRecord> UpdateMaturityAsync(
        Guid skillId,
        ProcedureSkillMaturity maturity,
        IReadOnlyList<Guid> validationEvidenceAnchorIds,
        CancellationToken cancellationToken = default);
}

public interface ISimulationSandboxService
{
    Task<ProcedureSimulationRecord> SimulateAsync(
        Guid projectId,
        IReadOnlyList<Guid> relatedProcedureSkillIds,
        IReadOnlyDictionary<string, string> options,
        CancellationToken cancellationToken = default);
}

public interface IMetamemoryAnswerGate
{
    Task<MetamemoryAnswerGateDecision> EvaluateAsync(
        MetamemoryAnswerGateRequest request,
        CancellationToken cancellationToken = default);
}
