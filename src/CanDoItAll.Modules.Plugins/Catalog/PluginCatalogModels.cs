using CanDoItAll.Plugins.Abstractions;

namespace CanDoItAll.Modules.Plugins;

public enum PluginInstallationStateKind
{
    NotInstalled,
    InstalledEnabled,
    InstalledDisabled
}

public enum PluginCatalogAvailabilityKind
{
    Available,
    Unavailable
}

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
    DateTimeOffset? UpdatedAtUtc)
{
    public bool IsInstalled => InstallationState != PluginInstallationStateKind.NotInstalled;

    public bool IsEnabled => InstallationState == PluginInstallationStateKind.InstalledEnabled;
}

public sealed record PluginInstallRequest(
    bool Enable = true,
    string Actor = "system");

public sealed record PluginInstallationUpdateRequest(
    string Actor = "system");
