using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.CognitiveMemory;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CanDoItAll.Web.Api;


internal static partial class CognitiveMemoryApi
{
    private static void MapIngestionEndpoints(
        RouteGroupBuilder memory,
        CognitiveMemoryApiSurface surface)
    {
        memory.MapPost("/ingestion/project-structure", async (
                CognitiveMemoryManualSourceIngestApiRequest request,
                ICognitiveMemorySourceIngestionService ingestionService,
                CancellationToken cancellationToken) =>
            await ExecuteAsync(() => ingestionService.IngestAsync(
                BuildManualSourceIngestionRequest(
                    request,
                    MemorySourceKind.WorkbenchProjectStructure,
                    requireScope: true,
                    "project-structure"),
                cancellationToken)))
            .WithName(EndpointName("IngestCognitiveMemoryProjectStructure", surface));

        memory.MapPost("/ingestion/processes", async (
                CognitiveMemoryManualSourceIngestApiRequest request,
                ICognitiveMemorySourceIngestionService ingestionService,
                CancellationToken cancellationToken) =>
            await ExecuteAsync(() => ingestionService.IngestAsync(
                BuildManualSourceIngestionRequest(
                    request,
                    MemorySourceKind.ProcessRuntime,
                    requireScope: false,
                    "process-runtime"),
                cancellationToken)))
            .WithName(EndpointName("IngestCognitiveMemoryProcesses", surface));

        memory.MapPost("/external-sources/files", async (
                [FromForm] CognitiveMemoryExternalFileUploadApiRequest request,
                ICognitiveMemoryExternalSourceIngestionService ingestionService,
                CancellationToken cancellationToken) =>
            await ExecuteAsync(async () =>
            {
                if (request.File is null)
                {
                    throw new ArgumentException("A file is required.", nameof(request.File));
                }

                if (request.File.Length > CognitiveMemoryExternalSourceIngestionLimits.MaxFileBytes)
                {
                    throw new InvalidOperationException($"File uploads for cognitive memory ingestion are limited to {CognitiveMemoryExternalSourceIngestionLimits.MaxFileBytes / 1024 / 1024} MB.");
                }

                await using var stream = request.File.OpenReadStream();
                return await ingestionService.IngestFileAsync(
                    request.ProjectId,
                    request.File.FileName,
                    request.File.ContentType,
                    stream,
                    request.File.Length,
                    NormalizeActorId(request.ActorId),
                    request.IdempotencyKey,
                    cancellationToken);
            }))
            .WithName(EndpointName("IngestCognitiveMemoryExternalFile", surface))
            .Accepts<CognitiveMemoryExternalFileUploadApiRequest>("multipart/form-data");

        memory.MapPost("/external-sources/web-links", async (
                CognitiveMemoryExternalWebLinkApiRequest request,
                ICognitiveMemoryExternalSourceIngestionService ingestionService,
                CancellationToken cancellationToken) =>
            await ExecuteAsync(() => ingestionService.IngestWebsiteAsync(
                request.ProjectId,
                BuildHttpUri(request.Url),
                NormalizeActorId(request.ActorId),
                request.IdempotencyKey,
                cancellationToken)))
            .WithName(EndpointName("IngestCognitiveMemoryExternalWebLink", surface));

        memory.MapGet("/external-sources/ingestions/{operationId:guid}", async (
                Guid operationId,
                ICognitiveMemoryExternalSourceIngestionService ingestionService,
                CancellationToken cancellationToken) =>
            await ExecuteAsync(async () =>
                await ingestionService.GetAsync(
                    EnsureNonEmpty(operationId, nameof(operationId)),
                    cancellationToken)
                ?? throw new InvalidOperationException("External source ingestion operation was not found.")))
            .WithName(EndpointName("GetCognitiveMemoryExternalSourceIngestion", surface));
    }
}
