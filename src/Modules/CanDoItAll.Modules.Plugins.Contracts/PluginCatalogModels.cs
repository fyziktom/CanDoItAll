using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Plugins.Abstractions;

namespace CanDoItAll.Modules.Plugins;

/// <summary>
/// Installation state of a plugin, as a JSON integer: 0 NotInstalled, 1 InstalledEnabled, 2 InstalledDisabled.
/// </summary>
public enum PluginInstallationStateKind
{
    NotInstalled,
    InstalledEnabled,
    InstalledDisabled
}

/// <summary>
/// Availability of a plugin's manifest, as a JSON integer: 0 Available (a current source, bundled or an installed
/// package, provides it), 1 Unavailable (the plugin is recorded as installed, but no current source provides its
/// manifest, for example after its package was removed).
/// </summary>
public enum PluginCatalogAvailabilityKind
{
    Available,
    Unavailable
}

/// <summary>
/// A plugin in the plugin catalog: its manifest summary, installation state and availability. Returned by
/// <c>GET /api/plugins/catalog</c> and by the install, enable and disable operations.
/// </summary>
/// <param name="PluginId">Identifier of the plugin; use it in the <c>/api/plugins/{pluginId}</c> operations.</param>
/// <param name="DisplayName">Display name of the plugin.</param>
/// <param name="Description">Description of the plugin.</param>
/// <param name="Version">Version text declared by the plugin, for example <c>1.0.0</c>.</param>
/// <param name="Vendor">Vendor of the plugin.</param>
/// <param name="SourceKind">
/// Where the plugin comes from, as a JSON integer: 0 Bundled (compiled into the host), 1 LocalPackage (installed from
/// a package), 2 RemotePackage, 3 ShopCatalog.
/// </param>
/// <param name="TrustLevel">
/// Trust level, as a JSON integer: 0 Application, 1 Bundled, 2 LocalPackage, 3 RemotePackage, 4 Untrusted.
/// </param>
/// <param name="Capabilities">
/// Declared capabilities, as a JSON integer bit mask: 1 WorkflowExecutor, 2 SettingsRenderer, 4 SecretReference,
/// 8 WorkspaceFiles, 16 Storage, 32 ProjectStructure, 64 HttpClient, 128 OAuth2, 256 ExecutionEvents, 512 HostCommand;
/// 0 for none.
/// </param>
/// <param name="PackageId">
/// Identifier of the plugin's package, for example <c>gmail.mail.bundled</c>; null when it declares none.
/// </param>
/// <param name="InstallationState">
/// Installation state, as a JSON integer: 0 NotInstalled, 1 InstalledEnabled, 2 InstalledDisabled.
/// </param>
/// <param name="Availability">
/// Whether a current source provides the manifest, as a JSON integer: 0 Available, 1 Unavailable.
/// </param>
/// <param name="UnavailableReason">Why the plugin is unavailable; empty when it is available.</param>
/// <param name="InstalledAtUtc">
/// When the plugin was first installed; it survives reinstallation. Null when it is not installed.
/// </param>
/// <param name="UpdatedAtUtc">
/// When the plugin was last installed, enabled or disabled; null when it is not installed.
/// </param>
/// <param name="Icon">Icon of the plugin; the default icon when the plugin declares none.</param>
public sealed record PluginCatalogItem(
    PluginId PluginId,
    string DisplayName,
    string Description,
    string Version,
    string Vendor,
    PluginSourceKind SourceKind,
    PluginTrustLevel TrustLevel,
    PluginCapabilityKind Capabilities,
    PluginPackageId? PackageId,
    PluginInstallationStateKind InstallationState,
    PluginCatalogAvailabilityKind Availability,
    string UnavailableReason,
    DateTimeOffset? InstalledAtUtc,
    DateTimeOffset? UpdatedAtUtc,
    UiIconDescriptor? Icon = null)
{
    /// <summary>
    /// Full manifest of the plugin: workflow executors, settings, connection kinds, package and OAuth metadata. For an
    /// unavailable plugin it is the manifest saved at installation, or a minimal placeholder when that cannot be read.
    /// </summary>
    public PluginDescriptor Descriptor { get; init; } = new(
        PluginId,
        DisplayName,
        Description,
        Version,
        Vendor,
        SourceKind,
        TrustLevel,
        "1.0.0",
        Capabilities,
        [],
        PluginSettingsDescriptor.Empty,
        [],
        PackageId is null
            ? null
            : new PluginPackageDescriptor(PackageId.Value, Version, "1.0.0", string.Empty, string.Empty),
        Icon: Icon ?? UiIconDescriptor.Default);

    /// <summary>
    /// True when the plugin is installed, enabled or disabled (derived from <c>installationState</c>).
    /// </summary>
    public bool IsInstalled => InstallationState != PluginInstallationStateKind.NotInstalled;

    /// <summary>True when the plugin is installed and enabled (derived from <c>installationState</c>).</summary>
    public bool IsEnabled => InstallationState == PluginInstallationStateKind.InstalledEnabled;
}

/// <summary>
/// Body of <c>POST /api/plugins/{pluginId}/install</c>. The JSON object is required; send <c>{}</c> for the defaults.
/// </summary>
/// <param name="Enable">
/// True (default): the plugin is installed enabled. False: it is installed, or reinstalled, disabled.
/// </param>
/// <param name="Actor">Ignored: the API records <c>api</c> as the actor.</param>
public sealed record PluginInstallRequest(
    bool Enable = true,
    string Actor = "system");

/// <summary>
/// Body of <c>POST /api/plugins/{pluginId}/enable</c> and <c>POST /api/plugins/{pluginId}/disable</c>. The JSON object
/// is required; send <c>{}</c>.
/// </summary>
/// <param name="Actor">Ignored: the API records <c>api</c> as the actor.</param>
public sealed record PluginInstallationUpdateRequest(
    string Actor = "system");
