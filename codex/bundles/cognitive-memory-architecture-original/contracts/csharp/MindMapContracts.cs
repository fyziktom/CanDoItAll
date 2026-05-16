using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CanDoItAll.CognitiveMemory.Abstractions;

public sealed record MindMapNodeSource(
    Guid ProjectId,
    string NodeKey,
    string? ParentNodeKey,
    string ObjectType,
    string? ObjectSubtype,
    string Title,
    string? Subtitle,
    string? Notes,
    double X,
    double Y,
    double? Z,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record MindMapLinkSource(
    Guid ProjectId,
    string SourceNodeKey,
    string TargetNodeKey,
    string LinkKind,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record MindMapFeatureVector(
    string NodeKey,
    SpatialFeatures Spatial,
    GraphFeatures Graph,
    SemanticFeatures Semantic,
    MetadataFeatures Metadata);

public sealed record SpatialFeatures(
    double XNorm,
    double YNorm,
    double ZNorm,
    int Depth,
    double DistanceFromRoot,
    double LocalDensity,
    double DistanceFromParent,
    int SiblingIndex);

public sealed record GraphFeatures(
    int Degree,
    int InDegree,
    int OutDegree,
    int AncestorCount,
    int DescendantCount,
    double BetweennessApproximation,
    IReadOnlyList<string> LinkKinds);

public sealed record SemanticFeatures(
    float[] Embedding,
    string EmbeddingProvider,
    string EmbeddingModel,
    string EmbeddingProfile,
    IReadOnlyList<string> SemanticLabels);

public sealed record MetadataFeatures(
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> Entities,
    IReadOnlyList<string> Scopes,
    IReadOnlyDictionary<string, string> Properties);

public sealed record MindMapClusterResult(
    Guid ProjectId,
    IReadOnlyList<ClusterAssignment> Assignments,
    IReadOnlyList<ClusterSummaryCandidate> SummaryCandidates,
    IReadOnlyList<MemoryRelation> ProposedRelations);

public sealed record ClusterAssignment(
    string NodeKey,
    string ClusterKind,
    string ClusterId,
    double Confidence,
    IReadOnlyDictionary<string, double> Scores);

public sealed record ClusterSummaryCandidate(
    string ClusterKind,
    string ClusterId,
    string Title,
    string Summary,
    IReadOnlyList<string> NodeKeys,
    double Confidence);

public interface IMindMapFeatureExtractor
{
    Task<IReadOnlyList<MindMapFeatureVector>> ExtractAsync(
        IReadOnlyList<MindMapNodeSource> nodes,
        IReadOnlyList<MindMapLinkSource> links,
        MindMapFeatureOptions options,
        CancellationToken cancellationToken = default);
}

public interface IMindMapClusterer
{
    Task<MindMapClusterResult> ClusterAsync(
        IReadOnlyList<MindMapFeatureVector> features,
        MindMapClusterOptions options,
        CancellationToken cancellationToken = default);
}

public sealed record MindMapFeatureOptions(
    string EmbeddingProfile,
    bool IncludeNeighbourText,
    bool NormalizeCoordinatesPerProject,
    IReadOnlyDictionary<string, string> Properties);

public sealed record MindMapClusterOptions(
    double SemanticWeight,
    double SpatialWeight,
    double GraphWeight,
    double MetadataWeight,
    int? SpatialClusterCount,
    int? SemanticClusterCount,
    IReadOnlyDictionary<string, string> Properties);
