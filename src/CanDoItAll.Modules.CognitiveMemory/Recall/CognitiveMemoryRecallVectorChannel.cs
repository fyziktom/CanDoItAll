using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.CognitiveMemory;

public sealed partial class CognitiveMemoryRecallOrchestrator
{
    private async Task AddVectorCandidatesAsync(
        AppDbContext dbContext,
        CognitiveMemoryRecallRequest request,
        Dictionary<Guid, RecallCandidateAccumulator> candidates,
        List<CognitiveMemoryRecallTraceStage> stages,
        List<string> warnings,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        if (request.ProjectionCollectionName is not { } collectionName ||
            request.ProjectionProfileId is not { } projectionProfileId ||
            request.EmbeddingProfileId is not { } embeddingProfileId)
        {
            stages.Add(Stage(
                CognitiveMemoryRecallTraceStageKind.CoarseCandidateActivation,
                CognitiveMemoryRecallChannelKind.VectorProjection,
                CognitiveMemoryRecallStageStatus.Skipped,
                0,
                0,
                0,
                "vector:projection-options-missing",
                completedAtUtc: nowUtc));
            return;
        }

        if (!projectionAdapter.Capabilities.SupportsFilters)
        {
            warnings.Add($"Projection provider '{projectionAdapter.Capabilities.ProviderName}' does not support typed filters; vector recall was not used.");
            stages.Add(Stage(
                CognitiveMemoryRecallTraceStageKind.CoarseCandidateActivation,
                CognitiveMemoryRecallChannelKind.VectorProjection,
                CognitiveMemoryRecallStageStatus.Unavailable,
                0,
                0,
                0,
                "vector:typed-filter-unavailable",
                failureCode: "ProjectionFiltersUnavailable",
                failureMessage: "Strict recall requires provider-side project/access filters.",
                completedAtUtc: nowUtc));
            return;
        }

        CognitiveMemoryProjectionSearchResult projectionResult;
        try
        {
            var embedding = await embeddingProvider.EmbedAsync(
                new CognitiveMemoryEmbeddingRequest(
                    embeddingProfileId,
                    request.Query,
                    new CognitiveMemoryProcessingBudget(1, request.Budget.MaxSourceBytes, TimeSpan.FromSeconds(10))),
                cancellationToken);

            projectionResult = await projectionAdapter.SearchAsync(
                new CognitiveMemoryProjectionSearchRequest(
                    collectionName,
                    projectionProfileId,
                    request.Query,
                    embedding.Vector,
                    new CognitiveMemoryPageRequest(take: request.Budget.VectorResultLimit),
                    new CognitiveMemoryProjectionFilter(
                        request.ProjectId,
                        NormalizePreferredKinds(request.PreferredRecordKinds),
                        [CognitiveMemoryProjectionKind.VectorCollection],
                        RecallReadableValidationStates,
                        GetProjectionMaximumAccessLevel(request.PolicyContext))),
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(
                exception,
                "Cognitive memory vector recall unavailable for ProjectId={ProjectId} Provider={Provider}.",
                request.ProjectId,
                projectionAdapter.Capabilities.ProviderName);

            warnings.Add($"Vector projection channel unavailable: {exception.GetType().Name}.");
            stages.Add(Stage(
                CognitiveMemoryRecallTraceStageKind.CoarseCandidateActivation,
                CognitiveMemoryRecallChannelKind.VectorProjection,
                CognitiveMemoryRecallStageStatus.Unavailable,
                0,
                0,
                0,
                "vector:unavailable",
                failureCode: exception.GetType().Name,
                failureMessage: exception.Message,
                completedAtUtc: nowUtc));
            return;
        }

        var hitRecordIds = projectionResult.Hits.Select(hit => hit.MemoryRecordId.Value).Distinct().ToArray();
        var records = await LoadRecordsByIdAsync(dbContext, request, hitRecordIds, cancellationToken);
        var recordsById = records.ToDictionary(record => record.Id);
        foreach (var hit in projectionResult.Hits)
        {
            if (!recordsById.TryGetValue(hit.MemoryRecordId.Value, out var record))
            {
                continue;
            }

            var candidate = GetCandidate(candidates, record);
            candidate.Channels.Add(CognitiveMemoryRecallChannelKind.VectorProjection);
            candidate.SemanticSimilarity = Math.Max(candidate.SemanticSimilarity ?? 0, Math.Clamp(hit.ProviderScore, 0, 1));
            candidate.ProjectionPayloadHash = hit.PayloadHash.Value;
            candidate.Reasons.Add("Vector projection channel returned a provider-scoped hit.");
        }

        stages.Add(Stage(
            CognitiveMemoryRecallTraceStageKind.CoarseCandidateActivation,
            CognitiveMemoryRecallChannelKind.VectorProjection,
            CognitiveMemoryRecallStageStatus.Completed,
            projectionResult.Hits.Count,
            records.Count,
            projectionResult.Hits.Count - records.Count,
            projectionResult.ProviderTrace,
            limitingBudget: projectionResult.Hits.Count >= request.Budget.VectorResultLimit ? CognitiveMemoryBudgetLimit.ItemCount : null,
            completedAtUtc: nowUtc));
    }
}
