namespace CanDoItAll.Modules.CognitiveMemory;

public enum CognitiveMemoryConsolidationMode
{
    IncrementalRecent = 0,
    ProjectNightly = 1,
    CrossProjectWeekly = 2,
    ProjectionRebuild = 3,
    ContradictionReview = 4,
    ProcedureMining = 5,
    FailureLearning = 6,
    KnowledgeCoverageRefresh = 7,
    EpistemicDriveScan = 8,
    LearningOpportunityReview = 9
}

public enum CognitiveMemoryConsolidationTriggerKind
{
    Manual = 0,
    Idle = 1,
    Nightly = 2,
    WorkflowCompleted = 3,
    ProcessCompleted = 4,
    SourceChanged = 5,
    DistributedWorkerReturned = 6
}

public enum CognitiveMemoryConsolidationCandidateKind
{
    Episode = 0,
    Procedure = 1,
    Decision = 2,
    Reflection = 3,
    Contradiction = 4,
    ProjectionInvalidation = 5,
    ReviewRequired = 6,
    Knowledge = 7
}

public enum CognitiveMemoryConsolidationCandidateStatus
{
    Draft = 0,
    MutationSubmitted = 1,
    ReviewRequired = 2,
    Rejected = 3,
    SkippedDuplicate = 4
}

public readonly record struct CognitiveMemoryConsolidationRunId
{
    public CognitiveMemoryConsolidationRunId(Guid value)
    {
        Value = CognitiveMemoryGuard.EnsureNonEmpty(value, nameof(value));
    }

    public Guid Value { get; }

    public static CognitiveMemoryConsolidationRunId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public readonly record struct CognitiveMemoryConsolidationCandidateId
{
    public CognitiveMemoryConsolidationCandidateId(Guid value)
    {
        Value = CognitiveMemoryGuard.EnsureNonEmpty(value, nameof(value));
    }

    public Guid Value { get; }

    public static CognitiveMemoryConsolidationCandidateId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public sealed record CognitiveMemoryConsolidationBudget
{
    public CognitiveMemoryConsolidationBudget(
        int sourceItemLimit,
        int candidateLimit,
        int reviewItemLimit,
        int maxSourceCharacters,
        TimeSpan leaseDuration)
    {
        if (sourceItemLimit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceItemLimit), "Source item limit must be positive.");
        }

        if (candidateLimit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(candidateLimit), "Candidate limit must be positive.");
        }

        if (reviewItemLimit < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(reviewItemLimit), "Review item limit must not be negative.");
        }

        if (maxSourceCharacters <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxSourceCharacters), "Source character budget must be positive.");
        }

        if (leaseDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(leaseDuration), "Lease duration must be positive.");
        }

        SourceItemLimit = sourceItemLimit;
        CandidateLimit = candidateLimit;
        ReviewItemLimit = reviewItemLimit;
        MaxSourceCharacters = maxSourceCharacters;
        LeaseDuration = leaseDuration;
    }

    public int SourceItemLimit { get; }

    public int CandidateLimit { get; }

    public int ReviewItemLimit { get; }

    public int MaxSourceCharacters { get; }

    public TimeSpan LeaseDuration { get; }

    public static CognitiveMemoryConsolidationBudget Default { get; } = new(
        sourceItemLimit: 50,
        candidateLimit: 50,
        reviewItemLimit: 25,
        maxSourceCharacters: 24_000,
        leaseDuration: TimeSpan.FromMinutes(15));
}

public sealed record CognitiveMemoryConsolidationProfile(
    string Name,
    bool ProcessSourceItems,
    bool DetectContradictions,
    bool ExtractProcedures,
    bool RebuildProjections,
    bool CreateHumanReviewItems,
    int MaxItems)
{
    public static CognitiveMemoryConsolidationProfile IncrementalRecent { get; } = new(
        "incremental-recent",
        ProcessSourceItems: true,
        DetectContradictions: true,
        ExtractProcedures: false,
        RebuildProjections: true,
        CreateHumanReviewItems: true,
        MaxItems: 50);
}

public sealed record CognitiveMemoryConsolidationRunRequest(
    Guid? ProjectId,
    CognitiveMemoryConsolidationMode Mode,
    CognitiveMemoryConsolidationTriggerKind TriggerKind,
    CognitiveMemoryConsolidationProfile Profile,
    CognitiveMemoryPolicyContext PolicyContext,
    CognitiveMemoryIdempotencyKey IdempotencyKey,
    CognitiveMemoryConsolidationBudget? Budget = null,
    string? Cursor = null,
    IReadOnlyDictionary<string, string>? Options = null);

public sealed record CognitiveMemoryConsolidationRunResult(
    CognitiveMemoryConsolidationRunId RunId,
    CognitiveMemoryRunStatus Status,
    int SourceItemsScanned,
    int CandidatesCreated,
    int MutationCommandsSubmitted,
    int ReviewItemsCreated,
    int ProjectionInvalidations,
    string? NextCursor,
    string? ReportHash,
    IReadOnlyList<string> Warnings);

public sealed record CognitiveMemoryConsolidationCandidatePayload(
    CognitiveMemoryConsolidationCandidateKind CandidateKind,
    Guid? SourceItemId,
    Guid? EvidenceAnchorId,
    Guid? MutationCommandId,
    Guid? ReviewItemId,
    string SourceSystem,
    string SourceItemType,
    string Title,
    string Summary,
    string SourceContentHash,
    string Reason);

public sealed record CognitiveMemoryConsolidationReportPayload(
    Guid RunId,
    Guid? ProjectId,
    CognitiveMemoryConsolidationMode Mode,
    CognitiveMemoryConsolidationTriggerKind TriggerKind,
    string ProfileName,
    int SourceItemsScanned,
    int CandidatesCreated,
    int MutationCommandsSubmitted,
    int ReviewItemsCreated,
    int ProjectionInvalidations,
    IReadOnlyList<string> Warnings);

public interface ICognitiveMemoryConsolidationEngine
{
    ValueTask<CognitiveMemoryConsolidationRunResult> RunAsync(
        CognitiveMemoryConsolidationRunRequest request,
        CancellationToken cancellationToken = default);
}
