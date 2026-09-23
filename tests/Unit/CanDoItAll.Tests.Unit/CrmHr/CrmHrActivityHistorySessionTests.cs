using CanDoItAll.CrmHr.UI.Activity;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.CrmHr.Components;

namespace CanDoItAll.Tests.Unit.CrmHr;

// The activity history lane shared by the CRM, Directory and Workforce hosts: accepted-data availability, page-request
// admission against a scripted query that ignores cancellation, retry admission, target changes and late outcomes.
public sealed class CrmHrActivityHistorySessionTests
{
    private const string InitialFailure = "Account activity could not be loaded.";
    private const string PageFailure = "The requested account activity page could not be loaded.";
    private static readonly Guid PartyA = Guid.Parse("84000000-0000-0000-0000-00000000000a");
    private static readonly Guid PartyB = Guid.Parse("84000000-0000-0000-0000-00000000000b");

    [Fact]
    public async Task First_read_is_not_accepted_until_the_query_answers_and_then_accepted_with_the_mapped_page()
    {
        var query = new ScriptedActivityQuery();
        using var session = Session(query);
        Assert.Equal(CrmHrActivityPresentation.NotAccepted, session.Presentation);
        Assert.Null(session.Target);

        var read = session.EnsureLoadedAsync(PartyA);

        Assert.Equal(PartyA, session.Target);
        Assert.True(session.Presentation.IsLoading);
        Assert.Null(session.Presentation.Accepted);
        var call = Assert.Single(query.Calls);
        Assert.Equal(new CrmActivityHistoryQuery(PartyA, 0, CrmActivityHistoryQueryLimits.DefaultPageSize), call.Query);

        query.Complete(0, Page(0, totalCount: 23, actionCount: 5, overdueCount: 2, "Quarterly review"));
        await read;

        Assert.False(session.Presentation.IsLoading);
        var accepted = Assert.IsType<CrmHrActivityPage>(session.Presentation.Accepted);
        Assert.Equal(23, accepted.TotalCount);
        Assert.Equal(5, accepted.ActionCount);
        Assert.Equal(2, accepted.OverdueActionCount);
        Assert.Equal("Quarterly review", Assert.Single(accepted.Items).Title);
        Assert.Null(session.FailureMessage);
    }

    [Fact]
    public async Task Same_target_calls_join_the_read_in_flight_and_issue_nothing_once_a_page_is_accepted()
    {
        var query = new ScriptedActivityQuery();
        using var session = Session(query);

        var first = session.EnsureLoadedAsync(PartyA);
        var joined = session.EnsureLoadedAsync(PartyA);
        Assert.Same(first, joined);
        Assert.Single(query.Calls);

        query.Complete(0, Page(0, totalCount: 1, actionCount: 0, overdueCount: 0, "Row"));
        await first;
        await session.EnsureLoadedAsync(PartyA);

        Assert.Single(query.Calls);
    }

    [Fact]
    public async Task A_page_request_keeps_the_accepted_totals_while_it_loads_and_is_rejected_while_a_read_is_in_flight()
    {
        var query = new ScriptedActivityQuery();
        using var session = Session(query);
        var first = session.EnsureLoadedAsync(PartyA);
        query.Complete(0, Page(0, totalCount: 25, actionCount: 3, overdueCount: 1, "Page one"));
        await first;

        var second = session.RequestPageAsync(1);

        Assert.True(session.Presentation.IsLoading);
        Assert.Equal(25, session.Presentation.Accepted?.TotalCount);
        Assert.Equal(0, session.Presentation.Accepted?.PageIndex);
        Assert.Equal(2, query.Calls.Count);
        Assert.Equal(new CrmActivityHistoryQuery(PartyA, 1, 10), query.Calls[1].Query);

        // A second admission (a retained pager callback, a double click) while the read is in flight issues nothing,
        // and a same-target ensure joins the page read instead of starting another.
        await session.RequestPageAsync(2);
        await session.RequestPageAsync(0);
        Assert.Same(second, session.EnsureLoadedAsync(PartyA));
        Assert.Equal(2, query.Calls.Count);

        query.Complete(1, Page(1, totalCount: 25, actionCount: 3, overdueCount: 1, "Page two"));
        await second;

        Assert.False(session.Presentation.IsLoading);
        Assert.Equal(1, session.Presentation.Accepted?.PageIndex);
        Assert.Equal("Page two", Assert.Single(session.Presentation.Accepted!.Items).Title);
    }

    [Fact]
    public async Task Page_requests_outside_the_accepted_range_or_before_acceptance_are_ignored()
    {
        var query = new ScriptedActivityQuery();
        using var session = Session(query);

        await session.RequestPageAsync(0);
        Assert.Empty(query.Calls);

        var first = session.EnsureLoadedAsync(PartyA);
        query.Complete(0, Page(0, totalCount: 25, actionCount: 0, overdueCount: 0, "Row"));
        await first;

        await session.RequestPageAsync(-1);
        await session.RequestPageAsync(3);
        Assert.Single(query.Calls);
    }

    [Fact]
    public async Task A_target_change_during_a_read_retires_it_and_the_first_target_read_again_does_not_revive_the_old_outcome()
    {
        var query = new ScriptedActivityQuery();
        using var session = Session(query);

        var readA = session.EnsureLoadedAsync(PartyA);
        var readB = session.EnsureLoadedAsync(PartyB);
        Assert.Equal(PartyB, session.Target);
        Assert.True(query.Calls[0].Token.IsCancellationRequested);
        Assert.Equal(2, query.Calls.Count);

        // A's late success is inert: B stays loading and nothing of A is shown as B.
        query.Complete(0, Page(0, totalCount: 99, actionCount: 9, overdueCount: 9, "A row"));
        await readA;
        Assert.True(session.Presentation.IsLoading);
        Assert.Null(session.Presentation.Accepted);

        query.Complete(1, Page(0, totalCount: 2, actionCount: 0, overdueCount: 0, "B row"));
        await readB;
        Assert.Equal("B row", Assert.Single(session.Presentation.Accepted!.Items).Title);

        // Back to A: a fresh read, and the first A outcome (already consumed) has no way back in.
        var readAgain = session.EnsureLoadedAsync(PartyA);
        Assert.Equal(3, query.Calls.Count);
        Assert.True(session.Presentation.IsLoading);
        Assert.Null(session.Presentation.Accepted);
        query.Complete(2, Page(0, totalCount: 1, actionCount: 0, overdueCount: 0, "A again"));
        await readAgain;
        Assert.Equal("A again", Assert.Single(session.Presentation.Accepted!.Items).Title);
    }

    [Fact]
    public async Task A_target_change_during_a_read_also_makes_the_late_failure_of_the_previous_target_inert()
    {
        var query = new ScriptedActivityQuery();
        using var session = Session(query);

        var readA = session.EnsureLoadedAsync(PartyA);
        var readB = session.EnsureLoadedAsync(PartyB);
        query.Fail(0, new IOException("late A failure"));
        await readA;

        Assert.Null(session.FailureMessage);
        Assert.True(session.Presentation.IsLoading);

        query.Complete(1, Page(0, totalCount: 0, actionCount: 0, overdueCount: 0));
        await readB;
        Assert.True(session.Presentation.HasAccepted);
        Assert.Empty(session.Presentation.Accepted!.Items);
    }

    [Fact]
    public async Task A_failed_first_read_is_not_accepted_and_retry_reads_again_with_duplicates_and_idle_retries_ignored()
    {
        var query = new ScriptedActivityQuery();
        using var session = Session(query);
        var first = session.EnsureLoadedAsync(PartyA);
        query.Fail(0, new InvalidOperationException("relation does not exist"));
        await first;

        Assert.Equal(InitialFailure, session.FailureMessage);
        Assert.Equal(CrmHrActivityPresentation.NotAccepted, session.Presentation);
        // A same-target ensure does not replay a failed read by itself.
        await session.EnsureLoadedAsync(PartyA);
        Assert.Single(query.Calls);

        var retry = session.RetryAsync();
        Assert.Null(session.FailureMessage);
        Assert.True(session.Presentation.IsLoading);
        Assert.Equal(2, query.Calls.Count);
        await session.RetryAsync();
        Assert.Equal(2, query.Calls.Count);

        query.Complete(1, Page(0, totalCount: 0, actionCount: 0, overdueCount: 0));
        await retry;
        Assert.True(session.Presentation.HasAccepted);
        Assert.Empty(session.Presentation.Accepted!.Items);

        // Nothing failed: a retry is not a refresh.
        await session.RetryAsync();
        Assert.Equal(2, query.Calls.Count);
    }

    [Fact]
    public async Task A_failed_page_read_keeps_the_accepted_page_and_reports_the_page_failure()
    {
        var query = new ScriptedActivityQuery();
        using var session = Session(query);
        var first = session.EnsureLoadedAsync(PartyA);
        query.Complete(0, Page(0, totalCount: 25, actionCount: 3, overdueCount: 1, "Page one"));
        await first;

        var second = session.RequestPageAsync(1);
        query.Fail(1, new IOException("offline"));
        await second;

        Assert.Equal(PageFailure, session.FailureMessage);
        Assert.False(session.Presentation.IsLoading);
        Assert.Equal(0, session.Presentation.Accepted?.PageIndex);
        Assert.Equal(25, session.Presentation.Accepted?.TotalCount);

        var retry = session.RetryAsync();
        Assert.Null(session.Presentation.Accepted);
        Assert.Equal(new CrmActivityHistoryQuery(PartyA, 0, 10), query.Calls[2].Query);
        query.Complete(2, Page(0, totalCount: 25, actionCount: 3, overdueCount: 1, "Page one again"));
        await retry;
        Assert.Equal("Page one again", Assert.Single(session.Presentation.Accepted!.Items).Title);
    }

    [Fact]
    public async Task Invalidation_and_disposal_make_a_late_outcome_inert_and_a_cancellation_honouring_query_is_silent()
    {
        var query = new ScriptedActivityQuery();
        var session = Session(query);
        var read = session.EnsureLoadedAsync(PartyA);

        session.Invalidate();
        Assert.Null(session.Target);
        Assert.Equal(CrmHrActivityPresentation.NotAccepted, session.Presentation);
        Assert.True(query.Calls[0].Token.IsCancellationRequested);

        query.Complete(0, Page(0, totalCount: 5, actionCount: 1, overdueCount: 0, "late"));
        await read;
        Assert.Equal(CrmHrActivityPresentation.NotAccepted, session.Presentation);

        // Disposal retires the read in flight; the honoured cancellation is silent and nothing is accepted or failed.
        var honouring = session.EnsureLoadedAsync(PartyB);
        session.Dispose();
        query.Cancel(1);
        await honouring;
        Assert.False(session.Presentation.HasAccepted);
        Assert.Null(session.FailureMessage);
        await session.EnsureLoadedAsync(PartyA);
        Assert.Equal(2, query.Calls.Count);
    }

    [Fact]
    public async Task Independent_sessions_keep_independent_targets_and_pages()
    {
        var query = new ScriptedActivityQuery();
        using var crm = Session(query);
        using var directory = Session(query);

        var crmRead = crm.EnsureLoadedAsync(PartyA);
        var directoryRead = directory.EnsureLoadedAsync(PartyA);
        query.Complete(0, Page(0, totalCount: 25, actionCount: 1, overdueCount: 0, "crm"));
        query.Complete(1, Page(0, totalCount: 25, actionCount: 1, overdueCount: 0, "directory"));
        await Task.WhenAll(crmRead, directoryRead);

        var page = directory.RequestPageAsync(1);
        Assert.False(crm.Presentation.IsLoading);
        Assert.True(directory.Presentation.IsLoading);
        query.Complete(2, Page(1, totalCount: 25, actionCount: 1, overdueCount: 0, "directory page two"));
        await page;

        Assert.Equal("crm", Assert.Single(crm.Presentation.Accepted!.Items).Title);
        Assert.Equal("directory page two", Assert.Single(directory.Presentation.Accepted!.Items).Title);
    }

    private static CrmHrActivityHistorySession Session(ScriptedActivityQuery query)
        => new(query.QueryAsync, InitialFailure, PageFailure);

    private static CrmActivityHistoryPage Page(int pageIndex, int totalCount, int actionCount, int overdueCount, params string[] titles)
        => new(
            titles.Select(title => new CrmAccountActivityTimelineItemModel(Guid.NewGuid(), "Interaction", title, string.Empty, "Call", DateTimeOffset.UnixEpoch, "info", false)).ToArray(),
            pageIndex,
            PageSize: 10,
            totalCount,
            actionCount,
            overdueCount);

    // Records every request with its token and completes only when the test says so; it never observes
    // cancellation by itself, so late success and failure reach the session like a backend that ignores the token.
    private sealed class ScriptedActivityQuery
    {
        private readonly List<TaskCompletionSource<CrmActivityHistoryPage>> pending = [];

        public List<(CrmActivityHistoryQuery Query, CancellationToken Token)> Calls { get; } = [];

        public Task<CrmActivityHistoryPage> QueryAsync(CrmActivityHistoryQuery query, CancellationToken cancellationToken)
        {
            Calls.Add((query, cancellationToken));
            var source = new TaskCompletionSource<CrmActivityHistoryPage>(TaskCreationOptions.RunContinuationsAsynchronously);
            pending.Add(source);
            return source.Task;
        }

        public void Complete(int call, CrmActivityHistoryPage page) => pending[call].SetResult(page);

        public void Fail(int call, Exception exception) => pending[call].SetException(exception);

        public void Cancel(int call) => pending[call].SetCanceled(Calls[call].Token);
    }
}
