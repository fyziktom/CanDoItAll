using Bunit;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.SharedKernel;
using CanDoItAll.Workspace.UI;
using CanDoItAll.Workspace.UiSandbox;
using Microsoft.AspNetCore.Components.Forms;

namespace CanDoItAll.Tests.Components.WorkspaceUi;

public sealed class WorkspaceDecisionPolicyTests {
    [Fact]
    public async Task Legacy_policy_starts_unset_and_incomplete_policy_never_falls_back_to_defaults_only_save() {
        var store = new WorkspaceScenarioStore();
        using var state = new WorkspaceDefaultsController(store);
        await state.RefreshAsync();
        using var context = WorkspaceDraftTests.Context();
        var cut = context.Render<WorkspaceDefaultsSurface>(p => p.Add(x => x.State, state));
        Assert.True(state.Draft.HasPolicyBaseline);
        Assert.Null(state.Draft.PolicyBaseline);
        Assert.Null(state.Draft.Policy.MaximumReplans);
        Assert.Empty(state.Draft.Policy.MaximumWorkDuration);
        var original = state.Draft.Expectation;
        cut.Find("[data-testid=defaults-policy-read]").Change("StepSnapshot");
        await cut.Find("form").SubmitAsync();
        Assert.Empty(state.Receipts);
        Assert.False(store.Calls.ContainsKey(WorkspaceOperation.DefaultsSave));
        Assert.Equal(original, state.Draft.Expectation);
        cut.Find("[data-testid=defaults-policy-freshness]").Change("RequireCurrent");
        cut.Find("[data-testid=defaults-policy-change]").Change("PauseForReview");
        cut.Find("[data-testid=defaults-policy-replans]").Input("0");
        cut.Find("[data-testid=defaults-policy-work]").Input("00:05:00.0000001");
        cut.Find("[data-testid=defaults-policy-age]").Input("00:01:00.0000002");
        await cut.Find("form").SubmitAsync();
        var receipt = Assert.Single(state.Receipts);
        Assert.Null(receipt.DefaultsAccepted);
        Assert.Equal(SettingsAction.SaveDefaultsWithPolicy, receipt.Action);
        Assert.Equal(TimeSpan.FromMinutes(5).Ticks + 1, receipt.DefaultsAcceptedV2!.Snapshot.DecisionDataPolicy!.Budget.MaximumWorkDuration.Ticks);
        Assert.Equal(TimeSpan.FromMinutes(1).Ticks + 2, receipt.DefaultsAcceptedV2.Snapshot.DecisionDataPolicy.Budget.MaximumBasisAge.Ticks);
        Assert.Equal(receipt.DefaultsAcceptedV2.Snapshot, await store.ReadV2Async(default));
        Assert.False(state.Draft.PolicyEditing);
    }

    [Fact]
    public async Task Defaults_only_acceptance_carries_a_matching_observed_policy_locally_without_fabricating_a_v2_receipt() {
        var store = new WorkspaceScenarioStore();
        var initial = await store.ReadAsync(default);
        var configured = Assert.IsType<WorkspaceSettingsSaveOutcomeV2.Accepted>(await store.SaveV2Async(
            new(initial.Expectation, initial.Values, Policy()), default));
        using var state = new WorkspaceDefaultsController(store);
        await state.RefreshAsync();
        state.Draft.Model.WorkspaceName = "Defaults only";
        var submitted = state.Draft.Capture();
        await state.SaveAsync();
        var receipt = Assert.Single(state.Receipts);
        Assert.NotNull(receipt.DefaultsAccepted);
        Assert.Null(receipt.DefaultsAcceptedV2);
        Assert.Null(state.LastAcceptedV2);
        Assert.Equal(configured.Snapshot.DecisionDataPolicy, state.Draft.PolicyBaseline);
        Assert.Equal(receipt.DefaultsAccepted.Snapshot.Expectation, state.Draft.PolicyBaselineExpectation);
        Assert.NotEqual(submitted.Expected, state.Draft.PolicyBaselineExpectation);
        Assert.Equal(configured.Snapshot.DecisionDataPolicy, (await store.ReadV2Async(default)).DecisionDataPolicy);
        var legacyOnly = new WorkspaceDefaultsDraft();
        legacyOnly.Acquire(initial, legacyOnly.CaptureVersions());
        legacyOnly.Acknowledge(new(initial.Expectation, initial.Values),
            new WorkspaceSettingsSnapshot(configured.Snapshot.Expectation, configured.Snapshot.Values), legacyOnly.CaptureVersions());
        Assert.False(legacyOnly.HasPolicyBaseline);
        Assert.Null(legacyOnly.PolicyBaselineExpectation);
    }

    [Fact]
    public async Task Frozen_v2_submission_reconciles_only_unchanged_fields_and_preserves_the_same_form() {
        var store = new WorkspaceScenarioStore();
        using var state = new WorkspaceDefaultsController(store);
        await state.RefreshAsync();
        using var context = WorkspaceDraftTests.Context();
        var cut = context.Render<WorkspaceDefaultsSurface>(p => p.Add(x => x.State, state));
        Configure(state.Draft);
        state.Draft.Model.WorkspaceName = " submitted ";
        state.Draft.Edited(DefaultsField.Name);
        var submitted = state.Draft.CapturePolicy();
        var form = state.Draft.EditContext;
        var gate = store.HoldNext(WorkspaceOperation.DefaultsSave);
        var pending = state.SaveAsync();
        try {
            await gate.Entered.WaitAsync(TimeSpan.FromSeconds(10));
            cut.Find("[data-testid=defaults-policy-work]").Input("00:07:00");
            cut.Find("[data-testid=defaults-name]").Input("Later name");
            await state.SaveAsync();
            gate.Release();
            await pending;
            var receipt = Assert.Single(state.Receipts);
            Assert.Equal(submitted.DecisionDataPolicy, receipt.DefaultsAcceptedV2!.Snapshot.DecisionDataPolicy);
            Assert.Equal("submitted", receipt.DefaultsAcceptedV2.Snapshot.Values.WorkspaceName);
            Assert.Equal("Later name", state.Draft.Model.WorkspaceName);
            Assert.Equal("00:07:00", state.Draft.Policy.MaximumWorkDuration);
            Assert.True(state.Draft.PolicyEditing);
            Assert.Same(form, cut.FindComponent<EditForm>().Instance.EditContext);
            Assert.Equal(1, store.Calls[WorkspaceOperation.DefaultsSave]);
        } finally {
            gate.Release();
            await pending;
        }
    }

    [Fact]
    public async Task Policy_conflict_read_does_not_rebase_dirty_values_and_explicit_reconciliation_keeps_the_old_draft() {
        var store = new WorkspaceScenarioStore();
        using var winner = new WorkspaceDefaultsController(store);
        using var stale = new WorkspaceDefaultsController(store);
        await winner.RefreshAsync();
        await stale.RefreshAsync();
        Configure(winner.Draft);
        Configure(stale.Draft);
        stale.Draft.Policy.MaximumBasisAge = "00:09:00";
        stale.Draft.Edited(DefaultsField.PolicyBasisAge);
        var original = stale.Draft;
        var expected = original.Expectation;
        await winner.SaveAsync();
        await stale.SaveAsync();
        Assert.Equal(WorkspaceSettingsRejectionReason.HeadChanged, stale.Rejection);
        await stale.ReadCurrentAsync();
        Assert.Same(original, stale.Draft);
        Assert.Equal(expected, original.Expectation);
        Assert.Equal("00:09:00", original.Policy.MaximumBasisAge);
        Assert.False(stale.CanSave);
        stale.BeginReconciliation();
        Assert.Same(original, stale.PreviousDraft);
        Assert.Equal(winner.LastAcceptedV2!.Expectation, stale.Draft.Expectation);
        Assert.Equal(winner.LastAcceptedV2.DecisionDataPolicy, stale.Draft.Policy.Capture());
        Assert.False(stale.Draft.PolicyEditing);
    }

    [Fact]
    public async Task Current_policy_read_cannot_resolve_unknown_or_authorize_another_attempt() {
        var store = new WorkspaceScenarioStore();
        using var state = new WorkspaceDefaultsController(store);
        await state.RefreshAsync();
        Configure(state.Draft);
        var original = state.Draft;
        var expected = original.Expectation;
        store.FaultNext(WorkspaceOperation.DefaultsSave, WorkspaceFault.Unknown);
        await state.SaveAsync();
        var receipt = Assert.Single(state.Receipts);
        await state.ReviewAsync(receipt);
        Assert.NotNull(state.CurrentSnapshot!.DecisionDataPolicy);
        Assert.Equal(SettingsEffect.Unknown, receipt.Effect);
        Assert.Null(receipt.DefaultsAcceptedV2);
        Assert.Null(state.LastAcceptedV2);
        Assert.Equal(expected, original.Expectation);
        state.ReloadCurrent();
        state.BeginReconciliation();
        await state.SaveAsync();
        Assert.Same(original, state.Draft);
        Assert.False(state.CanRecover);
        Assert.Equal(1, store.Calls[WorkspaceOperation.DefaultsSave]);
    }

    [Fact]
    public async Task Late_policy_acceptance_keeps_its_exact_receipt_without_touching_retired_or_successor_drafts() {
        var store = new WorkspaceScenarioStore();
        using var previous = new WorkspaceDefaultsController(store);
        await previous.RefreshAsync();
        Configure(previous.Draft);
        var expected = previous.Draft.Expectation;
        var gate = store.HoldNext(WorkspaceOperation.DefaultsSave);
        var pending = previous.SaveAsync();
        try {
            await gate.Entered.WaitAsync(TimeSpan.FromSeconds(10));
            previous.Dispose();
            using var next = new WorkspaceDefaultsController(store);
            await next.RefreshAsync();
            next.Draft.Model.Notes = "Successor input";
            gate.Release();
            await pending;
            var receipt = Assert.Single(previous.Receipts);
            Assert.Equal(SettingsEffect.Committed, receipt.Effect);
            Assert.NotNull(receipt.DefaultsAcceptedV2);
            Assert.Equal(expected, previous.Draft.Expectation);
            Assert.Equal("Successor input", next.Draft.Model.Notes);
            Assert.Empty(next.Receipts);
            Assert.Null(next.Draft.PolicyBaseline);
        } finally {
            gate.Release();
            await pending;
        }
    }

    private static DecisionDataPolicy Policy() => new(ReadBasisMode.StepSnapshot, PublicationFreshness.RequireCurrent,
        OnBasisChange.PauseForReview, new(0, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(1)));

    private static void Configure(WorkspaceDefaultsDraft draft) {
        draft.Policy.ReadBasisMode = ReadBasisMode.StepSnapshot;
        draft.Policy.PublicationFreshness = PublicationFreshness.RequireCurrent;
        draft.Policy.OnBasisChange = OnBasisChange.PauseForReview;
        draft.Policy.MaximumReplans = 0;
        draft.Policy.MaximumWorkDuration = "00:05:00";
        draft.Policy.MaximumBasisAge = "00:01:00";
        foreach (var field in Enum.GetValues<DefaultsField>().Where(field => field >= DefaultsField.PolicyReadBasis)) {
            draft.Edited(field);
        }
    }
}
