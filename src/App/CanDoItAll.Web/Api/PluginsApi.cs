using System.ComponentModel;
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

        plugins.MapGet("/catalog", ListCatalogAsync)
            .WithName("ListPluginCatalog")
            .Produces<IReadOnlyList<PluginCatalogItem>>();

        plugins.MapGet("/packages/catalog", ListPackageCatalogAsync)
            .WithName("ListPluginPackageCatalog")
            .Produces<IReadOnlyList<PluginPackageCatalogItem>>();

        plugins.MapGet("/packages/{packageId}/icon", GetPackageIconAsync)
            .WithName("GetPluginPackageIcon")
            .Produces(
                StatusCodes.Status200OK,
                typeof(Stream),
                "image/svg+xml",
                "image/png",
                "image/jpeg",
                "image/webp",
                "application/octet-stream")
            .Produces(StatusCodes.Status404NotFound)
            .ProducesApiErrors(StatusCodes.Status400BadRequest);

        plugins.MapGet("/logs", ListLogsAsync)
            .WithName("ListPluginLogs")
            .Produces<IReadOnlyList<PluginLogItem>>()
            .ProducesApiErrors(StatusCodes.Status400BadRequest);

        plugins.MapPost("/packages/catalog/{packageId}/install", InstallPackageFromCatalogAsync)
            .WithName("InstallPluginPackageFromCatalog")
            .Produces<PluginPackageInstallResult>()
            .ProducesApiErrors(StatusCodes.Status400BadRequest);

        plugins.MapPost("/packages/upload", UploadPackageAsync)
            .Accepts<IFormFile>("multipart/form-data")
            .WithName("UploadPluginPackage")
            .Produces<PluginPackageInstallResult>()
            .ProducesApiErrors(StatusCodes.Status400BadRequest);

        plugins.MapGet("/runtime/restart-status", GetRuntimeRestartStatusAsync)
            .WithName("GetPluginRuntimeRestartStatus")
            .Produces<PluginRuntimeRestartStatus>();

        plugins.MapPost("/runtime/restart", RestartRuntimeAsync)
            .WithName("RestartPluginRuntime")
            .Produces<PluginRuntimeRestartStatus>()
            .ProducesApiErrors(StatusCodes.Status400BadRequest);

        plugins.MapPost("/{pluginId}/install", InstallPluginAsync)
            .WithName("InstallPlugin")
            .Produces<PluginCatalogItem>()
            .ProducesApiErrors(StatusCodes.Status400BadRequest);

        plugins.MapPost("/{pluginId}/enable", EnablePluginAsync)
            .WithName("EnablePlugin")
            .Produces<PluginCatalogItem>()
            .ProducesApiErrors(StatusCodes.Status400BadRequest);

        plugins.MapPost("/{pluginId}/disable", DisablePluginAsync)
            .WithName("DisablePlugin")
            .Produces<PluginCatalogItem>()
            .ProducesApiErrors(StatusCodes.Status400BadRequest);

        plugins.MapGet("/{pluginId}/settings", GetSettingsAsync)
            .WithName("GetPluginSettings")
            .Produces<PluginSettingsDetail>()
            .ProducesApiErrors(StatusCodes.Status400BadRequest);

        plugins.MapGet("/{pluginId}/grants", ListGrantsAsync)
            .WithName("ListPluginGrants")
            .Produces<IReadOnlyList<PluginCapabilityGrantItem>>()
            .ProducesApiErrors(StatusCodes.Status400BadRequest);

        plugins.MapPut("/{pluginId}/grants", UpdateGrantAsync)
            .WithName("UpdatePluginGrant")
            .Produces<PluginCapabilityGrantItem>()
            .ProducesApiErrors(StatusCodes.Status400BadRequest);

        plugins.MapGet("/{pluginId}/connections", ListConnectionsAsync)
            .WithName("ListPluginConnections")
            .Produces<IReadOnlyList<PluginConnectionItem>>()
            .ProducesApiErrors(StatusCodes.Status400BadRequest);

        plugins.MapPost("/{pluginId}/connections", SaveConnectionAsync)
            .WithName("SavePluginConnection")
            .Produces<PluginConnectionItem>()
            .ProducesApiErrors(StatusCodes.Status400BadRequest);

        plugins.MapGet("/{pluginId}/oauth/status", ListOAuthStatusesAsync)
            .WithName("ListPluginOAuthStatuses")
            .Produces<IReadOnlyList<PluginOAuthConnectionStatusItem>>()
            .ProducesApiErrors(StatusCodes.Status400BadRequest);

        plugins.MapPost("/{pluginId}/oauth/start", StartOAuthAsync)
            .WithName("StartPluginOAuth")
            .Produces<PluginOAuthStartResponse>()
            .ProducesApiErrors(StatusCodes.Status400BadRequest);

        plugins.MapPost("/{pluginId}/connections/{connectionId:guid}/oauth/disconnect", DisconnectOAuthAsync)
            .WithName("DisconnectPluginOAuth")
            .Produces<PluginOAuthDisconnectResponse>()
            .ProducesApiErrors(StatusCodes.Status400BadRequest);

        plugins.MapGet("/oauth/callback", CompleteOAuthCallbackAsync)
            .AllowAnonymous()
            .WithName("CompletePluginOAuthCallback")
            .Produces(StatusCodes.Status302Found);

        return group;
    }

    /// <summary>
    /// List the plugins that this host provides, with their installation state.
    /// </summary>
    /// <remarks>
    /// Returns every plugin that a current source provides (plugins bundled with the host and plugins of installed
    /// packages), plus plugins recorded as installed that no current source provides any more (<c>availability</c>
    /// 1 Unavailable), ordered by display name. Packages in the server's catalogue folder that are not installed are
    /// not listed here; see <c>GET /api/plugins/packages/catalog</c>. Each item contains the full manifest in
    /// <c>descriptor</c>: workflow executors, settings forms, connection kinds and the public OAuth configuration,
    /// never secrets. The read changes nothing.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host; otherwise the route
    /// is open.
    /// </remarks>
    /// <response code="200">The plugins, ordered by display name.</response>
    internal static async Task<IResult> ListCatalogAsync(
        PluginCatalogService catalogService,
        CancellationToken cancellationToken)
        => Results.Ok(await catalogService.ListCatalogAsync(cancellationToken));

    /// <summary>
    /// List the plugin packages that are available to install or already installed on this host.
    /// </summary>
    /// <remarks>
    /// Lists the <c>.zip</c> plugin packages in the server's catalogue folder and the installed package folders, one
    /// entry per package identifier (a catalogue file takes precedence over an installed folder), ordered by display
    /// name. Plugins bundled with the host are not packages and are not listed. Install a catalogue package with
    /// <c>POST /api/plugins/packages/catalog/{packageId}/install</c>, or upload a package with
    /// <c>POST /api/plugins/packages/upload</c>. The read changes nothing.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host; otherwise the route
    /// is open.
    /// </remarks>
    /// <response code="200">The packages, ordered by display name.</response>
    internal static async Task<IResult> ListPackageCatalogAsync(
        PluginPackageService packageService,
        CancellationToken cancellationToken)
        => Results.Ok(await packageService.ListPackagesAsync(cancellationToken));

    /// <summary>
    /// Download the icon file of an installed plugin package.
    /// </summary>
    /// <remarks>
    /// Returns the icon file that the manifest of an installed plugin package declares, as binary content. The media
    /// type follows the file extension: <c>.svg</c> image/svg+xml, <c>.png</c> image/png, <c>.jpg</c> and
    /// <c>.jpeg</c> image/jpeg, <c>.webp</c> image/webp, anything else application/octet-stream. The response carries
    /// a <c>Last-Modified</c> header. Only installed packages have icons here: packages that are only in the catalogue
    /// folder and plugins bundled with the host get HTTP 404. Plugin icons of kind 2 (PackageAsset) refer to this
    /// operation.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host; otherwise the route
    /// is open.
    /// </remarks>
    /// <param name="packageId">
    /// Identifier of the installed package, for example <c>candoitall.docker.package</c>; letters, digits, <c>.</c>,
    /// <c>-</c> and <c>_</c>, matched case-insensitively.
    /// </param>
    /// <response code="200">The icon file.</response>
    /// <response code="400">
    /// The identifier contains other characters (<c>plugins.package-id-invalid</c>).
    /// </response>
    /// <response code="404">
    /// No installed package has this identifier, or its icon file is missing. No JSON error envelope; the host may
    /// return its HTML status page.
    /// </response>
    internal static async Task<IResult> GetPackageIconAsync(
        string packageId,
        PluginPackageAssetService assetService,
        CancellationToken cancellationToken)
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
    }

    /// <summary>
    /// List the newest plugin installation and runtime log entries.
    /// </summary>
    /// <remarks>
    /// Returns the 100 newest entries of the plugin log, newest first, optionally filtered by stream, plugin and
    /// package; the filters combine with AND and match exact identifiers. The log records package uploads and
    /// installations, plugin installation, enabling and disabling, restart requirements, and the runs and events of
    /// plugin workflow executors, with secrets redacted from messages and details. There is no paging: narrow the
    /// filters to reach older entries. The read changes nothing.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host; otherwise the route
    /// is open.
    /// </remarks>
    /// <param name="streamKind">
    /// Only entries of this stream, as an integer: 0 Installation or 1 Runtime. The names <c>Installation</c> and
    /// <c>Runtime</c>, with exactly this casing, are also accepted. Omitted means both streams.
    /// </param>
    /// <param name="pluginId">
    /// Only entries of this plugin, for example <c>gmail.mail</c>; letters, digits, <c>.</c>, <c>-</c> and <c>_</c>,
    /// compared after lower-casing. Omitted or blank means all plugins.
    /// </param>
    /// <param name="packageId">
    /// Only entries of this package, for example <c>candoitall.docker.package</c>; same format as
    /// <c>pluginId</c>. Omitted or blank means all packages.
    /// </param>
    /// <response code="200">The entries, newest first; at most 100.</response>
    /// <response code="400">
    /// An identifier contains characters other than letters, digits, <c>.</c>, <c>-</c> and <c>_</c>
    /// (<c>plugins.plugin-id-invalid</c> or <c>plugins.package-id-invalid</c>). A <c>streamKind</c> that is neither a
    /// number nor a member name is rejected by the framework with HTTP 400 without this envelope.
    /// </response>
    internal static async Task<IResult> ListLogsAsync(
        PluginLogStreamKind? streamKind,
        string? pluginId,
        string? packageId,
        PluginLogStore logStore,
        CancellationToken cancellationToken)
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
    }

    /// <summary>
    /// Install a plugin package from the server's package catalogue.
    /// </summary>
    /// <remarks>
    /// Finds the <c>.zip</c> package in the server's catalogue folder whose manifest declares this package identifier,
    /// validates it, extracts it into the installed packages folder, records the plugin as installed (enabled unless
    /// <c>enable</c> is false) and writes installation log entries. An existing installation of the same package is
    /// replaced without a version comparison. The hash and signature declared by the manifest are not verified.
    ///
    /// The plugin appears in <c>GET /api/plugins/catalog</c> at once, but when the package contains .NET assemblies or
    /// declares that it needs a restart, its code (for example its workflow executors) runs only after the host
    /// restarts: <c>restartRequired</c> is then true, and <c>POST /api/plugins/runtime/restart</c> requests the
    /// restart. Installing grants no capabilities; decide them with <c>PUT /api/plugins/{pluginId}/grants</c>.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host; otherwise the route
    /// is open.
    /// </remarks>
    /// <param name="packageId">
    /// Identifier of the package, as listed by <c>GET /api/plugins/packages/catalog</c>; letters, digits, <c>.</c>,
    /// <c>-</c> and <c>_</c>, matched case-insensitively.
    /// </param>
    /// <param name="request">
    /// Installation options. The JSON object is required; send <c>{}</c> for the defaults.
    /// </param>
    /// <response code="200">The package was installed; check <c>restartRequired</c>.</response>
    /// <response code="400">
    /// The package was not installed: <c>plugins.package-id-invalid</c> (invalid identifier characters),
    /// <c>plugins.package-not-found</c> (no catalogue package declares this identifier), <c>plugins.package-invalid</c>
    /// (missing or invalid manifest, missing icon or assembly, unsafe path, or too large), or a manifest validation
    /// code.
    /// </response>
    internal static async Task<IResult> InstallPackageFromCatalogAsync(
        string packageId,
        PluginPackageInstallRequest request,
        PluginPackageService packageService,
        CancellationToken cancellationToken)
        => await ToPackageApiResultAsync(packageId, id => packageService.InstallFromCatalogAsync(
            id,
            request with { Actor = ApiActor },
            cancellationToken));

    /// <summary>
    /// Upload a plugin package file and install it.
    /// </summary>
    /// <remarks>
    /// Accepts a <c>.zip</c> plugin package as the form field <c>file</c> of a <c>multipart/form-data</c> body and
    /// installs it like a catalogue package: validation, extraction into the installed packages folder (replacing an
    /// existing installation of the same package identifier), installation record and log entries. The archive must
    /// contain <c>plugin.package.json</c> at its root with the plugin and package metadata, a display name, a version
    /// and an icon path; a package cannot declare itself bundled or trusted at application level. The hash and
    /// signature declared by the manifest are not verified, and the uploaded file name is used only in messages.
    ///
    /// Size: the file and its extracted content may each be at most 100 MiB by default (host setting
    /// <c>PluginPackages:MaxPackageBytes</c>); the web server's request body limit also applies and can be lower.
    ///
    /// <c>restartRequired</c> true means that the package's code runs only after a host restart
    /// (<c>POST /api/plugins/runtime/restart</c>). Installing grants no capabilities.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host; otherwise the route
    /// is open.
    /// </remarks>
    /// <param name="file">
    /// The plugin package file (<c>.zip</c>), sent as the form field <c>file</c>. It must not be empty.
    /// </param>
    /// <param name="enable">False installs the plugin disabled. Omitted or true installs it enabled.</param>
    /// <response code="200">The package was installed; check <c>restartRequired</c>.</response>
    /// <response code="400">
    /// The package was not installed: <c>plugins.package-upload-empty</c> (empty file),
    /// <c>plugins.package-upload-invalid</c> (the upload could not be stored or exceeds the size limit),
    /// <c>plugins.package-invalid</c> (not a valid package) or a manifest validation code. A request without the
    /// <c>file</c> form field is rejected by the framework with HTTP 400 without this envelope.
    /// </response>
    internal static async Task<IResult> UploadPackageAsync(
        [Description("The plugin package file (`.zip`). It must not be empty.")] IFormFile file,
        bool? enable,
        PluginPackageService packageService,
        CancellationToken cancellationToken)
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
    }

    /// <summary>
    /// Read whether the host must restart to activate installed plugin packages.
    /// </summary>
    /// <remarks>
    /// Returns the plugin restart state kept on the server: whether an installed package needs a host restart and
    /// whether a restart was requested. The state is cleared when the host starts, so after a restart
    /// <c>isRestartRequired</c> is false again and <c>processId</c> is that of the new host process. The read changes
    /// nothing.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host; otherwise the route
    /// is open.
    /// </remarks>
    /// <response code="200">The restart state; every flag is false when nothing is pending.</response>
    internal static async Task<IResult> GetRuntimeRestartStatusAsync(
        PluginRuntimeRestartService restartService,
        CancellationToken cancellationToken)
        => Results.Ok(await restartService.GetStatusAsync(cancellationToken));

    /// <summary>
    /// Stop the host so that it restarts with the installed plugin packages.
    /// </summary>
    /// <remarks>
    /// Allowed only while a restart is required (<c>isRestartRequired</c> true). It records the request and, about
    /// 0.75 seconds after responding, stops the host application. This API does not start the host again: it comes
    /// back only when a process supervisor, such as a service manager or a container runtime, restarts it. Stopping
    /// the host interrupts every user and all running work of the host, so request it deliberately.
    ///
    /// After the host is back, read <c>GET /api/plugins/runtime/restart-status</c> (a new <c>processId</c>) and
    /// <c>GET /api/plugins/catalog</c>.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host; otherwise the route
    /// is open.
    /// </remarks>
    /// <param name="request">The JSON object is required; send <c>{}</c>.</param>
    /// <response code="200">The restart was requested; the host stops shortly after this response.</response>
    /// <response code="400">No installed package requires a restart (<c>plugins.restart-not-required</c>).</response>
    internal static async Task<IResult> RestartRuntimeAsync(
        PluginRuntimeRestartRequest request,
        PluginRuntimeRestartService restartService,
        CancellationToken cancellationToken)
        => ApiEndpointResults.FromResult(await restartService.RequestRestartAsync(
            request with { Actor = ApiActor },
            cancellationToken));

    /// <summary>
    /// Install a plugin that a current source provides, enabled or disabled.
    /// </summary>
    /// <remarks>
    /// Records the plugin as installed from its manifest in the plugin catalog (a bundled plugin or a plugin of an
    /// installed package), enabled unless <c>enable</c> is false. Installing again replaces the saved manifest and the
    /// enabled state and keeps the original installation time. Installing grants no capabilities: the plugin can use
    /// a capability only after <c>PUT /api/plugins/{pluginId}/grants</c> grants it. To add a new package, use the
    /// package operations first.
    ///
    /// After success the host also refreshes its managed example workflow definitions when example seeding is enabled
    /// (host setting <c>Workflows:ExampleSeed:Enabled</c>), so plugin-based examples can appear.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host; otherwise the route
    /// is open.
    /// </remarks>
    /// <param name="pluginId">
    /// Identifier of the plugin, as listed by <c>GET /api/plugins/catalog</c>, for example <c>gmail.mail</c>.
    /// </param>
    /// <param name="request">
    /// Installation options. The JSON object is required; send <c>{}</c> for the defaults.
    /// </param>
    /// <response code="200">The plugin is installed; the body is its updated catalog entry.</response>
    /// <response code="400">
    /// Not installed: <c>plugins.plugin-id-invalid</c> (invalid identifier characters), <c>plugins.not-found</c> (no
    /// current source provides the plugin; this API reports it with HTTP 400) or a manifest validation code.
    /// </response>
    internal static async Task<IResult> InstallPluginAsync(
        string pluginId,
        PluginInstallRequest request,
        PluginCatalogService catalogService,
        IServiceScopeFactory serviceScopeFactory,
        CancellationToken cancellationToken)
        => await ToApiResultAndRefreshWorkflowTemplatesAsync(
            pluginId,
            id => catalogService.InstallAsync(id, request with { Actor = ApiActor }, cancellationToken),
            serviceScopeFactory,
            cancellationToken);

    /// <summary>
    /// Enable an installed plugin.
    /// </summary>
    /// <remarks>
    /// Marks the installed plugin as enabled. It takes effect for the next capability evaluation, without a restart.
    /// Enabling grants nothing: the plugin can use a capability only when it is also granted. Repeating the call is
    /// harmless but updates <c>updatedAtUtc</c> and writes a log entry. After success the host refreshes its example
    /// workflow definitions when example seeding is enabled.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host; otherwise the route
    /// is open.
    /// </remarks>
    /// <param name="pluginId">Identifier of the installed plugin, for example <c>gmail.mail</c>.</param>
    /// <param name="request">The JSON object is required; send <c>{}</c>.</param>
    /// <response code="200">The plugin is enabled; the body is its updated catalog entry.</response>
    /// <response code="400">
    /// Not enabled: <c>plugins.plugin-id-invalid</c> (invalid identifier characters) or <c>plugins.not-installed</c>
    /// (install the plugin first).
    /// </response>
    internal static async Task<IResult> EnablePluginAsync(
        string pluginId,
        PluginInstallationUpdateRequest request,
        PluginCatalogService catalogService,
        IServiceScopeFactory serviceScopeFactory,
        CancellationToken cancellationToken)
        => await ToApiResultAndRefreshWorkflowTemplatesAsync(
            pluginId,
            id => catalogService.SetEnabledAsync(id, isEnabled: true, request with { Actor = ApiActor }, cancellationToken),
            serviceScopeFactory,
            cancellationToken);

    /// <summary>
    /// Disable an installed plugin.
    /// </summary>
    /// <remarks>
    /// Marks the installed plugin as disabled. From the next evaluation on, its workflow executors and OAuth
    /// operations are refused; saved grants and connections are kept, so enabling it again restores them. Code of an
    /// installed package stays loaded until the next host restart. Repeating the call is harmless.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host; otherwise the route
    /// is open.
    /// </remarks>
    /// <param name="pluginId">Identifier of the installed plugin, for example <c>gmail.mail</c>.</param>
    /// <param name="request">The JSON object is required; send <c>{}</c>.</param>
    /// <response code="200">The plugin is disabled; the body is its updated catalog entry.</response>
    /// <response code="400">
    /// Not disabled: <c>plugins.plugin-id-invalid</c> (invalid identifier characters) or <c>plugins.not-installed</c>.
    /// </response>
    internal static async Task<IResult> DisablePluginAsync(
        string pluginId,
        PluginInstallationUpdateRequest request,
        PluginCatalogService catalogService,
        CancellationToken cancellationToken)
        => await ToApiResultAsync(
            pluginId,
            id => catalogService.SetEnabledAsync(id, isEnabled: false, request with { Actor = ApiActor }, cancellationToken));

    /// <summary>
    /// Read the settings view of one plugin: catalog entry, grants, connections, host tool recipes and OAuth metadata.
    /// </summary>
    /// <remarks>
    /// Combines, for one plugin, its catalog entry, its effective grants (including synthesized undecided entries),
    /// its saved connections, the host tool recipes it can request, the connection kinds it declares and its public
    /// OAuth configuration. Connection settings are returned as saved; OAuth tokens and client secrets are never
    /// returned. The read changes nothing.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host; otherwise the route
    /// is open.
    /// </remarks>
    /// <param name="pluginId">Identifier of the plugin, for example <c>gmail.mail</c>.</param>
    /// <response code="200">The settings view.</response>
    /// <response code="400">
    /// <c>plugins.plugin-id-invalid</c> (invalid identifier characters) or <c>plugins.not-found</c> (no current source
    /// provides the plugin; this API reports it with HTTP 400).
    /// </response>
    internal static async Task<IResult> GetSettingsAsync(
        string pluginId,
        PluginSettingsService settingsService,
        CancellationToken cancellationToken)
        => await ToApiResultAsync(pluginId, async id =>
        {
            var detail = await settingsService.GetSettingsAsync(id, cancellationToken);
            return detail is null
                ? Result<PluginSettingsDetail>.Failure(Error.Failure($"Plugin '{id}' was not found.", "plugins.not-found"))
                : Result<PluginSettingsDetail>.Success(detail);
        });

    /// <summary>
    /// List the effective capability grants of a plugin.
    /// </summary>
    /// <remarks>
    /// Returns one entry per capability that the plugin declares and per host tool recipe that it offers, each either
    /// the saved decision or a synthesized undecided entry (state 0 Requested), followed by any other saved grants,
    /// ordered by capability value and then recipe. At runtime a capability is allowed only when the plugin is
    /// installed and enabled, declares the capability, and has a plugin-scope grant in state 1 Granted for exactly that
    /// capability; a host tool recipe needs its own grant and the HostCommand grant. The read changes nothing.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host; otherwise the route
    /// is open.
    /// </remarks>
    /// <param name="pluginId">Identifier of the plugin, for example <c>gmail.mail</c>.</param>
    /// <response code="200">The effective grants.</response>
    /// <response code="400">
    /// <c>plugins.plugin-id-invalid</c> (invalid identifier characters) or <c>plugins.not-found</c> (no current source
    /// provides the plugin).
    /// </response>
    internal static async Task<IResult> ListGrantsAsync(
        string pluginId,
        PluginSettingsService settingsService,
        CancellationToken cancellationToken)
        => await ToApiResultAsync(pluginId, async id =>
        {
            var detail = await settingsService.GetSettingsAsync(id, cancellationToken);
            return detail is null
                ? Result<IReadOnlyList<PluginCapabilityGrantItem>>.Failure(
                    Error.Failure($"Plugin '{id}' was not found.", "plugins.not-found"))
                : Result<IReadOnlyList<PluginCapabilityGrantItem>>.Success(detail.Grants);
        });

    /// <summary>
    /// Save a capability grant decision for a plugin.
    /// </summary>
    /// <remarks>
    /// Creates, or replaces the decision of, the grant identified by <c>capability</c>, <c>recipeId</c>,
    /// <c>scopeKind</c> and <c>scopeKey</c>, and records <c>api</c> as the actor. The decision applies from the next
    /// evaluation, without a restart. Only the state is validated: whether the plugin exists, whether it declares the
    /// capability, whether the recipe exists and whether the scope fits are not checked, and only plugin-scope grants
    /// of a single declared capability take effect. Read <c>GET /api/plugins/{pluginId}/grants</c> first and send one
    /// of its entries with the new state.
    ///
    /// After success the host refreshes its example workflow definitions when example seeding is enabled.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host; otherwise the route
    /// is open. Grants apply to the whole host, not to one caller; a HostCommand grant lets the plugin run its
    /// reviewed host commands.
    /// </remarks>
    /// <param name="pluginId">Identifier of the plugin, for example <c>candoitall.docker</c>.</param>
    /// <param name="request">The grant identity and decision. Send <c>capability</c> and <c>state</c>.</param>
    /// <response code="200">The decision was saved; the body is the saved grant.</response>
    /// <response code="400">
    /// Not saved: <c>plugins.plugin-id-invalid</c> (invalid identifier characters) or
    /// <c>plugins.grant-state-invalid</c> (state 0 Requested or 4 Unavailable).
    /// </response>
    internal static async Task<IResult> UpdateGrantAsync(
        string pluginId,
        PluginGrantUpdateRequest request,
        PluginSettingsService settingsService,
        IServiceScopeFactory serviceScopeFactory,
        CancellationToken cancellationToken)
        => await ToApiResultAndRefreshWorkflowTemplatesAsync(
            pluginId,
            id => settingsService.UpdateGrantAsync(id, request, ApiActor, cancellationToken),
            serviceScopeFactory,
            cancellationToken);

    /// <summary>
    /// List the saved connections of a plugin.
    /// </summary>
    /// <remarks>
    /// Returns the plugin's saved connections, ordered by connection key and then display name, with their settings
    /// exactly as saved. OAuth state is read with <c>GET /api/plugins/{pluginId}/oauth/status</c>; tokens are never
    /// returned. The read changes nothing.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host; otherwise the route
    /// is open.
    /// </remarks>
    /// <param name="pluginId">Identifier of the plugin, for example <c>gmail.mail</c>.</param>
    /// <response code="200">The connections; empty when there are none.</response>
    /// <response code="400">
    /// <c>plugins.plugin-id-invalid</c> (invalid identifier characters) or <c>plugins.not-found</c> (no current source
    /// provides the plugin).
    /// </response>
    internal static async Task<IResult> ListConnectionsAsync(
        string pluginId,
        PluginSettingsService settingsService,
        CancellationToken cancellationToken)
        => await ToApiResultAsync(pluginId, async id =>
        {
            var detail = await settingsService.GetSettingsAsync(id, cancellationToken);
            return detail is null
                ? Result<IReadOnlyList<PluginConnectionItem>>.Failure(
                    Error.Failure($"Plugin '{id}' was not found.", "plugins.not-found"))
                : Result<IReadOnlyList<PluginConnectionItem>>.Success(detail.Connections);
        });

    /// <summary>
    /// Create a plugin connection, or replace the display name, settings and enabled flag of an existing one.
    /// </summary>
    /// <remarks>
    /// Without <c>id</c>, creates a connection of the given connection kind. With <c>id</c>, replaces the display
    /// name, settings and enabled flag of that connection of this plugin and keeps its connection key; an <c>id</c>
    /// that no connection of this plugin has creates a connection with that identifier. The server does not check the
    /// plugin or the connection key against the catalog and stores <c>settingsJson</c> without validating it, so read
    /// <c>connectionDescriptors</c> of <c>GET /api/plugins/{pluginId}/settings</c> first and send a declared key and
    /// the fields of its settings form. Settings are returned unredacted: never put secrets in them. OAuth connections
    /// are authorized afterwards with <c>POST /api/plugins/{pluginId}/oauth/start</c>.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host; otherwise the route
    /// is open.
    /// </remarks>
    /// <param name="pluginId">Identifier of the plugin, for example <c>gmail.mail</c>.</param>
    /// <param name="request">The connection to create or replace.</param>
    /// <response code="200">The connection was saved; the body is the saved connection with its identifier.</response>
    /// <response code="400">
    /// Not saved: <c>plugins.plugin-id-invalid</c> (invalid identifier characters) or
    /// <c>plugins.connection-display-name-required</c> (blank display name).
    /// </response>
    internal static async Task<IResult> SaveConnectionAsync(
        string pluginId,
        PluginConnectionSaveRequest request,
        PluginSettingsService settingsService,
        CancellationToken cancellationToken)
        => await ToApiResultAsync(
            pluginId,
            id => settingsService.SaveConnectionAsync(id, request, ApiActor, cancellationToken));

    /// <summary>
    /// List the OAuth state of a plugin's connections.
    /// </summary>
    /// <remarks>
    /// Returns one entry per connection of this plugin that has OAuth state, ordered by connection key. OAuth state
    /// is created when an authorization callback completes or fails and is reset by a disconnect. A connected entry
    /// whose stored token lacks a scope that the plugin requires is reported as 2 ReconnectRequired with
    /// <c>lastErrorCode</c> <c>oauth-scope-missing</c>. Tokens are never returned. An unknown plugin returns an empty
    /// list. The read changes nothing.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host; otherwise the route
    /// is open.
    /// </remarks>
    /// <param name="pluginId">Identifier of the plugin, for example <c>gmail.mail</c>.</param>
    /// <response code="200">The OAuth state entries; empty when there are none.</response>
    /// <response code="400">The identifier contains invalid characters (<c>plugins.plugin-id-invalid</c>).</response>
    internal static async Task<IResult> ListOAuthStatusesAsync(
        string pluginId,
        PluginOAuthService oauthService,
        CancellationToken cancellationToken)
        => await ToApiResultAsync(pluginId, async id =>
            Result<IReadOnlyList<PluginOAuthConnectionStatusItem>>.Success(
                await oauthService.ListStatusesAsync(id, cancellationToken)));

    /// <summary>
    /// Start the OAuth authorization of a plugin connection and return the address where the user signs in.
    /// </summary>
    /// <remarks>
    /// Prepares an OAuth 2.0 authorization code flow for the connection kind named by <c>connectionKey</c>: selects or
    /// creates the connection (a new one is saved before the user signs in), creates a one-time <c>state</c> and, when
    /// the plugin uses PKCE, a code verifier, and returns <c>authorizationUrl</c>. Open that address in the user's
    /// browser. After sign-in the identity provider sends the browser to <c>GET /api/plugins/oauth/callback</c>, which
    /// stores the tokens and redirects to <c>returnPath</c>. The pending authorization expires after 10 minutes and
    /// can be completed once.
    ///
    /// Prerequisites: the plugin is in the catalog, declares OAuth for this connection kind, is installed and enabled,
    /// and has the OAuth2 capability (128) granted. The OAuth client identifier comes from the connection setting
    /// <c>clientId</c> or from the plugin. The callback also needs the client secret in the server environment
    /// variable that the plugin's OAuth configuration names.
    ///
    /// Next: after the browser returns, read <c>GET /api/plugins/{pluginId}/oauth/status</c>.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host; otherwise the route
    /// is open.
    /// </remarks>
    /// <param name="pluginId">Identifier of the plugin, for example <c>gmail.mail</c>.</param>
    /// <param name="request">Connection kind, optional connection, return path, scopes and redirect address.</param>
    /// <response code="200">The authorization was prepared; open <c>authorizationUrl</c>.</response>
    /// <response code="400">
    /// Not started: <c>plugins.plugin-id-invalid</c>, <c>plugins.not-found</c> (not in the catalog),
    /// <c>plugins.oauth-descriptor-missing</c> (no OAuth for this connection key), <c>plugins.oauth-grant-denied</c>
    /// (not installed, not enabled, or OAuth2 not granted), <c>plugins.connection-not-found</c> (<c>connectionId</c> is
    /// not a connection of this plugin) or <c>plugins.oauth-client-id-missing</c>. A connection created by this
    /// request remains when the client identifier is missing.
    /// </response>
    internal static async Task<IResult> StartOAuthAsync(
        string pluginId,
        PluginOAuthStartRequest request,
        HttpContext httpContext,
        PluginOAuthService oauthService,
        CancellationToken cancellationToken)
        => await ToApiResultAsync(pluginId, id => oauthService.StartAsync(
            id,
            request,
            ResolveRequestBaseUri(httpContext),
            ApiActor,
            cancellationToken));

    /// <summary>
    /// Delete the stored OAuth tokens of a plugin connection.
    /// </summary>
    /// <remarks>
    /// Deletes the connection's stored tokens and sets its OAuth state to 0 NotConnected. It does not revoke the
    /// tokens at the identity provider and keeps the connection and its settings; authorize again with
    /// <c>POST /api/plugins/{pluginId}/oauth/start</c>.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host; otherwise the route
    /// is open.
    /// </remarks>
    /// <param name="pluginId">Identifier of the plugin, for example <c>gmail.mail</c>.</param>
    /// <param name="connectionId">
    /// Identifier of the connection: <c>id</c> of a saved connection or <c>connectionId</c> of an OAuth state entry.
    /// </param>
    /// <response code="200">The tokens were deleted; <c>status</c> is 0 NotConnected.</response>
    /// <response code="400">
    /// <c>plugins.plugin-id-invalid</c> (invalid identifier characters) or <c>plugins.oauth-connection-not-found</c>
    /// (the connection has no OAuth state).
    /// </response>
    internal static async Task<IResult> DisconnectOAuthAsync(
        string pluginId,
        Guid connectionId,
        PluginOAuthService oauthService,
        CancellationToken cancellationToken)
        => await ToApiResultAsync(pluginId, id => oauthService.DisconnectAsync(
            id,
            new PluginConnectionId(connectionId),
            cancellationToken));

    /// <summary>
    /// Complete an OAuth authorization when the identity provider sends the user's browser back.
    /// </summary>
    /// <remarks>
    /// This is the redirect target registered with the identity provider, not an operation for API clients: the
    /// browser arrives here after sign-in with <c>state</c> and either <c>code</c> or <c>error</c>. The server checks
    /// the one-time <c>state</c> created by <c>POST /api/plugins/{pluginId}/oauth/start</c>, exchanges the code for
    /// tokens at the provider's token endpoint, keeps the tokens in its secret store (never in a response) and marks
    /// the connection as connected.
    ///
    /// It always answers with a redirect, a relative path in the <c>Location</c> header, to the start request's
    /// <c>returnPath</c> with the query parameters <c>oauth</c> (<c>connected</c> or <c>failed</c>),
    /// <c>connectionId</c> and, on failure, <c>reason</c>: for example <c>state-used</c>, <c>expired</c>,
    /// <c>missing-code</c>, the provider's error code, or a token exchange failure code such as
    /// <c>plugins.oauth-client-secret-missing</c>. A missing or unknown <c>state</c> redirects to
    /// <c>/plugins?oauth=failed</c> with <c>reason</c> <c>missing-state</c> or <c>invalid-state</c>.
    ///
    /// Authority: anonymous. The route accepts requests without a bearer token even when API authorization is
    /// enabled, because the browser returns from the identity provider without one; the single-use <c>state</c> ties
    /// the callback to the pending authorization.
    /// </remarks>
    /// <param name="state">One-time state value that the start operation put into the authorization address.</param>
    /// <param name="code">Authorization code issued by the identity provider after a successful sign-in.</param>
    /// <param name="error">Error code sent by the identity provider when sign-in failed or was declined.</param>
    /// <param name="error_description">Error description sent by the identity provider.</param>
    /// <response code="302">
    /// Redirect to the return path with the outcome in its query string. Failures are reported in the same way,
    /// never with an error status.
    /// </response>
    internal static async Task<IResult> CompleteOAuthCallbackAsync(
        string? state,
        string? code,
        string? error,
        string? error_description,
        PluginOAuthService oauthService,
        CancellationToken cancellationToken)
        => Results.Redirect((await oauthService.CompleteCallbackAsync(
            state,
            code,
            error,
            error_description,
            cancellationToken)).ToString());

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
