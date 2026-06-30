using CanDoItAll.SharedKernel.Configuration;

namespace CanDoItAll.Plugins.Abstractions;

public enum PluginRendererTrustLevel
{
    Bundled,
    LocalPackage,
    RemotePackage,
    Untrusted
}

public sealed record PluginSettingsDescriptor(
    ConfigurationSchema Schema,
    IReadOnlyList<PluginSettingsRendererDescriptor> Renderers)
{
    public static PluginSettingsDescriptor Empty { get; } = new(ConfigurationSchema.Empty(), []);
}

public sealed record PluginSettingsRendererDescriptor(
    PluginRendererKey RendererKey,
    string DisplayName,
    string ComponentTypeName,
    PluginRendererTrustLevel TrustLevel);
