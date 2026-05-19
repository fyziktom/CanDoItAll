using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.CognitiveMemory;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CanDoItAll.Web.Api;


internal static partial class CognitiveMemoryApi
{
    private static void MapDistributedEndpoints(
        RouteGroupBuilder memory,
        CognitiveMemoryApiSurface surface)
    {
        memory.MapPost("/distributed/workers", async (
                CognitiveMemoryDistributedWorkerApiRequest request,
                ICognitiveMemoryDistributedComputeCoordinator coordinator,
                CancellationToken cancellationToken) =>
            await ExecuteAsync(() => coordinator.RegisterWorkerAsync(
                EnsureText(request.WorkerId, nameof(request.WorkerId)),
                EnsureText(request.MachineName, nameof(request.MachineName)),
                request.Capabilities
                    .Select(item => ParseEnum(item, CognitiveMemoryDistributedJobKind.ProjectionRebuild, nameof(request.Capabilities)))
                    .ToArray(),
                cancellationToken)))
            .WithName(EndpointName("RegisterCognitiveMemoryDistributedWorker", surface));

        memory.MapPost("/distributed/jobs", async (
                CognitiveMemoryDistributedJobApiRequest request,
                ICognitiveMemoryDistributedComputeCoordinator coordinator,
                CancellationToken cancellationToken) =>
            await ExecuteAsync(() => coordinator.EnqueueAsync(
                new CognitiveMemoryDistributedJobEnqueueRequest(
                    EnsureNonEmpty(request.ProjectId, nameof(request.ProjectId)),
                    ParseEnum(request.JobKind, CognitiveMemoryDistributedJobKind.ProjectionRebuild, nameof(request.JobKind)),
                    EnsureText(request.SourceScopeKey, nameof(request.SourceScopeKey)),
                    EnsureText(request.InputPayloadJson, nameof(request.InputPayloadJson)),
                    EnsureText(request.ExpectedOutputSchema, nameof(request.ExpectedOutputSchema)),
                    EnsureText(request.AlgorithmVersion, nameof(request.AlgorithmVersion)),
                    EnsureText(request.PolicyProfileId, nameof(request.PolicyProfileId))),
                cancellationToken)))
            .WithName(EndpointName("EnqueueCognitiveMemoryDistributedJob", surface));

        memory.MapPost("/distributed/jobs/claim", async (
                CognitiveMemoryDistributedClaimApiRequest request,
                ICognitiveMemoryDistributedComputeCoordinator coordinator,
                CancellationToken cancellationToken) =>
            await ExecuteAsync(() => coordinator.ClaimAsync(
                EnsureText(request.WorkerId, nameof(request.WorkerId)),
                request.Capabilities
                    .Select(item => ParseEnum(item, CognitiveMemoryDistributedJobKind.ProjectionRebuild, nameof(request.Capabilities)))
                    .ToArray(),
                TimeSpan.FromMinutes(NormalizePositive(request.LeaseMinutes, nameof(request.LeaseMinutes))),
                cancellationToken)))
            .WithName(EndpointName("ClaimCognitiveMemoryDistributedJob", surface));

        memory.MapPost("/distributed/jobs/{jobId:guid}/results", async (
                Guid jobId,
                CognitiveMemoryDistributedResultApiRequest request,
                ICognitiveMemoryDistributedComputeCoordinator coordinator,
                CancellationToken cancellationToken) =>
            await ExecuteAsync(() => coordinator.SubmitResultAsync(
                EnsureNonEmpty(jobId, nameof(jobId)),
                EnsureText(request.WorkerId, nameof(request.WorkerId)),
                EnsureText(request.LeaseToken, nameof(request.LeaseToken)),
                EnsureText(request.InputHash, nameof(request.InputHash)),
                EnsureText(request.OutputPayloadJson, nameof(request.OutputPayloadJson)),
                EnsureText(request.AlgorithmVersion, nameof(request.AlgorithmVersion)),
                EnsureText(request.OutputSchema, nameof(request.OutputSchema)),
                cancellationToken)))
            .WithName(EndpointName("SubmitCognitiveMemoryDistributedResult", surface));
    }
}
