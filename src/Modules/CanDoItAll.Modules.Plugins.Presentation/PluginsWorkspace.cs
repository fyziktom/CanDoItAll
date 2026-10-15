using CanDoItAll.AppComponents.Navigation;
using CanDoItAll.Plugins.Abstractions;
using CanDoItAll.Plugins.UI;
using Microsoft.AspNetCore.Components.Forms;

namespace CanDoItAll.Modules.Plugins.Presentation;

public sealed class PluginsWorkspace : IPluginsWorkspace, IDisposable {
    private readonly IPluginWorkspaceOwner owner;
    private readonly IPluginsWorkspaceRoute? route;
    private readonly PluginDraftRegistry drafts;
    private readonly PluginWorkspaceReads reads;
    private readonly PluginWorkspaceOperations operations;
    private long selectionGeneration;
    private long dialogGeneration;
    private CancellationTokenSource? upload;

    public PluginsWorkspace(IPluginWorkspaceOwner owner, string callbackUri, IPluginsWorkspaceRoute? route = null) {
        this.owner = owner;
        this.route = route;
        View = new(callbackUri);
        drafts = new(View);
        reads = new(View, owner, drafts, Notify, () => selectionGeneration++);
        operations = new(View, owner, Notify, AcceptPackageProgress);
    }

    public PluginsWorkspaceView View { get; }
    public event Action? Changed;

    public PluginsUrlState UrlState => new(View.SelectedPluginId, View.Section,
        View.Section == PluginSection.Logs && View.ShowAllLogs ? PluginLogScope.All : PluginLogScope.Selected,
        View.PackagesOpen ? PluginsDialog.Packages : null);

    public Task OpenAsync(PluginsUrlState state) {
        ArgumentNullException.ThrowIfNull(state);
        if (View.IsDisposed) {
            return Task.CompletedTask;
        }
        View.SelectedPluginId = state.PluginId;
        View.Section = state.Section;
        View.ShowAllLogs = state.Logs == PluginLogScope.All;
        if (state.Dialog == PluginsDialog.Packages) {
            dialogGeneration++;
            View.PackagesOpen = true;
        }
        return RefreshAsync();
    }

    public Task ApplyRouteAsync(PluginsUrlState state) {
        ArgumentNullException.ThrowIfNull(state);
        if (View.IsDisposed) {
            return Task.CompletedTask;
        }
        var work = new List<Task>();
        var republish = false;
        var showAll = state.Logs == PluginLogScope.All;
        var scopeChanged = state.Section == PluginSection.Logs && showAll != View.ShowAllLogs;
        if (scopeChanged) {
            View.ShowAllLogs = showAll;
            reads.ChangeLogScope();
        }
        var target = state.PluginId ?? View.Catalog.Value.FirstOrDefault()?.PluginId;
        if (target != View.SelectedPluginId) {
            selectionGeneration++;
            View.SelectedPluginId = target;
            reads.ChangeSelection();
            work.Add(reads.SelectedAsync());
            republish = state.PluginId is null && target is not null;
        } else if (scopeChanged) {
            work.Add(reads.LogsAsync());
        }
        View.Section = state.Section;
        var open = state.Dialog == PluginsDialog.Packages;
        if (open && !View.PackagesOpen) {
            dialogGeneration++;
            View.PackagesOpen = true;
            work.Add(reads.PackagesAsync());
        } else if (!open && View.PackagesOpen) {
            if (View.UploadReading) {
                republish = true;
            } else {
                dialogGeneration++;
                View.PackagesOpen = false;
                reads.ClosePackages();
            }
        }
        if (republish) {
            Publish(UrlHistoryMode.Replace);
        }
        Notify();
        return Task.WhenAll(work);
    }

    public void ReportOAuthReturn(PluginOAuthReturn result) {
        ArgumentNullException.ThrowIfNull(result);
        if (View.IsDisposed) {
            return;
        }
        View.Notice = result.Outcome == PluginOAuthOutcome.Connected
            ? "OAuth sign-in completed. The connection status shown below is read from stored state."
            : result.Reason is { } reason
                ? $"OAuth sign-in failed ({reason}). Review the connection and start sign-in again."
                : "OAuth sign-in failed. Review the connection and start sign-in again.";
        View.NoticeStatus = result.Outcome == PluginOAuthOutcome.Connected ? PluginMutationStatus.Saved : PluginMutationStatus.Refused;
        if (result.ConnectionId is { } connection && View.Section != PluginSection.Connections &&
            View.Settings.Value is { } settings && settings.CatalogItem.PluginId == View.SelectedPluginId &&
            settings.Connections.Any(item => item.Id == connection)) {
            View.Section = PluginSection.Connections;
            Publish(UrlHistoryMode.Replace);
        }
        Notify();
    }

    public async Task RefreshAsync() {
        if (View.IsDisposed) {
            return;
        }
        var generation = selectionGeneration;
        var unselected = View.SelectedPluginId is null;
        var catalog = reads.CatalogAsync();
        var selection = unselected ? InitialSelectionAsync() : reads.SelectedAsync();
        await Task.WhenAll(catalog, selection, reads.PackagesAsync(), reads.RestartAsync());

        async Task InitialSelectionAsync() {
            if (await catalog && generation == selectionGeneration && !View.IsDisposed) {
                if (View.SelectedPluginId is not null) {
                    Publish(UrlHistoryMode.Replace);
                }
                await reads.SelectedAsync();
            }
        }
    }

    public async Task SelectAsync(PluginCatalogItem plugin) {
        if (View.IsDisposed || !View.Catalog.Value.Any(item => item.PluginId == plugin.PluginId)) {
            return;
        }
        selectionGeneration++;
        View.SelectedPluginId = plugin.PluginId;
        View.Section = PluginSection.Main;
        reads.ChangeSelection();
        Publish(UrlHistoryMode.Push);
        Notify();
        await reads.SelectedAsync();
    }

    public void SelectSection(PluginSection section) {
        View.Section = section;
        Publish(UrlHistoryMode.Push);
        Notify();
    }

    public void ToggleNode(string nodeId) {
        if (!View.ExpandedNodeIds.Add(nodeId)) {
            View.ExpandedNodeIds.Remove(nodeId);
        }
        Notify();
    }

    public Task SetLogScopeAsync(bool showAll) {
        View.ShowAllLogs = showAll;
        reads.ChangeLogScope();
        Publish(UrlHistoryMode.Push);
        return reads.LogsAsync();
    }

    public Task OpenPackagesAsync() {
        dialogGeneration++;
        View.PackagesOpen = true;
        if (!View.IsDisposed) {
            route?.OpenOverlay(UrlState);
        }
        Notify();
        return reads.PackagesAsync();
    }

    public void ClosePackages() {
        if (View.UploadReading) {
            return;
        }
        var wasOpen = View.PackagesOpen;
        dialogGeneration++;
        View.PackagesOpen = false;
        reads.ClosePackages();
        if (wasOpen && !View.IsDisposed) {
            route?.CloseOverlay(UrlState);
        }
        Notify();
    }

    public Task SaveAsync(PluginConnectionEditorState editor) {
        if (!drafts.IsLive(editor) || editor.Capture() is not { } submission) {
            Notify();
            return Task.CompletedTask;
        }
        var generation = selectionGeneration;
        reads.InvalidateSettings();
        return operations.RunAsync(new PluginOperationTarget.Connection(editor.Origin),
            () => owner.SaveAsync(editor.Origin.Key.PluginId, submission.Request),
            saved => {
                editor.Accept(submission, saved);
                reads.InvalidateSettings();
            },
            () => RefreshSettingsIfSelectedAsync(editor.Origin.Key.PluginId),
            $"Saved {editor.Descriptor.DisplayName} settings.",
            () => generation == selectionGeneration && drafts.IsLive(editor),
            operation => editor.Operation = operation);
    }

    public void ResetDraft(PluginConnectionEditorState editor, PluginConnectionId? connectionId, bool reviewedUnknown) {
        drafts.Reset(editor, connectionId, reviewedUnknown);
        Notify();
    }

    public Task SetGrantAsync(PluginCatalogItem plugin, PluginCapabilityGrantItem grant, PluginGrantState state) {
        if (grant.PluginId != plugin.PluginId || state is not (PluginGrantState.Granted or PluginGrantState.Denied or PluginGrantState.Revoked)) {
            return Task.CompletedTask;
        }
        var generation = selectionGeneration;
        var target = PluginGrantTarget.From(grant);
        var request = new PluginGrantUpdateRequest(grant.Capability, state, grant.RecipeId?.Value,
            grant.ScopeKind, grant.ScopeKey, grant.RiskKind, $"{state} from plugin settings UI.");
        return operations.RunAsync(new PluginOperationTarget.Grant(target), () => owner.GrantAsync(plugin.PluginId, request),
            saved => {
                reads.InvalidateSettings();
                if (View.Settings.Value is { } settings && settings.CatalogItem.PluginId == plugin.PluginId) {
                    View.Settings.Value = settings with { Grants = settings.Grants
                        .Where(item => PluginGrantTarget.From(item) != target).Append(saved).ToArray() };
                }
            }, () => RefreshSettingsIfSelectedAsync(plugin.PluginId), "Grant decision saved.",
            () => generation == selectionGeneration);
    }

    public Task SetLifecycleAsync(PluginCatalogItem plugin, PluginLifecycleAction action) {
        var generation = selectionGeneration;
        return operations.RunAsync(new PluginOperationTarget.Lifecycle(plugin.PluginId), () => owner.LifecycleAsync(plugin.PluginId, action),
            saved => {
                reads.InvalidateCatalog();
                View.Catalog.Value = View.Catalog.Value.Select(item => item.PluginId == saved.PluginId
                    ? item with { InstallationState = saved.InstallationState, InstalledAtUtc = saved.InstalledAtUtc, UpdatedAtUtc = saved.UpdatedAtUtc } : item).ToArray();
            },
            () => RefreshSettingsIfSelectedAsync(plugin.PluginId), "Plugin state saved.", () => generation == selectionGeneration);
    }

    public Task StartOAuthAsync(PluginConnectionEditorState editor) {
        if (!CanUseOAuth(editor) || editor.ConnectionId is not { } connectionId) {
            return Task.CompletedTask;
        }
        var generation = selectionGeneration;
        var request = new PluginOAuthStartRequest(editor.ConnectionKey, connectionId, editor.DisplayName,
            ReturnPath: PluginsUrlCodec.ConnectionsHref(editor.Origin.Key.PluginId));
        return operations.RunAsync(new PluginOperationTarget.Connection(editor.Origin), async () => {
            var result = await owner.StartOAuthAsync(editor.Origin.Key.PluginId, request);
            if (result.Committed && result.Value is { } response && generation == selectionGeneration && CanUseOAuth(editor, ignoreBusy: true)) {
                try {
                    await owner.OpenOAuthAsync(response.AuthorizationUrl);
                } catch (Exception exception) {
                    owner.ReportFailure("OAuth browser effect", exception);
                    return result with { Status = PluginMutationStatus.SavedWithWarning };
                }
            }
            return result;
        }, _ => { },
            () => View.SelectedPluginId == editor.Origin.Key.PluginId ? reads.OAuthAsync() : Task.FromResult(true),
            "Authorization session created. Complete sign-in in the provider tab; refresh status afterwards.",
            () => generation == selectionGeneration && drafts.IsLive(editor), operation => editor.Operation = operation);
    }

    public Task DisconnectOAuthAsync(PluginConnectionEditorState editor) {
        if (!drafts.IsLive(editor) || editor.PreventsReplay || editor.ConnectionId is not { } id ||
            !View.OAuth.IsCurrent || View.OAuthStatus(editor)?.Status != PluginOAuthConnectionStatusKind.Connected) {
            return Task.CompletedTask;
        }
        var generation = selectionGeneration;
        return operations.RunAsync(new PluginOperationTarget.Connection(editor.Origin),
            () => owner.DisconnectOAuthAsync(editor.Origin.Key.PluginId, id), _ => reads.InvalidateOAuth(),
            () => View.SelectedPluginId == editor.Origin.Key.PluginId ? reads.OAuthAsync() : Task.FromResult(true),
            "OAuth connection disconnected.", () => generation == selectionGeneration, operation => editor.Operation = operation);
    }

    public Task InstallPackageAsync(PluginPackageCatalogItem package) {
        if (package.IsInstalled) {
            return Task.CompletedTask;
        }
        var dialog = dialogGeneration;
        return operations.RunAsync(new PluginOperationTarget.Package(package.PackageId), () => owner.InstallAsync(package.PackageId),
            AcceptPackage, RefreshPackagesAsync, "Installed package. Restart is required when shown below.",
            () => dialog == dialogGeneration);
    }

    public async Task UploadAsync(IBrowserFile file) {
        var target = new PluginOperationTarget.Upload();
        if (View.IsDisposed || View.UploadReading || View.IsBlocked(target) || !View.PackagesOpen) {
            return;
        }
        var dialog = dialogGeneration;
        using var source = new CancellationTokenSource();
        upload = source;
        View.UploadReading = true;
        View.UploadFileName = file.Name;
        Notify();
        try {
            await operations.RunAsync(target, async () => {
                Stream stream;
                try {
                    stream = file.OpenReadStream(owner.MaxPackageBytes, source.Token);
                } catch (IOException) {
                    return PluginWriteReceipt<PluginPackageInstallResult>.Refused();
                }
                await using (stream) {
                    return await owner.UploadAsync(stream, file.Name, source.Token);
                }
            }, AcceptPackage, RefreshPackagesAsync, "Uploaded package. Restart is required when shown below.",
                () => dialog == dialogGeneration);
        } finally {
            upload = null;
            View.UploadReading = false;
            Notify();
        }
    }

    public Task RestartAsync() {
        if (View.Restart.Value is not { IsRestartRequired: true, IsRestartRequested: false }) {
            return Task.CompletedTask;
        }
        return operations.RunAsync(new PluginOperationTarget.Restart(), owner.RestartAsync,
            saved => {
                reads.InvalidateRestart();
                View.Restart.Value = saved;
            }, () => Task.FromResult(true), "Restart requested.", () => true);
    }

    public void AcknowledgeUnknown(PluginOperationTarget target) {
        if (target is PluginOperationTarget.Connection || !View.Operations.TryGetValue(target, out var operation) ||
            operation.Status != PluginMutationStatus.Unknown) {
            return;
        }
        View.Operations.Remove(target);
        View.Notice = "Operator reviewed the unknown outcome. Any next operation is a new explicit request.";
        Notify();
    }

    private bool CanUseOAuth(PluginConnectionEditorState editor, bool ignoreBusy = false)
        => drafts.IsLive(editor) && (!editor.PreventsReplay || (ignoreBusy && !editor.ReferenceChanged)) &&
           editor.ConnectionId is not null && !editor.IsDirty && editor.Validation.Succeeded && editor.IsEnabled &&
           View.SelectedPluginId == editor.Origin.Key.PluginId && View.SelectedPlugin is { IsEnabled: true } &&
           View.Settings.IsCurrent && View.OAuth.IsCurrent && View.Settings.Value is { } settings &&
           settings.Grants.Any(grant => grant.Capability == PluginCapabilityKind.OAuth2 && grant.RecipeId is null &&
               grant.ScopeKind == PluginGrantScopeKind.Plugin && string.IsNullOrEmpty(grant.ScopeKey) && grant.State == PluginGrantState.Granted);

    private Task<bool> RefreshSettingsIfSelectedAsync(PluginId pluginId)
        => View.SelectedPluginId == pluginId ? reads.SettingsAsync() : Task.FromResult(true);

    private void AcceptPackage(PluginPackageInstallResult saved) {
        reads.InvalidatePackages();
        reads.InvalidateRestart();
        View.Restart.Value = saved.RestartStatus;
        View.Packages.Value = View.Packages.Value.Select(item => item.PackageId == saved.PackageId ? item with { IsInstalled = true } : item).ToArray();
    }

    private void AcceptPackageProgress(PluginPackageProgress progress) {
        if (progress.Stage >= PluginPackageStage.Installed) {
            reads.InvalidatePackages();
            View.Packages.Value = View.Packages.Value.Select(item => item.PackageId == progress.PackageId ? item with { IsInstalled = true } : item).ToArray();
        }
        if (progress.RestartStatus is { } status) {
            reads.InvalidateRestart();
            View.Restart.Value = status;
        }
    }

    private async Task<bool> RefreshPackagesAsync() {
        var selected = View.SelectedPluginId;
        var catalogRefreshed = await reads.CatalogAsync();
        if (selected is null && View.SelectedPluginId is not null) {
            Publish(UrlHistoryMode.Replace);
        }
        var results = await Task.WhenAll(reads.PackagesAsync(), reads.RestartAsync());
        return catalogRefreshed && results.All(result => result);
    }

    private void Publish(UrlHistoryMode mode) {
        if (!View.IsDisposed) {
            route?.Navigate(UrlState, mode);
        }
    }

    private void Notify() {
        if (!View.IsDisposed) {
            Changed?.Invoke();
        }
    }

    public void Dispose() {
        if (View.IsDisposed) {
            return;
        }
        View.IsDisposed = true;
        selectionGeneration++;
        dialogGeneration++;
        Changed = null;
        upload?.Cancel();
        reads.Dispose();
        foreach (var editor in View.Editors.Values) {
            editor.Retired = true;
        }
    }
}
