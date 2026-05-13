using CanDoItAll.Modules.Plugins;
using CanDoItAll.Plugins.Abstractions;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Web.Api;

internal static class PluginsApi
{
    public static RouteGroupBuilder MapPluginsApi(this RouteGroupBuilder group)
    {
        var plugins = group.MapGroup("/plugins")
            .WithTags("Plugins")
            .DisableAntiforgery();

        plugins.MapGet("/catalog", async (
                PluginCatalogService catalogService,
                CancellationToken cancellationToken) =>
            Results.Ok(await catalogService.ListCatalogAsync(cancellationToken)))
            .WithName("ListPluginCatalog");

        plugins.MapPost("/{pluginId}/install", async (
                string pluginId,
                PluginInstallRequest request,
                PluginCatalogService catalogService,
                CancellationToken cancellationToken) =>
            await ToApiResultAsync(pluginId, id => catalogService.InstallAsync(id, request, cancellationToken)))
            .WithName("InstallPlugin");

        plugins.MapPost("/{pluginId}/enable", async (
                string pluginId,
                PluginInstallationUpdateRequest request,
                PluginCatalogService catalogService,
                CancellationToken cancellationToken) =>
            await ToApiResultAsync(pluginId, id => catalogService.SetEnabledAsync(id, isEnabled: true, request, cancellationToken)))
            .WithName("EnablePlugin");

        plugins.MapPost("/{pluginId}/disable", async (
                string pluginId,
                PluginInstallationUpdateRequest request,
                PluginCatalogService catalogService,
                CancellationToken cancellationToken) =>
            await ToApiResultAsync(pluginId, id => catalogService.SetEnabledAsync(id, isEnabled: false, request, cancellationToken)))
            .WithName("DisablePlugin");

        return group;
    }

    private static async Task<IResult> ToApiResultAsync(
        string pluginId,
        Func<PluginId, Task<Result<PluginCatalogItem>>> action)
    {
        PluginId id;
        try
        {
            id = new PluginId(pluginId);
        }
        catch (ArgumentException exception)
        {
            return ApiEndpointResults.BadRequest(exception.Message, "plugins.plugin-id-invalid");
        }

        return ApiEndpointResults.FromResult(await action(id));
    }
}
