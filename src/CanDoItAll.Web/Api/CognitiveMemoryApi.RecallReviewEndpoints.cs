using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.CognitiveMemory;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CanDoItAll.Web.Api;


internal static partial class CognitiveMemoryApi
{
    private static void MapRecallReviewEndpoints(
        RouteGroupBuilder memory,
        CognitiveMemoryApiSurface surface)
    {
        memory.MapGet("/snapshot", async (
                [AsParameters] CognitiveMemorySnapshotApiQuery query,
                ICognitiveMemoryReviewUiService reviewUiService,
                CancellationToken cancellationToken) =>
            await ExecuteAsync(() => reviewUiService.GetSnapshotAsync(
                new CognitiveMemoryReviewUiQuery(
                    query.ProjectId,
                    NormalizeTake(query.Take, 12, 200),
                    query.IncludeResolvedReviewItems.GetValueOrDefault()),
                cancellationToken)))
            .WithName(EndpointName("GetCognitiveMemorySnapshot", surface));

        memory.MapPost("/sources/ingest", async (
                CognitiveMemorySourceIngestApiRequest request,
                ICognitiveMemorySourceIngestionService ingestionService,
                CancellationToken cancellationToken) =>
            await ExecuteAsync(() => ingestionService.IngestAsync(
                BuildSourceIngestionRequest(request),
                cancellationToken)))
            .WithName(EndpointName("IngestCognitiveMemorySource", surface));

        memory.MapPost("/consolidation/runs", async (
                CognitiveMemoryConsolidationRunApiRequest request,
                ICognitiveMemoryConsolidationEngine consolidationEngine,
                CancellationToken cancellationToken) =>
            await ExecuteAsync(() => consolidationEngine.RunAsync(
                BuildConsolidationRunRequest(request),
                cancellationToken)))
            .WithName(EndpointName("RunCognitiveMemoryConsolidation", surface));

        memory.MapPost("/recall", async (
                CognitiveMemoryRecallApiRequest request,
                IServiceProvider serviceProvider,
                CancellationToken cancellationToken) =>
            await ExecuteAsync(() =>
            {
                var recallOrchestrator = serviceProvider.GetRequiredService<ICognitiveMemoryRecallOrchestrator>();
                return recallOrchestrator.RecallAsync(
                    BuildRecallRequest(request),
                    cancellationToken);
            }))
            .WithName(EndpointName("RecallCognitiveMemoryContext", surface));

        memory.MapPost("/review-items/{reviewItemId:guid}/decisions", async (
                Guid reviewItemId,
                CognitiveMemoryReviewDecisionApiRequest request,
                ICognitiveMemoryReviewUiService reviewUiService,
                CancellationToken cancellationToken) =>
            await ExecuteAsync(() => reviewUiService.DecideReviewItemAsync(
                BuildReviewDecisionRequest(reviewItemId, request),
                cancellationToken)))
            .WithName(EndpointName("DecideCognitiveMemoryReviewItem", surface));
    }
}
