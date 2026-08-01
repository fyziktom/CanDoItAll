using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Plugins;
using CanDoItAll.Plugins.Abstractions;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

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

        plugins.MapGet("/packages/catalog", async (
                PluginPackageService packageService,
                CancellationToken cancellationToken) =>
            Results.Ok(await packageService.ListPackagesAsync(cancellationToken)))
            .WithName("ListPluginPackageCatalog");

        plugins.MapGet("/packages/{packageId}/icon", async (
                string packageId,
                PluginPackageAssetService assetService,
                CancellationToken cancellationToken) =>
            {
                PluginPackageId id;
                try
                {
                    id = new PluginPackageId(packageId);
                }
                catch (ArgumentException exception)
                {
                    return ApiEndpointResults.BadRequest(exception.Message, "plugins.package-id-invalid");
                }

                var asset = await assetService.ResolveIconAsync(id, cancellationToken);
                return asset is null
                    ? Results.NotFound()
                    : Results.File(asset.FilePath, asset.ContentType, lastModified: asset.LastModifiedUtc);
            })
            .WithName("GetPluginPackageIcon");

        plugins.MapGet("/logs", async (
                PluginLogStreamKind? streamKind,
                string? pluginId,
                string? packageId,
                PluginLogStore logStore,
                CancellationToken cancellationToken) =>
            {
                var plugin = ResolveOptionalPluginId(pluginId);
                if (plugin.IsFailure)
                {
                    return ApiEndpointResults.BadRequest(plugin.Errors[0].Message, plugin.Errors[0].Code);
                }

                var package = ResolveOptionalPackageId(packageId);
                if (package.IsFailure)
                {
                    return ApiEndpointResults.BadRequest(package.Errors[0].Message, package.Errors[0].Code);
                }

                return Results.Ok(await logStore.ListAsync(new PluginLogQuery(
                    streamKind,
                    plugin.Value,
                    package.Value), cancellationToken));
            })
            .WithName("ListPluginLogs");

        plugins.MapPost("/packages/catalog/{packageId}/install", async (
                string packageId,
                PluginPackageInstallRequest request,
                PluginPackageService packageService,
                CancellationToken cancellationToken) =>
            await ToPackageApiResultAsync(packageId, id => packageService.InstallFromCatalogAsync(
                id,
                request with { Actor = ApiActor },
                cancellationToken)))
            .WithName("InstallPluginPackageFromCatalog");

        plugins.MapPost("/packages/upload", async (
                IFormFile file,
                bool? enable,
                PluginPackageService packageService,
                CancellationToken cancellationToken) =>
            {
                if (file.Length == 0)
                {
                    return ApiEndpointResults.BadRequest("Plugin package upload is empty.", "plugins.package-upload-empty");
                }

                await using var stream = file.OpenReadStream();
                return ApiEndpointResults.FromResult(await packageService.InstallUploadedPackageAsync(
                    stream,
                    file.FileName,
                    new PluginPackageInstallRequest(enable ?? true, ApiActor),
                    cancellationToken));
            })
            .Accepts<IFormFile>("multipart/form-data")
            .WithName("UploadPluginPackage");

        plugins.MapGet("/runtime/restart-status", async (
                PluginRuntimeRestartService restartService,
                CancellationToken cancellationToken) =>
            Results.Ok(await restartService.GetStatusAsync(cancellationToken)))
            .WithName("GetPluginRuntimeRestartStatus");

        plugins.MapPost("/runtime/restart", async (
                PluginRuntimeRestartRequest request,
                PluginRuntimeRestartService restartService,
                CancellationToken cancellationToken) =>
            ApiEndpointResults.FromResult(await restartService.RequestRestartAsync(
                request with { Actor = ApiActor },
                cancellationToken)))
            .WithName("RestartPluginRuntime");

        plugins.MapPost("/{pluginId}/install", async (
                string pluginId,
                PluginInstallRequest request,
                PluginCatalogService catalogService,
                IServiceScopeFactory serviceScopeFactory,
                CancellationToken cancellationToken) =>
            await ToApiResultAndRefreshWorkflowTemplatesAsync(
                pluginId,
                id => catalogService.InstallAsync(id, request with { Actor = ApiActor }, cancellationToken),
                serviceScopeFactory,
                cancellationToken))
            .WithName("InstallPlugin");

        plugins.MapPost("/{pluginId}/enable", async (
                string pluginId,
                PluginInstallationUpdateRequest request,
                PluginCatalogService catalogService,
                IServiceScopeFactory serviceScopeFactory,
                CancellationToken cancellationToken) =>
            await ToApiResultAndRefreshWorkflowTemplatesAsync(
                pluginId,
                id => catalogService.SetEnabledAsync(id, isEnabled: true, request with { Actor = ApiActor }, cancellationToken),
                serviceScopeFactory,
                cancellationToken))
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
                IServiceScopeFactory serviceScopeFactory,
                CancellationToken cancellationToken) =>
            await ToApiResultAndRefreshWorkflowTemplatesAsync(
                pluginId,
                id => settingsService.UpdateGrantAsync(id, request, ApiActor, cancellationToken),
                serviceScopeFactory,
                cancellationToken))
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

    private static async Task<IResult> ToPackageApiResultAsync(
        string packageId,
        Func<PluginPackageId, Task<Result<PluginPackageInstallResult>>> action)
    {
        PluginPackageId id;
        try
        {
            id = new PluginPackageId(packageId);
        }
        catch (ArgumentException exception)
        {
            return ApiEndpointResults.BadRequest(exception.Message, "plugins.package-id-invalid");
        }

        return ApiEndpointResults.FromResult(await action(id));
    }

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

    private static async Task<IResult> ToApiResultAndRefreshWorkflowTemplatesAsync<T>(
        string pluginId,
        Func<PluginId, Task<Result<T>>> action,
        IServiceScopeFactory serviceScopeFactory,
        CancellationToken cancellationToken)
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

        var result = await action(id);
        if (result.IsSuccess)
        {
            await using var scope = serviceScopeFactory.CreateAsyncScope();
            var workflowExampleCatalogSeedService = scope.ServiceProvider.GetRequiredService<WorkflowExampleCatalogSeedService>();
            await workflowExampleCatalogSeedService.EnsureSeededAsync(cancellationToken);
        }

        return ApiEndpointResults.FromResult(result);
    }

    private static Result<PluginId?> ResolveOptionalPluginId(string? pluginId)
    {
        if (string.IsNullOrWhiteSpace(pluginId))
        {
            return Result<PluginId?>.Success(null);
        }

        try
        {
            return Result<PluginId?>.Success(new PluginId(pluginId));
        }
        catch (ArgumentException exception)
        {
            return Result<PluginId?>.Failure(Error.Validation(exception.Message, "plugins.plugin-id-invalid"));
        }
    }

    private static Result<PluginPackageId?> ResolveOptionalPackageId(string? packageId)
    {
        if (string.IsNullOrWhiteSpace(packageId))
        {
            return Result<PluginPackageId?>.Success(null);
        }

        try
        {
            return Result<PluginPackageId?>.Success(new PluginPackageId(packageId));
        }
        catch (ArgumentException exception)
        {
            return Result<PluginPackageId?>.Failure(Error.Validation(exception.Message, "plugins.package-id-invalid"));
        }
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
