using CanDoItAll.Modules.Plugins;
using CanDoItAll.Plugins.Abstractions;
using CanDoItAll.SharedKernel.Configuration;

namespace CanDoItAll.Modules.Plugins.Pages;

internal sealed class PluginConnectionEditorState
{
    private PluginConnectionEditorState(
        PluginConnectionId? connectionId,
        PluginConnectionKey connectionKey,
        string displayName,
        ConfigurationState state,
        bool isEnabled)
    {
        ConnectionId = connectionId;
        ConnectionKey = connectionKey;
        DisplayName = displayName;
        State = state;
        IsEnabled = isEnabled;
    }

    public PluginConnectionId? ConnectionId { get; }

    public PluginConnectionKey ConnectionKey { get; }

    public string DisplayName { get; set; }

    public ConfigurationState State { get; set; }

    public bool IsEnabled { get; set; }

    public ConfigurationValidationResult Validation { get; set; } = ConfigurationValidationResult.Success;

    public bool IsDirty { get; set; }

    public static PluginConnectionEditorState Create(
        PluginConnectionDescriptor descriptor,
        PluginConnectionItem? connection)
        => new(
            connection?.Id,
            descriptor.Key,
            string.IsNullOrWhiteSpace(connection?.DisplayName) ? descriptor.DisplayName : connection.DisplayName,
            ConfigurationState.FromJson(connection?.SettingsJson),
            connection?.IsEnabled ?? true);
}
