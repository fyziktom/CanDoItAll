using CanDoItAll.Plugins.Abstractions;
using CanDoItAll.Plugins.UI;

namespace CanDoItAll.Modules.Plugins.Presentation;

public sealed class PluginWorkspaceReads : IDisposable {
    private readonly PluginsWorkspaceView view;
    private readonly IPluginWorkspaceOwner owner;
    private readonly PluginDraftRegistry drafts;
    private readonly Action selectionUnavailable;
    private readonly PluginReadLane<IReadOnlyList<PluginCatalogItem>> catalog;
    private readonly PluginReadLane<PluginSettingsDetail?> settings;
    private readonly PluginReadLane<IReadOnlyList<PluginOAuthConnectionStatusItem>> oauth;
    private readonly PluginReadLane<IReadOnlyList<PluginLogItem>> installation;
    private readonly PluginReadLane<IReadOnlyList<PluginLogItem>> runtime;
    private readonly PluginReadLane<IReadOnlyList<PluginPackageCatalogItem>> packages;
    private readonly PluginReadLane<PluginRuntimeRestartStatus?> restart;
    private readonly HashSet<string> knownTags = new(StringComparer.OrdinalIgnoreCase);

    public PluginWorkspaceReads(PluginsWorkspaceView view, IPluginWorkspaceOwner owner, PluginDraftRegistry drafts, Action changed, Action selectionUnavailable) {
        this.view = view;
        this.owner = owner;
        this.drafts = drafts;
        this.selectionUnavailable = selectionUnavailable;
        catalog = new(view.Catalog, changed, error => owner.ReportFailure("catalog read", error));
        settings = new(view.Settings, changed, error => owner.ReportFailure("settings read", error));
        oauth = new(view.OAuth, changed, error => owner.ReportFailure("OAuth status read", error));
        installation = new(view.InstallationLogs, changed, error => owner.ReportFailure("installation log read", error));
        runtime = new(view.RuntimeLogs, changed, error => owner.ReportFailure("runtime log read", error));
        packages = new(view.Packages, changed, error => owner.ReportFailure("package read", error));
        restart = new(view.Restart, changed, error => owner.ReportFailure("restart read", error));
    }

    public Task<bool> CatalogAsync() {
        return catalog.LoadAsync(owner.CatalogAsync, items => {
            if (view.SelectedPlugin is { } selected && !items.Any(item => item.PluginId == selected.PluginId)) {
                selectionUnavailable();
            }
            if (view.SelectedPluginId is null) {
                view.SelectedPluginId = items.FirstOrDefault()?.PluginId;
            }
            var tags = PluginCatalogTreeNodeBuilder.BuildTagNodeIds(items).ToHashSet(StringComparer.OrdinalIgnoreCase);
            knownTags.RemoveWhere(tag => !tags.Contains(tag));
            view.ExpandedNodeIds.RemoveWhere(tag => !tags.Contains(tag));
            foreach (var tag in tags.Where(knownTags.Add)) {
                view.ExpandedNodeIds.Add(tag);
            }
        });
    }

    public Task SelectedAsync() => Task.WhenAll(SettingsAsync(), OAuthAsync(), LogsAsync());

    public Task<bool> SettingsAsync() {
        if (view.SelectedPluginId is not { } id) {
            return Task.FromResult(false);
        }
        return settings.LoadAsync(token => owner.SettingsAsync(id, token), detail => {
            if (detail is not null) {
                drafts.Reconcile(detail);
            }
        });
    }

    public Task<bool> OAuthAsync() => view.SelectedPluginId is { } id
        ? oauth.LoadAsync(token => owner.OAuthAsync(id, token)) : Task.FromResult(false);

    public Task LogsAsync() {
        var id = view.ShowAllLogs ? null : view.SelectedPluginId;
        return Task.WhenAll(
            installation.LoadAsync(token => owner.LogsAsync(new(PluginLogStreamKind.Installation, id, Take: 50), token)),
            runtime.LoadAsync(token => owner.LogsAsync(new(PluginLogStreamKind.Runtime, id, Take: 50), token)));
    }

    public Task<bool> PackagesAsync() => packages.LoadAsync(owner.PackagesAsync);
    public Task<bool> RestartAsync() => restart.LoadAsync(async token => await owner.RestartStatusAsync(token));
    public void InvalidateSettings() => settings.Invalidate();
    public void InvalidateOAuth() => oauth.Invalidate();
    public void InvalidateCatalog() => catalog.Invalidate();
    public void InvalidatePackages() => packages.Invalidate();
    public void InvalidateRestart() => restart.Invalidate();
    public void ClosePackages() => packages.Invalidate();

    public void ChangeSelection() {
        settings.Invalidate(clear: true, null);
        oauth.Invalidate(clear: true, []);
        if (!view.ShowAllLogs) {
            ChangeLogScope();
        }
    }

    public void ChangeLogScope() {
        installation.Invalidate(clear: true, []);
        runtime.Invalidate(clear: true, []);
    }

    public void Dispose() {
        catalog.Dispose();
        settings.Dispose();
        oauth.Dispose();
        installation.Dispose();
        runtime.Dispose();
        packages.Dispose();
        restart.Dispose();
    }
}
