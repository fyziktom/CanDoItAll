using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.CognitiveMemory;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CanDoItAll.Web.Api;


internal static partial class CognitiveMemoryApi
{
    private static void MapSettingsEndpoints(RouteGroupBuilder memory)
    {
        memory.MapGet("/settings", async (
                ICognitiveMemoryAutomationSettingsService settingsService,
                CancellationToken cancellationToken) =>
            await ExecuteAsync(() => settingsService.GetAsync(cancellationToken)))
            .WithName("GetCognitiveMemorySettings");

        memory.MapPut("/settings", async (
                CognitiveMemoryAutomationSettingsApiRequest request,
                ICognitiveMemoryAutomationSettingsService settingsService,
                CancellationToken cancellationToken) =>
            await ExecuteAsync(() => settingsService.SaveAsync(
                BuildAutomationSettingsUpdate(request),
                cancellationToken)))
            .WithName("UpdateCognitiveMemorySettings");
    }
}