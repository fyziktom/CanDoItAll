using CanDoItAll.Plugins.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Modules.Plugins;

public interface IRuntimePluginServiceRegistrar
{
    void ConfigureServices(IServiceCollection services);
}

/// <summary>
/// Where a listed plugin package was found, as a JSON integer: 0 Catalogue (a package file in the server's catalogue
/// folder), 1 Installed (only an installed package folder exists).
/// </summary>
public enum PluginPackageCatalogSourceKind
{
    Catalogue,
    Installed
}

/// <summary>
/// How a plugin package was installed, as a JSON integer: 0 Catalogue (from the server's catalogue folder), 1 Upload
/// (from an uploaded file).
/// </summary>
public enum PluginPackageInstallSourceKind
{
    Catalogue,
    Upload
}

public sealed class PluginPackageOptions
{
    public const string SectionName = "PluginPackages";

    public string RootPath { get; init; } = string.Empty;

    public string CatalogRootPath { get; init; } = string.Empty;

    public string InstalledRootPath { get; init; } = string.Empty;

    public string RuntimeStateRootPath { get; init; } = string.Empty;

    public long MaxPackageBytes { get; init; } = 100L * 1024L * 1024L;

    public static PluginPackageOptions FromConfiguration(
        IConfiguration? configuration,
        string? contentRootPath)
    {
        var section = configuration?.GetSection(SectionName);
        var basePath = string.IsNullOrWhiteSpace(contentRootPath)
            ? AppContext.BaseDirectory
            : contentRootPath;
        var rootPath = ResolvePath(
            basePath,
            section?["RootPath"],
            Path.Combine(basePath, "App_Data", "plugins"));

        return new PluginPackageOptions
        {
            RootPath = rootPath,
            CatalogRootPath = ResolvePath(
                rootPath,
                section?["CatalogRootPath"],
                Path.Combine(rootPath, "catalogue")),
            InstalledRootPath = ResolvePath(
                rootPath,
                section?["InstalledRootPath"],
                Path.Combine(rootPath, "installed")),
            RuntimeStateRootPath = ResolvePath(
                rootPath,
                section?["RuntimeStateRootPath"],
                Path.Combine(rootPath, "state")),
            MaxPackageBytes = ResolveMaxPackageBytes(section?["MaxPackageBytes"])
        };
    }

    private static string ResolvePath(
        string basePath,
        string? configuredPath,
        string defaultPath)
    {
        var path = string.IsNullOrWhiteSpace(configuredPath)
            ? defaultPath
            : configuredPath.Trim();

        return Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(basePath, path));
    }

    private static long ResolveMaxPackageBytes(string? configuredValue)
    {
        if (long.TryParse(configuredValue, out var value) && value > 0)
        {
            return value;
        }

        return 100L * 1024L * 1024L;
    }
}

public sealed class PluginPackageManifest
{
    public PluginDescriptor Plugin { get; init; } = default!;

    public string EntryAssembly { get; init; } = string.Empty;

    public IReadOnlyList<string> Assemblies { get; init; } = [];

    public string IconPath { get; init; } = string.Empty;

    public bool RequiresRestart { get; init; } = true;
}

/// <summary>
/// A plugin package known to the server: a <c>.zip</c> file in the catalogue folder or an installed package folder,
/// listed by <c>GET /api/plugins/packages/catalog</c>. Bundled plugins are not listed.
/// </summary>
/// <param name="PackageId">Identifier of the package; use it to install it from the catalogue.</param>
/// <param name="PluginId">Identifier of the plugin that the package contains.</param>
/// <param name="DisplayName">Display name of the plugin.</param>
/// <param name="Description">Description of the plugin.</param>
/// <param name="Version">Version text of the plugin.</param>
/// <param name="Vendor">Vendor of the plugin.</param>
/// <param name="CatalogSourceKind">
/// Where the package was found, as a JSON integer: 0 Catalogue (a package file exists), 1 Installed (only an installed
/// folder exists).
/// </param>
/// <param name="PluginSourceKind">
/// Source kind declared by the manifest, as a JSON integer: 0 Bundled, 1 LocalPackage, 2 RemotePackage,
/// 3 ShopCatalog.
/// </param>
/// <param name="TrustLevel">
/// Trust level declared by the manifest, as a JSON integer: 0 Application, 1 Bundled, 2 LocalPackage, 3 RemotePackage,
/// 4 Untrusted.
/// </param>
/// <param name="Capabilities">
/// Declared capabilities, as a JSON integer bit mask: 1 WorkflowExecutor, 2 SettingsRenderer, 4 SecretReference,
/// 8 WorkspaceFiles, 16 Storage, 32 ProjectStructure, 64 HttpClient, 128 OAuth2, 256 ExecutionEvents, 512 HostCommand;
/// 0 for none.
/// </param>
/// <param name="IsInstalled">True when the plugin of this package is recorded as installed.</param>
/// <param name="RequiresRestart">
/// True when installing the package requires a restart of the host before its code can run.
/// </param>
/// <param name="HasRuntimeAssemblies">
/// True when the package contains .NET assemblies that the host loads at startup.
/// </param>
/// <param name="IconPath">Relative path of the icon file inside the package, as declared by its manifest.</param>
/// <param name="SourceName">File name of the package in the catalogue folder, or <c>installed</c>.</param>
public sealed record PluginPackageCatalogItem(
    PluginPackageId PackageId,
    PluginId PluginId,
    string DisplayName,
    string Description,
    string Version,
    string Vendor,
    PluginPackageCatalogSourceKind CatalogSourceKind,
    PluginSourceKind PluginSourceKind,
    PluginTrustLevel TrustLevel,
    PluginCapabilityKind Capabilities,
    bool IsInstalled,
    bool RequiresRestart,
    bool HasRuntimeAssemblies,
    string IconPath,
    string SourceName);

/// <summary>
/// Body of <c>POST /api/plugins/packages/catalog/{packageId}/install</c>. The JSON object is required; send <c>{}</c>
/// for the defaults.
/// </summary>
/// <param name="Enable">
/// True (default): the plugin is installed enabled. False: it is installed disabled.
/// </param>
/// <param name="Actor">Ignored: the API records <c>api</c> as the actor.</param>
public sealed record PluginPackageInstallRequest(
    bool Enable = true,
    string Actor = "system");

/// <summary>
/// Result of installing a plugin package from the catalogue or from an upload.
/// </summary>
/// <param name="PackageId">Identifier of the installed package.</param>
/// <param name="PluginId">Identifier of the installed plugin.</param>
/// <param name="DisplayName">Display name of the plugin.</param>
/// <param name="Version">Version text of the plugin.</param>
/// <param name="SourceKind">
/// How the package was installed, as a JSON integer: 0 Catalogue, 1 Upload.
/// </param>
/// <param name="RestartRequired">
/// True when the host must restart before the package's code (assemblies, workflow executors) can run.
/// </param>
/// <param name="RestartStatus">Restart state of the host after the installation.</param>
public sealed record PluginPackageInstallResult(
    PluginPackageId PackageId,
    PluginId PluginId,
    string DisplayName,
    string Version,
    PluginPackageInstallSourceKind SourceKind,
    bool RestartRequired,
    PluginRuntimeRestartStatus RestartStatus);

public sealed record PluginPackageAsset(
    string FilePath,
    string ContentType,
    DateTimeOffset LastModifiedUtc);

/// <summary>
/// Plugin-related restart state of the host, kept in a marker file on the server. It is cleared when the host
/// starts.
/// </summary>
/// <param name="IsRestartRequired">
/// True when an installed plugin package needs a host restart before its code can run.
/// </param>
/// <param name="IsRestartRequested">
/// True when a restart was requested through <c>POST /api/plugins/runtime/restart</c>.
/// </param>
/// <param name="Reason">
/// Why the restart is required, as a sentence naming the package; empty when none is required.
/// </param>
/// <param name="RequiredAtUtc">When the restart became required; null when none is required.</param>
/// <param name="RequestedBy">Actor that requested the restart (<c>api</c> for this API); empty until requested.</param>
/// <param name="RequestedAtUtc">When the restart was requested; null until requested.</param>
/// <param name="ProcessId">
/// Operating system process identifier of the host process that wrote the state; with no pending state, the current
/// process. A different value after a restart shows that the host was restarted.
/// </param>
public sealed record PluginRuntimeRestartStatus(
    bool IsRestartRequired,
    bool IsRestartRequested,
    string Reason,
    DateTimeOffset? RequiredAtUtc,
    string RequestedBy,
    DateTimeOffset? RequestedAtUtc,
    int ProcessId);

/// <summary>
/// Body of <c>POST /api/plugins/runtime/restart</c>. The JSON object is required; send <c>{}</c>.
/// </summary>
/// <param name="Actor">Ignored: the API records <c>api</c> as the requester.</param>
public sealed record PluginRuntimeRestartRequest(
    string Actor = "system");
