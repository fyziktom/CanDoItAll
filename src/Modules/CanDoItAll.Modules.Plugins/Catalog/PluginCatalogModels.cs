using CanDoItAll.AgentFramework.Models;
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
    DateTimeOffset? UpdatedAtUtc,
    UiIconDescriptor? Icon = null)
{
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

    public bool IsInstalled => InstallationState != PluginInstallationStateKind.NotInstalled;

    public bool IsEnabled => InstallationState == PluginInstallationStateKind.InstalledEnabled;
}

public sealed record PluginInstallRequest(
    bool Enable = true,
    string Actor = "system");

public sealed record PluginInstallationUpdateRequest(
    string Actor = "system");
