using CanDoItAll.Modules.Workspace.ApiAccess.Contracts;
using CanDoItAll.Workspace.ApiAccess.UI;
using CanDoItAll.Workspace.ApiAccess.UiSandbox;

namespace CanDoItAll.Tests.Components.WorkspaceApiUi;

public sealed class ApiAccountTests {
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Known_identity_and_version_are_accepted_before_readback_while_later_fields_survive(bool existing, bool warning) {
        var store = new ApiScenarioStore();
        using var session = new ApiAccessSession(store, store, store, new());
        await session.LoadAsync();
        var controller = session.Accounts!;
        if (existing) {
            await controller.OpenAsync(store.Accounts[0].Id, ApiWriteAction.UpdateAccount);
        } else {
            controller.Create();
        }
        var draft = controller.Editor!;
        draft.UserName = "captured-name";
        draft.DisplayName = "Captured display";
        draft.Password = "private-sentinel-material";
        var form = draft.Form;
        var operation = existing ? ApiScenarioOperation.UpdateAccount : ApiScenarioOperation.CreateAccount;
        var write = store.HoldNext(operation);
        var read = store.HoldNext(ApiScenarioOperation.AccountSearch);
        store.FaultNext(ApiScenarioOperation.AccountSearch, ApiScenarioFault.Unavailable);
        if (warning) {
            store.FaultNext(operation, ApiScenarioFault.CommittedWarning);
        }
        var saving = controller.SaveAsync(draft.Origin);
        await write.Entered.Task;
        draft.DisplayName = "Later display";
        draft.ScopeText = ApiScenarioStore.WriteScope;
        await controller.SaveAsync(draft.Origin);
        write.Release();
        await read.Entered.Task;
        Assert.Equal(existing ? 2 : 1, draft.Version);
        Assert.NotNull(draft.Id);
        Assert.Equal(draft.Id, controller.SavedAccount!.Id);
        Assert.Equal("Captured display", controller.SavedAccount.DisplayName);
        Assert.Equal("Later display", draft.DisplayName);
        Assert.Equal(ApiScenarioStore.WriteScope, draft.ScopeText);
        Assert.Same(form, draft.Form);
        Assert.Empty(draft.Password);
        Assert.Equal(warning ? ApiWriteState.CommittedWithWarning : ApiWriteState.Committed, controller.Outcome!.State);
        read.Release();
        await saving;
        Assert.NotNull(controller.Page.Error);
        Assert.Equal(1, store.DurableWrites);
        Assert.Same(draft, controller.Editor);
        await controller.Page.RefreshAsync();
        Assert.Equal(1, store.DurableWrites);
        Assert.Null(controller.Page.Error);
    }

    [Theory]
    [InlineData(Retirement.New)]
    [InlineData(Retirement.Select)]
    [InlineData(Retirement.Dispose)]
    public async Task An_old_save_cannot_lend_its_identity_password_or_close_to_a_successor(Retirement retirement) {
        var store = new ApiScenarioStore();
        var ledger = new ApiOperationLedger();
        using var session = new ApiAccessSession(store, store, store, ledger);
        await session.LoadAsync();
        var controller = session.Accounts!;
        controller.Create();
        var original = controller.Editor!;
        original.UserName = "new-original";
        original.DisplayName = "Original";
        original.Password = "private-sentinel-material";
        var gate = store.HoldNext(ApiScenarioOperation.CreateAccount);
        var saving = controller.SaveAsync(original.Origin);
        await gate.Entered.Task;
        switch (retirement) {
            case Retirement.New:
                controller.Create();
                break;
            case Retirement.Select:
                await controller.OpenAsync(store.Accounts[0].Id, ApiWriteAction.ResetPassword);
                break;
            case Retirement.Dispose:
                controller.Dispose();
                break;
        }
        var successor = controller.Editor;
        var successorId = successor?.Id;
        Assert.Empty(original.Password);
        gate.Release();
        await saving;
        Assert.Same(successor, controller.Editor);
        Assert.Equal(successorId, controller.Editor?.Id);
        Assert.Equal(1, store.DurableWrites);
        Assert.Equal(ApiWriteState.Committed, Assert.Single(ledger.Receipts).Result.State);
    }

    [Theory]
    [InlineData(ApiScenarioFault.UnknownWithoutCommit)]
    [InlineData(ApiScenarioFault.UnknownAfterCommit)]
    public async Task Unknown_create_retains_candidate_without_adopting_identity_or_replaying(ApiScenarioFault fault) {
        var store = new ApiScenarioStore();
        using var session = new ApiAccessSession(store, store, store, new());
        await session.LoadAsync();
        var controller = session.Accounts!;
        controller.Create();
        var draft = controller.Editor!;
        draft.UserName = "unknown-new";
        draft.DisplayName = "Unknown";
        draft.Password = "private-sentinel-material";
        store.FaultNext(ApiScenarioOperation.CreateAccount, fault);
        await controller.SaveAsync(draft.Origin);
        Assert.Null(draft.Id);
        Assert.Null(draft.Version);
        Assert.Empty(draft.Password);
        Assert.True(draft.Unknown);
        Assert.False(controller.CanSave);
        await controller.ObserveOutcomeAsync();
        Assert.Equal(ApiWriteState.Unknown, controller.Outcome!.State);
        Assert.False(controller.CanSave);
        controller.Close(draft.Origin);
        controller.Create();
        controller.Editor!.UserName = "UNKNOWN-NEW";
        controller.Editor.DisplayName = "Retry refused";
        controller.Editor.Password = "private-sentinel-material";
        await controller.SaveAsync(controller.Editor.Origin);
        Assert.Equal(1, store.Calls.GetValueOrDefault(ApiScenarioOperation.CreateAccount));
    }

    [Fact]
    public async Task Missing_and_stale_accounts_require_exact_read_and_do_not_write() {
        var store = new ApiScenarioStore();
        using var session = new ApiAccessSession(store, store, store, new());
        await session.LoadAsync();
        var controller = session.Accounts!;
        var id = store.Accounts[0].Id;
        store.FaultNext(ApiScenarioOperation.AccountObserve, ApiScenarioFault.Missing);
        await controller.OpenAsync(id, ApiWriteAction.UpdateAccount);
        Assert.Null(controller.Editor);
        Assert.NotNull(controller.EditorError);
        await controller.SaveAsync(Guid.NewGuid());
        await controller.OpenAsync(id, ApiWriteAction.UpdateAccount);
        var draft = controller.Editor!;
        store.ChangeAccountsExternally();
        await controller.SaveAsync(draft.Origin);
        Assert.Equal(ApiFailure.Conflict, controller.Outcome!.Failure);
        Assert.Equal(0, store.DurableWrites);
        Assert.Equal(1, draft.Version);
        await controller.OpenAsync(id, ApiWriteAction.UpdateAccount);
        Assert.Equal(2, controller.Editor!.Version);
    }

    [Fact]
    public async Task Password_reset_clears_completed_secret_and_preserves_empty_grants() {
        var store = new ApiScenarioStore();
        using var session = new ApiAccessSession(store, store, store, new());
        await session.LoadAsync();
        var controller = session.Accounts!;
        var account = store.Accounts[0];
        await controller.OpenAsync(account.Id, ApiWriteAction.ResetPassword);
        var draft = controller.Editor!;
        draft.Password = " private-sentinel-material ";
        await controller.SaveAsync(draft.Origin);
        Assert.Empty(draft.Password);
        Assert.Null(controller.Editor);
        Assert.Empty(controller.SavedAccount!.Scopes);
        Assert.Equal(2, controller.SavedAccount.Version);
    }

    [Fact]
    public void Receipt_capacity_preserves_unresolved_operations_and_allows_independent_targets() {
        var ledger = new ApiOperationLedger(2);
        var firstTarget = new ApiWriteTarget(ApiTargetKind.Account, Guid.NewGuid());
        var first = ledger.Begin(firstTarget, ApiWriteAction.UpdateAccount)!.Value;
        Assert.Null(ledger.Begin(firstTarget, ApiWriteAction.DeleteAccount));
        ledger.Complete(first, new(ApiWriteState.Unknown, ApiFailure.UnknownAcknowledgement, firstTarget.Id));
        var secondTarget = new ApiWriteTarget(ApiTargetKind.Token, Guid.NewGuid());
        var second = ledger.Begin(secondTarget, ApiWriteAction.RevokeToken)!.Value;
        Assert.Null(ledger.Begin(new(ApiTargetKind.Account, Guid.NewGuid()), ApiWriteAction.UpdateAccount));
        ledger.Complete(second, new(ApiWriteState.Committed, Identity: secondTarget.Id));
        Assert.NotNull(ledger.Begin(new(ApiTargetKind.Account, Guid.NewGuid()), ApiWriteAction.UpdateAccount));
        Assert.Contains(ledger.Receipts, receipt => receipt.OperationId == first && receipt.Result.State == ApiWriteState.Unknown);
        Assert.Equal(2, ledger.Receipts.Count);
    }

    [Fact]
    public async Task Reset_keeps_admitted_writes_with_the_original_store_and_bounds_retention() {
        using var workspace = new ApiScenarioWorkspace();
        await workspace.Session.LoadAsync();
        var original = workspace.Store;
        var gate = original.HoldNext(ApiScenarioOperation.IssueToken);
        var issuing = workspace.Session.Issuance!.IssueAsync();
        await gate.Entered.Task;
        await workspace.ResetAsync(ApiScenario.Empty);
        Assert.Equal(0, workspace.Store.DurableWrites);
        gate.Release();
        await issuing;
        Assert.Equal(1, original.DurableWrites);
        Assert.Empty(workspace.Store.Tokens);
        Assert.Null(workspace.Session.Issuance!.Disclosure);
        for (var index = 0; index < 12; index++) {
            await workspace.ResetAsync(ApiScenario.Empty);
        }
        Assert.Equal(8, workspace.Retired.Count);
    }

    public enum Retirement { New, Select, Dispose }
}
