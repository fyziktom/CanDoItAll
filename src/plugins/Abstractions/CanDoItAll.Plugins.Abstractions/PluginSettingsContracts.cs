using CanDoItAll.SharedKernel.Configuration;

namespace CanDoItAll.Plugins.Abstractions;

/// <summary>
/// Trust level of a settings renderer, as a JSON integer: 0 Bundled, 1 LocalPackage, 2 RemotePackage, 3 Untrusted.
/// Only bundled renderers may back custom settings user interfaces.
/// </summary>
public enum PluginRendererTrustLevel
{
    Bundled,
    LocalPackage,
    RemotePackage,
    Untrusted
}

/// <summary>
/// Plugin-level settings of a plugin: the settings form and the renderers that display it.
/// </summary>
/// <param name="Schema">Form of the plugin-level settings; a schema without fields when the plugin has none.</param>
/// <param name="Renderers">Settings renderers that the plugin provides; empty when it provides none.</param>
public sealed record PluginSettingsDescriptor(
    ConfigurationSchema Schema,
    IReadOnlyList<PluginSettingsRendererDescriptor> Renderers)
{
    public static PluginSettingsDescriptor Empty { get; } = new(ConfigurationSchema.Empty(), []);
}

/// <summary>
/// A settings renderer, a user interface component that edits plugin or executor settings in the web application.
/// </summary>
/// <param name="RendererKey">Key of the renderer, for example <c>gmail.settings</c>.</param>
/// <param name="DisplayName">Display name of the renderer.</param>
/// <param name="ComponentTypeName">Name of the user interface component type that implements the renderer.</param>
/// <param name="TrustLevel">
/// Trust level of the renderer, as a JSON integer: 0 Bundled, 1 LocalPackage, 2 RemotePackage, 3 Untrusted.
/// </param>
public sealed record PluginSettingsRendererDescriptor(
    PluginRendererKey RendererKey,
    string DisplayName,
    string ComponentTypeName,
    PluginRendererTrustLevel TrustLevel);
