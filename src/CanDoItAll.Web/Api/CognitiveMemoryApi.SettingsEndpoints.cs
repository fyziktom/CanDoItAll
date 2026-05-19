using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.CognitiveMemory;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CanDoItAll.Web.Api;


internal static partial class CognitiveMemoryApi
{
    private static void MapSettingsEndpoints(
        RouteGroupBuilder memory,
        CognitiveMemoryApiSurface surface)
    {
        memory.MapGet("/settings", async (
                ICognitiveMemoryAutomationSettingsService settingsService,
                CancellationToken cancellationToken) =>
            await ExecuteAsync(() => settingsService.GetAsync(cancellationToken)))
            .WithName(EndpointName("GetCognitiveMemorySettings", surface));

        memory.MapPut("/settings", async (
                CognitiveMemoryAutomationSettingsApiRequest request,
                ICognitiveMemoryAutomationSettingsService settingsService,
                CancellationToken cancellationToken) =>
            await ExecuteAsync(() => settingsService.SaveAsync(
                BuildAutomationSettingsUpdate(request),
                cancellationToken)))
            .WithName(EndpointName("UpdateCognitiveMemorySettings", surface));
    }
}
