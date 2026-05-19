using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.CognitiveMemory;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CanDoItAll.Web.Api;

internal static partial class CognitiveMemoryApi
{
    private static void MapOperationsEndpoints(RouteGroupBuilder memory)
    {
        memory.MapPost("/projections/rebuild", async (
                CognitiveMemoryProjectionRebuildApiRequest request,
                ICognitiveMemoryProjectionRebuildService rebuildService,
                CancellationToken cancellationToken) =>
            await ExecuteAsync(() => rebuildService.RebuildAsync(
                new CognitiveMemoryProjectionRebuildRequest(
                    request.ProjectId,
                    NormalizeTake(request.Take, 50, 500),
                    NormalizeActorId(request.ActorId),
                    string.IsNullOrWhiteSpace(request.CollectionName)
                        ? null
                        : new CognitiveMemoryProjectionCollectionName(request.CollectionName.Trim())),
                cancellationToken)))
            .WithName("RebuildCognitiveMemoryProjections");

        memory.MapPost("/automation/run", async (
                CognitiveMemoryAutomationRunApiRequest request,
                ICognitiveMemoryScheduledAutomationRunner automationRunner,
                CancellationToken cancellationToken) =>
            await ExecuteAsync(() => automationRunner.RunAsync(
                new CognitiveMemoryScheduledAutomationRunRequest(
                    request.ProjectId,
                    ParseEnum(request.TriggerKind, CognitiveMemoryAutomationTriggerKind.Manual, nameof(request.TriggerKind)),
                    NormalizeActorId(request.ActorId),
                    NormalizeTake(request.Take, 50, 500)),
                cancellationToken)))
            .WithName("RunCognitiveMemoryScheduledAutomation");
    }
}
