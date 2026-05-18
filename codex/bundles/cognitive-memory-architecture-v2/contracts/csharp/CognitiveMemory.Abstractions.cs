using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CanDoItAll.CognitiveMemory.Abstractions;

public interface ICognitiveMemoryService
{
    Task<MemoryIngestionResult> IngestSourceAsync(
        MemorySourceIngestionRequest request,
        CancellationToken cancellationToken = default);

    Task<RecallResult> RecallAsync(
        RecallRequest request,
        CancellationToken cancellationToken = default);

    Task<ConsolidationRunResult> RunConsolidationAsync(
        ConsolidationRunRequest request,
        CancellationToken cancellationToken = default);

    Task<ProjectedMemoryResult> ProjectMemoryAsync(
        MemoryProjectionRequest request,
        CancellationToken cancellationToken = default);

    Task<MemoryProbeAnswerResult> ProbeAsync(
        MemoryProbeTurnRequest request,
        CancellationToken cancellationToken = default);

    Task<MemoryMutationResult> SubmitMemoryMutationAsync(
        MemoryMutationCommand command,
        CancellationToken cancellationToken = default);
}

public interface IMemoryStore
{
    Task<MemoryItem?> GetMemoryItemAsync(Guid memoryItemId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MemoryItem>> GetMemoryItemsAsync(
        MemoryItemQuery query,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MemoryRelation>> GetRelationsAsync(
        MemoryRelationQuery query,
        CancellationToken cancellationToken = default);
}

public interface IMemorySourceStore
{
    Task<MemorySourceManifest> UpsertManifestAsync(
        MemorySourceManifest manifest,
        CancellationToken cancellationToken = default);

    Task<MemorySourceItem> UpsertSourceItemAsync(
        MemorySourceItem item,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MemorySourceItem>> GetChangedSourceItemsAsync(
        SourceDeltaQuery query,
        CancellationToken cancellationToken = default);
}

public interface IRecallTraceStore
{
    Task<Guid> SaveTraceAsync(RecallTrace trace, CancellationToken cancellationToken = default);

    Task<RecallTrace?> GetTraceAsync(Guid traceId, CancellationToken cancellationToken = default);
}

public interface IMemoryAccessPolicy
{
    Task<MemoryAccessDecision> EvaluateAsync(
        MemoryAccessRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record MemoryAccessRequest(
    MemoryAccessContext AccessContext,
    IReadOnlyList<MemoryItem> CandidateItems,
    string Operation);

public sealed record MemoryAccessDecision(
    IReadOnlyList<MemoryItem> AllowedItems,
    IReadOnlyDictionary<Guid, string> RedactionReasons,
    IReadOnlyDictionary<Guid, string> DenyReasons);
