using CanDoItAll.Modules.Workspace;
using CanDoItAll.Workspace.UI;
using CanDoItAll.Workspace.UiSandbox;

namespace CanDoItAll.Tests.Components.WorkspaceUi;

public sealed class WorkspaceDefaultsConcurrencyTests {
    [Fact]
    public async Task Conflict_read_preserves_the_original_form_and_manual_reconciliation_starts_from_current_values() {
        var store = new WorkspaceScenarioStore();
        using var first = new WorkspaceDefaultsController(store);
        using var second = new WorkspaceDefaultsController(store);
        await first.RefreshAsync();
        await second.RefreshAsync();
        var original = second.Draft;
        var expected = original.Expectation;
        original.Model.WorkspaceName = "Local draft";
        original.Model.Notes = "Keep every local field";
        original.Edited(DefaultsField.Name);
        original.Edited(DefaultsField.Notes);
        first.Draft.Model.WorkspaceName = "Current winner";
        await first.SaveAsync();
        await second.SaveAsync();
        Assert.Equal(WorkspaceSettingsRejectionReason.HeadChanged, second.Rejection);
        Assert.False(second.CanSave);
        await second.ReadCurrentAsync();
        Assert.Same(original, second.Draft);
        Assert.Equal(expected, original.Expectation);
        Assert.Equal("Local draft", original.Model.WorkspaceName);
        Assert.Equal("Current winner", second.CurrentSnapshot!.Values.WorkspaceName);
        second.BeginReconciliation();
        Assert.Same(original, second.PreviousDraft);
        Assert.NotSame(original.EditContext, second.Draft.EditContext);
        Assert.Equal("Current winner", second.Draft.Model.WorkspaceName);
        Assert.NotEqual(expected, second.Draft.Expectation);
        Assert.True(second.CanSave);
        var reconciled = second.Draft;
        await second.ReadCurrentAsync();
        second.BeginReconciliation();
        Assert.Same(reconciled, second.Draft);
        Assert.Same(original, second.PreviousDraft);
        second.DiscardPreviousDraft();
        second.BeginReconciliation();
        Assert.Same(reconciled, second.PreviousDraft);
        Assert.Equal(1, store.DurableWrites);
    }

    [Fact]
    public async Task Current_read_after_unknown_does_not_authorize_reload_reconciliation_or_another_save() {
        var store = new WorkspaceScenarioStore();
        using var state = new WorkspaceDefaultsController(store);
        await state.RefreshAsync();
        var original = state.Draft;
        original.Model.WorkspaceName = "Unconfirmed submission";
        store.FaultNext(WorkspaceOperation.DefaultsSave, WorkspaceFault.Unknown);
        await state.SaveAsync();
        var receipt = Assert.Single(state.Receipts);
        Assert.Equal(SettingsEffect.Unknown, receipt.Effect);
        await state.ReviewAsync(receipt);
        Assert.Equal("Unconfirmed submission", state.CurrentSnapshot!.Values.WorkspaceName);
        Assert.Equal(SettingsEffect.Unknown, receipt.Effect);
        Assert.False(state.CanSave);
        Assert.False(state.CanRecover);
        state.ReloadCurrent();
        state.BeginReconciliation();
        await state.SaveAsync();
        Assert.Same(original, state.Draft);
        Assert.Equal(1, store.Calls[WorkspaceOperation.DefaultsSave]);
    }

    [Fact]
    public async Task Presentation_failure_after_acceptance_keeps_the_exact_snapshot_and_known_commit() {
        var store = new WorkspaceScenarioStore();
        using var state = new WorkspaceDefaultsController(store);
        await state.RefreshAsync();
        state.Draft.Model.WorkspaceName = " Accepted ";
        state.Changed += FailPresentation;
        await state.SaveAsync();
        state.Changed -= FailPresentation;
        Assert.Equal(await store.ReadAsync(default), state.LastAccepted);
        Assert.Equal(SettingsEffect.CommittedWarning, Assert.Single(state.Receipts).Effect);
        Assert.Equal("Accepted", state.LastAccepted!.Values.WorkspaceName);
        Assert.Equal(state.LastAccepted.Expectation, state.Draft.Expectation);
        Assert.NotNull(state.Error);

        static void FailPresentation() => throw new InvalidOperationException("Owned presentation failure.");
    }

    [Fact]
    public async Task Invalid_acknowledgement_cannot_erase_known_acceptance_or_partially_reconcile_the_draft() {
        var store = new WorkspaceScenarioStore();
        var original = await store.ReadAsync(default);
        var owner = new AcknowledgementFault(store, original);
        using var state = new WorkspaceDefaultsController(owner);
        await state.RefreshAsync();
        var draft = state.Draft;
        draft.Model.WorkspaceName = "Preserve local name";
        draft.Model.Notes = "Preserve local notes";
        await state.SaveAsync();
        Assert.Equal(SettingsEffect.CommittedWarning, Assert.Single(state.Receipts).Effect);
        Assert.Equal(original, state.LastAccepted);
        Assert.Same(draft, state.Draft);
        Assert.Equal(original.Expectation, draft.Expectation);
        Assert.Equal("Preserve local name", draft.Model.WorkspaceName);
        Assert.Equal("Preserve local notes", draft.Model.Notes);
        Assert.False(state.CanSave);
        await state.SaveAsync();
        Assert.Equal(1, owner.Calls);
    }

    [Fact]
    public async Task Retired_current_read_cannot_populate_a_successor_or_rebind_the_original_draft() {
        var store = new WorkspaceScenarioStore();
        using var previous = new WorkspaceDefaultsController(store);
        await previous.RefreshAsync();
        var original = previous.Draft;
        original.Model.WorkspaceName = "Retired local draft";
        var gate = store.HoldNext(WorkspaceOperation.DefaultsRead);
        var pending = previous.ReadCurrentAsync();
        try {
            await gate.Entered.WaitAsync(TimeSpan.FromSeconds(10));
            previous.Dispose();
            using var next = new WorkspaceDefaultsController(store);
            await next.RefreshAsync();
            next.Draft.Model.WorkspaceName = "Successor draft";
            gate.Release();
            await pending;
            Assert.Null(previous.CurrentSnapshot);
            Assert.Same(original, previous.Draft);
            Assert.Equal("Retired local draft", original.Model.WorkspaceName);
            Assert.Equal("Successor draft", next.Draft.Model.WorkspaceName);
            Assert.Empty(next.Receipts);
        } finally {
            gate.Release();
            await pending;
        }
    }

    private sealed class AcknowledgementFault(WorkspaceScenarioStore store, WorkspaceSettingsSnapshot acknowledgement) : IWorkspaceDefaultsOwner {
        public int Calls { get; private set; }
        public bool IsCurrent => store.IsCurrent;
        public Task<WorkspaceSettingsSnapshot> ReadAsync(CancellationToken cancellationToken) => store.ReadAsync(cancellationToken);
        public Task<WorkspaceSettingsSnapshotV2> ReadV2Async(CancellationToken cancellationToken) => store.ReadV2Async(cancellationToken);
        public Task<WorkspaceSettingsSaveOutcomeV2> SaveV2Async(ReplaceWorkspaceSettingsV2Command command, CancellationToken cancellationToken) => store.SaveV2Async(command, cancellationToken);
        public Task<IReadOnlyList<WorkspaceProviderOption>> ProvidersAsync(CancellationToken cancellationToken) => store.ProvidersAsync(cancellationToken);
        public Task<WorkspaceSettingsSaveOutcome> SaveAsync(ReplaceWorkspaceSettingsCommand command, CancellationToken cancellationToken) {
            Calls++;
            return Task.FromResult<WorkspaceSettingsSaveOutcome>(new WorkspaceSettingsSaveOutcome.Accepted(acknowledgement));
        }
    }
}
