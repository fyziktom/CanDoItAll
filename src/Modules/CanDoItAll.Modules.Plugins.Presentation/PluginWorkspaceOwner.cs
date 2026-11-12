using CanDoItAll.Plugins.Abstractions;
using CanDoItAll.Plugins.UI;

namespace CanDoItAll.Modules.Plugins.Presentation;

public sealed record PluginWriteReceipt<T>(PluginMutationStatus Status, T? Value = default) {
    public PluginPackageProgress? PackageProgress { get; init; }
    public PluginOAuthProgress? OAuthProgress { get; init; }
    public bool Committed => Status is PluginMutationStatus.Saved or PluginMutationStatus.SavedWithWarning;
    public static PluginWriteReceipt<T> Saved(T value) => new(PluginMutationStatus.Saved, value);
    public static PluginWriteReceipt<T> Refused() => new(PluginMutationStatus.Refused);
    public static PluginWriteReceipt<T> Unknown() => new(PluginMutationStatus.Unknown);
}

public interface IPluginWorkspaceOwner {
    long MaxPackageBytes { get; }
    Task<IReadOnlyList<PluginCatalogItem>> CatalogAsync(CancellationToken cancellationToken);
    Task<PluginSettingsDetail?> SettingsAsync(PluginId pluginId, CancellationToken cancellationToken);
    Task<IReadOnlyList<PluginOAuthConnectionStatusItem>> OAuthAsync(PluginId pluginId, CancellationToken cancellationToken);
    Task<IReadOnlyList<PluginLogItem>> LogsAsync(PluginLogQuery query, CancellationToken cancellationToken);
    Task<IReadOnlyList<PluginPackageCatalogItem>> PackagesAsync(CancellationToken cancellationToken);
    Task<PluginRuntimeRestartStatus> RestartStatusAsync(CancellationToken cancellationToken);
    Task<PluginWriteReceipt<PluginConnectionItem>> SaveAsync(PluginId pluginId, PluginConnectionSaveRequest request);
    Task<PluginWriteReceipt<PluginCapabilityGrantItem>> GrantAsync(PluginId pluginId, PluginGrantUpdateRequest request);
    Task<PluginWriteReceipt<PluginCatalogItem>> LifecycleAsync(PluginId pluginId, PluginLifecycleAction action);
    Task<PluginWriteReceipt<PluginOAuthStartResponse>> StartOAuthAsync(PluginId pluginId, PluginOAuthStartRequest request);
    Task OpenOAuthAsync(string authorizationUrl);
    Task<PluginWriteReceipt<PluginOAuthDisconnectResponse>> DisconnectOAuthAsync(PluginId pluginId, PluginConnectionId connectionId);
    Task<PluginWriteReceipt<PluginPackageInstallResult>> InstallAsync(PluginPackageId packageId);
    Task<PluginWriteReceipt<PluginPackageInstallResult>> UploadAsync(Stream stream, string fileName, CancellationToken cancellationToken);
    Task<PluginWriteReceipt<PluginRuntimeRestartStatus>> RestartAsync();
    void ReportFailure(string operation, Exception exception);
}
