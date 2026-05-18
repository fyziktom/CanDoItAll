using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CanDoItAll.CognitiveMemory.Abstractions;

public enum ConsolidationTriggerKind
{
    Manual = 0,
    Idle = 1,
    Nightly = 2,
    WorkflowCompleted = 3,
    ProcessCompleted = 4,
    SourceChanged = 5,
    DistributedWorkerReturned = 6
}

public sealed record ConsolidationRunRequest(
    Guid ProjectId,
    ConsolidationTriggerKind TriggerKind,
    ConsolidationProfile Profile,
    IReadOnlyDictionary<string, string> Options);

public sealed record ConsolidationProfile(
    string Name,
    bool ProcessSources,
    bool GenerateEmbeddings,
    bool RunClustering,
    bool DetectContradictions,
    bool ExtractProcedures,
    bool RebuildProjections,
    bool CreateHumanReviewItems,
    int MaxItems);

public sealed record ConsolidationRunResult(
    Guid RunId,
    string Status,
    int SourcesProcessed,
    int MemoryItemsCreated,
    int RelationsCreated,
    int ReviewItemsCreated,
    int ProjectionsUpdated,
    string? ReportStoragePath,
    IReadOnlyList<string> Warnings);

public sealed record ConsolidationRunState(
    Guid RunId,
    Guid ProjectId,
    ConsolidationTriggerKind TriggerKind,
    string Status,
    string InputHash,
    string? OutputHash,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    IReadOnlyDictionary<string, string> Metrics);

public interface IMemoryConsolidationEngine
{
    Task<ConsolidationRunResult> RunAsync(
        ConsolidationRunRequest request,
        CancellationToken cancellationToken = default);
}

public interface IEpisodeExtractor
{
    Task<IReadOnlyList<MemoryItem>> ExtractEpisodesAsync(
        EpisodeExtractionRequest request,
        CancellationToken cancellationToken = default);
}

public interface IProcedureExtractor
{
    Task<IReadOnlyList<ProcedureSkillRecord>> ExtractProcedureSkillsAsync(
        ProcedureExtractionRequest request,
        CancellationToken cancellationToken = default);
}

public interface IContradictionDetector
{
    Task<IReadOnlyList<ContradictionCandidate>> DetectAsync(
        IReadOnlyList<MemoryItem> items,
        CancellationToken cancellationToken = default);
}

public sealed record EpisodeExtractionRequest(
    Guid ProjectId,
    Guid? ProcessRunId,
    Guid? WorkflowRunId,
    IReadOnlyDictionary<string, string> Options);

public sealed record ProcedureExtractionRequest(
    Guid ProjectId,
    IReadOnlyList<Guid> EpisodeMemoryItemIds,
    IReadOnlyDictionary<string, string> Options);

public sealed record ContradictionCandidate(
    Guid SourceMemoryItemId,
    Guid TargetMemoryItemId,
    string Summary,
    ScoreEvaluationTrace ContradictionTrace,
    ScoreScalarProjection? DisplayConfidence,
    IReadOnlyList<RelationEvidence> Evidence);
