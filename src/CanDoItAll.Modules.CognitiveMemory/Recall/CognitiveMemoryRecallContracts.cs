using System.Text.Json.Serialization;

namespace CanDoItAll.Modules.CognitiveMemory;

public enum CognitiveMemoryRecallMode
{
    Unknown = 0,
    QuickAssociative = 1,
    FocusedTaskContext = 2,
    DeepSourceGrounded = 3,
    CrossProjectAnalogy = 4,
    ProcedureLookup = 5,
    DecisionLookup = 6,
    IncidentLearning = 7
}

public enum CognitiveMemoryRecallIntentKind
{
    Unknown = 0,
    Architecture = 1,
    Implementation = 2,
    Procedure = 3,
    DecisionHistory = 4,
    Debugging = 5,
    Testing = 6,
    Deployment = 7,
    CrossProjectAnalogy = 8,
    SourceLookup = 9
}

public enum CognitiveMemoryRecallTraceStageKind
{
    Unknown = 0,
    IntentAndScope = 1,
    CoarseCandidateActivation = 2,
    AssociationExpansion = 3,
    FocusSelection = 4,
    DetailRetrieval = 5,
    ContextPackRendering = 6
}

public enum CognitiveMemoryRecallChannelKind
{
    Unknown = 0,
    Lexical = 1,
    VectorProjection = 2,
    Graph = 3,
    Workspace = 4,
    SourceDetail = 5,
    SignalActivation = 6,
    ContextPack = 7
}

public enum CognitiveMemoryRecallStageStatus
{
    NotStarted = 0,
    Completed = 1,
    Skipped = 2,
    Unavailable = 3,
    Failed = 4
}

public enum CognitiveMemoryRecallCandidateDecisionKind
{
    Unknown = 0,
    Selected = 1,
    SideContext = 2,
    Excluded = 3,
    Inhibited = 4
}

public enum CognitiveMemoryRecallExclusionReasonKind
{
    None = 0,
    BudgetLimit = 1,
    AccessPolicy = 2,
    ContextBoundary = 3,
    SourceInsufficient = 4,
    RedactedSource = 5,
    Stale = 6,
    ContradictionRisk = 7,
    ProjectionUnavailable = 8,
    EmbeddingUnavailable = 9,
    ScoreGeometryRejected = 10,
    NotInFocus = 11
}

public enum CognitiveMemoryRecallContextSectionKind
{
    Unknown = 0,
    SelectedMemory = 1,
    SideContext = 2,
    SourceReference = 3,
    OpenQuestion = 4,
    Warning = 5,
    DoNotConfuseWith = 6
}

public readonly record struct CognitiveMemoryRecallContextPackId
{
    [JsonConstructor]
    public CognitiveMemoryRecallContextPackId(Guid value)
    {
        Value = CognitiveMemoryGuard.EnsureNonEmpty(value, nameof(value));
    }

    public Guid Value { get; }

    public static CognitiveMemoryRecallContextPackId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public readonly record struct CognitiveMemoryRecallCandidateId
{
    [JsonConstructor]
    public CognitiveMemoryRecallCandidateId(Guid value)
    {
        Value = CognitiveMemoryGuard.EnsureNonEmpty(value, nameof(value));
    }

    public Guid Value { get; }

    public static CognitiveMemoryRecallCandidateId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public sealed record CognitiveMemoryRecallBudget
{
    public CognitiveMemoryRecallBudget(
        int coarseCandidateLimit,
        int graphExpansionDepth,
        int vectorResultLimit,
        int focusLimit,
        int detailItemLimit,
        int contextCharacterBudget,
        int maxSourceBytes)
    {
        if (coarseCandidateLimit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(coarseCandidateLimit), "Coarse candidate limit must be positive.");
        }

        if (graphExpansionDepth < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(graphExpansionDepth), "Graph expansion depth must not be negative.");
        }

        if (vectorResultLimit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(vectorResultLimit), "Vector result limit must be positive.");
        }

        if (focusLimit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(focusLimit), "Focus limit must be positive.");
        }

        if (detailItemLimit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(detailItemLimit), "Detail item limit must be positive.");
        }

        if (contextCharacterBudget <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(contextCharacterBudget), "Context character budget must be positive.");
        }

        if (maxSourceBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxSourceBytes), "Source byte budget must be positive.");
        }

        CoarseCandidateLimit = coarseCandidateLimit;
        GraphExpansionDepth = graphExpansionDepth;
        VectorResultLimit = vectorResultLimit;
        FocusLimit = focusLimit;
        DetailItemLimit = detailItemLimit;
        ContextCharacterBudget = contextCharacterBudget;
        MaxSourceBytes = maxSourceBytes;
    }

    public int CoarseCandidateLimit { get; }

    public int GraphExpansionDepth { get; }

    public int VectorResultLimit { get; }

    public int FocusLimit { get; }

    public int DetailItemLimit { get; }

    public int ContextCharacterBudget { get; }

    public int MaxSourceBytes { get; }
}

public sealed record CognitiveMemoryRecallRequest(
    Guid ProjectId,
    string Query,
    CognitiveMemoryRecallIntentKind Intent,
    CognitiveMemoryRecallMode Mode,
    CognitiveMemoryPolicyContext PolicyContext,
    CognitiveMemoryRecallBudget Budget,
    CognitiveMemoryWorkspaceFrameId? WorkspaceFrameId = null,
    CognitiveMemoryWorkspaceOpenRequest? WorkspaceOpenRequest = null,
    CognitiveMemoryAttentionDecisionId? AttentionDecisionId = null,
    Guid? SelfRegulationAssessmentId = null,
    Guid? AnswerPostureDecisionId = null,
    Guid? AnswerGateDecisionId = null,
    IReadOnlyList<CognitiveMemoryRecordKind>? PreferredRecordKinds = null,
    CognitiveMemoryProjectionCollectionName? ProjectionCollectionName = null,
    CognitiveMemoryProjectionProfileId? ProjectionProfileId = null,
    CognitiveMemoryEmbeddingProfileId? EmbeddingProfileId = null,
    IReadOnlyDictionary<string, string>? Metadata = null);

public sealed record CognitiveMemoryRecallCandidate(
    CognitiveMemoryRecallCandidateId Id,
    CognitiveMemoryRecordId MemoryRecordId,
    CognitiveMemoryRecordKind MemoryKind,
    string Title,
    CognitiveMemoryRecallChannelKind PrimaryChannelKind,
    CognitiveMemoryRecallCandidateDecisionKind DecisionKind,
    CognitiveMemoryRecallExclusionReasonKind ExclusionReasonKind,
    CognitiveMemoryScoreEvaluationTrace ScoreTrace,
    CognitiveMemoryScoreScalarProjection? DisplayRankProjection,
    IReadOnlyList<CognitiveMemoryClaimId> SelectedClaimIds,
    IReadOnlyList<CognitiveMemoryEvidenceAnchorId> EvidenceAnchorIds,
    string Reason);

public sealed record CognitiveMemoryRecallSourceRef(
    CognitiveMemoryRecordId MemoryRecordId,
    CognitiveMemorySourceItemId? SourceItemId,
    CognitiveMemoryEvidenceAnchorId? EvidenceAnchorId,
    string SourceSystem,
    string Locator,
    string Summary,
    CognitiveMemoryAccessLevel AccessLevel,
    CognitiveMemoryRedactionState RedactionState,
    bool IncludedInContext,
    CognitiveMemoryRecallExclusionReasonKind ExclusionReasonKind);

public sealed record CognitiveMemoryRecallContextSection(
    CognitiveMemorySectionId SectionId,
    CognitiveMemoryRecallContextSectionKind SectionKind,
    string Title,
    string Content,
    IReadOnlyList<CognitiveMemoryRecordId> MemoryRecordIds,
    IReadOnlyList<CognitiveMemoryClaimId> ClaimIds,
    IReadOnlyList<CognitiveMemoryRecallSourceRef> SourceRefs);

public sealed record CognitiveMemoryRecallContextPack(
    CognitiveMemoryRecallContextPackId Id,
    Guid ProjectId,
    CognitiveMemoryWorkspaceFrameId? WorkspaceFrameId,
    string Title,
    string Summary,
    IReadOnlyList<CognitiveMemoryRecallContextSection> Sections,
    IReadOnlyList<CognitiveMemoryRecallSourceRef> SourceRefs,
    IReadOnlyList<string> Warnings,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record CognitiveMemoryRecallTraceStage(
    CognitiveMemoryRecallTraceStageKind StageKind,
    CognitiveMemoryRecallChannelKind ChannelKind,
    CognitiveMemoryRecallStageStatus Status,
    int CandidateCount,
    int SelectedCount,
    int ExcludedCount,
    CognitiveMemoryBudgetLimit? LimitingBudget,
    string ProviderTrace,
    string FailureCode,
    string FailureMessage,
    DateTimeOffset CompletedAtUtc);

public sealed record CognitiveMemoryRecallTracePayload(
    CognitiveMemoryRecallMode Mode,
    CognitiveMemoryRecallIntentKind Intent,
    IReadOnlyList<CognitiveMemoryRecallTraceStage> Stages,
    IReadOnlyList<string> Warnings,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record CognitiveMemoryRecallResult(
    Guid TraceId,
    CognitiveMemoryRecallContextPack ContextPack,
    IReadOnlyList<CognitiveMemoryRecallCandidate> Candidates,
    IReadOnlyList<CognitiveMemoryRecallTraceStage> Stages,
    IReadOnlyList<string> Warnings);

public interface ICognitiveMemoryRecallOrchestrator
{
    ValueTask<CognitiveMemoryRecallResult> RecallAsync(
        CognitiveMemoryRecallRequest request,
        CancellationToken cancellationToken = default);
}
