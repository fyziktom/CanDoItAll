using Bunit;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.Workspace.UI;
using CanDoItAll.Workspace.UiSandbox;

namespace CanDoItAll.Tests.Components.WorkspaceUi;

public sealed class WorkspaceFileIdentityTests {
    [Theory]
    [InlineData(false, WorkspaceFault.None)]
    [InlineData(true, WorkspaceFault.None)]
    [InlineData(false, WorkspaceFault.CommittedWarning)]
    [InlineData(true, WorkspaceFault.CommittedWarning)]
    [InlineData(false, WorkspaceFault.ReadFailure)]
    [InlineData(true, WorkspaceFault.ReadFailure)]
    public async Task Known_destination_survives_later_path_input_and_secondary_failures(bool create, WorkspaceFault fault) {
        var store = new WorkspaceScenarioStore();
        using var state = new WorkspaceFilesController(store);
        await state.RefreshAsync();
        if (!create) {
            state.Select(Assert.Single(state.Preferences));
        }
        state.Draft.Extension = " NEXT ";
        state.Draft.ExecutablePath = "/synthetic/captured";
        state.Draft.Edited();
        var form = state.Draft.EditContext;
        store.FaultNext(fault == WorkspaceFault.ReadFailure ? WorkspaceOperation.FileList : WorkspaceOperation.FileSave, fault);
        var gate = store.HoldNext(WorkspaceOperation.FileSave);
        var pending = state.SaveAsync();
        await gate.Entered.WaitAsync(TimeSpan.FromSeconds(10));
        state.Draft.ExecutablePath = "/synthetic/later";
        state.Draft.Edited();
        gate.Release();
        await pending;
        Assert.Equal(new WorkspaceFileExtension(".next"), state.Draft.Selected);
        Assert.Equal(".next", state.Draft.Extension);
        Assert.Equal("/synthetic/later", state.Draft.ExecutablePath);
        Assert.Same(form, state.Draft.EditContext);
        Assert.Equal(fault == WorkspaceFault.None ? SettingsEffect.Committed : SettingsEffect.CommittedWarning,
            Assert.Single(state.Receipts).Effect);
        await state.DeleteAsync();
        Assert.Equal(".next", state.Receipts.Last().Extension!.Value.Value);
        Assert.Equal(".sample", Assert.Single(state.Preferences).Extension.Value);
    }

    [Theory]
    [InlineData(TargetChange.Edit)]
    [InlineData(TargetChange.AwayAndBack)]
    [InlineData(TargetChange.New)]
    [InlineData(TargetChange.Select)]
    [InlineData(TargetChange.Dispose)]
    public async Task Old_save_cannot_adopt_identity_into_changed_target(TargetChange change) {
        var store = new WorkspaceScenarioStore();
        using var state = new WorkspaceFilesController(store);
        await state.RefreshAsync();
        state.Select(Assert.Single(state.Preferences));
        state.Draft.Extension = " NEXT ";
        state.Draft.Edited();
        var gate = store.HoldNext(WorkspaceOperation.FileSave);
        var pending = state.SaveAsync();
        await gate.Entered.WaitAsync(TimeSpan.FromSeconds(10));
        switch (change) {
            case TargetChange.Edit:
                state.Draft.Extension = ".later";
                state.Draft.Edited();
                break;
            case TargetChange.AwayAndBack:
                state.Draft.Extension = ".later";
                state.Draft.Edited();
                state.Draft.Extension = " NEXT ";
                state.Draft.Edited();
                break;
            case TargetChange.New:
                state.New();
                break;
            case TargetChange.Select:
                state.Select(new(new(".other"), "/synthetic/other", false));
                break;
            case TargetChange.Dispose:
                state.Dispose();
                break;
        }
        var draft = state.Draft;
        var selected = draft.Selected;
        var raw = draft.Extension;
        gate.Release();
        await pending;
        Assert.Same(draft, state.Draft);
        Assert.Equal(selected, draft.Selected);
        Assert.Equal(raw, draft.Extension);
        Assert.Equal(SettingsEffect.Committed, Assert.Single(state.Receipts).Effect);
        var persisted = await ((IWorkspaceFilesOwner)store).ListAsync(default);
        Assert.Contains(persisted, item => item.Extension.Value == ".sample");
        Assert.Contains(persisted, item => item.Extension.Value == ".next");
    }

    [Fact]
    public async Task Unknown_destination_is_not_adopted_or_unlocked_by_observation() {
        var store = new WorkspaceScenarioStore();
        using var state = new WorkspaceFilesController(store);
        await state.RefreshAsync();
        state.Select(Assert.Single(state.Preferences));
        state.Draft.Extension = ".next";
        store.FaultNext(WorkspaceOperation.FileSave, WorkspaceFault.Unknown);
        await state.SaveAsync();
        var receipt = Assert.Single(state.Receipts);
        Assert.Equal(SettingsEffect.Unknown, receipt.Effect);
        Assert.Equal(new WorkspaceFileExtension(".sample"), state.Draft.Selected);
        await state.ReviewAsync(receipt);
        Assert.True(receipt.ObservedExists);
        await state.SaveAsync();
        await state.DeleteAsync();
        Assert.False(state.CanMutate);
        Assert.Equal(1, store.Calls[WorkspaceOperation.FileSave]);
        Assert.False(store.Calls.ContainsKey(WorkspaceOperation.FileDelete));
    }

    [Fact]
    public async Task Path_edit_during_save_adopts_confirmed_destination_before_delete() {
        var store = new WorkspaceScenarioStore();
        using var state = new WorkspaceFilesController(store);
        await state.RefreshAsync();
        state.Select(Assert.Single(state.Preferences));
        var draft = state.Draft;
        draft.Extension = ".next";
        draft.Edited();
        var gate = store.HoldNext(WorkspaceOperation.FileSave);
        var pending = state.SaveAsync();
        await gate.Entered.WaitAsync(TimeSpan.FromSeconds(10));
        draft.ExecutablePath = "/synthetic/later";
        draft.Edited();
        gate.Release();
        await pending;
        Assert.Same(draft, state.Draft);
        Assert.Equal("/synthetic/later", draft.ExecutablePath);
        Assert.Equal(new WorkspaceFileExtension(".next"), draft.Selected);
        await state.DeleteAsync();
        Assert.Equal(".next", state.Receipts.Last().Extension!.Value.Value);
        Assert.Equal(".sample", Assert.Single(state.Preferences).Extension.Value);
    }

    [Fact]
    public async Task Immediate_path_input_preserves_form_and_next_delete_targets_saved_destination() {
        var store = new WorkspaceScenarioStore();
        using var state = new WorkspaceFilesController(store);
        await state.RefreshAsync();
        using var context = WorkspaceDraftTests.Context();
        var cut = context.Render<WorkspaceFilesSurface>(p => p.Add(x => x.State, state));
        await cut.InvokeAsync(() => cut.Find(".cda-selection-list-item__button").ClickAsync());
        var draft = state.Draft;
        var form = draft.EditContext;
        await cut.InvokeAsync(() => cut.Find("[data-testid=file-application-extension]").Input(" NEXT "));
        var gate = store.HoldNext(WorkspaceOperation.FileSave);
        var pending = cut.InvokeAsync(() => cut.Find("form").SubmitAsync());
        await gate.Entered.WaitAsync(TimeSpan.FromSeconds(10));
        await cut.InvokeAsync(() => cut.Find("[data-testid=file-application-executable]").Input("/synthetic/later"));
        gate.Release();
        await pending;
        Assert.Same(form, state.Draft.EditContext);
        Assert.Equal("/synthetic/later", state.Draft.ExecutablePath);
        Assert.Equal(new WorkspaceFileExtension(".next"), state.Draft.Selected);
        await cut.InvokeAsync(() => cut.Find("[data-testid=file-application-delete]").ClickAsync());
        Assert.Equal(".next", state.Receipts.Last().Extension!.Value.Value);
        Assert.Equal(".sample", Assert.Single(state.Preferences).Extension.Value);
    }

    public enum TargetChange { Edit, AwayAndBack, New, Select, Dispose }
}
