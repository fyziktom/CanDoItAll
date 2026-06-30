namespace CanDoItAll.Modules.CognitiveMemory;

public enum CognitiveMemoryProjectionPayloadField
{
    SchemaVersion = 0,
    SchemaKind = 1,
    ProjectId = 2,
    MemoryRecordId = 3,
    MemoryKind = 4,
    ProjectionKind = 5,
    SourceSystem = 6,
    SourceItemKey = 7,
    SourceHash = 8,
    PayloadHash = 9,
    AccessLevel = 10,
    RedactionState = 11,
    ValidationState = 12,
    ClaimId = 13,
    ContextFrameId = 14,
    EvidenceAnchorId = 15,
    EntityId = 16,
    BeliefState = 17,
    EmbeddingProfileId = 18,
    ProjectionProfileId = 19,
    UpdatedAtUtc = 20
}

public enum CognitiveMemoryProjectionPayloadIndexKind
{
    Keyword = 0,
    Integer = 1,
    Float = 2,
    Boolean = 3,
    DateTime = 4,
    Text = 5,
    Uuid = 6
}

public enum CognitiveMemoryProjectionPayloadIndexStatus
{
    Ensured = 0
}

public enum CognitiveMemorySemanticClassificationDecision
{
    Rejected = 0,
    WeakMatch = 1,
    Accepted = 2
}

public static class CognitiveMemoryProjectionPayloadFieldNames
{
    public const string SchemaVersion = "schemaVersion";
    public const string SchemaKind = "schemaKind";
    public const string ProjectId = "projectId";
    public const string MemoryRecordId = "memoryRecordId";
    public const string MemoryKind = "memoryKind";
    public const string ProjectionKind = "projectionKind";
    public const string SourceSystem = "sourceSystem";
    public const string SourceItemKey = "sourceItemKey";
    public const string SourceHash = "sourceHash";
    public const string PayloadHash = "payloadHash";
    public const string AccessLevel = "accessLevel";
    public const string RedactionState = "redactionState";
    public const string ValidationState = "validationState";
    public const string ClaimId = "claimId";
    public const string ContextFrameId = "contextFrameId";
    public const string EvidenceAnchorId = "evidenceAnchorId";
    public const string EntityId = "entityId";
    public const string BeliefState = "beliefState";
    public const string EmbeddingProfileId = "embeddingProfileId";
    public const string ProjectionProfileId = "projectionProfileId";
    public const string UpdatedAtUtc = "updatedAtUtc";

    public static string Resolve(CognitiveMemoryProjectionPayloadField field)
        => field switch
        {
            CognitiveMemoryProjectionPayloadField.SchemaVersion => SchemaVersion,
            CognitiveMemoryProjectionPayloadField.SchemaKind => SchemaKind,
            CognitiveMemoryProjectionPayloadField.ProjectId => ProjectId,
            CognitiveMemoryProjectionPayloadField.MemoryRecordId => MemoryRecordId,
            CognitiveMemoryProjectionPayloadField.MemoryKind => MemoryKind,
            CognitiveMemoryProjectionPayloadField.ProjectionKind => ProjectionKind,
            CognitiveMemoryProjectionPayloadField.SourceSystem => SourceSystem,
            CognitiveMemoryProjectionPayloadField.SourceItemKey => SourceItemKey,
            CognitiveMemoryProjectionPayloadField.SourceHash => SourceHash,
            CognitiveMemoryProjectionPayloadField.PayloadHash => PayloadHash,
            CognitiveMemoryProjectionPayloadField.AccessLevel => AccessLevel,
            CognitiveMemoryProjectionPayloadField.RedactionState => RedactionState,
            CognitiveMemoryProjectionPayloadField.ValidationState => ValidationState,
            CognitiveMemoryProjectionPayloadField.ClaimId => ClaimId,
            CognitiveMemoryProjectionPayloadField.ContextFrameId => ContextFrameId,
            CognitiveMemoryProjectionPayloadField.EvidenceAnchorId => EvidenceAnchorId,
            CognitiveMemoryProjectionPayloadField.EntityId => EntityId,
            CognitiveMemoryProjectionPayloadField.BeliefState => BeliefState,
            CognitiveMemoryProjectionPayloadField.EmbeddingProfileId => EmbeddingProfileId,
            CognitiveMemoryProjectionPayloadField.ProjectionProfileId => ProjectionProfileId,
            CognitiveMemoryProjectionPayloadField.UpdatedAtUtc => UpdatedAtUtc,
            _ => throw new ArgumentOutOfRangeException(nameof(field), field, "Unsupported cognitive memory projection payload field.")
        };
}

public readonly record struct CognitiveMemoryProjectionCollectionName
{
    public CognitiveMemoryProjectionCollectionName(string value)
    {
        Value = CognitiveMemoryGuard.EnsureText(value, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public readonly record struct CognitiveMemoryProjectionPointId
{
    public CognitiveMemoryProjectionPointId(string value)
    {
        Value = CognitiveMemoryGuard.EnsureText(value, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public sealed record CognitiveMemoryProjectionAdapterCapabilities(
    string ProviderName,
    bool SupportsFilters,
    bool SupportsPayloadIndexes,
    bool SupportsDeleteByFilter,
    bool SupportsNamedVectors);

public sealed record CognitiveMemoryProjectionCollectionRequest(
    CognitiveMemoryProjectionCollectionName CollectionName,
    int VectorDimensions);

public sealed record CognitiveMemoryProjectionPayloadIndexSpec(
    CognitiveMemoryProjectionPayloadField Field,
    CognitiveMemoryProjectionPayloadIndexKind IndexKind);

public sealed record CognitiveMemoryProjectionPayloadIndexRequest(
    CognitiveMemoryProjectionCollectionName CollectionName,
    IReadOnlyList<CognitiveMemoryProjectionPayloadIndexSpec> Indexes);

public sealed record CognitiveMemoryProjectionPayloadIndexResult(
    CognitiveMemoryProjectionPayloadField Field,
    CognitiveMemoryProjectionPayloadIndexKind IndexKind,
    CognitiveMemoryProjectionPayloadIndexStatus Status);

public sealed record CognitiveMemoryProjectionEntry(
    CognitiveMemoryProjectionPointId PointId,
    Guid? ProjectId,
    CognitiveMemoryRecordId MemoryRecordId,
    CognitiveMemoryRecordKind MemoryKind,
    CognitiveMemoryProjectionKind ProjectionKind,
    CognitiveMemoryProjectionProfileId ProjectionProfileId,
    CognitiveMemoryEmbeddingProfileId EmbeddingProfileId,
    string ProjectionText,
    CognitiveMemoryVector Vector,
    CognitiveMemoryClaimProjectionPayload ClaimPayload,
    CognitiveMemoryHash SourceHash,
    CognitiveMemoryHash PayloadHash,
    CognitiveMemoryAccessLevel AccessLevel,
    CognitiveMemoryRedactionState RedactionState,
    CognitiveMemoryValidationState ValidationState,
    string SourceSystem,
    string SourceItemKey,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<CognitiveMemoryEvidenceAnchorId>? EvidenceAnchorIds = null,
    IReadOnlyDictionary<string, string>? Metadata = null,
    IReadOnlyList<string>? Tags = null);

public sealed record CognitiveMemoryProjectionWriteRequest(
    CognitiveMemoryProjectionCollectionName CollectionName,
    IReadOnlyList<CognitiveMemoryProjectionEntry> Entries,
    int? ExpectedVectorDimensions = null);

public sealed record CognitiveMemoryProjectionFilter
{
    public CognitiveMemoryProjectionFilter(
        Guid? projectId = null,
        IReadOnlyList<CognitiveMemoryRecordKind>? memoryKinds = null,
        IReadOnlyList<CognitiveMemoryProjectionKind>? projectionKinds = null,
        IReadOnlyList<CognitiveMemoryValidationState>? validationStates = null,
        CognitiveMemoryAccessLevel? maximumAccessLevel = null,
        string? sourceSystem = null,
        string? sourceItemKey = null,
        CognitiveMemoryHash? sourceHash = null,
        CognitiveMemoryHash? payloadHash = null)
    {
        ProjectId = projectId;
        MemoryKinds = memoryKinds ?? [];
        ProjectionKinds = projectionKinds ?? [];
        ValidationStates = validationStates ?? [];
        MaximumAccessLevel = maximumAccessLevel;
        SourceSystem = sourceSystem;
        SourceItemKey = sourceItemKey;
        SourceHash = sourceHash;
        PayloadHash = payloadHash;
    }

    public Guid? ProjectId { get; }

    public IReadOnlyList<CognitiveMemoryRecordKind> MemoryKinds { get; }

    public IReadOnlyList<CognitiveMemoryProjectionKind> ProjectionKinds { get; }

    public IReadOnlyList<CognitiveMemoryValidationState> ValidationStates { get; }

    public CognitiveMemoryAccessLevel? MaximumAccessLevel { get; }

    public string? SourceSystem { get; }

    public string? SourceItemKey { get; }

    public CognitiveMemoryHash? SourceHash { get; }

    public CognitiveMemoryHash? PayloadHash { get; }

    public bool IsEmpty =>
        ProjectId is null &&
        MemoryKinds.Count == 0 &&
        ProjectionKinds.Count == 0 &&
        ValidationStates.Count == 0 &&
        MaximumAccessLevel is null &&
        string.IsNullOrWhiteSpace(SourceSystem) &&
        string.IsNullOrWhiteSpace(SourceItemKey) &&
        SourceHash is null &&
        PayloadHash is null;
}

public sealed record CognitiveMemoryProjectionSearchRequest(
    CognitiveMemoryProjectionCollectionName CollectionName,
    CognitiveMemoryProjectionProfileId ProjectionProfileId,
    string QueryText,
    CognitiveMemoryVector? QueryVector,
    CognitiveMemoryPageRequest Page,
    CognitiveMemoryProjectionFilter? Filter = null,
    double? MinScore = null);

public sealed record CognitiveMemoryProjectionSearchResult(
    CognitiveMemoryProjectionProfileId ProjectionProfileId,
    IReadOnlyList<CognitiveMemoryProjectionSearchHit> Hits,
    string ProviderTrace);

public sealed record CognitiveMemoryProjectionSearchHit(
    CognitiveMemoryProjectionPointId PointId,
    CognitiveMemoryRecordId MemoryRecordId,
    CognitiveMemoryHash PayloadHash,
    double ProviderScore,
    IReadOnlyDictionary<string, object?> Metadata);

public sealed record CognitiveMemoryProjectionDeleteBySourceRequest(
    CognitiveMemoryProjectionCollectionName CollectionName,
    Guid? ProjectId,
    string SourceSystem,
    IReadOnlyList<string>? SourceItemKeys = null,
    IReadOnlyList<CognitiveMemoryHash>? SourceHashes = null);

public sealed record CognitiveMemoryProjectionDeleteResult(string ProviderTrace);

public interface ICognitiveMemoryProjectionAdapter
{
    CognitiveMemoryProjectionAdapterCapabilities Capabilities { get; }

    ValueTask EnsureCollectionAsync(
        CognitiveMemoryProjectionCollectionRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyList<CognitiveMemoryProjectionPayloadIndexResult>> EnsurePayloadIndexesAsync(
        CognitiveMemoryProjectionPayloadIndexRequest request,
        CancellationToken cancellationToken = default);

    ValueTask ProjectAsync(
        CognitiveMemoryProjectionWriteRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<CognitiveMemoryProjectionSearchResult> SearchAsync(
        CognitiveMemoryProjectionSearchRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<CognitiveMemoryProjectionDeleteResult> DeleteBySourceAsync(
        CognitiveMemoryProjectionDeleteBySourceRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record CognitiveMemorySemanticRankRequest(
    string Text,
    CognitiveMemoryPageRequest Page,
    CognitiveMemoryProcessingBudget Budget);

public sealed record CognitiveMemorySemanticRankResult(
    IReadOnlyList<CognitiveMemorySemanticTextMatch> Matches,
    string ProviderTrace);

public sealed record CognitiveMemorySemanticTextMatch(
    string Key,
    string Text,
    float Score,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record CognitiveMemorySemanticClassificationRequest(
    string Text,
    CognitiveMemoryProcessingBudget Budget,
    IReadOnlyDictionary<string, string>? Metadata = null);

public sealed record CognitiveMemorySemanticClassificationResult<TLabel>(
    TLabel? Label,
    CognitiveMemorySemanticClassificationDecision Decision,
    float Score,
    float Margin,
    string MatchedIntentKey,
    string MatchedPhrase,
    IReadOnlyList<CognitiveMemorySemanticClassificationMatch<TLabel>> Matches,
    IReadOnlyList<string> GuardHits,
    string ProviderTrace)
    where TLabel : struct, Enum;

public sealed record CognitiveMemorySemanticClassificationMatch<TLabel>(
    TLabel Label,
    string IntentKey,
    string Phrase,
    float Score)
    where TLabel : struct, Enum;

public interface ICognitiveMemorySemanticRanker
{
    ValueTask<CognitiveMemorySemanticRankResult> RankAsync(
        CognitiveMemorySemanticRankRequest request,
        CancellationToken cancellationToken = default);
}

public interface ICognitiveMemorySemanticClassifier<TLabel>
    where TLabel : struct, Enum
{
    ValueTask<CognitiveMemorySemanticClassificationResult<TLabel>> ClassifyAsync(
        CognitiveMemorySemanticClassificationRequest request,
        CancellationToken cancellationToken = default);
}
