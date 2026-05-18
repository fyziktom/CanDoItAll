using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CanDoItAll.CognitiveMemory.Abstractions;

/// <summary>
/// Neuro-cognitive patch contracts for architecture planning.
/// These contracts are sketches and should be split into focused files during implementation.
/// </summary>
public static class NeuroCognitivePatchContractMarker
{
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
    string FrameKind,
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
    double CognitiveLoad,
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
    double AttentionWeight,
    double Confidence,
    double RiskLevel,
    string InclusionReason,
    IReadOnlyList<Guid> EvidenceAnchorIds,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record InhibitedMemoryCandidate(
    Guid CandidateId,
    string CandidateKind,
    string Reason,
    double RelevanceScore,
    double InhibitionStrength,
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
    IReadOnlyDictionary<string, double> ScoreBreakdown,
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
    string ClaimKind,
    string? SubjectKey,
    string? PredicateKey,
    string? ObjectKey,
    IReadOnlyList<Guid> ContextFrameIds,
    DateTimeOffset? ValidFromUtc,
    DateTimeOffset? ValidToUtc,
    double Confidence,
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
    double SupportScore,
    double AttackScore,
    double SourceQualityScore,
    double ContextFitScore,
    double StalenessScore,
    string Explanation,
    DateTimeOffset CalculatedAtUtc,
    string AlgorithmVersion,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record MemoryMutationCommand(
    Guid Id,
    Guid ProjectId,
    string CommandKind,
    string ActorKind,
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
    double Magnitude,
    double Confidence,
    string Summary,
    IReadOnlyList<Guid> EvidenceAnchorIds,
    DateTimeOffset ObservedAtUtc,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record PredictionExpectation(
    Guid Id,
    Guid ProjectId,
    string ExpectationKind,
    string Summary,
    IReadOnlyList<Guid> ExpectedClaimIds,
    IReadOnlyList<Guid> ExpectedProcedureSkillIds,
    double ExpectedConfidenceMin,
    double ExpectedConfidenceMax,
    IReadOnlyDictionary<string, string> ExpectedContext,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record PredictionErrorRecord(
    Guid Id,
    Guid ProjectId,
    Guid? ExpectationId,
    PredictionErrorKind ErrorKind,
    double Magnitude,
    double Confidence,
    string ObservationSummary,
    string CauseHypothesis,
    IReadOnlyList<Guid> RelatedMemoryItemIds,
    IReadOnlyList<Guid> RelatedClaimIds,
    IReadOnlyList<Guid> EvidenceAnchorIds,
    IReadOnlyList<Guid> CreatedSignalIds,
    bool RequiresReview,
    DateTimeOffset ObservedAtUtc,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record ContextFrameRecord(
    Guid Id,
    Guid ProjectId,
    string FrameKind,
    string DisplayName,
    IReadOnlyDictionary<string, string> Dimensions,
    IReadOnlyList<Guid> SourceEvidenceAnchorIds,
    double Confidence,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record EntityRegistryRecord(
    Guid Id,
    Guid ProjectId,
    string EntityType,
    string CanonicalName,
    IReadOnlyList<string> Aliases,
    IReadOnlyList<Guid> ContextFrameIds,
    IReadOnlyList<Guid> EvidenceAnchorIds,
    double Confidence,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record TemporalEpisodeRecord(
    Guid Id,
    Guid ProjectId,
    string EpisodeKind,
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
    string ActorKind,
    string ActorId,
    string ActionKind,
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
    double Priority,
    IReadOnlyList<Guid> TargetMemoryItemIds,
    IReadOnlyList<Guid> TargetClaimIds,
    IReadOnlyList<Guid> TargetProcedureSkillIds,
    IReadOnlyList<Guid> TriggerSignalIds,
    string Status,
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
    double RiskLevel,
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
    double Confidence,
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
    double DisplayConfidence,
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
        string frameKind,
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
        IReadOnlyDictionary<string, string> dimensions,
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

public interface IMetamemoryAnswerGate
{
    Task<MetamemoryAnswerGateDecision> EvaluateAsync(
        MetamemoryAnswerGateRequest request,
        CancellationToken cancellationToken = default);
}
