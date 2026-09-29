using Bunit;
using CanDoItAll.AgentFramework.ProviderHistory;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.Workspace.UI;
using CanDoItAll.Workspace.UiSandbox;

namespace CanDoItAll.Tests.Components.WorkspaceUi;

public sealed class WorkspaceHistoryAndShellTests {
    [Fact]
    public async Task Direct_handler_validates_ranges_without_a_rendered_form() {
        var store = new WorkspaceScenarioStore();
        using var state = new WorkspaceHistoryController(store);
        await state.LoadAsync();
        state.Draft.BatchSize = 0;
        await state.ApplyFutureAsync();
        await state.PreviewAsync();
        Assert.False(store.Calls.ContainsKey(WorkspaceOperation.PolicyPreview));
        Assert.Equal(0, store.DurableWrites);
    }

    [Fact]
    public async Task Raw_invalid_number_invalidates_preview_and_direct_confirmation() {
        var store = new WorkspaceScenarioStore();
        using var state = new WorkspaceHistoryController(store);
        using var context = WorkspaceDraftTests.Context();
        var cut = context.Render<WorkspaceHistorySurface>(p => p.Add(x => x.State, state));
        Assert.Empty(store.Calls);
        await cut.Find("[data-testid=history-policy-load]").ClickAsync();
        cut.Find("[data-testid=history-policy-metadata-days]").Input("10");
        await cut.Find("[data-testid=history-policy-preview]").ClickAsync();
        Assert.NotNull(state.Preview);
        cut.Find("[data-testid=history-policy-batch]").Input("1e-");
        Assert.Null(state.Preview);
        await state.ConfirmShorterAsync();
        await cut.Find("form").SubmitAsync();
        Assert.False(store.Calls.ContainsKey(WorkspaceOperation.PolicyUpdate));
        Assert.Equal("1e-", cut.Find("[data-testid=history-policy-batch]").GetAttribute("value"));
        Assert.NotEmpty(state.EditContext.GetValidationMessages());
    }

    [Theory]
    [InlineData(WorkspaceScenario.DeniedHistory)]
    [InlineData(WorkspaceScenario.OversizePreview)]
    public async Task Permission_or_oversize_refusal_cannot_shorten_retention(WorkspaceScenario scenario) {
        var store = new WorkspaceScenarioStore(scenario);
        using var state = new WorkspaceHistoryController(store);
        using var context = WorkspaceDraftTests.Context();
        var cut = context.Render<WorkspaceHistorySurface>(p => p.Add(x => x.State, state));
        await cut.Find("[data-testid=history-policy-load]").ClickAsync();
        if (state.Snapshot is not null) {
            await cut.Find("[data-testid=history-policy-preview]").ClickAsync();
            Assert.True(state.Preview!.Preview.ExceedsLimit);
        } else {
            Assert.Contains("Denied", state.Error);
        }
        await state.ConfirmShorterAsync();
        Assert.Equal(0, store.DurableWrites);
    }

    [Fact]
    public async Task Known_update_is_versioned_and_unknown_update_cannot_replay_after_observation() {
        var store = new WorkspaceScenarioStore();
        using var state = new WorkspaceHistoryController(store);
        using var context = WorkspaceDraftTests.Context();
        var cut = context.Render<WorkspaceHistorySurface>(p => p.Add(x => x.State, state));
        await cut.Find("[data-testid=history-policy-load]").ClickAsync();
        await cut.Find("form").SubmitAsync();
        Assert.Equal(2, state.Snapshot!.Version);
        Assert.Equal(SettingsEffect.Committed, Assert.Single(state.Receipts).Effect);
        Assert.Equal(1, state.Receipts[0].PolicyVersion);
        store.FaultNext(WorkspaceOperation.PolicyUpdate, WorkspaceFault.Unknown);
        await cut.Find("[data-testid=history-policy-preview]").ClickAsync();
        await cut.Find("[data-testid=history-policy-confirm]").ClickAsync();
        Assert.Equal(SettingsEffect.Unknown, state.Receipts[1].Effect);
        await state.LoadAsync();
        Assert.Equal(3, state.Snapshot!.Version);
        await state.ApplyFutureAsync();
        await state.PreviewAsync();
        await state.ConfirmShorterAsync();
        Assert.Equal(2, store.Calls[WorkspaceOperation.PolicyUpdate]);
        Assert.Equal(SettingsEffect.Unknown, state.Receipts[1].Effect);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Retired_history_continuation_cannot_clear_successor_pending_or_publish_policy(bool dispose) {
        var store = new WorkspaceScenarioStore();
        using var state = new WorkspaceHistoryController(store);
        var oldGate = store.HoldNext(WorkspaceOperation.PolicyLoad);
        var old = state.LoadAsync();
        await oldGate.Entered.WaitAsync(TimeSpan.FromSeconds(10));
        state.Retire();
        var newGate = store.HoldNext(WorkspaceOperation.PolicyLoad);
        var successor = state.LoadAsync();
        await newGate.Entered.WaitAsync(TimeSpan.FromSeconds(10));
        oldGate.Release();
        await old;
        Assert.True(state.IsBusy);
        Assert.Null(state.Snapshot);
        if (dispose) {
            state.Dispose();
        }
        newGate.Release();
        await successor;
        Assert.Equal(dispose, state.Snapshot is null);
        Assert.False(state.IsBusy);
    }

    [Fact]
    public async Task Real_shell_retires_reveal_but_preserves_masked_draft_and_does_not_prefetch_history_or_deferred_slots() {
        var store = new WorkspaceScenarioStore();
        using var defaults = new WorkspaceDefaultsController(store);
        using var secrets = new WorkspaceSecretsController(store);
        using var files = new WorkspaceFilesController(store);
        using var history = new WorkspaceHistoryController(store);
        await defaults.RefreshAsync();
        await secrets.RefreshAsync();
        using var context = WorkspaceDraftTests.Context();
        var deferred = 0;
        var cut = context.Render<WorkspaceSettingsSurface>(p => p
            .Add(x => x.Defaults, defaults).Add(x => x.Secrets, secrets).Add(x => x.Files, files)
            .Add(x => x.Section, WorkspaceSection.Workspace)
            .Add(x => x.DeferredSection, _ => builder => { deferred++; })
            .Add(x => x.History, builder => {
                builder.OpenComponent<WorkspaceHistorySurface>(0);
                builder.AddAttribute(1, nameof(WorkspaceHistorySurface.State), history);
                builder.CloseComponent();
            }));
        Assert.Equal(0, deferred);
        Assert.False(store.Calls.ContainsKey(WorkspaceOperation.SecretGet));
        Assert.False(store.Calls.ContainsKey(WorkspaceOperation.PolicyLoad));
        Assert.Contains("Not loaded", cut.Markup);
        cut.Render(p => p.Add(x => x.Section, WorkspaceSection.Secrets));
        await cut.Find(".cda-selection-list-item__button").ClickAsync();
        var draft = secrets.Draft;
        var secret = cut.FindComponent<SecretField>();
        secret.Find(".cda-secret-field__reveal").Click();
        Assert.Equal("text", cut.Find("[data-testid=settings-secret-value]").GetAttribute("type"));
        cut.Render(p => p.Add(x => x.Section, WorkspaceSection.Files));
        Assert.Empty(cut.FindComponents<SecretField>());
        cut.Render(p => p.Add(x => x.Section, WorkspaceSection.Secrets));
        Assert.Same(draft, secrets.Draft);
        Assert.Equal("password", cut.Find("[data-testid=settings-secret-value]").GetAttribute("type"));
        Assert.Equal(1, store.Calls[WorkspaceOperation.SecretGet]);
        cut.Render(p => p.Add(x => x.Section, WorkspaceSection.ProviderHistory));
        Assert.Contains("Policy not requested", cut.Markup);
        Assert.False(store.Calls.ContainsKey(WorkspaceOperation.PolicyLoad));
        Assert.Equal(0, deferred);
        cut.Render(p => p.Add(x => x.Section, WorkspaceSection.Storage));
        Assert.Equal(1, deferred);
    }

    [Fact]
    public async Task An_admitted_sandbox_write_finishes_in_retired_store_after_reset() {
        var original = new WorkspaceScenarioStore();
        using var state = new WorkspaceFilesController(original);
        state.Draft.Extension = ".owned";
        state.Draft.ExecutablePath = "/synthetic/owned";
        var gate = original.HoldNext(WorkspaceOperation.FileSave);
        var pending = state.SaveAsync();
        await gate.Entered.WaitAsync(TimeSpan.FromSeconds(10));
        state.Dispose();
        original.IsCurrent = false;
        var successor = new WorkspaceScenarioStore(WorkspaceScenario.Empty);
        using var next = new WorkspaceFilesController(successor);
        await next.RefreshAsync();
        gate.Release();
        await pending;
        Assert.Equal(1, original.DurableWrites);
        Assert.Equal(0, successor.DurableWrites);
        Assert.Empty(next.Preferences);
        Assert.Empty(next.Receipts);
        Assert.Equal(SettingsEffect.Committed, Assert.Single(state.Receipts).Effect);
    }
}
