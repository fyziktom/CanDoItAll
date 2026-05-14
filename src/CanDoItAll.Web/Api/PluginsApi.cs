using CanDoItAll.Modules.Plugins;
using CanDoItAll.Plugins.Abstractions;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Web.Api;

internal static class PluginsApi
{
    private const string ApiActor = "api";

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
            await ToApiResultAsync(pluginId, id => catalogService.InstallAsync(id, request with { Actor = ApiActor }, cancellationToken)))
            .WithName("InstallPlugin");

        plugins.MapPost("/{pluginId}/enable", async (
                string pluginId,
                PluginInstallationUpdateRequest request,
                PluginCatalogService catalogService,
                CancellationToken cancellationToken) =>
            await ToApiResultAsync(pluginId, id => catalogService.SetEnabledAsync(id, isEnabled: true, request with { Actor = ApiActor }, cancellationToken)))
            .WithName("EnablePlugin");

        plugins.MapPost("/{pluginId}/disable", async (
                string pluginId,
                PluginInstallationUpdateRequest request,
                PluginCatalogService catalogService,
                CancellationToken cancellationToken) =>
            await ToApiResultAsync(pluginId, id => catalogService.SetEnabledAsync(id, isEnabled: false, request with { Actor = ApiActor }, cancellationToken)))
            .WithName("DisablePlugin");

        plugins.MapGet("/{pluginId}/settings", async (
                string pluginId,
                PluginSettingsService settingsService,
                CancellationToken cancellationToken) =>
            await ToApiResultAsync(pluginId, async id =>
            {
                var detail = await settingsService.GetSettingsAsync(id, cancellationToken);
                return detail is null
                    ? Result<PluginSettingsDetail>.Failure(Error.Failure($"Plugin '{id}' was not found.", "plugins.not-found"))
                    : Result<PluginSettingsDetail>.Success(detail);
            }))
            .WithName("GetPluginSettings");

        plugins.MapGet("/{pluginId}/grants", async (
                string pluginId,
                PluginSettingsService settingsService,
                CancellationToken cancellationToken) =>
            await ToApiResultAsync(pluginId, async id =>
            {
                var detail = await settingsService.GetSettingsAsync(id, cancellationToken);
                return detail is null
                    ? Result<IReadOnlyList<PluginCapabilityGrantItem>>.Failure(Error.Failure($"Plugin '{id}' was not found.", "plugins.not-found"))
                    : Result<IReadOnlyList<PluginCapabilityGrantItem>>.Success(detail.Grants);
            }))
            .WithName("ListPluginGrants");

        plugins.MapPut("/{pluginId}/grants", async (
                string pluginId,
                PluginGrantUpdateRequest request,
                PluginSettingsService settingsService,
                CancellationToken cancellationToken) =>
            await ToApiResultAsync(pluginId, id => settingsService.UpdateGrantAsync(id, request, ApiActor, cancellationToken)))
            .WithName("UpdatePluginGrant");

        plugins.MapGet("/{pluginId}/connections", async (
                string pluginId,
                PluginSettingsService settingsService,
                CancellationToken cancellationToken) =>
            await ToApiResultAsync(pluginId, async id =>
            {
                var detail = await settingsService.GetSettingsAsync(id, cancellationToken);
                return detail is null
                    ? Result<IReadOnlyList<PluginConnectionItem>>.Failure(Error.Failure($"Plugin '{id}' was not found.", "plugins.not-found"))
                    : Result<IReadOnlyList<PluginConnectionItem>>.Success(detail.Connections);
            }))
            .WithName("ListPluginConnections");

        plugins.MapPost("/{pluginId}/connections", async (
                string pluginId,
                PluginConnectionSaveRequest request,
                PluginSettingsService settingsService,
                CancellationToken cancellationToken) =>
            await ToApiResultAsync(pluginId, id => settingsService.SaveConnectionAsync(id, request, ApiActor, cancellationToken)))
            .WithName("SavePluginConnection");

        plugins.MapGet("/{pluginId}/oauth/status", async (
                string pluginId,
                PluginOAuthService oauthService,
                CancellationToken cancellationToken) =>
            await ToApiResultAsync(pluginId, async id =>
                Result<IReadOnlyList<PluginOAuthConnectionStatusItem>>.Success(await oauthService.ListStatusesAsync(id, cancellationToken))))
            .WithName("ListPluginOAuthStatuses");

        plugins.MapPost("/{pluginId}/oauth/start", async (
                string pluginId,
                PluginOAuthStartRequest request,
                HttpContext httpContext,
                PluginOAuthService oauthService,
                CancellationToken cancellationToken) =>
            await ToApiResultAsync(pluginId, id => oauthService.StartAsync(
                id,
                request,
                ResolveRequestBaseUri(httpContext),
                ApiActor,
                cancellationToken)))
            .WithName("StartPluginOAuth");

        plugins.MapPost("/{pluginId}/connections/{connectionId:guid}/oauth/disconnect", async (
                string pluginId,
                Guid connectionId,
                PluginOAuthService oauthService,
                CancellationToken cancellationToken) =>
            await ToApiResultAsync(pluginId, id => oauthService.DisconnectAsync(
                id,
                new PluginConnectionId(connectionId),
                cancellationToken)))
            .WithName("DisconnectPluginOAuth");

        plugins.MapGet("/oauth/callback", async (
                string? state,
                string? code,
                string? error,
                string? error_description,
                PluginOAuthService oauthService,
                CancellationToken cancellationToken) =>
            Results.Redirect((await oauthService.CompleteCallbackAsync(
                state,
                code,
                error,
                error_description,
                cancellationToken)).ToString()))
            .AllowAnonymous()
            .WithName("CompletePluginOAuthCallback");

        return group;
    }

    private static async Task<IResult> ToApiResultAsync(
        string pluginId,
        Func<PluginId, Task<Result<PluginCatalogItem>>> action)
        => await ToApiResultAsync<PluginCatalogItem>(pluginId, action);

    private static async Task<IResult> ToApiResultAsync<T>(
        string pluginId,
        Func<PluginId, Task<Result<T>>> action)
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

    private static Uri ResolveRequestBaseUri(HttpContext httpContext)
    {
        var request = httpContext.Request;
        var basePath = string.IsNullOrWhiteSpace(request.PathBase)
            ? "/"
            : $"{request.PathBase}/";
        return new Uri($"{request.Scheme}://{request.Host}{basePath}");
    }
}
