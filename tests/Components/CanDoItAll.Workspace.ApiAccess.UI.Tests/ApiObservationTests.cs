using CanDoItAll.Modules.Workspace.ApiAccess.Contracts;
using CanDoItAll.Workspace.ApiAccess.UI;
using CanDoItAll.Workspace.ApiAccess.UiSandbox;

namespace CanDoItAll.Tests.Components.WorkspaceApiUi;

public sealed class ApiObservationTests {
    [Theory]
    [InlineData(Surface.Issuance, ApiScenarioFault.None)]
    [InlineData(Surface.Issuance, ApiScenarioFault.Unavailable)]
    [InlineData(Surface.Issuance, ApiScenarioFault.Denied)]
    [InlineData(Surface.Tokens, ApiScenarioFault.None)]
    [InlineData(Surface.Tokens, ApiScenarioFault.Unavailable)]
    [InlineData(Surface.Tokens, ApiScenarioFault.Denied)]
    [InlineData(Surface.Accounts, ApiScenarioFault.None)]
    [InlineData(Surface.Accounts, ApiScenarioFault.Unavailable)]
    [InlineData(Surface.Accounts, ApiScenarioFault.Denied)]
    public async Task Observation_of_an_old_outcome_cannot_publish_after_a_new_write(Surface surface, ApiScenarioFault fault) {
        var store = new ApiScenarioStore();
        var configuration = await store.ReadAsync(CancellationToken.None);
        using var authority = new ApiViewLifetime();
        var ledger = new ApiOperationLedger();
        using var issuance = new ApiTokenIssueController(store, configuration, authority, ledger);
        using var tokens = new ApiTokenListController(store, authority, ledger);
        using var accounts = new ApiAccountController(store, configuration, authority, ledger);
        await tokens.Page.RefreshAsync();
        await accounts.Page.RefreshAsync();
        var token = store.Tokens[0];
        var account = store.Accounts[0];
        await Write();
        var operation = surface == Surface.Accounts ? ApiScenarioOperation.AccountObserve : ApiScenarioOperation.TokenObserve;
        var gate = store.HoldNext(operation);
        store.FaultNext(operation, fault);
        var observing = surface switch {
            Surface.Issuance => issuance.ObserveAsync(),
            Surface.Tokens => tokens.ObserveAsync(),
            _ => accounts.ObserveOutcomeAsync()
        };
        await gate.Entered.Task;
        await Write();
        gate.Release();
        await observing;
        Assert.True(authority.IsActive);
        Assert.Null(issuance.ObservationMessage);
        Assert.Null(tokens.ObservationMessage);
        Assert.Null(accounts.ObservationMessage);
        Assert.Equal(2, store.DurableWrites);
        Assert.All(ledger.Receipts, receipt => Assert.True(receipt.Result.IsCommitted));

        async Task Write() {
            if (surface == Surface.Issuance) {
                await issuance.IssueAsync();
            } else if (surface == Surface.Tokens) {
                tokens.Confirm(token, store.DurableWrites == 0 ? ApiWriteAction.RevokeToken : ApiWriteAction.DeleteToken);
                await tokens.ApplyAsync(tokens.Confirmation!.Origin);
            } else {
                await accounts.OpenAsync(account.Id, ApiWriteAction.UpdateAccount);
                accounts.Editor!.DisplayName = $"Write {store.DurableWrites}";
                await accounts.SaveAsync(accounts.Editor.Origin);
            }
        }
    }

    public enum Surface { Issuance, Tokens, Accounts }
}
