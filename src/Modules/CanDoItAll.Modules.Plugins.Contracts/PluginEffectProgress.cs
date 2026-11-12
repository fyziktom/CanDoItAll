using CanDoItAll.Plugins.Abstractions;

namespace CanDoItAll.Modules.Plugins;

public enum PluginPackageStage { ReplacementStarted, Extracted, Installed, RestartRecorded }
public sealed record PluginPackageProgress(PluginPackageId PackageId, PluginId PluginId, PluginPackageStage Stage,
    bool RestartRequired, PluginRuntimeRestartStatus? RestartStatus = null);
public enum PluginOAuthStage { ConnectionResolved, SessionCreated }
public sealed record PluginOAuthProgress(PluginConnectionId ConnectionId, PluginOAuthStage Stage);
