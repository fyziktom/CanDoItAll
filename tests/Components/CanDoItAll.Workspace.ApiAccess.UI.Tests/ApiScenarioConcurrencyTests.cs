using CanDoItAll.Modules.Workspace.ApiAccess.Contracts;
using CanDoItAll.Workspace.ApiAccess.UiSandbox;

namespace CanDoItAll.Tests.Components.WorkspaceApiUi;

public sealed class ApiScenarioConcurrencyTests {
    [Theory]
    [InlineData(ApiWriteAction.UpdateAccount, ApiScenarioOperation.UpdateAccount)]
    [InlineData(ApiWriteAction.ResetPassword, ApiScenarioOperation.ResetPassword)]
    [InlineData(ApiWriteAction.DeleteAccount, ApiScenarioOperation.DeleteAccount)]
    public async Task Version_changed_during_held_write_is_refused(ApiWriteAction action, ApiScenarioOperation operation) {
        var store = new ApiScenarioStore();
        var original = store.Accounts[0];
        using var intent = new ApiAccountIntent(action, original.Id, original.Version, original.UserName,
            "Captured name", true, "", "fixture-password-only");
        var gate = store.HoldNext(operation);
        var writing = store.ApplyAsync(intent, CancellationToken.None);
        await gate.Entered.Task;
        store.ChangeAccountsExternally();
        gate.Release();
        var result = await writing;
        Assert.Equal(ApiWriteState.Refused, result.Outcome.State);
        Assert.Equal(ApiFailure.Conflict, result.Outcome.Failure);
        Assert.Equal(0, store.DurableWrites);
        var latest = Assert.Single(store.Accounts, account => account.Id == original.Id);
        Assert.Equal(2, latest.Version);
        Assert.Equal("Changed externally", latest.DisplayName);
    }

    [Fact]
    public async Task Concurrent_creates_cannot_commit_the_same_normalized_name() {
        var store = new ApiScenarioStore(ApiScenario.Empty);
        using var first = new ApiAccountIntent(ApiWriteAction.CreateAccount, null, null, "held-name", "First", true, "", "fixture-password-only");
        using var second = new ApiAccountIntent(ApiWriteAction.CreateAccount, null, null, "HELD-NAME", "Second", true, "", "fixture-password-only");
        var gate = store.HoldNext(ApiScenarioOperation.CreateAccount);
        var writing = store.ApplyAsync(first, CancellationToken.None);
        await gate.Entered.Task;
        var accepted = await store.ApplyAsync(second, CancellationToken.None);
        gate.Release();
        var refused = await writing;
        Assert.True(accepted.Outcome.IsCommitted);
        Assert.Equal(ApiFailure.Conflict, refused.Outcome.Failure);
        Assert.Equal(1, store.DurableWrites);
        Assert.Equal(accepted.Account!.Id, Assert.Single(store.Accounts).Id);
    }

    [Fact]
    public async Task Held_revocation_cannot_restore_a_deleted_token() {
        var store = new ApiScenarioStore();
        var token = store.Tokens[0];
        var gate = store.HoldNext(ApiScenarioOperation.RevokeToken);
        var revoking = store.ApplyAsync(new ApiTokenAction(token.Id, token.Kind, ApiWriteAction.RevokeToken), CancellationToken.None);
        await gate.Entered.Task;
        var deleted = await store.ApplyAsync(new ApiTokenAction(token.Id, token.Kind, ApiWriteAction.DeleteToken), CancellationToken.None);
        gate.Release();
        var refused = await revoking;
        Assert.True(deleted.IsCommitted);
        Assert.Equal(ApiFailure.Missing, refused.Failure);
        Assert.DoesNotContain(store.Tokens, item => item.Id == token.Id);
        Assert.Equal(1, store.DurableWrites);
    }
}
