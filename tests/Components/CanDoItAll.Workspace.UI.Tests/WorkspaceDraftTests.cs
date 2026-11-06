using System.Text.Json;
using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.Security;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.Workspace.UI;
using CanDoItAll.Workspace.UiSandbox;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.WorkspaceUi;

public sealed class WorkspaceDraftTests {
    [Fact]
    public async Task Initial_defaults_read_preserves_unblurred_input_and_form() {
        var store = new WorkspaceScenarioStore();
        var gate = store.HoldNext(WorkspaceOperation.DefaultsRead);
        using var state = new WorkspaceDefaultsController(store);
        using var context = Context();
        var cut = context.Render<WorkspaceDefaultsSurface>(p => p.Add(x => x.State, state));
        var form = state.Draft.EditContext;
        var pending = state.RefreshAsync();
        await gate.Entered.WaitAsync(TimeSpan.FromSeconds(10));
        cut.Find("[data-testid=defaults-name]").Input("Before load completed");
        await state.SaveAsync();
        Assert.False(store.Calls.ContainsKey(WorkspaceOperation.DefaultsSave));
        gate.Release();
        await pending;
        cut.WaitForAssertion(() => Assert.Equal("Before load completed", state.Draft.Model.WorkspaceName));
        Assert.Same(form, cut.FindComponent<EditForm>().Instance.EditContext);
        Assert.Equal(WorkspaceScenarioStore.ProviderId, state.Draft.Model.DefaultProviderProfileId);
    }

    [Fact]
    public async Task Defaults_capture_all_fields_and_preserve_edit_away_and_back_during_normalization() {
        var store = new WorkspaceScenarioStore();
        using var state = new WorkspaceDefaultsController(store);
        await state.RefreshAsync();
        using var context = Context();
        var cut = context.Render<WorkspaceDefaultsSurface>(p => p.Add(x => x.State, state));
        cut.Find("[data-testid=defaults-name]").Input(" submitted ");
        cut.Find("[data-testid=defaults-notes]").Input(" submitted notes ");
        cut.Find("[data-testid=defaults-currency]").Input("eur");
        cut.Find("[data-testid=defaults-culture]").Input("de-DE");
        cut.Find("[data-testid=defaults-outputformat]").Input("Plain text");
        var gate = store.HoldNext(WorkspaceOperation.DefaultsSave);
        var pending = cut.Find("form").SubmitAsync();
        await gate.Entered.WaitAsync(TimeSpan.FromSeconds(10));
        cut.Find("[data-testid=defaults-name]").Input("away");
        cut.Find("[data-testid=defaults-name]").Input(" submitted ");
        cut.Find("[data-testid=defaults-notes]").Input("newer notes");
        await state.SaveAsync();
        gate.Release();
        await pending;
        Assert.Equal(1, store.Calls[WorkspaceOperation.DefaultsSave]);
        Assert.Equal(" submitted ", state.Draft.Model.WorkspaceName);
        Assert.Equal("newer notes", state.Draft.Model.Notes);
        Assert.Equal("EUR", state.Draft.Model.CurrencyCode);
        var saved = (await store.ReadAsync(default)).Values;
        Assert.Equal("submitted", saved.WorkspaceName);
        Assert.Equal("submitted notes", saved.Notes);
        Assert.Equal("de-DE", saved.CurrencyCultureName);
        Assert.Equal("Plain text", saved.DefaultPromptOutputFormat);
        Assert.Equal(WorkspaceScenarioStore.ProviderId, saved.DefaultProviderProfileId);
    }

    [Theory]
    [InlineData(WorkspaceScenario.MissingProvider)]
    [InlineData(WorkspaceScenario.PartialReferences)]
    public async Task Missing_provider_and_failed_references_preserve_exact_reference(WorkspaceScenario scenario) {
        var store = new WorkspaceScenarioStore(scenario);
        using var state = new WorkspaceDefaultsController(store);
        await state.RefreshAsync();
        using var context = Context();
        var cut = context.Render<WorkspaceDefaultsSurface>(p => p.Add(x => x.State, state));
        Assert.Contains("Unavailable provider", cut.Markup);
        Assert.Equal(WorkspaceScenarioStore.ProviderId, state.Draft.Model.DefaultProviderProfileId);
        Assert.True(state.CanSave);
    }

    [Fact]
    public async Task Provider_comparisons_show_catalog_labels_and_preserve_unavailable_reference_values() {
        var enabledId = Guid.NewGuid();
        var disabledId = Guid.NewGuid();
        var unavailableId = Guid.NewGuid();
        WorkspaceProviderOption[] providers = [new(enabledId, "Team provider", true), new(disabledId, "Archived provider", false)];
        (Guid? Id, string Label)[] cases = [
            (enabledId, "Team provider"),
            (disabledId, "Archived provider (disabled)"),
            (unavailableId, "Unavailable provider"),
            (null, "None")
        ];
        using var context = Context();
        foreach (var (id, label) in cases) {
            var values = WorkspaceSettingsValues.Capture(new() { DefaultProviderProfileId = id });
            using var comparison = context.Render<WorkspaceDefaultsComparison>(parameters => parameters
                .Add(component => component.Title, "Saved values")
                .Add(component => component.Values, values)
                .Add(component => component.Providers, providers));
            Assert.Contains($"Default provider: {label}", comparison.Markup);
            Assert.Equal(id, values.DefaultProviderProfileId);
            if (id is { } selected) {
                Assert.DoesNotContain(selected.ToString(), comparison.Markup);
            }
        }
        var store = new WorkspaceScenarioStore(WorkspaceScenario.MissingProvider);
        using var state = new WorkspaceDefaultsController(store);
        await state.RefreshAsync();
        var calls = store.Calls.ToArray();
        var surface = context.Render<WorkspaceDefaultsSurface>(parameters => parameters.Add(component => component.State, state));
        var option = surface.Find($"[data-testid=defaults-provider] option[value='{WorkspaceScenarioStore.ProviderId}']");
        Assert.Equal("Unavailable provider", option.TextContent);
        Assert.Equal(WorkspaceScenarioStore.ProviderId, state.Draft.Capture().Values.DefaultProviderProfileId);
        Assert.Equal(calls, store.Calls.ToArray());
    }

    [Fact]
    public async Task Defaults_commit_with_failed_reference_read_is_known_and_refresh_never_replays() {
        var store = new WorkspaceScenarioStore();
        using var state = new WorkspaceDefaultsController(store);
        await state.RefreshAsync();
        store.FaultNext(WorkspaceOperation.ProvidersRead, WorkspaceFault.ReadFailure);
        await state.SaveAsync();
        Assert.Equal(SettingsEffect.CommittedWarning, Assert.Single(state.Receipts).Effect);
        await state.RefreshAsync();
        Assert.Equal(1, store.Calls[WorkspaceOperation.DefaultsSave]);
        store.IsCurrent = false;
        await state.SaveAsync();
        Assert.Equal(1, store.Calls[WorkspaceOperation.DefaultsSave]);
    }

    [Fact]
    public async Task Secret_save_uses_live_input_and_adopts_id_without_clearing_newer_sensitive_fields() {
        var store = new WorkspaceScenarioStore(WorkspaceScenario.Empty);
        using var state = new WorkspaceSecretsController(store);
        using var context = Context();
        var cut = context.Render<WorkspaceSecretsSurface>(p => p.Add(x => x.State, state));
        cut.Find("[data-testid=secret-name]").Input("Captured name");
        cut.Find("[data-testid=settings-secret-value]").Input("first synthetic value");
        cut.Find("[data-testid=secret-scope]").Input("agent");
        cut.Find("[data-testid=secret-rotationnote]").Input("rotation");
        cut.Find("[data-testid=secret-metadatajson]").Input("{\"sentinel\":1}");
        var gate = store.HoldNext(WorkspaceOperation.SecretSave);
        var pending = cut.Find("form").SubmitAsync();
        await gate.Entered.WaitAsync(TimeSpan.FromSeconds(10));
        cut.Find("[data-testid=settings-secret-value]").Input("newer synthetic value");
        cut.Find("[data-testid=secret-metadatajson]").Input("{ unfinished");
        await state.SaveAsync();
        await state.DeleteAsync();
        gate.Release();
        await pending;
        var receipt = Assert.Single(state.Receipts);
        Assert.Equal(SettingsEffect.Committed, receipt.Effect);
        Assert.Equal(receipt.RecordId, state.Draft.Model.Id);
        Assert.Equal("newer synthetic value", state.Draft.Model.SecretValue);
        Assert.Equal("{ unfinished", state.Draft.Model.MetadataJson);
        var saved = await store.GetAsync(receipt.RecordId!.Value, default);
        Assert.Equal("first synthetic value", saved!.SecretValue);
        Assert.Equal("{\"sentinel\":1}", saved.MetadataJson);
        Assert.Equal("rotation", saved.RotationNote);
        Assert.Equal("agent", saved.Scope);
        Assert.Equal(1, store.Calls[WorkspaceOperation.SecretSave]);
        Assert.False(store.Calls.ContainsKey(WorkspaceOperation.SecretDelete));
        var serialized = JsonSerializer.Serialize(state.Receipts);
        Assert.DoesNotContain("synthetic value", serialized);
        Assert.DoesNotContain("sentinel", serialized);
        Assert.DoesNotContain("MetadataJson", serialized);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Secret_save_or_delete_after_reset_only_updates_its_original_receipt(bool delete) {
        var store = new WorkspaceScenarioStore();
        using var state = new WorkspaceSecretsController(store);
        await state.SelectAsync(WorkspaceScenarioStore.SecretId);
        var old = state.Draft;
        var gate = store.HoldNext(delete ? WorkspaceOperation.SecretDelete : WorkspaceOperation.SecretSave);
        var pending = delete ? state.DeleteAsync() : state.SaveAsync();
        await gate.Entered.WaitAsync(TimeSpan.FromSeconds(10));
        state.New();
        state.Draft.Model.Name = "Successor";
        state.Draft.Model.SecretValue = "Successor synthetic value";
        gate.Release();
        await pending;
        Assert.Equal("Successor", state.Draft.Model.Name);
        Assert.Null(state.Draft.Model.Id);
        Assert.Equal("Successor synthetic value", state.Draft.Model.SecretValue);
        Assert.Empty(old.Model.SecretValue);
        Assert.Equal(WorkspaceScenarioStore.SecretId, Assert.Single(state.Receipts).RecordId);
        Assert.Equal(SettingsEffect.Committed, state.Receipts[0].Effect);
    }

    [Fact]
    public async Task Secret_conflict_requires_review_and_a_separate_save_without_disclosing_a_payload() {
        var store = new WorkspaceScenarioStore();
        using var local = new WorkspaceSecretsController(store);
        using var peer = new WorkspaceSecretsController(store);
        var id = WorkspaceScenarioStore.SecretId;
        await local.SelectAsync(id);
        await peer.SelectAsync(id);
        var draft = local.Draft;
        Assert.Empty(draft.Model.SecretValue);
        draft.Model.Name = "My label";
        draft.SetReplacement("local synthetic replacement");
        peer.Draft.Model.Scope = "peer-scope";
        peer.Draft.Model.MetadataJson = "private comparison sentinel";
        peer.Draft.SetReplacement("peer synthetic replacement");
        await peer.SaveAsync();

        await local.SaveAsync();
        await local.SelectAsync(id);
        Assert.Same(draft, local.Draft);
        Assert.False(local.CanMutate);
        await local.CompareAsync();
        var review = Assert.IsType<WorkspaceSecretReview>(local.Comparison);
        using var context = Context();
        var cut = context.Render<WorkspaceSecretReviewDialog>(p => p.Add(x => x.State, local).Add(x => x.Review, review));
        Assert.DoesNotContain("private comparison sentinel", cut.Markup);
        Assert.DoesNotContain("local synthetic replacement", cut.Markup);
        Assert.DoesNotContain("peer synthetic replacement", cut.Markup);
        local.ApplyComparison();
        Assert.Same(review, local.Comparison);
        review.Replacement!.UseCurrent();
        local.ApplyComparison();

        Assert.Same(draft, local.Draft);
        Assert.Equal("My label", draft.Model.Name);
        Assert.Equal("peer-scope", draft.Model.Scope);
        Assert.Empty(draft.Model.SecretValue);
        Assert.Equal(1, store.DurableWrites);
        await local.SaveAsync();
        var saved = (await store.GetAsync(id, default))!;
        Assert.Equal("My label", saved.Name);
        Assert.Equal("peer-scope", saved.Scope);
        Assert.Equal("peer synthetic replacement", saved.SecretValue);
        Assert.Equal(2, store.DurableWrites);
    }

    [Fact]
    public async Task Accepted_rotation_clears_the_submitted_value_while_preserving_a_later_metadata_edit() {
        var store = new WorkspaceScenarioStore();
        using var state = new WorkspaceSecretsController(store);
        await state.SelectAsync(WorkspaceScenarioStore.SecretId);
        var draft = state.Draft;
        draft.SetReplacement("submitted synthetic replacement");
        var gate = store.HoldNext(WorkspaceOperation.SecretSave);
        var pending = state.SaveAsync();
        await gate.Entered.WaitAsync(TimeSpan.FromSeconds(10));
        draft.Model.Name = "Changed while saving";
        draft.Edited();
        gate.Release();
        await pending;

        Assert.Same(draft, state.Draft);
        Assert.Equal("Changed while saving", draft.Model.Name);
        Assert.Empty(draft.Model.SecretValue);
        Assert.Equal(1, store.DurableWrites);
    }

    [Fact]
    public async Task Missing_explicit_secret_stays_unacquired_and_same_id_can_retry() {
        var store = new WorkspaceScenarioStore(WorkspaceScenario.MissingSecret);
        using var state = new WorkspaceSecretsController(store);
        await state.SelectAsync(WorkspaceScenarioStore.SecretId);
        state.Draft.Model.Name = "Must not resurrect";
        state.Draft.Model.SecretValue = "synthetic";
        await state.SaveAsync();
        await state.RefreshAsync();
        Assert.False(state.CanMutate);
        await state.SelectAsync(WorkspaceScenarioStore.SecretId);
        Assert.Equal(2, store.Calls[WorkspaceOperation.SecretGet]);
        Assert.False(store.Calls.ContainsKey(WorkspaceOperation.SecretSave));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Late_secret_selection_cannot_restore_plaintext_after_new_or_disposal(bool dispose) {
        var store = new WorkspaceScenarioStore();
        using var state = new WorkspaceSecretsController(store);
        var gate = store.HoldNext(WorkspaceOperation.SecretGet);
        var pending = state.SelectAsync(WorkspaceScenarioStore.SecretId);
        await gate.Entered.WaitAsync(TimeSpan.FromSeconds(10));
        if (dispose) {
            state.Dispose();
        } else {
            state.New();
        }
        gate.Release();
        await pending;
        Assert.Empty(state.Draft.Model.SecretValue);
    }

    [Fact]
    public async Task Unknown_secret_write_blocks_replay_and_metadata_observation_does_not_claim_success() {
        var store = new WorkspaceScenarioStore();
        using var state = new WorkspaceSecretsController(store);
        await state.SelectAsync(WorkspaceScenarioStore.SecretId);
        store.FaultNext(WorkspaceOperation.SecretSave, WorkspaceFault.Unknown);
        await state.SaveAsync();
        var receipt = Assert.Single(state.Receipts);
        Assert.Equal(SettingsEffect.Unknown, receipt.Effect);
        var gate = store.HoldNext(WorkspaceOperation.SecretList);
        var review = state.ReviewAsync(receipt);
        await gate.Entered.WaitAsync(TimeSpan.FromSeconds(10));
        await state.ReviewAsync(receipt);
        await state.SaveAsync();
        await state.DeleteAsync();
        gate.Release();
        await review;
        Assert.True(receipt.ObservedExists);
        Assert.Equal(SettingsEffect.Unknown, receipt.Effect);
        Assert.Equal(1, store.Calls[WorkspaceOperation.SecretSave]);
        Assert.Equal(1, store.Calls[WorkspaceOperation.SecretList]);
        Assert.Equal(1, store.Calls[WorkspaceOperation.SecretGet]);
    }

    [Fact]
    public void Receipt_capacity_never_evicts_unresolved_operations() {
        var ledger = new SettingsOperationLedger();
        for (var index = 0; index < SettingsOperationLedger.Capacity; index++) {
            Assert.NotNull(ledger.Admit(Guid.NewGuid(), SettingsAction.SaveSecret));
        }
        Assert.Null(ledger.Admit(Guid.NewGuid(), SettingsAction.SaveSecret));
        Assert.Equal(SettingsOperationLedger.Capacity, ledger.Entries.Count);
        Assert.All(ledger.Entries, item => Assert.Equal(SettingsEffect.Pending, item.Effect));
    }

    [Theory]
    [InlineData(WorkspaceScenario.Populated, false)]
    [InlineData(WorkspaceScenario.Empty, true)]
    public async Task File_list_renders_only_the_actual_empty_or_populated_state(WorkspaceScenario scenario, bool empty) {
        var store = new WorkspaceScenarioStore(scenario);
        using var state = new WorkspaceFilesController(store);
        await state.RefreshAsync();
        using var context = Context();
        var cut = context.Render<WorkspaceFilesSurface>(p => p.Add(x => x.State, state));
        Assert.Equal(empty, cut.Markup.Contains("All files use the system default", StringComparison.Ordinal));
        Assert.Equal(empty ? 0 : 1, cut.FindAll(".cda-selection-list-item__button").Count);
        Assert.DoesNotContain("State.Preferences", cut.Markup);
    }

    [Fact]
    public async Task Unknown_create_exposes_exact_metadata_observation_without_payload_reads_or_replay() {
        var store = new WorkspaceScenarioStore();
        using var state = new WorkspaceSecretsController(store);
        state.Draft.Model.Name = "Unknown create";
        state.Draft.Model.SecretValue = "synthetic-only";
        store.FaultNext(WorkspaceOperation.SecretSave, WorkspaceFault.Unknown);
        await state.SaveAsync();
        using var context = Context();
        var cut = context.Render<WorkspaceSecretsSurface>(p => p.Add(x => x.State, state));
        var receipt = Assert.Single(state.Receipts);
        Assert.Null(receipt.RecordId);
        cut.Find("[data-testid=secret-review-id]").Input(WorkspaceScenarioStore.SecretId.ToString("D"));
        await cut.Find("[data-testid=secret-review-exact]").ClickAsync();
        Assert.Equal(WorkspaceScenarioStore.SecretId, receipt.ObservedRecordId);
        Assert.True(receipt.ObservedExists);
        Assert.Equal(SettingsEffect.Unknown, receipt.Effect);
        Assert.False(store.Calls.ContainsKey(WorkspaceOperation.SecretGet));
        await state.SaveAsync();
        Assert.Equal(1, store.Calls[WorkspaceOperation.SecretSave]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task File_operations_capture_normalized_target_and_leave_successor_draft_unchanged(bool delete) {
        var store = new WorkspaceScenarioStore();
        using var state = new WorkspaceFilesController(store);
        await state.RefreshAsync();
        state.Select(Assert.Single(state.Preferences));
        state.Draft.Extension = " SAMPLE ";
        state.Draft.ExecutablePath = "/synthetic/original";
        var gate = store.HoldNext(delete ? WorkspaceOperation.FileDelete : WorkspaceOperation.FileSave);
        var pending = delete ? state.DeleteAsync() : state.SaveAsync();
        await gate.Entered.WaitAsync(TimeSpan.FromSeconds(10));
        state.New();
        state.Draft.Extension = ".successor";
        state.Draft.ExecutablePath = "/synthetic/successor";
        gate.Release();
        await pending;
        Assert.Equal(".sample", Assert.Single(state.Receipts).Extension!.Value.Value);
        Assert.Equal(".successor", state.Draft.Extension);
        Assert.Null(state.Draft.Selected);
        Assert.Equal("/synthetic/successor", state.Draft.ExecutablePath);
    }

    [Fact]
    public async Task File_write_warning_and_failed_list_preserve_stale_data_without_replay() {
        var store = new WorkspaceScenarioStore(WorkspaceScenario.RebindRequired);
        using var state = new WorkspaceFilesController(store);
        await state.RefreshAsync();
        using var context = Context();
        var cut = context.Render<WorkspaceFilesSurface>(p => p.Add(x => x.State, state));
        Assert.Contains("requires rebind", cut.Markup);
        var stale = state.Preferences;
        state.Select(Assert.Single(stale));
        store.FaultNext(WorkspaceOperation.FileSave, WorkspaceFault.CommittedWarning);
        store.FaultNext(WorkspaceOperation.FileList, WorkspaceFault.ReadFailure);
        await state.SaveAsync();
        Assert.Same(stale, state.Preferences);
        Assert.Equal(SettingsEffect.CommittedWarning, Assert.Single(state.Receipts).Effect);
        await state.RefreshAsync();
        Assert.False(Assert.Single(state.Preferences).RequiresRebind);
        Assert.Equal(1, store.Calls[WorkspaceOperation.FileSave]);
    }

    internal static BunitContext Context() {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        return context;
    }
}
