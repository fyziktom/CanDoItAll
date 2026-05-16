using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CanDoItAll.CognitiveMemory.Abstractions;

public enum MemoryProjectionType
{
    AtomicNode = 0,
    LocalCluster = 1,
    SemanticTopic = 2,
    ProjectCanonicalTopic = 3,
    CrossProjectTopic = 4,
    Procedure = 5,
    Decision = 6,
    Episode = 7,
    Reflection = 8
}

public enum ProjectionPayloadValueKind
{
    String = 0,
    Number = 1,
    Boolean = 2,
    StringArray = 3,
    NumberArray = 4
}

public sealed record ProjectionPayloadValue(
    ProjectionPayloadValueKind Kind,
    string? StringValue,
    double? NumberValue,
    bool? BooleanValue,
    IReadOnlyList<string> StringValues,
    IReadOnlyList<double> NumberValues);

public sealed record MemoryProjectionPayload(
    string SchemaVersion,
    IReadOnlyDictionary<string, ProjectionPayloadValue> Values);

public sealed record MemoryProjectionRequest(
    Guid ProjectId,
    IReadOnlyList<Guid> MemoryItemIds,
    string ProjectionProfile,
    bool RebuildExisting,
    IReadOnlyDictionary<string, string> Options);

public sealed record ProjectedMemoryResult(
    int Upserted,
    int Deleted,
    int Skipped,
    IReadOnlyList<string> Warnings);

public sealed record MemoryProjectionPoint(
    string PointId,
    Guid MemoryItemId,
    MemoryProjectionType ProjectionType,
    string CollectionName,
    string VectorProfile,
    float[] Vector,
    MemoryProjectionPayload Payload);

public sealed record VectorSearchRequest(
    Guid ProjectId,
    string QueryText,
    float[]? QueryVector,
    string CollectionName,
    string VectorProfile,
    MemoryVectorFilter Filter,
    int Limit,
    double? MinimumScore);

public sealed record MemoryVectorFilter(
    IReadOnlyList<Guid> ProjectIds,
    IReadOnlyList<MemoryType> MemoryTypes,
    IReadOnlyList<MemoryProjectionType> ProjectionTypes,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> Scopes,
    IReadOnlyList<MemoryValidationState> ValidationStates,
    IReadOnlyDictionary<string, string> PayloadEquals);

public sealed record VectorSearchResult(
    IReadOnlyList<VectorSearchHit> Hits);

public sealed record VectorSearchHit(
    string PointId,
    Guid MemoryItemId,
    double Score,
    MemoryProjectionPayload Payload);

public interface ICognitiveVectorProjectionStore
{
    Task EnsureProjectionCollectionAsync(
        ProjectionCollectionOptions options,
        CancellationToken cancellationToken = default);

    Task UpsertAsync(
        IReadOnlyList<MemoryProjectionPoint> points,
        CancellationToken cancellationToken = default);

    Task DeleteByMemoryItemIdsAsync(
        IReadOnlyList<Guid> memoryItemIds,
        CancellationToken cancellationToken = default);

    Task<VectorSearchResult> SearchAsync(
        VectorSearchRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ProjectionCollectionOptions(
    string CollectionName,
    int VectorSize,
    string DistanceMetric,
    string VectorProfile,
    bool UseNamedVectors,
    IReadOnlyDictionary<string, string> PayloadIndexes);

public interface IMemoryProjectionBuilder
{
    Task<IReadOnlyList<MemoryProjectionPoint>> BuildProjectionPointsAsync(
        IReadOnlyList<MemoryItem> items,
        ProjectionBuildOptions options,
        CancellationToken cancellationToken = default);
}

public sealed record ProjectionBuildOptions(
    string ProjectionProfile,
    string EmbeddingProfile,
    bool IncludeSourceContext,
    IReadOnlyDictionary<string, string> Properties);
