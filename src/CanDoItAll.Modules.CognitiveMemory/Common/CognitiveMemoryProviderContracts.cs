namespace CanDoItAll.Modules.CognitiveMemory;

public interface ICognitiveMemoryEmbeddingProvider
{
    ValueTask<CognitiveMemoryEmbeddingResult> EmbedAsync(
        CognitiveMemoryEmbeddingRequest request,
        CancellationToken cancellationToken = default);
}

public interface ICognitiveMemoryVectorStore
{
    ValueTask<CognitiveMemoryVectorSearchResult> SearchAsync(
        CognitiveMemoryVectorSearchRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record CognitiveMemoryEmbeddingRequest(
    CognitiveMemoryEmbeddingProfileId EmbeddingProfileId,
    string Input,
    CognitiveMemoryProcessingBudget Budget);

public sealed record CognitiveMemoryEmbeddingResult(
    CognitiveMemoryEmbeddingProfileId EmbeddingProfileId,
    CognitiveMemoryHash InputHash,
    CognitiveMemoryVector Vector,
    string ProviderTrace);

public sealed record CognitiveMemoryVectorSearchRequest(
    CognitiveMemoryProjectionProfileId ProjectionProfileId,
    CognitiveMemoryVector QueryVector,
    CognitiveMemoryPageRequest Page,
    CognitiveMemoryPolicyContext PolicyContext);

public sealed record CognitiveMemoryVectorSearchResult(
    CognitiveMemoryProjectionProfileId ProjectionProfileId,
    IReadOnlyList<CognitiveMemoryVectorSearchHit> Hits,
    string ProviderTrace);

public sealed record CognitiveMemoryVectorSearchHit(
    CognitiveMemoryRecordId RecordId,
    CognitiveMemoryHash PayloadHash,
    float ProviderDistance);
