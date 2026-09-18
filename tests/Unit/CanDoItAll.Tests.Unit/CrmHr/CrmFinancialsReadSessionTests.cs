using CanDoItAll.CrmHr.UI.Financials;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.CrmHr.Components;

namespace CanDoItAll.Tests.Unit.CrmHr;

// The Financials read owner: one accepted snapshot per read, explicit phases, retry admission, the local period
// projection and the fencing of late results from a query that ignores cancellation.
public sealed class CrmFinancialsReadSessionTests
{
    private static readonly Guid AccountA = Guid.Parse("85000000-0000-0000-0000-00000000000a");
    private static readonly Guid AccountB = Guid.Parse("85000000-0000-0000-0000-00000000000b");

    [Fact]
    public async Task First_load_is_loading_and_then_ready_with_the_mapped_snapshot_and_the_monthly_period()
    {
        var query = new ScriptedFinancialQuery();
        var changes = 0;
        using var session = new CrmFinancialsReadSession(query) { Changed = () => { changes++; return Task.CompletedTask; } };
        Assert.Equal(CrmHrFinancialsPhase.Loading, session.Presentation.Phase);

        var load = session.LoadAsync(AccountA);

        Assert.Equal(AccountA, session.Target);
        Assert.Equal(CrmHrFinancialsPhase.Loading, session.Presentation.Phase);
        Assert.Equal(CrmHrFinancialPeriod.Month, session.Presentation.Period);
        Assert.Equal(AccountA, Assert.Single(query.Calls).AccountPartyId);
        Assert.Equal(1, changes);

        query.Complete(0, Snapshot(AccountA, ("USD", 150m), ("EUR", 80m)));
        await load;

        Assert.Equal(CrmHrFinancialsPhase.Ready, session.Presentation.Phase);
        var snapshot = Assert.IsType<CrmHrFinancialsSnapshot>(session.Presentation.Snapshot);
        Assert.Equal(AccountA, snapshot.AccountPartyId);
        Assert.Equal(new[] { ("EUR", 80m), ("USD", 150m) }, snapshot.SoldTotals.Select(total => (total.CurrencyCode, total.Amount)));
        Assert.Equal(2, changes);
    }

    [Fact]
    public async Task A_same_account_echo_issues_no_read_and_does_not_replay_a_failed_one()
    {
        var query = new ScriptedFinancialQuery();
        using var session = new CrmFinancialsReadSession(query);
        var load = session.LoadAsync(AccountA);
        await session.LoadAsync(AccountA);
        Assert.Single(query.Calls);

        query.Fail(0, new InvalidOperationException("secret-provider-detail"));
        await load;

        Assert.Equal(CrmHrFinancialsPhase.Failed, session.Presentation.Phase);
        Assert.Equal(CrmFinancialsReadSession.FailureMessage, session.Presentation.FailureMessage);
        await session.LoadAsync(AccountA);
        Assert.Single(query.Calls);
    }

    [Fact]
    public async Task Another_account_retires_the_pending_read_and_the_late_outcomes_of_the_first_are_inert()
    {
        var query = new ScriptedFinancialQuery();
        using var session = new CrmFinancialsReadSession(query);

        var loadA = session.LoadAsync(AccountA);
        var loadB = session.LoadAsync(AccountB);
        Assert.Equal(AccountB, session.Target);
        Assert.True(query.Calls[0].Token.IsCancellationRequested);
        Assert.Equal(CrmHrFinancialsPhase.Loading, session.Presentation.Phase);

        // A's late success arrives while B is loading: B's phase, figures and failure are untouched.
        query.Complete(0, Snapshot(AccountA, ("USD", 999m)));
        await loadA;
        Assert.Equal(CrmHrFinancialsPhase.Loading, session.Presentation.Phase);
        Assert.Null(session.Presentation.Snapshot);

        query.Complete(1, Snapshot(AccountB, ("EUR", 1m)));
        await loadB;
        Assert.Equal(AccountB, session.Presentation.Snapshot?.AccountPartyId);

        // Back to A: a fresh read; the first A outcome has no way back in, and A's late failure is inert too.
        var loadAgain = session.LoadAsync(AccountA);
        Assert.Equal(3, query.Calls.Count);
        Assert.Equal(CrmHrFinancialsPhase.Loading, session.Presentation.Phase);
        query.Complete(2, Snapshot(AccountA, ("USD", 5m)));
        await loadAgain;
        Assert.Equal(5m, Assert.Single(session.Presentation.Snapshot!.SoldTotals).Amount);
    }

    [Fact]
    public async Task A_late_failure_of_a_retired_read_does_not_fail_the_current_account()
    {
        var query = new ScriptedFinancialQuery();
        using var session = new CrmFinancialsReadSession(query);
        var loadA = session.LoadAsync(AccountA);
        var loadB = session.LoadAsync(AccountB);

        query.Fail(0, new IOException("late A failure"));
        await loadA;
        Assert.Equal(CrmHrFinancialsPhase.Loading, session.Presentation.Phase);
        Assert.Null(session.Presentation.FailureMessage);

        query.Complete(1, Snapshot(AccountB));
        await loadB;
        Assert.Equal(CrmHrFinancialsPhase.Ready, session.Presentation.Phase);
        Assert.Equal(CrmHrFinancialAvailability.Empty, session.Presentation.Snapshot?.SoldAvailability);
    }

    [Fact]
    public async Task A_snapshot_for_another_account_is_never_accepted()
    {
        var query = new ScriptedFinancialQuery();
        using var session = new CrmFinancialsReadSession(query);
        var load = session.LoadAsync(AccountA);

        query.Complete(0, Snapshot(AccountB, ("USD", 1m)));
        await load;

        Assert.Equal(CrmHrFinancialsPhase.Failed, session.Presentation.Phase);
        Assert.Null(session.Presentation.Snapshot);
    }

    [Fact]
    public async Task An_empty_account_identifier_fails_without_a_read()
    {
        var query = new ScriptedFinancialQuery();
        using var session = new CrmFinancialsReadSession(query);

        await session.LoadAsync(Guid.Empty);

        Assert.Empty(query.Calls);
        Assert.Equal(CrmHrFinancialsPhase.Failed, session.Presentation.Phase);
        Assert.Equal(CrmFinancialsReadSession.AccountRequiredMessage, session.Presentation.FailureMessage);
        await session.RetryAsync(session.Presentation.Generation);
        Assert.Empty(query.Calls);
    }

    [Fact]
    public async Task Retry_reads_the_failed_account_again_and_stale_duplicate_or_idle_retries_are_ignored()
    {
        var query = new ScriptedFinancialQuery();
        using var session = new CrmFinancialsReadSession(query);
        var load = session.LoadAsync(AccountA);
        query.Fail(0, new IOException("offline"));
        await load;
        var failedGeneration = session.Presentation.Generation;

        var retry = session.RetryAsync(failedGeneration);
        Assert.True(session.Presentation.IsRetrying);
        Assert.Equal(CrmHrFinancialsPhase.Failed, session.Presentation.Phase);
        Assert.Equal(2, query.Calls.Count);

        await session.RetryAsync(session.Presentation.Generation);
        await session.RetryAsync(failedGeneration);
        Assert.Equal(2, query.Calls.Count);

        query.Complete(1, Snapshot(AccountA, ("USD", 10m)));
        await retry;
        Assert.Equal(CrmHrFinancialsPhase.Ready, session.Presentation.Phase);
        Assert.False(session.Presentation.IsRetrying);

        // A retry of a ready account is not a refresh.
        await session.RetryAsync(session.Presentation.Generation);
        Assert.Equal(2, query.Calls.Count);
    }

    [Fact]
    public async Task Period_switches_are_local_and_survive_an_account_change_on_the_same_instance()
    {
        var query = new ScriptedFinancialQuery();
        using var session = new CrmFinancialsReadSession(query);
        var load = session.LoadAsync(AccountA);
        query.Complete(0, Snapshot(AccountA, ("USD", 10m)));
        await load;

        session.SetPeriod(CrmHrFinancialPeriod.Year);
        session.SetPeriod(CrmHrFinancialPeriod.Year);

        Assert.Equal(CrmHrFinancialPeriod.Year, session.Presentation.Period);
        Assert.Equal(CrmHrFinancialsPhase.Ready, session.Presentation.Phase);
        Assert.Single(query.Calls);

        var loadB = session.LoadAsync(AccountB);
        Assert.Equal(CrmHrFinancialPeriod.Year, session.Presentation.Period);
        query.Complete(1, Snapshot(AccountB, ("EUR", 1m)));
        await loadB;
        Assert.Equal(CrmHrFinancialPeriod.Year, session.Presentation.Period);
        Assert.Equal(2, query.Calls.Count);
    }

    [Fact]
    public async Task Disposal_makes_late_outcomes_inert_and_a_cancellation_honouring_query_is_silent()
    {
        var query = new ScriptedFinancialQuery();
        var session = new CrmFinancialsReadSession(query);
        var load = session.LoadAsync(AccountA);

        session.Dispose();
        Assert.True(query.Calls[0].Token.IsCancellationRequested);
        query.Complete(0, Snapshot(AccountA, ("USD", 1m)));
        await load;
        Assert.Equal(CrmHrFinancialsPhase.Loading, session.Presentation.Phase);

        var honouring = new ScriptedFinancialQuery();
        using var second = new CrmFinancialsReadSession(honouring);
        var pending = second.LoadAsync(AccountA);
        var replaced = second.LoadAsync(AccountB);
        honouring.Cancel(0);
        await pending;
        Assert.Equal(CrmHrFinancialsPhase.Loading, second.Presentation.Phase);
        Assert.Null(second.Presentation.FailureMessage);
        honouring.Complete(1, Snapshot(AccountB));
        await replaced;
        Assert.Equal(AccountB, second.Presentation.Snapshot?.AccountPartyId);
    }

    [Fact]
    public async Task Separate_instances_keep_separate_accounts_periods_and_phases()
    {
        var query = new ScriptedFinancialQuery();
        using var first = new CrmFinancialsReadSession(query);
        using var second = new CrmFinancialsReadSession(query);

        var loadFirst = first.LoadAsync(AccountA);
        var loadSecond = second.LoadAsync(AccountB);
        first.SetPeriod(CrmHrFinancialPeriod.Year);
        query.Complete(0, Snapshot(AccountA, ("USD", 1m)));
        query.Fail(1, new IOException("offline"));
        await Task.WhenAll(loadFirst, loadSecond);

        Assert.Equal(CrmHrFinancialsPhase.Ready, first.Presentation.Phase);
        Assert.Equal(CrmHrFinancialPeriod.Year, first.Presentation.Period);
        Assert.Equal(CrmHrFinancialsPhase.Failed, second.Presentation.Phase);
        Assert.Equal(CrmHrFinancialPeriod.Month, second.Presentation.Period);
    }

    private static CrmAccountFinancialSnapshot Snapshot(Guid accountId, params (string Currency, decimal Amount)[] totals)
        => totals.Length == 0
            ? CrmAccountFinancialSnapshot.Empty(accountId)
            : new CrmAccountFinancialSnapshot(
                accountId,
                FinancialDataAvailability.Available,
                totals.Select(total => new CrmCurrencyAmount(total.Currency, total.Amount)).OrderBy(total => total.CurrencyCode, StringComparer.Ordinal).ToArray(),
                totals.Select(total => new CrmFinancialPeriodAmount(new DateOnly(2026, 5, 1), total.Currency, total.Amount)).ToArray(),
                totals.Select(total => new CrmFinancialPeriodAmount(new DateOnly(2026, 1, 1), total.Currency, total.Amount)).ToArray(),
                0,
                FinancialDataAvailability.Unavailable,
                FinancialDataAvailability.Unavailable,
                FinancialDataAvailability.Unavailable);

    // Records every request with its token and completes only when the test says so; it never observes
    // cancellation by itself unless a test cancels a call explicitly.
    private sealed class ScriptedFinancialQuery : ICrmFinancialSnapshotQueryService
    {
        private readonly List<TaskCompletionSource<CrmAccountFinancialSnapshot>> pending = [];

        public List<(Guid AccountPartyId, CancellationToken Token)> Calls { get; } = [];

        public Task<CrmAccountFinancialSnapshot> GetAsync(Guid accountPartyId, CancellationToken cancellationToken = default)
        {
            Calls.Add((accountPartyId, cancellationToken));
            var source = new TaskCompletionSource<CrmAccountFinancialSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
            pending.Add(source);
            return source.Task;
        }

        public void Complete(int call, CrmAccountFinancialSnapshot snapshot) => pending[call].SetResult(snapshot);

        public void Fail(int call, Exception exception) => pending[call].SetException(exception);

        public void Cancel(int call) => pending[call].SetCanceled(Calls[call].Token);
    }
}
