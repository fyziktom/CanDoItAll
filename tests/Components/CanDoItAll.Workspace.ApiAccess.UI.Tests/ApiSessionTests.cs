using CanDoItAll.Modules.Workspace.ApiAccess.Contracts;
using CanDoItAll.Workspace.ApiAccess.UI;
using CanDoItAll.Workspace.ApiAccess.UiSandbox;

namespace CanDoItAll.Tests.Components.WorkspaceApiUi;

public sealed class ApiSessionTests {
    [Theory]
    [InlineData(ApiScenario.Representative)]
    [InlineData(ApiScenario.Empty)]
    [InlineData(ApiScenario.MultiplePages)]
    [InlineData(ApiScenario.Denied)]
    [InlineData(ApiScenario.StatusUnavailable)]
    [InlineData(ApiScenario.AccessUnavailable)]
    [InlineData(ApiScenario.ReadUnavailable)]
    [InlineData(ApiScenario.AuthorizationOff)]
    [InlineData(ApiScenario.MissingSigningKey)]
    public async Task Scenarios_keep_status_access_and_list_availability_separate(ApiScenario scenario) {
        var store = new ApiScenarioStore(scenario);
        using var session = new ApiAccessSession(store, store, store, new());
        await session.LoadAsync();
        Assert.Equal(scenario != ApiScenario.StatusUnavailable, session.Configuration is not null);
        Assert.False(session.StatusLoading);
        Assert.False(session.ManagementLoading);
        var granted = scenario is ApiScenario.Representative or ApiScenario.Empty or ApiScenario.MultiplePages or ApiScenario.ReadUnavailable;
        Assert.Equal(granted, session.Issuance is not null);
        Assert.Equal(granted, session.Accounts is not null);
        Assert.Equal(0, store.Calls.GetValueOrDefault(ApiScenarioOperation.TokenSearch));
        if (scenario == ApiScenario.Empty) {
            Assert.Empty(session.Accounts!.Page.Page!.Items);
        } else if (scenario == ApiScenario.MultiplePages) {
            Assert.Equal(25, session.Accounts!.Page.Page!.Items.Length);
            Assert.Equal(61, session.Accounts.Page.Page.TotalCount);
        } else if (scenario == ApiScenario.ReadUnavailable) {
            Assert.Null(session.Accounts!.Page.Page);
            Assert.NotNull(session.Accounts.Page.Error);
        }
    }

    [Fact]
    public async Task Failed_retry_clears_prior_status_and_sensitive_disclosure() {
        var store = new ApiScenarioStore();
        using var session = new ApiAccessSession(store, store, store, new());
        await session.LoadAsync();
        var issuer = session.Issuance!;
        await issuer.IssueAsync();
        Assert.NotNull(issuer.Disclosure);
        store.FaultNext(ApiScenarioOperation.Status, ApiScenarioFault.Unavailable);
        await session.LoadAsync();
        Assert.Null(session.Configuration);
        Assert.Null(session.Issuance);
        Assert.Null(issuer.Disclosure);
        await session.LoadAsync();
        Assert.NotNull(session.Configuration);
        Assert.NotNull(session.Issuance);
        Assert.Null(session.Issuance.Disclosure);
        Assert.Equal(1, store.DurableWrites);
    }

    [Fact]
    public async Task Old_access_failure_cannot_retire_a_newer_successful_activation() {
        var store = new ApiScenarioStore();
        var configuration = await store.ReadAsync(CancellationToken.None);
        var firstAccess = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var owner = new HeldConfiguration(configuration, firstAccess);
        using var session = new ApiAccessSession(owner, store, store, new());
        var first = session.LoadAsync();
        Assert.True(session.ManagementLoading);
        Assert.Null(session.Issuance);
        await session.LoadAsync();
        var successor = session.Issuance;
        firstAccess.SetException(new IOException("late access failure"));
        await first;
        Assert.Same(successor, session.Issuance);
        Assert.NotNull(successor);
        Assert.Equal(ApiFailure.None, session.ManagementFailure);
        Assert.False(session.ManagementLoading);
    }

    [Theory]
    [InlineData(ApiScenarioFault.None)]
    [InlineData(ApiScenarioFault.Unavailable)]
    [InlineData(ApiScenarioFault.Denied)]
    public async Task Old_receipt_observation_cannot_replace_a_newer_result_or_retire_authority(ApiScenarioFault fault) {
        var store = new ApiScenarioStore();
        var ledger = new ApiOperationLedger();
        using var session = new ApiAccessSession(store, store, store, ledger);
        await session.LoadAsync();
        var identities = store.Tokens.Take(2).Select(token => token.Id).ToArray();
        var operations = identities.Select(id => {
            var operation = ledger.Begin(new(ApiTargetKind.Token, id), ApiWriteAction.RevokeToken)!.Value;
            ledger.Complete(operation, new(ApiWriteState.Unknown, ApiFailure.UnknownAcknowledgement, id));
            return operation;
        }).ToArray();
        var gate = store.HoldNext(ApiScenarioOperation.TokenObserve);
        store.FaultNext(ApiScenarioOperation.TokenObserve, fault);
        var first = session.ObserveAsync(operations[0]);
        await gate.Entered.Task;
        await session.ObserveAsync(operations[1]);
        var message = session.ObservationMessage;
        Assert.Contains(identities[1].ToString(), message);
        gate.Release();
        await first;
        Assert.Equal(message, session.ObservationMessage);
        Assert.NotNull(session.Issuance);
        Assert.Equal(ApiFailure.None, session.ManagementFailure);
        Assert.All(ledger.Receipts, receipt => Assert.Equal(ApiWriteState.Unknown, receipt.Result.State));
        Assert.Equal(0, store.DurableWrites);
    }

    private sealed class HeldConfiguration(ApiAccessConfiguration configuration, TaskCompletionSource<bool> access) : IApiAccessConfigurationOwner {
        private int calls;
        public Task<ApiAccessConfiguration> ReadAsync(CancellationToken cancellationToken) => Task.FromResult(configuration);
        public ValueTask<bool> CanManageAsync(CancellationToken cancellationToken) => ++calls == 1 ? new(access.Task) : ValueTask.FromResult(true);
    }
}
