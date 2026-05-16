using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CanDoItAll.CognitiveMemory.Abstractions;

public enum MemorySourceKind
{
    Unknown = 0,
    MindMapNode = 1,
    ProjectObject = 2,
    ProjectObjectLink = 3,
    File = 4,
    Repository = 5,
    Commit = 6,
    Issue = 7,
    Email = 8,
    WorkflowRun = 9,
    ProcessRun = 10,
    ProcessDecision = 11,
    ProcessArtifact = 12,
    PluginOutput = 13,
    HumanNote = 14
}

public sealed record MemorySourceManifest(
    Guid Id,
    Guid ProjectId,
    string SourceSystem,
    MemorySourceKind SourceKind,
    string ExternalId,
    string? Locator,
    string ContentHash,
    string? AccessScope,
    DateTimeOffset ObservedAtUtc,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record MemorySourceItem(
    Guid Id,
    Guid ManifestId,
    string SourceItemKey,
    string Title,
    string NormalizedText,
    string? RawStoragePath,
    string ContentHash,
    MemorySourceKind ItemKind,
    DateTimeOffset SourceUpdatedAtUtc,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record CanonicalSourceItem(
    Guid Id,
    Guid SourceItemId,
    string CanonicalKind,
    string Title,
    string CanonicalText,
    IReadOnlyList<string> Entities,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> Scopes,
    ScoreVectorSnapshot ConfidenceVector,
    ScoreScalarProjection? DisplayConfidence,
    string AlgorithmVersion,
    string ContentHash);

public sealed record MemorySourceIngestionRequest(
    Guid ProjectId,
    string SourceSystem,
    MemorySourceKind SourceKind,
    string ExternalId,
    string? Locator,
    string? RawText,
    string? RawStoragePath,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record MemoryIngestionResult(
    Guid ManifestId,
    IReadOnlyList<Guid> SourceItemIds,
    IReadOnlyList<Guid> CanonicalItemIds,
    IReadOnlyList<string> Warnings);

public sealed record SourceDeltaQuery(
    Guid ProjectId,
    IReadOnlyList<MemorySourceKind> SourceKinds,
    DateTimeOffset? SinceUtc,
    int Limit);

public interface ISourceIngestionAdapter
{
    string SourceSystem { get; }

    IReadOnlyList<MemorySourceKind> SupportedKinds { get; }

    Task<IReadOnlyList<MemorySourceIngestionRequest>> DiscoverAsync(
        SourceDiscoveryRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record SourceDiscoveryRequest(
    Guid ProjectId,
    IReadOnlyDictionary<string, string> Options);

public interface ICanonicalizationEngine
{
    Task<IReadOnlyList<CanonicalSourceItem>> CanonicalizeAsync(
        MemorySourceItem sourceItem,
        CanonicalizationOptions options,
        CancellationToken cancellationToken = default);
}

public sealed record CanonicalizationOptions(
    string Profile,
    bool AllowLlmAssistance,
    bool RequireSourceQuotes,
    IReadOnlyDictionary<string, string> Properties);
