using CanDoItAll.AppComponents.Navigation;
using CanDoItAll.Modules.Plugins.Presentation;
using CanDoItAll.Plugins.Abstractions;
using CanDoItAll.Plugins.UI;
using CanDoItAll.Plugins.UiSandbox;

namespace CanDoItAll.Tests.Components.Plugins;

public sealed class PluginsWorkspaceRouteTests {
    private const string CallbackUri = "http://sandbox.invalid/api/plugins/oauth/callback";
    private const string ClientId = "96523be6-67a2-41fb-8c15-0df3a502fa07";

    [Fact]
    public async Task The_first_load_default_is_replaced_and_operator_intents_push_or_open_overlays() {
        var route = new RecordingRoute();
        using var workspace = new PluginsWorkspace(new PluginScenarioStore(), CallbackUri, route);

        await workspace.OpenAsync(new());
        var first = workspace.View.Catalog.Value[0].PluginId;
        var second = workspace.View.Catalog.Value[1];
        await workspace.SelectAsync(second);
        workspace.SelectSection(PluginSection.Logs);
        await workspace.SetLogScopeAsync(true);
        await workspace.OpenPackagesAsync();
        workspace.ClosePackages();

        Assert.Equal([
            new RouteCall(RouteCallKind.Navigate, new(first), UrlHistoryMode.Replace),
            new RouteCall(RouteCallKind.Navigate, new(second.PluginId), UrlHistoryMode.Push),
            new RouteCall(RouteCallKind.Navigate, new(second.PluginId, PluginSection.Logs), UrlHistoryMode.Push),
            new RouteCall(RouteCallKind.Navigate, new(second.PluginId, PluginSection.Logs, PluginLogScope.All), UrlHistoryMode.Push),
            new RouteCall(RouteCallKind.OpenOverlay, new(second.PluginId, PluginSection.Logs, PluginLogScope.All, PluginsDialog.Packages), null),
            new RouteCall(RouteCallKind.CloseOverlay, new(second.PluginId, PluginSection.Logs, PluginLogScope.All), null)
        ], route.Calls);
    }

    [Fact]
    public async Task An_explicit_plugin_and_section_win_over_the_first_catalog_item_without_publishing() {
        var store = new PluginScenarioStore();
        var route = new RecordingRoute();
        using var workspace = new PluginsWorkspace(store, CallbackUri, route);
        var catalog = await store.CatalogAsync(CancellationToken.None);

        await workspace.OpenAsync(new(catalog[1].PluginId, PluginSection.Settings));

        Assert.Equal(catalog[1].PluginId, workspace.View.SelectedPluginId);
        Assert.Equal(PluginSection.Settings, workspace.View.Section);
        Assert.Equal(catalog[1].PluginId, workspace.View.Settings.Value!.CatalogItem.PluginId);
        Assert.Empty(route.Calls);
    }

    [Fact]
    public async Task A_missing_explicit_plugin_stays_selected_and_unavailable() {
        var route = new RecordingRoute();
        using var workspace = new PluginsWorkspace(new PluginScenarioStore(), CallbackUri, route);
        var missing = new PluginId("missing.plugin");

        await workspace.OpenAsync(new(missing));

        Assert.Equal(missing, workspace.View.SelectedPluginId);
        Assert.Null(workspace.View.SelectedPlugin);
        Assert.Empty(route.Calls);
    }

    [Fact]
    public async Task Applying_a_location_selects_scopes_and_reconciles_the_dialog_without_publishing() {
        var route = new RecordingRoute();
        using var workspace = new PluginsWorkspace(new PluginScenarioStore(), CallbackUri, route);
        await workspace.OpenAsync(new());
        var second = workspace.View.Catalog.Value[1].PluginId;
        var published = route.Calls.Count;

        await workspace.ApplyRouteAsync(new(second, PluginSection.Logs, PluginLogScope.All, PluginsDialog.Packages));
        Assert.Equal(second, workspace.View.SelectedPluginId);
        Assert.Equal(PluginSection.Logs, workspace.View.Section);
        Assert.True(workspace.View.ShowAllLogs);
        Assert.True(workspace.View.PackagesOpen);

        await workspace.ApplyRouteAsync(new(second, PluginSection.Grants));
        Assert.Equal(PluginSection.Grants, workspace.View.Section);
        Assert.True(workspace.View.ShowAllLogs);
        Assert.False(workspace.View.PackagesOpen);
        Assert.Equal(published, route.Calls.Count);

        await workspace.ApplyRouteAsync(new());
        Assert.Equal(workspace.View.Catalog.Value[0].PluginId, workspace.View.SelectedPluginId);
        Assert.Equal(new RouteCall(RouteCallKind.Navigate, new(workspace.View.Catalog.Value[0].PluginId), UrlHistoryMode.Replace), route.Calls[^1]);
    }

    [Fact]
    public async Task An_OAuth_return_reports_the_outcome_and_selects_the_connections_of_its_owning_plugin() {
        var route = new RecordingRoute();
        using var workspace = new PluginsWorkspace(new PluginScenarioStore(), CallbackUri, route);
        await workspace.OpenAsync(new());
        var editor = workspace.View.Editors.Values.Single();
        editor.SetField("clientId", ClientId);
        await workspace.SaveAsync(editor);
        var published = route.Calls.Count;

        workspace.ReportOAuthReturn(new(PluginOAuthOutcome.Failed, null, "access_denied"));
        Assert.Contains("access_denied", workspace.View.Notice, StringComparison.Ordinal);
        Assert.Equal(PluginMutationStatus.Refused, workspace.View.NoticeStatus);
        Assert.Equal(PluginSection.Main, workspace.View.Section);
        Assert.Equal(published, route.Calls.Count);

        workspace.ReportOAuthReturn(new(PluginOAuthOutcome.Connected, editor.ConnectionId));
        Assert.Equal(PluginMutationStatus.Saved, workspace.View.NoticeStatus);
        Assert.Equal(PluginSection.Connections, workspace.View.Section);
        Assert.Equal(new RouteCall(RouteCallKind.Navigate, new(editor.Origin.Key.PluginId, PluginSection.Connections), UrlHistoryMode.Replace), route.Calls[^1]);
    }

    [Fact]
    public async Task OAuth_sign_in_returns_to_the_owning_plugin_connections_section() {
        var (store, workspace, editor) = await PluginsWorkspaceTests.ReadyAsync();
        using (workspace) {
            await workspace.SaveAsync(editor);

            await workspace.StartOAuthAsync(editor);

            Assert.Equal(1, store.OAuthStarts);
            Assert.Equal($"/plugins?pluginId={Uri.EscapeDataString(editor.Origin.Key.PluginId.Value)}&section=connections", store.LastOAuthStart!.ReturnPath);
        }
    }

    private enum RouteCallKind { Navigate, OpenOverlay, CloseOverlay }

    private sealed record RouteCall(RouteCallKind Kind, PluginsUrlState State, UrlHistoryMode? Mode);

    private sealed class RecordingRoute : IPluginsWorkspaceRoute {
        public List<RouteCall> Calls { get; } = [];
        public void Navigate(PluginsUrlState state, UrlHistoryMode mode) => Calls.Add(new(RouteCallKind.Navigate, state, mode));
        public void OpenOverlay(PluginsUrlState state) => Calls.Add(new(RouteCallKind.OpenOverlay, state, null));
        public void CloseOverlay(PluginsUrlState parent) => Calls.Add(new(RouteCallKind.CloseOverlay, parent, null));
    }
}
