using CanDoItAll.Modules.Workspace.ApiAccess.Contracts;
using CanDoItAll.Workspace.ApiAccess.UI;
using CanDoItAll.Workspace.ApiAccess.UiSandbox;

namespace CanDoItAll.Tests.Components.WorkspaceApiUi;

public sealed class ApiTokenTests {
    [Theory]
    [InlineData(ApiScenarioFault.None)]
    [InlineData(ApiScenarioFault.UnknownAfterCommit)]
    public async Task Reads_do_not_unlock_pending_mutation_and_completion_does_not_hijack_a_new_query(ApiScenarioFault fault) {
        var store = new ApiScenarioStore();
        using var authority = new ApiViewLifetime();
        var ledger = new ApiOperationLedger();
        using var controller = new ApiTokenListController(store, authority, ledger);
        await controller.Page.RefreshAsync();
        var token = controller.Page.Page!.Items[0];
        controller.Confirm(token, ApiWriteAction.RevokeToken);
        var confirmation = controller.Confirmation!;
        var gate = store.HoldNext(ApiScenarioOperation.RevokeToken);
        store.FaultNext(ApiScenarioOperation.RevokeToken, fault);
        var writing = controller.ApplyAsync(confirmation.Origin);
        await gate.Entered.Task;
        await controller.Page.RefreshAsync();
        await controller.ApplyAsync(confirmation.Origin);
        Assert.True(controller.ConfirmationBlocked);
        Assert.Equal(1, store.Calls.GetValueOrDefault(ApiScenarioOperation.RevokeToken));
        controller.Page.DesiredSearch = "new-query";
        await controller.Page.SearchAsync();
        var reads = store.Calls.GetValueOrDefault(ApiScenarioOperation.TokenSearch);
        gate.Release();
        await writing;
        Assert.Equal("new-query", controller.Page.AcceptedQuery!.Search);
        Assert.Equal(reads, store.Calls.GetValueOrDefault(ApiScenarioOperation.TokenSearch));
        if (fault == ApiScenarioFault.UnknownAfterCommit) {
            await controller.ObserveAsync();
            await controller.ApplyAsync(confirmation.Origin);
            Assert.True(controller.ConfirmationBlocked);
            Assert.Equal(ApiWriteState.Unknown, controller.Outcome!.State);
        }
        Assert.Equal(1, store.DurableWrites);
    }

    [Fact]
    public async Task Confirmation_captures_exact_identity_action_and_kind_and_does_not_close_a_successor() {
        var store = new ApiScenarioStore();
        using var authority = new ApiViewLifetime();
        using var controller = new ApiTokenListController(store, authority, new());
        await controller.Page.RefreshAsync();
        var first = controller.Page.Page!.Items[0];
        var second = controller.Page.Page.Items[1];
        controller.Confirm(first, ApiWriteAction.RevokeToken);
        var original = controller.Confirmation!;
        var gate = store.HoldNext(ApiScenarioOperation.RevokeToken);
        var writing = controller.ApplyAsync(original.Origin);
        await gate.Entered.Task;
        controller.CancelConfirmation(original.Origin);
        controller.Confirm(second, ApiWriteAction.DeleteToken);
        var successor = controller.Confirmation;
        await controller.ApplyAsync(original.Origin);
        gate.Release();
        await writing;
        Assert.Same(successor, controller.Confirmation);
        Assert.Equal(ApiCredentialStatus.Revoked, store.Tokens.Single(token => token.Id == first.Id).GetStatus(DateTimeOffset.UtcNow));
        Assert.Contains(store.Tokens, token => token.Id == second.Id);
        Assert.Equal(0, store.Calls.GetValueOrDefault(ApiScenarioOperation.DeleteToken));
    }

    [Fact]
    public async Task Default_machine_list_status_precedence_and_non_machine_handler_refusal_are_preserved() {
        var store = new ApiScenarioStore();
        using var authority = new ApiViewLifetime();
        using var controller = new ApiTokenListController(store, authority, new());
        await controller.Page.RefreshAsync();
        var now = DateTimeOffset.UtcNow;
        Assert.Contains(controller.Page.Page!.Items, token => token.GetStatus(now) == ApiCredentialStatus.Expired);
        var token = controller.Page.Page.Items[0];
        Assert.Equal(ApiCredentialStatus.Revoked, (token with { RevokedAtUtc = now, ExpiresAtUtc = now.AddHours(-1) }).GetStatus(now));
        controller.Confirm(token with { Kind = ApiTokenCategory.AdministratorSession }, ApiWriteAction.DeleteToken);
        Assert.Null(controller.Confirmation);
        await controller.ApplyAsync(Guid.NewGuid());
        Assert.Equal(0, store.DurableWrites);
    }
}
