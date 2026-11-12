using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.Plugins;
using CanDoItAll.Modules.Plugins.Presentation;
using CanDoItAll.Plugins.Abstractions;
using CanDoItAll.Plugins.UI;
using CanDoItAll.Plugins.UiSandbox;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.Plugins;

public sealed class PluginsEffectTests {
    [Fact]
    public async Task Installed_package_keeps_warning_when_only_catalog_refresh_fails() {
        var (store, workspace, _) = await PluginsWorkspaceTests.ReadyAsync();
        using (workspace) {
            var package = workspace.View.Packages.Value[0];
            store.FailCatalog = true;
            await workspace.InstallPackageAsync(package);
            Assert.Equal(PluginMutationStatus.SavedWithWarning,
                workspace.View.Operations[new PluginOperationTarget.Package(package.PackageId)].Status);
            Assert.Equal(PluginReadStatus.Stale, workspace.View.Catalog.Status);
            Assert.True(workspace.View.Packages.Value[0].IsInstalled);
            Assert.True(workspace.View.Restart.Value!.IsRestartRequired);
            store.FailCatalog = false;
            await workspace.RefreshAsync();
            Assert.Equal(PluginReadStatus.Ready, workspace.View.Catalog.Status);
            Assert.Equal(1, store.PackageWrites);
        }
    }

    [Fact]
    public async Task Representative_logs_and_recipe_grants_keep_real_fake_storage_and_distinct_targets() {
        var (store, workspace, _) = await PluginsWorkspaceTests.ReadyAsync(PluginScenario.HeldGrant);
        using (workspace) {
            Assert.Equal(workspace.View.SelectedPluginId, Assert.Single(workspace.View.InstallationLogs.Value).PluginId);
            Assert.Equal(PluginLogStreamKind.Runtime, Assert.Single(workspace.View.RuntimeLogs.Value).StreamKind);
            await workspace.SetLogScopeAsync(true);
            Assert.Equal(3, workspace.View.RuntimeLogs.Value.Count);
            await workspace.SelectAsync(workspace.View.Catalog.Value[1]);
            var recipes = workspace.View.Settings.Value!.Grants.Where(item => item.RecipeId is not null).ToArray();
            var first = workspace.SetGrantAsync(workspace.View.SelectedPlugin!, recipes[0], PluginGrantState.Granted);
            var second = workspace.SetGrantAsync(workspace.View.SelectedPlugin!, recipes[1], PluginGrantState.Denied);
            store.ReleaseAll();
            await Task.WhenAll(first, second);
            var saved = store.Snapshot(workspace.View.SelectedPluginId!.Value)!.Grants;
            Assert.Equal(PluginGrantState.Granted, saved.Single(item => item.RecipeId == recipes[0].RecipeId).State);
            Assert.Equal(PluginGrantState.Denied, saved.Single(item => item.RecipeId == recipes[1].RecipeId).State);
            Assert.Equal(2, store.GrantWrites);
        }
    }

    [Fact]
    public async Task Accepted_identity_survives_newer_refusal_and_unknown_without_passive_replay() {
        var (store, workspace, editor) = await PluginsWorkspaceTests.ReadyAsync();
        using (workspace) {
            await workspace.SaveAsync(editor);
            var id = editor.ConnectionId;
            editor.SetName("Refused edit");
            store.Scenario = PluginScenario.RefusedSave;
            await workspace.SaveAsync(editor);
            Assert.Equal(PluginMutationStatus.Refused, editor.Operation!.Status);
            Assert.True(editor.IsDirty);
            Assert.Equal(id, editor.ConnectionId);
            Assert.Equal(1, store.SaveWrites);
            store.Scenario = PluginScenario.UnknownSave;
            await workspace.SaveAsync(editor);
            await workspace.RefreshAsync();
            await workspace.SaveAsync(editor);
            Assert.Equal(PluginMutationStatus.Unknown, editor.Operation!.Status);
            Assert.Equal(2, store.SaveWrites);
            Assert.Equal(id, Assert.Single(store.Connections).Id);
            Assert.True(editor.PreventsReplay);
        }
    }

    [Fact]
    public async Task Lifecycle_conflicts_share_one_target_while_another_plugin_can_progress() {
        var (store, workspace, _) = await PluginsWorkspaceTests.ReadyAsync();
        using (workspace) {
            store.Hold(PluginScenarioWait.Lifecycle);
            var plugin = workspace.View.SelectedPlugin!;
            var first = workspace.SetLifecycleAsync(plugin, PluginLifecycleAction.Disable);
            await store.EnteredAsync(PluginScenarioWait.Lifecycle);
            await workspace.SetLifecycleAsync(plugin, PluginLifecycleAction.Enable);
            var second = workspace.SetLifecycleAsync(workspace.View.Catalog.Value[1], PluginLifecycleAction.Disable);
            store.ReleaseAll();
            await Task.WhenAll(first, second);
            Assert.Equal(2, store.LifecycleWrites);
            Assert.All(workspace.View.Catalog.Value.Take(2), item => Assert.False(item.IsEnabled));
        }
    }

    [Fact]
    public async Task Partial_read_failures_keep_independent_data_and_restart_receipts() {
        var (store, workspace, editor) = await PluginsWorkspaceTests.ReadyAsync();
        using (workspace) {
            await workspace.SaveAsync(editor);
            store.FailSettings = true;
            store.FailOAuth = true;
            store.FailPackages = true;
            store.FailRestart = true;
            await workspace.RefreshAsync();
            Assert.Equal(PluginReadStatus.Ready, workspace.View.Catalog.Status);
            Assert.Equal(PluginReadStatus.Ready, workspace.View.RuntimeLogs.Status);
            Assert.Equal(PluginReadStatus.Stale, workspace.View.Settings.Status);
            Assert.Equal(PluginReadStatus.Stale, workspace.View.OAuth.Status);
            Assert.Equal(PluginReadStatus.Stale, workspace.View.Packages.Status);
            Assert.Equal(PluginReadStatus.Stale, workspace.View.Restart.Status);
            await workspace.StartOAuthAsync(editor);
            Assert.Equal(0, store.OAuthStarts);
            store.Scenario = PluginScenario.PackageWarning;
            await workspace.InstallPackageAsync(workspace.View.Packages.Value[0]);
            Assert.True(workspace.View.Restart.Value!.IsRestartRequired);
            Assert.True(workspace.View.Packages.Value[0].IsInstalled);
            Assert.Equal(PluginMutationStatus.SavedWithWarning, workspace.View.Operations[new PluginOperationTarget.Package(new("sandbox.package"))].Status);
            Assert.Equal(PluginPackageStage.RestartRecorded, workspace.View.Operations[new PluginOperationTarget.Package(new("sandbox.package"))].PackageProgress!.Stage);
            await workspace.RefreshAsync();
            Assert.Equal(1, store.PackageWrites);
        }
    }

    [Theory]
    [InlineData(PluginScenario.OAuthConnected, PluginOAuthConnectionStatusKind.Connected)]
    [InlineData(PluginScenario.OAuthReconnect, PluginOAuthConnectionStatusKind.ReconnectRequired)]
    [InlineData(PluginScenario.OAuthError, PluginOAuthConnectionStatusKind.Error)]
    public async Task OAuth_scenarios_keep_stored_status_distinct_from_a_start_effect(PluginScenario scenario, PluginOAuthConnectionStatusKind expected) {
        var store = new PluginScenarioStore(scenario);
        using var workspace = new PluginsWorkspace(store, "http://fixture.invalid/api/plugins/oauth/callback");
        await workspace.RefreshAsync();
        var editor = workspace.View.Editors.Values.Single();
        Assert.Equal(expected, workspace.View.OAuthStatus(editor)!.Status);
        await workspace.StartOAuthAsync(editor);
        Assert.Equal(1, store.OAuthStarts);
        Assert.Equal(1, store.BrowserEffects);
        Assert.Equal(expected, workspace.View.OAuthStatus(editor)!.Status);
        if (expected == PluginOAuthConnectionStatusKind.Connected) {
            await workspace.DisconnectOAuthAsync(editor);
            Assert.Null(workspace.View.OAuthStatus(editor));
        }
        await workspace.RefreshAsync();
        Assert.Equal(1, store.OAuthStarts);
    }

    [Fact]
    public async Task Closed_package_dialog_does_not_reopen_after_late_read_or_installation() {
        var (store, workspace, _) = await PluginsWorkspaceTests.ReadyAsync();
        using (workspace) {
            store.Hold(PluginScenarioWait.Package);
            var opening = workspace.OpenPackagesAsync();
            await store.EnteredAsync(PluginScenarioWait.Package);
            workspace.ClosePackages();
            store.ReleaseAll();
            await opening;
            Assert.False(workspace.View.PackagesOpen);
            await workspace.OpenPackagesAsync();
            store.Hold(PluginScenarioWait.Package);
            var installing = workspace.InstallPackageAsync(workspace.View.Packages.Value[0]);
            await store.EnteredAsync(PluginScenarioWait.Package);
            workspace.ClosePackages();
            store.ReleaseAll();
            await installing;
            Assert.False(workspace.View.PackagesOpen);
            Assert.True(workspace.View.Restart.Value!.IsRestartRequired);
            Assert.Equal(1, store.PackageWrites);
            Assert.DoesNotContain("Installed package", workspace.View.Notice, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Removed_descriptor_retains_visible_raw_draft_until_explicit_discard() {
        var (store, workspace, editor) = await PluginsWorkspaceTests.ReadyAsync();
        using (workspace) {
            editor.SetName("Retained missing descriptor draft");
            store.Scenario = PluginScenario.MissingDescriptor;
            await workspace.RefreshAsync();
            using var context = new BunitContext();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddCanDoItAllBaseLib();
            var cut = context.Render<PluginSettingsTab>(parameters => parameters.Add(item => item.Plugin, workspace.View.SelectedPlugin!)
                .Add(item => item.Settings, workspace.View.Settings.Value!).Add(item => item.Workspace, workspace));
            Assert.Contains("Retained missing descriptor draft", cut.Markup, StringComparison.Ordinal);
            Assert.True(editor.ReferenceChanged);
            await cut.FindAll("button").Single(item => item.TextContent.Contains("Reset to stored values", StringComparison.Ordinal)).ClickAsync(new MouseEventArgs());
            Assert.Empty(workspace.View.Editors);
            Assert.True(editor.Retired);
        }
    }
}
