using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.CognitiveMemory;

public enum CognitiveMemoryProjectionStoreKind
{
    GenericRag = 0,
    Qdrant = 1
}

public enum CognitiveMemoryProjectionLifecycleDecisionKind
{
    NoChange = 0,
    Project = 1,
    Rebuild = 2,
    Delete = 3,
    Failed = 4
}

public enum CognitiveMemoryProjectionStaleReason
{
    None = 0,
    MissingProjection = 1,
    SourceHashChanged = 2,
    PayloadHashChanged = 3,
    ProjectionProfileChanged = 4,
    EmbeddingProfileChanged = 5,
    ProjectionSchemaChanged = 6,
    AlgorithmVersionChanged = 7,
    SourceTombstoned = 8,
    PreviousFailure = 9
}

public sealed record CognitiveMemoryRelationDraft(
    Guid ProjectId,
    CognitiveMemoryRecordId SourceMemoryRecordId,
    CognitiveMemoryRecordId TargetMemoryRecordId,
    CognitiveMemoryRelationKind RelationKind,
    IReadOnlyList<CognitiveMemoryEvidenceAnchorId> EvidenceAnchorIds,
    IReadOnlyList<CognitiveMemoryContextBoundaryPolicy> ContextBoundaryPolicies,
    CognitiveMemoryAlgorithmVersion AlgorithmVersion);

public sealed record CognitiveMemoryProjectionLifecycleDecision(
    CognitiveMemoryProjectionLifecycleDecisionKind DecisionKind,
    CognitiveMemoryProjectionStaleReason StaleReason,
    string Reason);

public sealed record CognitiveMemoryProjectionLifecycleEvaluationRequest(
    CognitiveMemoryProjectionRecord? ExistingProjection,
    CognitiveMemoryHash CurrentSourceHash,
    CognitiveMemoryHash CurrentPayloadHash,
    CognitiveMemoryProjectionProfileId ProjectionProfileId,
    CognitiveMemoryEmbeddingProfileId EmbeddingProfileId,
    CognitiveMemoryPayloadSchemaVersion ProjectionSchemaVersion,
    CognitiveMemoryAlgorithmVersion AlgorithmVersion,
    bool SourceTombstoned);

public sealed record CognitiveMemoryProjectionLifecycleRequest(
    CognitiveMemoryProjectionCollectionName CollectionName,
    CognitiveMemoryProjectionStoreKind ProjectionStoreKind,
    string TargetProviderName,
    string SourceSystem,
    string SourceItemKey,
    CognitiveMemoryRecord MemoryRecord,
    IReadOnlyList<CognitiveMemorySourceLinkRecord> SourceLinks,
    CognitiveMemoryClaimProjectionPayload ClaimPayload,
    IReadOnlyList<CognitiveMemoryEvidenceAnchorId> EvidenceAnchorIds,
    CognitiveMemoryProjectionKind ProjectionKind,
    CognitiveMemoryProjectionProfileId ProjectionProfileId,
    CognitiveMemoryEmbeddingProfileId EmbeddingProfileId,
    CognitiveMemoryPayloadSchemaVersion ProjectionSchemaVersion,
    CognitiveMemoryAlgorithmVersion AlgorithmVersion,
    CognitiveMemoryProcessingBudget Budget,
    int? ExpectedVectorDimensions = null,
    IReadOnlyDictionary<string, string>? Metadata = null,
    IReadOnlyList<string>? Tags = null);

public sealed record CognitiveMemoryProjectionLifecycleResult(
    CognitiveMemoryProjectionLifecycleDecision Decision,
    CognitiveMemoryProjectionRecord ProjectionRecord,
    CognitiveMemoryProjectionWriteRequest? ProjectionWriteRequest,
    string ProviderTrace);

public interface ICognitiveMemoryTaxonomyValidator
{
    Result ValidateMemoryRecord(
        CognitiveMemoryRecord record,
        IReadOnlyList<CognitiveMemorySourceLinkRecord> sourceLinks,
        IReadOnlyList<CognitiveMemoryEvidenceAnchorId> evidenceAnchorIds);

    Result ValidateRelationDraft(CognitiveMemoryRelationDraft draft);
}

public interface ICognitiveMemoryProjectionLifecycleService
{
    CognitiveMemoryProjectionLifecycleDecision EvaluateLifecycle(CognitiveMemoryProjectionLifecycleEvaluationRequest request);

    ValueTask<CognitiveMemoryProjectionLifecycleResult> ProjectAsync(
        CognitiveMemoryProjectionLifecycleRequest request,
        CancellationToken cancellationToken = default);
}
