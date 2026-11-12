using CanDoItAll.Modules.Plugins;
using CanDoItAll.Modules.Plugins.Presentation;
using CanDoItAll.Plugins.Abstractions;
using CanDoItAll.Plugins.UI;
using CanDoItAll.Plugins.UiSandbox;
using CanDoItAll.SharedKernel.Configuration;

namespace CanDoItAll.Tests.Components.Plugins;

public sealed class PluginsWorkspaceTests {
    private const string ClientId = "96523be6-67a2-41fb-8c15-0df3a502fa07";

    [Fact]
    public async Task Save_captures_all_fields_and_preserves_later_typing_and_validation() {
        var (store, workspace, editor) = await ReadyAsync(PluginScenario.HeldSave);
        using (workspace) {
            editor.SetName("Submitted");
            var save = workspace.SaveAsync(editor);
            await store.EnteredAsync(PluginScenarioWait.Save);
            editor.SetName("Newer name ");
            editor.SetEnabled(false);
            editor.SetField("clientId", "incomplete-");
            store.ReleaseAll();
            await save;
            var saved = Assert.Single(store.Connections);
            Assert.Equal("Submitted", saved.DisplayName);
            Assert.True(saved.IsEnabled);
            Assert.Equal(ClientId, ConfigurationState.FromJson(saved.SettingsJson).GetText("clientId"));
            Assert.Equal(saved.Id, editor.ConnectionId);
            Assert.Equal(saved.ConcurrencyToken, editor.ConcurrencyToken);
            Assert.Equal("Newer name ", editor.DisplayName);
            Assert.False(editor.IsEnabled);
            Assert.Equal("incomplete-", editor.State.GetText("clientId"));
            Assert.True(editor.IsDirty);
            Assert.False(editor.Validation.Succeeded);
            Assert.Equal(PluginMutationStatus.Saved, editor.Operation!.Status);
        }
    }

    [Fact]
    public async Task Accepted_identity_is_visible_during_readback_and_failed_refresh_never_replays() {
        var (store, workspace, editor) = await ReadyAsync(PluginScenario.HeldReadback);
        using (workspace) {
            var save = workspace.SaveAsync(editor);
            await store.EnteredAsync(PluginScenarioWait.Settings);
            var id = Assert.Single(store.Connections).Id;
            Assert.Equal(id, editor.ConnectionId);
            Assert.True(editor.Operation!.HasCommittedValue);
            Assert.Equal(PluginMutationStatus.Pending, editor.Operation.Status);
            editor.SetName("Later edit");
            store.FailSettings = true;
            store.ReleaseAll();
            await save;
            Assert.Equal(PluginMutationStatus.SavedWithWarning, editor.Operation.Status);
            await workspace.RefreshAsync();
            Assert.Equal(1, store.SaveWrites);
            Assert.Equal(id, editor.ConnectionId);
            store.FailSettings = false;
            store.Scenario = PluginScenario.Normal;
            await workspace.SaveAsync(editor);
            Assert.Equal(id, Assert.Single(store.Connections).Id);
            Assert.Equal("Later edit", Assert.Single(store.Connections).DisplayName);
            Assert.False(editor.IsDirty);
        }
    }

    [Fact]
    public async Task Unknown_retains_submission_and_requires_explicit_exact_target_recovery() {
        var (store, workspace, editor) = await ReadyAsync(PluginScenario.UnknownSave);
        using (workspace) {
            await workspace.SaveAsync(editor);
            Assert.Equal(PluginMutationStatus.Unknown, editor.Operation!.Status);
            Assert.NotNull(editor.Submission);
            Assert.Null(editor.ConnectionId);
            await workspace.RefreshAsync();
            await workspace.SaveAsync(editor);
            workspace.ResetDraft(editor, null, false);
            Assert.Same(editor, workspace.View.Editor(editor.Origin.Key.PluginId, editor.Descriptor));
            Assert.Equal(1, store.SaveWrites);
            var saved = Assert.Single(store.Connections);
            workspace.ResetDraft(editor, saved.Id, true);
            var successor = workspace.View.Editor(saved.PluginId, editor.Descriptor);
            Assert.True(editor.Retired);
            Assert.Equal(saved.Id, successor.ConnectionId);
            Assert.NotEqual(editor.Origin.Generation, successor.Origin.Generation);
            store.Scenario = PluginScenario.Normal;
            successor.SetName("Reviewed exact connection");
            await workspace.SaveAsync(successor);
            Assert.Equal(saved.Id, Assert.Single(store.Connections).Id);
        }
    }

    [Fact]
    public async Task Direct_double_dispatch_is_rejected_and_independent_editor_can_progress() {
        var (store, workspace, editor) = await ReadyAsync(PluginScenario.HeldSave);
        using (workspace) {
            var first = workspace.SaveAsync(editor);
            await workspace.SaveAsync(editor);
            var otherPlugin = workspace.View.Catalog.Value[1];
            await workspace.SelectAsync(otherPlugin);
            var secondEditor = workspace.View.Editor(otherPlugin.PluginId, otherPlugin.Descriptor.Connections[0]);
            var second = workspace.SaveAsync(secondEditor);
            Assert.Equal(2, store.SaveDispatches);
            store.ReleaseAll();
            await Task.WhenAll(first, second);
            Assert.Equal(2, store.SaveWrites);
            Assert.Equal(2, store.Connections.Count);
            Assert.NotEqual(editor.ConnectionId, secondEditor.ConnectionId);
        }
    }

    [Fact]
    public async Task Explicit_retirement_allows_successor_and_late_receipt_does_not_patch_it() {
        var (store, workspace, editor) = await ReadyAsync(PluginScenario.HeldSave);
        using (workspace) {
            var oldSave = workspace.SaveAsync(editor);
            await workspace.RefreshAsync();
            workspace.ResetDraft(editor, null, true);
            var successor = workspace.View.Editor(editor.Origin.Key.PluginId, editor.Descriptor);
            successor.SetField("clientId", ClientId);
            successor.SetName("Successor");
            var nextSave = workspace.SaveAsync(successor);
            store.ReleaseAll();
            await Task.WhenAll(oldSave, nextSave);
            Assert.True(editor.Retired);
            Assert.Equal(2, store.SaveWrites);
            Assert.Equal("Successor", store.Connections.Single(item => item.Id == successor.ConnectionId).DisplayName);
            Assert.Equal(PluginMutationStatus.Saved, successor.Operation!.Status);
        }
    }

    [Fact]
    public async Task Draft_survives_refresh_selection_and_unrelated_grant_or_package_operations() {
        var (store, workspace, editor) = await ReadyAsync();
        using (workspace) {
            editor.SetName("Keep my draft ");
            var origin = editor.Origin;
            var a = workspace.View.SelectedPlugin!;
            await workspace.SelectAsync(workspace.View.Catalog.Value[1]);
            await workspace.SetGrantAsync(workspace.View.SelectedPlugin!, workspace.View.Settings.Value!.Grants[0], PluginGrantState.Denied);
            await workspace.InstallPackageAsync(workspace.View.Packages.Value[0]);
            await workspace.RefreshAsync();
            await workspace.SelectAsync(a);
            Assert.Same(editor, workspace.View.Editor(a.PluginId, editor.Descriptor));
            Assert.Equal(origin, editor.Origin);
            Assert.Equal("Keep my draft ", editor.DisplayName);
            Assert.True(editor.IsDirty);
            Assert.Equal(0, store.SaveWrites);
        }
    }

    [Fact]
    public async Task Descriptor_replacement_and_missing_connection_require_explicit_review() {
        var (store, workspace, editor) = await ReadyAsync();
        using (workspace) {
            await workspace.SaveAsync(editor);
            store.ReadSettingsOverride = (id, _) => Task.FromResult(store.Snapshot(id)! with {
                ConnectionDescriptors = [editor.Descriptor with { Description = "Replaced descriptor" }], Connections = []
            })!;
            await workspace.RefreshAsync();
            Assert.True(editor.ReferenceChanged);
            await workspace.SaveAsync(editor);
            Assert.Equal(1, store.SaveWrites);
            Assert.Equal(Assert.Single(store.Connections).Id, editor.ConnectionId);
        }
    }

    [Fact]
    public async Task A_B_A_reads_ignore_stale_success_failure_and_finally() {
        var (store, workspace, _) = await ReadyAsync();
        using (workspace) {
            var requests = new List<TaskCompletionSource<PluginSettingsDetail?>>();
            store.ReadSettingsOverride = (_, _) => {
                var request = new TaskCompletionSource<PluginSettingsDetail?>(TaskCreationOptions.RunContinuationsAsynchronously);
                requests.Add(request);
                return request.Task;
            };
            var a = workspace.View.Catalog.Value[0];
            var b = workspace.View.Catalog.Value[1];
            var first = workspace.RefreshAsync();
            var second = workspace.SelectAsync(b);
            var third = workspace.SelectAsync(a);
            requests[2].SetResult(store.Snapshot(a.PluginId));
            await third;
            requests[1].SetException(new InvalidOperationException("stale failure"));
            requests[0].SetResult(store.Snapshot(b.PluginId));
            await Task.WhenAll(first, second);
            Assert.Equal(a.PluginId, workspace.View.SelectedPluginId);
            Assert.Equal(a.PluginId, workspace.View.Settings.Value!.CatalogItem.PluginId);
            Assert.Equal(PluginReadStatus.Ready, workspace.View.Settings.Status);
            Assert.Empty(store.Diagnostics);
        }
    }

    [Fact]
    public async Task Log_lanes_are_bounded_independent_and_fenced_by_scope() {
        var (store, workspace, _) = await ReadyAsync();
        using (workspace) {
            var requests = new List<(PluginLogQuery Query, TaskCompletionSource<IReadOnlyList<PluginLogItem>> Completion)>();
            store.ReadLogsOverride = (query, _) => {
                var completion = new TaskCompletionSource<IReadOnlyList<PluginLogItem>>(TaskCreationOptions.RunContinuationsAsynchronously);
                requests.Add((query, completion));
                return completion.Task;
            };
            var old = workspace.SetLogScopeAsync(false);
            var current = workspace.SetLogScopeAsync(true);
            requests[2].Completion.SetResult([]);
            requests[3].Completion.SetException(new InvalidOperationException("runtime unavailable"));
            await current;
            requests[0].Completion.SetException(new InvalidOperationException("stale installation failure"));
            requests[1].Completion.SetResult([]);
            await old;
            Assert.Null(requests[2].Query.PluginId);
            Assert.All(store.LogQueries, query => Assert.Equal(50, query.Take));
            Assert.Equal(PluginReadStatus.Ready, workspace.View.InstallationLogs.Status);
            Assert.Equal(PluginReadStatus.Unavailable, workspace.View.RuntimeLogs.Status);
            Assert.Single(store.Diagnostics);
        }
    }

    [Fact]
    public async Task Grant_target_includes_scope_and_conflicting_decisions_share_admission() {
        var (store, workspace, _) = await ReadyAsync(PluginScenario.HeldGrant);
        using (workspace) {
            var settings = workspace.View.Settings.Value!;
            var scoped = settings.Grants.Where(grant => grant.ScopeKind == PluginGrantScopeKind.Connection).ToArray();
            var first = workspace.SetGrantAsync(settings.CatalogItem, scoped[0], PluginGrantState.Granted);
            await workspace.SetGrantAsync(settings.CatalogItem, scoped[0], PluginGrantState.Denied);
            Assert.True(workspace.View.IsBusy(new PluginOperationTarget.Grant(PluginGrantTarget.From(scoped[0]))));
            var other = workspace.SetGrantAsync(settings.CatalogItem, scoped[1], PluginGrantState.Revoked);
            store.ReleaseAll();
            await Task.WhenAll(first, other);
            var saved = store.Snapshot(settings.CatalogItem.PluginId)!.Grants;
            Assert.Equal(2, store.GrantWrites);
            Assert.Equal(PluginGrantState.Granted, saved.Single(item => item.ScopeKey == scoped[0].ScopeKey).State);
            Assert.Equal(PluginGrantState.Revoked, saved.Single(item => item.ScopeKey == scoped[1].ScopeKey).State);
        }
    }

    [Fact]
    public async Task OAuth_requires_saved_clean_settings_and_suppresses_late_A_B_A_popup() {
        var (store, workspace, editor) = await ReadyAsync(PluginScenario.HeldOAuth);
        using (workspace) {
            await workspace.StartOAuthAsync(editor);
            Assert.Equal(0, store.OAuthStarts);
            await workspace.SaveAsync(editor);
            var a = workspace.View.SelectedPlugin!;
            var login = workspace.StartOAuthAsync(editor);
            await store.EnteredAsync(PluginScenarioWait.OAuth);
            await workspace.SaveAsync(editor);
            await workspace.SelectAsync(workspace.View.Catalog.Value[1]);
            await workspace.SelectAsync(a);
            store.ReleaseAll();
            await login;
            Assert.Equal(1, store.SaveWrites);
            Assert.Equal(1, store.OAuthStarts);
            Assert.Equal(0, store.BrowserEffects);
            Assert.Empty(workspace.View.OAuth.Value);
            editor.SetName("Dirty");
            await workspace.StartOAuthAsync(editor);
            Assert.Equal(1, store.OAuthStarts);
        }
    }

    [Fact]
    public async Task Browser_failure_preserves_created_session_and_passive_refresh_never_starts_another() {
        var (store, workspace, editor) = await ReadyAsync();
        using (workspace) {
            await workspace.SaveAsync(editor);
            store.FailBrowser = true;
            await workspace.StartOAuthAsync(editor);
            Assert.Equal(PluginMutationStatus.SavedWithWarning, editor.Operation!.Status);
            await workspace.RefreshAsync();
            Assert.Equal(1, store.OAuthStarts);
            Assert.Equal(0, store.BrowserEffects);
        }
    }

    [Fact]
    public async Task Disposal_observes_noncooperative_write_but_suppresses_notifications_and_effects() {
        var (store, workspace, editor) = await ReadyAsync(PluginScenario.HeldSave);
        var notifications = 0;
        workspace.Changed += () => notifications++;
        var save = workspace.SaveAsync(editor);
        workspace.Dispose();
        var before = notifications;
        store.ReleaseAll();
        await save;
        Assert.Equal(before, notifications);
        Assert.Equal(1, store.SaveWrites);
        Assert.Single(store.Connections);
        Assert.Null(editor.ConnectionId);
    }

    [Fact]
    public async Task Large_catalog_loads_selected_references_only_and_bounds_clean_drafts() {
        var (store, workspace, editor) = await ReadyAsync(PluginScenario.Large);
        using (workspace) {
            Assert.Equal(100, workspace.View.Catalog.Value.Count);
            Assert.Equal(1, store.SettingsReads);
            for (var index = 0; index < 20; index++) {
                editor.SetName($"Raw {index}");
                workspace.SelectSection(PluginSection.Settings);
            }
            Assert.Equal(1, store.SettingsReads);
            foreach (var plugin in workspace.View.Catalog.Value.Skip(1)) {
                await workspace.SelectAsync(plugin);
            }
            Assert.True(workspace.View.Editors.Count <= PluginDraftRegistry.MaximumDrafts);
            Assert.Same(editor, workspace.View.Editors[editor.Origin.Key]);
            Assert.Equal(100, store.SettingsReads);
        }
    }

    [Fact]
    public async Task Scenario_transition_retires_pending_work_and_fake_commit_remains_readable_once() {
        using var host = new PluginScenarioHost();
        await host.ChangeAsync(PluginScenario.HeldSave);
        var previousStore = host.Store;
        var previous = host.Workspace;
        var editor = previous.View.Editors.Values.Single();
        editor.SetField("clientId", ClientId);
        var save = previous.SaveAsync(editor);
        await host.ChangeAsync(PluginScenario.Normal);
        await save;
        Assert.Single(previousStore.Snapshot(editor.Origin.Key.PluginId)!.Connections);
        Assert.Equal(1, previousStore.SaveWrites);
        Assert.Equal(0, host.Store.SaveWrites);
        Assert.Empty(host.Workspace.View.Settings.Value!.Connections);
    }

    internal static async Task<(PluginScenarioStore Store, PluginsWorkspace Workspace, PluginConnectionEditorState Editor)> ReadyAsync(PluginScenario scenario = PluginScenario.Normal) {
        var store = new PluginScenarioStore(scenario);
        var workspace = new PluginsWorkspace(store, "http://sandbox.invalid/api/plugins/oauth/callback");
        await workspace.RefreshAsync();
        var editor = workspace.View.Editors.Values.Single();
        editor.SetField("clientId", ClientId);
        return (store, workspace, editor);
    }
}
