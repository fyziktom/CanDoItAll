using CanDoItAll.Modules.Plugins;
using CanDoItAll.Plugins.Abstractions;
using Microsoft.AspNetCore.Components.Forms;

namespace CanDoItAll.Plugins.UI;

public enum PluginSection { Main, Executors, Settings, Connections, Logs, Grants }
public enum PluginReadStatus { Initial, Loading, Ready, Stale, Unavailable }
public enum PluginMutationStatus { Idle, Pending, Saved, SavedWithWarning, Refused, Unknown }
public enum PluginLifecycleAction { Install, Enable, Disable }

public sealed class PluginReadState<T>(T initial) {
    public T Value { get; set; } = initial;
    public PluginReadStatus Status { get; set; }
    public bool IsCurrent => Status == PluginReadStatus.Ready;
}

public readonly record struct PluginEditorKey(PluginId PluginId, PluginConnectionKey ConnectionKey);
public sealed record PluginDraftOrigin(PluginEditorKey Key, PluginConnectionId? InitialConnectionId, Guid Generation);
public sealed record PluginGrantTarget(PluginId PluginId, PluginCapabilityKind Capability,
    PluginHostToolRecipeId? RecipeId, PluginGrantScopeKind ScopeKind, string ScopeKey) {
    public static PluginGrantTarget From(PluginCapabilityGrantItem grant)
        => new(grant.PluginId, grant.Capability, grant.RecipeId, grant.ScopeKind, grant.ScopeKey.Trim());
}

public abstract record PluginOperationTarget {
    public sealed record Lifecycle(PluginId PluginId) : PluginOperationTarget;
    public sealed record Grant(PluginGrantTarget Target) : PluginOperationTarget;
    public sealed record Connection(PluginDraftOrigin Origin) : PluginOperationTarget;
    public sealed record Package(PluginPackageId PackageId) : PluginOperationTarget;
    public sealed record Upload : PluginOperationTarget;
    public sealed record Restart : PluginOperationTarget;
}

public sealed class PluginOperationState {
    public Guid Id { get; } = Guid.NewGuid();
    public PluginMutationStatus Status { get; set; } = PluginMutationStatus.Pending;
    public string Message { get; set; } = string.Empty;
    public bool HasCommittedValue { get; set; }
    public PluginPackageProgress? PackageProgress { get; set; }
    public PluginOAuthProgress? OAuthProgress { get; set; }
    public bool PreventsReplay => Status is PluginMutationStatus.Pending or PluginMutationStatus.Unknown ||
        PackageProgress?.Stage >= PluginPackageStage.Installed;
}

public interface IPluginsWorkspace {
    PluginsWorkspaceView View { get; }
    event Action? Changed;
    Task RefreshAsync();
    Task SelectAsync(PluginCatalogItem plugin);
    void SelectSection(PluginSection section);
    void ToggleNode(string nodeId);
    Task SetLogScopeAsync(bool showAll);
    Task OpenPackagesAsync();
    void ClosePackages();
    Task SetLifecycleAsync(PluginCatalogItem plugin, PluginLifecycleAction action);
    Task SetGrantAsync(PluginCatalogItem plugin, PluginCapabilityGrantItem grant, PluginGrantState state);
    Task SaveAsync(PluginConnectionEditorState editor);
    void ResetDraft(PluginConnectionEditorState editor, PluginConnectionId? connectionId, bool reviewedUnknown);
    Task StartOAuthAsync(PluginConnectionEditorState editor);
    Task DisconnectOAuthAsync(PluginConnectionEditorState editor);
    Task InstallPackageAsync(PluginPackageCatalogItem package);
    Task UploadAsync(IBrowserFile file);
    Task RestartAsync();
    void AcknowledgeUnknown(PluginOperationTarget target);
}

public sealed class PluginsWorkspaceView(string callbackUri) {
    public string CallbackUri { get; } = callbackUri;
    public PluginReadState<IReadOnlyList<PluginCatalogItem>> Catalog { get; } = new([]);
    public PluginReadState<PluginSettingsDetail?> Settings { get; } = new(null);
    public PluginReadState<IReadOnlyList<PluginOAuthConnectionStatusItem>> OAuth { get; } = new([]);
    public PluginReadState<IReadOnlyList<PluginLogItem>> InstallationLogs { get; } = new([]);
    public PluginReadState<IReadOnlyList<PluginLogItem>> RuntimeLogs { get; } = new([]);
    public PluginReadState<IReadOnlyList<PluginPackageCatalogItem>> Packages { get; } = new([]);
    public PluginReadState<PluginRuntimeRestartStatus?> Restart { get; } = new(null);
    public Dictionary<PluginEditorKey, PluginConnectionEditorState> Editors { get; } = [];
    public Dictionary<PluginOperationTarget, PluginOperationState> Operations { get; } = [];
    public HashSet<string> ExpandedNodeIds { get; } = new(StringComparer.OrdinalIgnoreCase);
    public PluginId? SelectedPluginId { get; set; }
    public PluginSection Section { get; set; }
    public bool ShowAllLogs { get; set; }
    public bool PackagesOpen { get; set; }
    public bool UploadReading { get; set; }
    public string UploadFileName { get; set; } = string.Empty;
    public string Notice { get; set; } = string.Empty;
    public PluginMutationStatus NoticeStatus { get; set; }
    public bool IsDisposed { get; set; }
    public PluginCatalogItem? SelectedPlugin => Catalog.Value.FirstOrDefault(item => item.PluginId == SelectedPluginId);
    public bool IsBusy(PluginOperationTarget target)
        => Operations.TryGetValue(target, out var operation) && operation.Status == PluginMutationStatus.Pending;
    public bool IsBlocked(PluginOperationTarget target)
        => Operations.TryGetValue(target, out var operation) && operation.PreventsReplay;
    public PluginConnectionEditorState Editor(PluginId pluginId, PluginConnectionDescriptor descriptor)
        => Editors[new(pluginId, descriptor.Key)];
    public PluginOAuthConnectionStatusItem? OAuthStatus(PluginConnectionEditorState editor)
        => OAuth.Value.SingleOrDefault(item => item.ConnectionId == editor.ConnectionId);
}
