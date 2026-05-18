using System.Text.Json.Serialization;

namespace CanDoItAll.Modules.CognitiveMemory;

public enum CognitiveMemoryTemporalEpisodeKind
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

public enum CognitiveMemoryEpisodeStepActionKind
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

public enum CognitiveMemoryTemporalEpisodeLinkKind
{
    Unknown = 0,
    ContextFrame = 1,
    PredictionError = 2,
    Claim = 3,
    ProcedureSkill = 4,
    Decision = 5,
    Artifact = 6,
    SourceItem = 7,
    EvidenceAnchor = 8,
    MemoryRecord = 9,
    ReviewItem = 10,
    ConsolidationCandidate = 11,
    WorkflowRun = 12,
    ProcessRun = 13,
    ReplayJob = 14
}

public enum CognitiveMemoryEpisodeStepEvidenceRole
{
    Unknown = 0,
    Input = 1,
    Output = 2,
    Supporting = 3,
    Result = 4
}

public enum CognitiveMemoryEpisodeCausalLinkKind
{
    Unknown = 0,
    StepCausedStep = 1,
    DecisionLedToArtifact = 2,
    FailureCausedRework = 3,
    SourceSupersededClaim = 4,
    ProbeCorrectionAttackedClaim = 5,
    WorkflowSuccessReinforcedProcedure = 6,
    EvidenceQualifiedClaim = 7
}

public enum CognitiveMemoryReplayJobKind
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

public enum CognitiveMemoryReplayJobState
{
    Draft = 0,
    Ready = 1,
    Running = 2,
    Completed = 3,
    Failed = 4,
    NeedsReview = 5,
    Cancelled = 6
}

public enum CognitiveMemoryReplayJobTargetKind
{
    Unknown = 0,
    MemoryRecord = 1,
    Claim = 2,
    ProcedureSkill = 3,
    PredictionError = 4,
    CognitiveSignal = 5,
    SourceItem = 6,
    EvidenceAnchor = 7,
    TemporalEpisode = 8,
    ProbeRegression = 9,
    Projection = 10
}

public enum CognitiveMemoryReplayOutputKind
{
    Unknown = 0,
    DraftClaimUpdate = 1,
    ReviewItem = 2,
    ProjectionInvalidationRequest = 3,
    RegressionResult = 4,
    LearningProposalCandidate = 5,
    ActivationSignal = 6
}

public enum CognitiveMemoryReplayOutputStatus
{
    Draft = 0,
    NeedsReview = 1,
    Rejected = 2
}

public enum CognitiveMemoryReplayWorkerResultStatus
{
    Submitted = 0,
    Accepted = 1,
    Rejected = 2
}

public readonly record struct CognitiveMemoryTemporalEpisodeId
{
    [JsonConstructor]
    public CognitiveMemoryTemporalEpisodeId(Guid value)
    {
        Value = CognitiveMemoryGuard.EnsureNonEmpty(value, nameof(value));
    }

    public Guid Value { get; }

    public static CognitiveMemoryTemporalEpisodeId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public readonly record struct CognitiveMemoryEpisodeStepId
{
    [JsonConstructor]
    public CognitiveMemoryEpisodeStepId(Guid value)
    {
        Value = CognitiveMemoryGuard.EnsureNonEmpty(value, nameof(value));
    }

    public Guid Value { get; }

    public static CognitiveMemoryEpisodeStepId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public readonly record struct CognitiveMemoryReplayJobId
{
    [JsonConstructor]
    public CognitiveMemoryReplayJobId(Guid value)
    {
        Value = CognitiveMemoryGuard.EnsureNonEmpty(value, nameof(value));
    }

    public Guid Value { get; }

    public static CognitiveMemoryReplayJobId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public sealed record CognitiveMemoryTemporalEpisodeLinkDraft(
    CognitiveMemoryTemporalEpisodeLinkKind LinkKind,
    Guid? TargetId,
    string TargetKey,
    string Summary);

public sealed record CognitiveMemoryTemporalEpisodeCreateRequest(
    Guid ProjectId,
    CognitiveMemoryTemporalEpisodeKind EpisodeKind,
    string Goal,
    string ExpectedOutcome,
    string ActualOutcome,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? EndedAtUtc = null,
    IReadOnlyList<CognitiveMemoryTemporalEpisodeLinkDraft>? Links = null,
    IReadOnlyDictionary<string, string>? Metadata = null);

public sealed record CognitiveMemoryEpisodeStepAppendRequest(
    CognitiveMemoryTemporalEpisodeId EpisodeId,
    int? SequenceIndex,
    DateTimeOffset OccurredAtUtc,
    CognitiveMemoryActorKind ActorKind,
    string ActorId,
    CognitiveMemoryEpisodeStepActionKind ActionKind,
    string Summary,
    IReadOnlyList<CognitiveMemoryEvidenceAnchorId>? InputEvidenceAnchorIds = null,
    IReadOnlyList<CognitiveMemoryEvidenceAnchorId>? OutputEvidenceAnchorIds = null,
    bool Succeeded = true,
    string ErrorCode = "",
    string ErrorSummary = "",
    string ToolOrPluginKey = "",
    IReadOnlyDictionary<string, string>? Metadata = null);

public sealed record CognitiveMemoryEpisodeCausalLinkRequest(
    CognitiveMemoryTemporalEpisodeId EpisodeId,
    CognitiveMemoryEpisodeCausalLinkKind LinkKind,
    CognitiveMemoryEpisodeStepId? FromStepId,
    CognitiveMemoryEpisodeStepId? ToStepId,
    string Summary,
    CognitiveMemoryEvidenceAnchorId? EvidenceAnchorId = null,
    CognitiveMemoryClaimId? ClaimId = null,
    CognitiveMemoryPredictionErrorId? PredictionErrorId = null,
    Guid? ProcedureSkillId = null);

public sealed record CognitiveMemoryReplayJobTargetDraft(
    CognitiveMemoryReplayJobTargetKind TargetKind,
    Guid? TargetId,
    string TargetKey,
    string RequiredInputHash,
    string Summary);

public sealed record CognitiveMemoryReplayEnqueueRequest(
    Guid ProjectId,
    CognitiveMemoryReplayJobKind JobKind,
    string Reason,
    CognitiveMemoryPolicyContext PolicyContext,
    IReadOnlyList<CognitiveMemoryReplayJobTargetDraft>? Targets = null,
    IReadOnlyList<CognitiveMemorySignalId>? TriggerSignalIds = null,
    IReadOnlyList<CognitiveMemoryPredictionErrorId>? PredictionErrorIds = null,
    DateTimeOffset? ScheduledAtUtc = null,
    string SourceScopeKey = "",
    string ExpectedOutputSchema = "CanDoItAll.CognitiveMemory.ReplayOutput/1.0",
    IReadOnlyDictionary<string, string>? Metadata = null);

public sealed record CognitiveMemoryReplayPlanRequest(
    Guid ProjectId,
    CognitiveMemoryPolicyContext PolicyContext,
    CognitiveMemoryPageRequest Page,
    DateTimeOffset? SinceUtc = null);

public sealed record CognitiveMemoryReplayPlanResult(
    IReadOnlyList<CognitiveMemoryReplayJobRecord> Jobs);

public sealed record CognitiveMemoryReplayOutputRequest(
    CognitiveMemoryReplayJobId ReplayJobId,
    CognitiveMemoryReplayOutputKind OutputKind,
    string Summary,
    string PayloadJson,
    CognitiveMemoryReviewItemId? ReviewItemId = null,
    CognitiveMemoryMutationCommandId? MutationCommandId = null,
    Guid? ProjectionId = null);

public sealed record CognitiveMemoryReplayWorkerResultSubmission(
    CognitiveMemoryReplayJobId ReplayJobId,
    string WorkerId,
    string InputHash,
    string OutputHash,
    string AlgorithmVersion,
    string SourceScopeKey,
    string PolicyProfileId,
    string OutputSchema,
    string ResultStorageReference,
    IReadOnlyList<string>? Warnings = null);

public sealed record CognitiveMemoryReplayWorkerResultValidation(
    CognitiveMemoryReplayWorkerResultRecord Result,
    bool Accepted,
    string? RejectionReason);

public interface ICognitiveMemoryTemporalEpisodeService
{
    ValueTask<CognitiveMemoryTemporalEpisodeRecord> CreateEpisodeAsync(
        CognitiveMemoryTemporalEpisodeCreateRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<CognitiveMemoryEpisodeStepRecord> AppendStepAsync(
        CognitiveMemoryEpisodeStepAppendRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<CognitiveMemoryEpisodeCausalLinkRecord> AddCausalLinkAsync(
        CognitiveMemoryEpisodeCausalLinkRequest request,
        CancellationToken cancellationToken = default);
}

public interface ICognitiveMemoryReplayScheduler
{
    ValueTask<CognitiveMemoryReplayPlanResult> PlanReplayJobsAsync(
        CognitiveMemoryReplayPlanRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<CognitiveMemoryReplayJobRecord> EnqueueAsync(
        CognitiveMemoryReplayEnqueueRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<CognitiveMemoryReplayOutputRecord> RecordOutputAsync(
        CognitiveMemoryReplayOutputRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<CognitiveMemoryReplayWorkerResultValidation> SubmitWorkerResultAsync(
        CognitiveMemoryReplayWorkerResultSubmission submission,
        CancellationToken cancellationToken = default);
}
