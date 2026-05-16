using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CanDoItAll.CognitiveMemory.Abstractions;

public enum MemoryDistributedJobKind
{
    SpatialClustering = 0,
    GraphFeatureExtraction = 1,
    SemanticEmbeddingBatch = 2,
    RelationCandidateScoring = 3,
    SourceHashing = 4,
    ProjectionPayloadBuild = 5
}

public enum MemoryDistributedJobStatus
{
    Pending = 0,
    Claimed = 1,
    Running = 2,
    Completed = 3,
    Accepted = 4,
    Rejected = 5,
    Failed = 6,
    Expired = 7
}

public sealed record MemoryDistributedJobPacket(
    Guid JobId,
    Guid ProjectId,
    MemoryDistributedJobKind Kind,
    string InputStoragePath,
    string InputHash,
    string AlgorithmVersion,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    IReadOnlyDictionary<string, string> Options);

public sealed record MemoryDistributedWorkerInfo(
    string WorkerId,
    string DeviceName,
    string DeviceType,
    string CapabilitiesJson,
    DateTimeOffset LastSeenAtUtc);

public sealed record MemoryDistributedJobResult(
    Guid JobId,
    string WorkerId,
    string OutputStoragePath,
    string OutputHash,
    string Status,
    IReadOnlyDictionary<string, string> Metrics,
    IReadOnlyList<string> Warnings);

public interface IMemoryDistributedJobCoordinator
{
    Task<MemoryDistributedJobPacket?> ClaimJobAsync(
        MemoryDistributedWorkerInfo worker,
        CancellationToken cancellationToken = default);

    Task SubmitResultAsync(
        MemoryDistributedJobResult result,
        CancellationToken cancellationToken = default);

    Task ValidateAndAcceptAsync(
        Guid jobId,
        CancellationToken cancellationToken = default);
}

public interface IMemoryDistributedWorker
{
    Task<MemoryDistributedJobResult> ExecuteAsync(
        MemoryDistributedJobPacket packet,
        CancellationToken cancellationToken = default);
}
