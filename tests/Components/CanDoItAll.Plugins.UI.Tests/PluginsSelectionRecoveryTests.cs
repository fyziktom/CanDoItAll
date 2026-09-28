using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.Plugins;
using CanDoItAll.Plugins.UI;
using CanDoItAll.Plugins.UiSandbox;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.Plugins;

public sealed class PluginsSelectionRecoveryTests : BunitContext {
    public PluginsSelectionRecoveryTests() {
        Services.AddCanDoItAllBaseLib();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public async Task Removed_then_repopulated_selection_does_not_launch_an_earlier_OAuth_popup() {
        var (store, workspace, editor) = await PluginsWorkspaceTests.ReadyAsync(PluginScenario.HeldOAuth);
        using (workspace) {
            await workspace.SaveAsync(editor);
            var pending = workspace.StartOAuthAsync(editor);
            await store.EnteredAsync(PluginScenarioWait.OAuth);
            store.CatalogScenario = PluginCatalogScenario.MissingFirst;
            await workspace.RefreshAsync();
            var cut = Render<PluginsWorkspaceSurface>(parameters => parameters.Add(component => component.Workspace, workspace));
            Assert.Contains("Selected plugin is unavailable", cut.Markup, StringComparison.Ordinal);
            store.CatalogScenario = PluginCatalogScenario.All;
            await workspace.RefreshAsync();
            store.Release(PluginScenarioWait.OAuth);
            await pending;
            Assert.Equal(0, store.BrowserEffects);
            Assert.Equal(1, store.OAuthStarts);
            Assert.Same(editor, workspace.View.Editors[editor.Origin.Key]);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Missing_selection_keeps_remaining_catalog_selectable_and_retains_unknown_origin(bool lateFailure) {
        var (store, workspace, editor) = await PluginsWorkspaceTests.ReadyAsync(PluginScenario.UnknownSave);
        using (workspace) {
            var original = workspace.View.Catalog.Value.Take(2).ToArray();
            editor.SetName("Unresolved A draft");
            await workspace.SaveAsync(editor);
            Assert.Equal(PluginMutationStatus.Unknown, editor.Operation!.Status);
            var pending = new TaskCompletionSource<IReadOnlyList<PluginCatalogItem>>(TaskCreationOptions.RunContinuationsAsynchronously);
            store.ReadCatalogOverride = _ => pending.Task;
            var earlier = workspace.RefreshAsync();
            store.ReadCatalogOverride = _ => Task.FromResult<IReadOnlyList<PluginCatalogItem>>([original[1]]);
            await workspace.RefreshAsync();
            var cut = Render<PluginsWorkspaceSurface>(parameters => parameters.Add(component => component.Workspace, workspace));
            Assert.Single(cut.FindAll("[data-testid=plugins-list-item-sandbox-plugin-1]"));
            Assert.Contains("Selected plugin is unavailable", cut.Markup, StringComparison.Ordinal);
            await cut.Find("[data-testid=plugins-list-item-sandbox-plugin-1]").ClickAsync(new());
            Assert.Equal(original[1].PluginId, workspace.View.SelectedPluginId);
            Assert.Equal(original[1].PluginId, workspace.View.Settings.Value!.CatalogItem.PluginId);
            if (lateFailure) {
                pending.SetException(new IOException("Late catalog failure"));
            } else {
                pending.SetResult(original);
            }
            await earlier;
            Assert.Single(workspace.View.Catalog.Value);
            Assert.True(workspace.View.Catalog.IsCurrent);
            store.ReadCatalogOverride = _ => Task.FromResult<IReadOnlyList<PluginCatalogItem>>([]);
            await workspace.RefreshAsync();
            cut.Render();
            Assert.Contains("No plugins are registered", cut.Markup, StringComparison.Ordinal);
            store.ReadCatalogOverride = _ => Task.FromResult<IReadOnlyList<PluginCatalogItem>>(original);
            await workspace.RefreshAsync();
            cut.Render();
            await cut.Find("[data-testid=plugins-list-item-office365-mail]").ClickAsync(new());
            Assert.Same(editor, workspace.View.Editors[editor.Origin.Key]);
            Assert.Equal("Unresolved A draft", editor.DisplayName);
            Assert.Equal(PluginMutationStatus.Unknown, editor.Operation.Status);
            await workspace.SaveAsync(editor);
            Assert.Equal(1, store.SaveDispatches);
            Assert.Equal(0, store.BrowserEffects);
        }
    }
}
