using System.Text.Json;
using CanDoItAll.Modules.Workspace.ApiAccess.Contracts;
using CanDoItAll.Workspace.ApiAccess.UI;
using CanDoItAll.Workspace.ApiAccess.UiSandbox;

namespace CanDoItAll.Tests.Components.WorkspaceApiUi;

public sealed class ApiIssueTests {
    [Theory]
    [InlineData("-")]
    [InlineData("1.")]
    [InlineData(" 2")]
    [InlineData("0")]
    [InlineData("61")]
    public async Task Raw_invalid_lifetime_stays_invalid_until_explicit_correction(string raw) {
        var store = new ApiScenarioStore();
        using var session = new ApiAccessSession(store, store, store, new());
        await session.LoadAsync();
        var controller = session.Issuance!;
        controller.Draft.LifetimeText = raw;
        await controller.IssueAsync();
        Assert.Equal(raw, controller.Draft.LifetimeText);
        Assert.NotEmpty(controller.Form.GetValidationMessages());
        Assert.Equal(0, store.DurableWrites);
        controller.Draft.LifetimeText = "";
        await controller.IssueAsync();
        Assert.Equal(1, store.DurableWrites);
        Assert.Empty(controller.Form.GetValidationMessages());
        Assert.Equal(TimeSpan.FromMinutes(30), controller.Issued!.ExpiresAtUtc - controller.Issued.IssuedAtUtc);
    }

    [Fact]
    public async Task Held_permission_captures_fields_once_and_keeps_later_input() {
        var store = new ApiScenarioStore();
        using var session = new ApiAccessSession(store, store, store, new());
        await session.LoadAsync();
        var controller = session.Issuance!;
        controller.Draft.Subject = "original";
        controller.Draft.DisplayName = "Original";
        controller.Draft.LifetimeText = "17";
        var gate = store.HoldNext(ApiScenarioOperation.Access);
        var issuing = controller.IssueAsync();
        await gate.Entered.Task;
        controller.Draft.Subject = "later";
        controller.Draft.DisplayName = "Later";
        controller.Draft.LifetimeText = "29";
        controller.Draft.ScopeText = ApiScenarioStore.WriteScope;
        await controller.IssueAsync();
        Assert.Equal(0, store.DurableWrites);
        gate.Release();
        await issuing;
        Assert.Equal(1, store.DurableWrites);
        Assert.Equal("original", controller.Issued!.Subject);
        Assert.Equal("Original", controller.Issued.DisplayName);
        Assert.Equal(new[] { ApiScenarioStore.ReadScope }, controller.Issued.Scopes.ToArray());
        Assert.Equal(TimeSpan.FromMinutes(17), controller.Issued.ExpiresAtUtc - controller.Issued.IssuedAtUtc);
        Assert.Equal("later", controller.Draft.Subject);
        Assert.Equal("29", controller.Draft.LifetimeText);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Acknowledged_registration_survives_retirement_without_late_disclosure_or_replay(bool retire) {
        var store = new ApiScenarioStore();
        var ledger = new ApiOperationLedger();
        using var session = new ApiAccessSession(store, store, store, ledger);
        await session.LoadAsync();
        var controller = session.Issuance!;
        var gate = store.HoldNext(ApiScenarioOperation.IssueToken);
        var issuing = controller.IssueAsync();
        await gate.Entered.Task;
        if (retire) {
            session.Dispose();
        }
        await controller.IssueAsync();
        gate.Release();
        await issuing;
        var receipt = Assert.Single(ledger.Receipts);
        Assert.Equal(ApiWriteState.Committed, receipt.Result.State);
        Assert.Equal(1, store.DurableWrites);
        Assert.Contains(store.Tokens, token => token.Id == receipt.Result.Identity);
        Assert.Equal(!retire, controller.Disclosure is not null);
        controller.Dismiss();
        Assert.Null(controller.Disclosure);
        Assert.Equal(1, store.Calls.GetValueOrDefault(ApiScenarioOperation.IssueToken));
        Assert.Equal(0, store.Calls.GetValueOrDefault(ApiScenarioOperation.RevokeToken));
    }

    [Theory]
    [InlineData(ApiScenarioFault.UnknownAfterCommit, true)]
    [InlineData(ApiScenarioFault.UnknownWithoutCommit, false)]
    public async Task Unknown_candidate_is_not_a_commit_and_observation_never_unlocks_retry(ApiScenarioFault fault, bool exists) {
        var store = new ApiScenarioStore();
        var ledger = new ApiOperationLedger();
        using var session = new ApiAccessSession(store, store, store, ledger);
        await session.LoadAsync();
        var controller = session.Issuance!;
        store.FaultNext(ApiScenarioOperation.IssueToken, fault);
        await controller.IssueAsync();
        var outcome = controller.Outcome;
        Assert.Equal(ApiWriteState.Unknown, outcome!.State);
        Assert.NotNull(outcome.Identity);
        Assert.Null(controller.Disclosure);
        Assert.Null(controller.Issued);
        await controller.ObserveAsync();
        Assert.Equal(exists, controller.Observation is not null);
        Assert.Same(outcome, controller.Outcome);
        store.FaultNext(ApiScenarioOperation.TokenObserve, ApiScenarioFault.Unavailable);
        await controller.ObserveAsync();
        Assert.Same(outcome, controller.Outcome);
        await controller.IssueAsync();
        await session.LoadAsync();
        await session.Issuance!.IssueAsync();
        Assert.Equal(1, store.Calls.GetValueOrDefault(ApiScenarioOperation.IssueToken));
        Assert.Single(ledger.Receipts);
    }

    [Fact]
    public async Task Disclosure_and_password_are_absent_from_serialization_and_receipts() {
        const string sentinel = "private-sentinel-material";
        using var command = new ApiAccountIntent(ApiWriteAction.CreateAccount, null, null, "safe", "Safe", true, "", sentinel);
        using var disclosure = new ApiTokenDisclosure(new(ApiWriteState.Committed), value: sentinel);
        Assert.DoesNotContain(sentinel, JsonSerializer.Serialize(command));
        Assert.DoesNotContain(sentinel, command.ToString());
        Assert.DoesNotContain(sentinel, JsonSerializer.Serialize(disclosure));
        Assert.DoesNotContain(sentinel, disclosure.ToString());
        Assert.Equal(sentinel, disclosure.TakeValue());
        Assert.Null(disclosure.TakeValue());
        command.Dispose();
        Assert.Empty(command.TakePassword());
        var store = new ApiScenarioStore();
        using var session = new ApiAccessSession(store, store, store, new());
        await session.LoadAsync();
        await session.Issuance!.IssueAsync();
        var value = session.Issuance.Disclosure!;
        Assert.DoesNotContain(value, JsonSerializer.Serialize(session.Receipts));
        Assert.DoesNotContain(value, JsonSerializer.Serialize(session.Issuance.Draft));
    }

    [Fact]
    public async Task Denied_new_operation_retires_existing_disclosure_and_active_editors() {
        var store = new ApiScenarioStore();
        using var session = new ApiAccessSession(store, store, store, new());
        await session.LoadAsync();
        var issuance = session.Issuance!;
        await issuance.IssueAsync();
        var accounts = session.Accounts!;
        accounts.Create();
        var draft = accounts.Editor!;
        draft.Password = "private-sentinel-material";
        store.Allowed = false;
        await issuance.IssueAsync();
        Assert.Equal(ApiFailure.Denied, session.ManagementFailure);
        Assert.Null(issuance.Disclosure);
        Assert.Empty(draft.Password);
        Assert.Null(session.Accounts);
        Assert.Equal(1, store.DurableWrites);
    }
}
