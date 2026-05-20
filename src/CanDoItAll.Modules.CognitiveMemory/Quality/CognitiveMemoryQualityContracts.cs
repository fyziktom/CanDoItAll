using System.Text.Json.Serialization;

namespace CanDoItAll.Modules.CognitiveMemory;

public enum CognitiveMemoryQualityClusterKeyFamily
{
    ProjectScope = 0,
    SourceTopology = 1,
    SemanticTopic = 2,
    Entity = 3,
    TaskIntent = 4,
    Temporal = 5,
    EvidenceOverlap = 6,
    Relation = 7,
    AccessRisk = 8
}

public enum CognitiveMemoryQualityClusterMemberKind
{
    MemoryRecord = 0,
    SourceItem = 1
}

public enum CognitiveMemoryQualityClusterReadiness
{
    Unknown = 0,
    AggregateReady = 1,
    NeedsMoreEvidence = 2,
    NeedsHumanReview = 3,
    Contradictory = 4,
    Restricted = 5
}

public enum CognitiveMemoryDreamAggregateCandidateStatus
{
    Proposed = 0,
    Approved = 1,
    NeedsHumanReview = 2,
    Rejected = 3,
    Applied = 4
}

public enum CognitiveMemoryDreamValidationDecision
{
    Approved = 0,
    NeedsHumanReview = 1,
    Rejected = 2
}

public enum CognitiveMemoryDreamValidationIssueKind
{
    MissingSourceMap = 0,
    WeakEvidence = 1,
    Contradiction = 2,
    StaleOrSuperseded = 3,
    RestrictedContent = 4,
    RedactedSource = 5,
    AccessPolicy = 6,
    GeneratedTextLeakage = 7,
    OverbroadCluster = 8,
    LowCohesion = 9,
    WeakSourceIndependence = 10,
    DuplicateAggregate = 11,
    UnsupportedClaim = 12
}

public readonly record struct CognitiveMemoryQualityClusterId
{
    [JsonConstructor]
    public CognitiveMemoryQualityClusterId(Guid value)
    {
        Value = CognitiveMemoryGuard.EnsureNonEmpty(value, nameof(value));
    }

    public Guid Value { get; }

    public static CognitiveMemoryQualityClusterId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public readonly record struct CognitiveMemoryDreamRunId
{
    [JsonConstructor]
    public CognitiveMemoryDreamRunId(Guid value)
    {
        Value = CognitiveMemoryGuard.EnsureNonEmpty(value, nameof(value));
    }

    public Guid Value { get; }

    public static CognitiveMemoryDreamRunId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public readonly record struct CognitiveMemoryDreamAggregateCandidateId
{
    [JsonConstructor]
    public CognitiveMemoryDreamAggregateCandidateId(Guid value)
    {
        Value = CognitiveMemoryGuard.EnsureNonEmpty(value, nameof(value));
    }

    public Guid Value { get; }

    public static CognitiveMemoryDreamAggregateCandidateId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public readonly record struct CognitiveMemorySynthesizedRecallId
{
    [JsonConstructor]
    public CognitiveMemorySynthesizedRecallId(Guid value)
    {
        Value = CognitiveMemoryGuard.EnsureNonEmpty(value, nameof(value));
    }

    public Guid Value { get; }

    public static CognitiveMemorySynthesizedRecallId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public readonly record struct CognitiveMemorySynthesizedStatementId
{
    [JsonConstructor]
    public CognitiveMemorySynthesizedStatementId(Guid value)
    {
        Value = CognitiveMemoryGuard.EnsureNonEmpty(value, nameof(value));
    }

    public Guid Value { get; }

    public static CognitiveMemorySynthesizedStatementId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public sealed record CognitiveMemoryQualityDiagnosticWarning(
    string Code,
    string Message,
    CognitiveMemoryRiskLevel RiskLevel);

public sealed record CognitiveMemoryQualityDiagnosticsRequest(
    Guid? ProjectId,
    CognitiveMemoryPolicyContext PolicyContext,
    DateTimeOffset? FromUtc = null,
    DateTimeOffset? ToUtc = null);

public sealed record CognitiveMemoryQualityDiagnosticsReport(
    Guid? ProjectId,
    int SourceItemCount,
    int MemoryRecordCount,
    int ClusterCount,
    int ClusterMemberCount,
    int DreamRunCount,
    int DreamRunClusterCount,
    int AggregateCandidateCount,
    int AggregateClaimCount,
    int AggregateClaimSourceMapCount,
    int ValidationCount,
    int ReviewItemCount,
    int SynthesizedRecallCount,
    int SynthesizedStatementCount,
    TimeSpan Elapsed,
    IReadOnlyList<CognitiveMemoryQualityDiagnosticWarning> Warnings)
{
    public bool IsShallowDreamRun
        => DreamRunCount > 0 &&
           (ClusterCount == 0 ||
            DreamRunClusterCount == 0 ||
            AggregateCandidateCount == 0 ||
            AggregateClaimSourceMapCount == 0);
}

public sealed record CognitiveMemoryClusterKey(
    CognitiveMemoryQualityClusterKeyFamily Family,
    string Key,
    string DisplayText);

public sealed record CognitiveMemoryClusterMember(
    CognitiveMemoryQualityClusterMemberKind MemberKind,
    CognitiveMemoryRecordId? MemoryRecordId,
    CognitiveMemorySourceItemId? SourceItemId,
    CognitiveMemoryEvidenceAnchorId? EvidenceAnchorId,
    string Title,
    CognitiveMemoryAccessLevel AccessLevel,
    CognitiveMemoryRiskLevel RiskLevel,
    CognitiveMemoryValidationState ValidationState,
    CognitiveMemoryStabilityState StabilityState);

public sealed record CognitiveMemoryClusterQualityMetrics(
    double CohesionScore,
    double SourceIndependenceScore,
    double SourceDiversityScore,
    double SemanticSignalScore,
    double SupportingSignalScore,
    double GuardPenaltyScore,
    double CompositeScore,
    bool AggregateEligible,
    string EligibilityReason);

public sealed record CognitiveMemoryClusterPlan(
    CognitiveMemoryQualityClusterId ClusterId,
    Guid? ProjectId,
    string ClusterHash,
    CognitiveMemoryQualityClusterKeyFamily PrimaryKeyFamily,
    CognitiveMemoryQualityClusterReadiness Readiness,
    IReadOnlyList<CognitiveMemoryClusterKey> Keys,
    IReadOnlyList<CognitiveMemoryClusterMember> Members,
    CognitiveMemoryClusterQualityMetrics QualityMetrics,
    IReadOnlyList<string> Warnings);

public sealed record CognitiveMemoryClusterPlannerMetrics(
    int RecordsConsidered,
    int SourceItemsConsidered,
    int KeysGenerated,
    int CandidatePairsEvaluated,
    int ClustersCreated,
    int MembersLinked,
    int ContradictionRelationsObserved,
    TimeSpan Elapsed);

public sealed record CognitiveMemoryClusterPlanningRequest
{
    public CognitiveMemoryClusterPlanningRequest(
        Guid? projectId,
        CognitiveMemoryPolicyContext policyContext,
        IReadOnlyList<CognitiveMemoryQualityClusterKeyFamily>? keyFamilies = null,
        int minMembers = 2,
        int maxRecords = 500,
        bool persistClusters = true)
    {
        if (minMembers < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(minMembers), "Cluster planning requires at least two members.");
        }

        if (maxRecords <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxRecords), "Record budget must be positive.");
        }

        ProjectId = projectId;
        PolicyContext = policyContext;
        KeyFamilies = keyFamilies is null || keyFamilies.Count == 0
            ? Enum.GetValues<CognitiveMemoryQualityClusterKeyFamily>()
            : keyFamilies.Distinct().ToArray();
        MinMembers = minMembers;
        MaxRecords = maxRecords;
        PersistClusters = persistClusters;
    }

    public Guid? ProjectId { get; }

    public CognitiveMemoryPolicyContext PolicyContext { get; }

    public IReadOnlyList<CognitiveMemoryQualityClusterKeyFamily> KeyFamilies { get; }

    public int MinMembers { get; }

    public int MaxRecords { get; }

    public bool PersistClusters { get; }
}

public sealed record CognitiveMemoryClusterPlanningResult(
    IReadOnlyList<CognitiveMemoryClusterPlan> Clusters,
    CognitiveMemoryClusterPlannerMetrics Metrics,
    IReadOnlyList<string> Warnings);

public sealed record CognitiveMemoryDreamConsolidationMetrics(
    int ClustersConsidered,
    int ClusterMembersRead,
    int ClaimsExtracted,
    int AggregateCandidatesCreated,
    int AggregateClaimsCreated,
    int AggregateClaimSourceMapsCreated,
    int ValidationRecordsCreated,
    int ReviewItemsCreated,
    int ApprovedCandidates,
    int RejectedCandidates,
    int NeedsReviewCandidates,
    double EvidenceCoverageRatio,
    TimeSpan Elapsed);

public sealed record CognitiveMemoryDreamRunRequest
{
    public CognitiveMemoryDreamRunRequest(
        Guid? projectId,
        CognitiveMemoryConsolidationMode mode,
        CognitiveMemoryConsolidationTriggerKind triggerKind,
        CognitiveMemoryPolicyContext policyContext,
        CognitiveMemoryIdempotencyKey idempotencyKey,
        int maxClusters = 50,
        int minMembersPerCluster = 2,
        bool persistChanges = true)
    {
        if (mode == CognitiveMemoryConsolidationMode.IncrementalRecent)
        {
            throw new ArgumentException("Dream consolidation must be explicit and must not run through the incremental profile.", nameof(mode));
        }

        if (maxClusters <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxClusters), "Cluster budget must be positive.");
        }

        ProjectId = projectId;
        Mode = mode;
        TriggerKind = triggerKind;
        PolicyContext = policyContext;
        IdempotencyKey = idempotencyKey;
        MaxClusters = maxClusters;
        MinMembersPerCluster = minMembersPerCluster;
        PersistChanges = persistChanges;
    }

    public Guid? ProjectId { get; }

    public CognitiveMemoryConsolidationMode Mode { get; }

    public CognitiveMemoryConsolidationTriggerKind TriggerKind { get; }

    public CognitiveMemoryPolicyContext PolicyContext { get; }

    public CognitiveMemoryIdempotencyKey IdempotencyKey { get; }

    public int MaxClusters { get; }

    public int MinMembersPerCluster { get; }

    public bool PersistChanges { get; }
}

public sealed record CognitiveMemoryDreamAggregateSourceMap(
    CognitiveMemoryRecordId SourceMemoryRecordId,
    CognitiveMemorySourceItemId? SourceItemId,
    CognitiveMemoryEvidenceAnchorId? EvidenceAnchorId,
    CognitiveMemoryEvidenceDirection Direction,
    CognitiveMemoryAccessLevel AccessLevel,
    CognitiveMemoryRedactionState RedactionState,
    string Summary);

public sealed record CognitiveMemoryDreamAggregateClaim(
    Guid ClaimId,
    CognitiveMemoryClaimKind ClaimKind,
    string ClaimText,
    string SubjectKey,
    string PredicateKey,
    string ObjectKey,
    IReadOnlyList<CognitiveMemoryDreamAggregateSourceMap> SourceMaps);

public sealed record CognitiveMemoryDreamAggregateCandidate(
    CognitiveMemoryDreamAggregateCandidateId Id,
    CognitiveMemoryDreamRunId DreamRunId,
    CognitiveMemoryQualityClusterId ClusterId,
    Guid? ProjectId,
    CognitiveMemoryConsolidationMode Mode,
    CognitiveMemoryDreamAggregateCandidateStatus Status,
    string Title,
    string SummaryText,
    string CanonicalText,
    CognitiveMemoryAccessLevel AccessLevel,
    CognitiveMemoryRiskLevel RiskLevel,
    IReadOnlyList<CognitiveMemoryDreamAggregateClaim> Claims);

public sealed record CognitiveMemoryDreamRunResult(
    CognitiveMemoryDreamRunId RunId,
    CognitiveMemoryRunStatus Status,
    CognitiveMemoryDreamConsolidationMetrics Metrics,
    IReadOnlyList<CognitiveMemoryDreamAggregateCandidate> AggregateCandidates,
    IReadOnlyList<string> Warnings);

public sealed record CognitiveMemoryDreamValidationIssue(
    CognitiveMemoryDreamValidationIssueKind IssueKind,
    CognitiveMemoryRiskLevel RiskLevel,
    string Message);

public sealed record CognitiveMemoryDreamValidationRequest(
    CognitiveMemoryDreamAggregateCandidateId AggregateCandidateId,
    CognitiveMemoryPolicyContext PolicyContext,
    bool CreateReviewItemWhenNeeded = true);

public sealed record CognitiveMemoryDreamValidationResult(
    CognitiveMemoryDreamAggregateCandidateId AggregateCandidateId,
    CognitiveMemoryDreamValidationDecision Decision,
    IReadOnlyList<CognitiveMemoryDreamValidationIssue> Issues,
    Guid? ReviewItemId);

public sealed record CognitiveMemoryAggregateMemoryApplyRequest(
    CognitiveMemoryDreamAggregateCandidateId AggregateCandidateId,
    string ActorId,
    CognitiveMemoryPolicyContext PolicyContext);

public sealed record CognitiveMemoryAggregateMemoryApplyResult(
    CognitiveMemoryRecordId MemoryRecordId,
    IReadOnlyList<CognitiveMemoryClaimId> ClaimIds,
    bool Created);

public sealed record CognitiveMemorySynthesizedRecallStatement(
    CognitiveMemorySynthesizedStatementId StatementId,
    string Text,
    IReadOnlyList<CognitiveMemoryRecallSourceRef> SourceRefs);

public sealed record CognitiveMemoryRecallSynthesisRequest(
    CognitiveMemoryRecallResult RecallResult,
    CognitiveMemoryPolicyContext PolicyContext,
    int MaxStatements = 5,
    bool PersistSynthesis = true);

public sealed record CognitiveMemorySynthesizedRecallResult(
    CognitiveMemorySynthesizedRecallId SynthesisId,
    Guid ProjectId,
    Guid RecallTraceId,
    string Brief,
    IReadOnlyList<CognitiveMemorySynthesizedRecallStatement> Statements,
    bool ReferencesShownByDefault,
    IReadOnlyList<string> Warnings);

public sealed record CognitiveMemoryReferenceResolverRequest(
    CognitiveMemorySynthesizedStatementId StatementId,
    CognitiveMemoryPolicyContext PolicyContext,
    bool IncludeRestrictedContent = false);

public sealed record CognitiveMemoryResolvedReference(
    CognitiveMemorySynthesizedStatementId StatementId,
    CognitiveMemoryRecordId MemoryRecordId,
    CognitiveMemorySourceItemId? SourceItemId,
    CognitiveMemoryEvidenceAnchorId? EvidenceAnchorId,
    string SourceSystem,
    string Locator,
    string Summary,
    bool Included,
    CognitiveMemoryRecallExclusionReasonKind ExclusionReasonKind);

public sealed record CognitiveMemoryReferenceResolverResult(
    IReadOnlyList<CognitiveMemoryResolvedReference> References,
    IReadOnlyList<string> Warnings);

public interface ICognitiveMemoryQualityDiagnosticsService
{
    ValueTask<CognitiveMemoryQualityDiagnosticsReport> CreateReportAsync(
        CognitiveMemoryQualityDiagnosticsRequest request,
        CancellationToken cancellationToken = default);
}

public interface ICognitiveMemoryClusterPlanner
{
    ValueTask<CognitiveMemoryClusterPlanningResult> PlanAsync(
        CognitiveMemoryClusterPlanningRequest request,
        CancellationToken cancellationToken = default);
}

public interface ICognitiveMemoryDreamConsolidationService
{
    ValueTask<CognitiveMemoryDreamRunResult> RunAsync(
        CognitiveMemoryDreamRunRequest request,
        CancellationToken cancellationToken = default);
}

public interface ICognitiveMemoryDreamValidator
{
    ValueTask<CognitiveMemoryDreamValidationResult> ValidateAsync(
        CognitiveMemoryDreamValidationRequest request,
        CancellationToken cancellationToken = default);
}

public interface ICognitiveMemoryAggregateMemoryApplicator
{
    ValueTask<CognitiveMemoryAggregateMemoryApplyResult> ApplyAsync(
        CognitiveMemoryAggregateMemoryApplyRequest request,
        CancellationToken cancellationToken = default);
}

public interface ICognitiveMemoryRecallSynthesisService
{
    ValueTask<CognitiveMemorySynthesizedRecallResult> SynthesizeAsync(
        CognitiveMemoryRecallSynthesisRequest request,
        CancellationToken cancellationToken = default);
}

public interface ICognitiveMemoryReferenceResolver
{
    ValueTask<CognitiveMemoryReferenceResolverResult> ResolveAsync(
        CognitiveMemoryReferenceResolverRequest request,
        CancellationToken cancellationToken = default);
}
