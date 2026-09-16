using CanDoItAll.CrmHr.UI.Home;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.CrmHr.Components;

namespace CanDoItAll.Tests.Unit.CrmHr;

// The Home read owner: one accepted snapshot per instance, explicit phases, retry admission and fencing of late
// results from a query that ignores cancellation.
public sealed class CrmHrHomeReadSessionTests
{
    private static readonly CrmHrHomeSnapshotModel Snapshot = new(
        PartyCount: 42,
        OrganizationCount: 7,
        OpportunityCount: 9,
        WorkforceProfileCount: 11,
        AgentProjectionCount: 2,
        SensitiveCount: 3,
        DirectoryPreview: [Party("Aurora Logistics", PartyType.Organization)],
        SensitivePreview: [Party("Dana Reyes", PartyType.Person, isSensitive: true)],
        OpenPipelinePreview: []);

    [Fact]
    public async Task Initial_read_is_loading_until_the_snapshot_arrives_and_then_ready_with_the_mapped_overview()
    {
        var query = new ScriptedHomeQuery();
        var changes = 0;
        using var session = new CrmHrHomeReadSession(query) { Changed = () => { changes++; return Task.CompletedTask; } };

        var load = session.LoadAsync();

        Assert.Equal(CrmHrHomePhase.Loading, session.Presentation.Phase);
        Assert.Null(session.Presentation.Overview);
        Assert.Equal(1, changes);

        query.Complete(Snapshot);
        await load;

        Assert.Equal(CrmHrHomePhase.Ready, session.Presentation.Phase);
        Assert.Equal(42, session.Presentation.Overview!.Totals.Parties);
        Assert.Equal(3, session.Presentation.Overview.Totals.SensitiveRecords);
        Assert.Single(session.Presentation.Overview.DirectoryPreview);
        Assert.Equal(2, changes);
        Assert.Single(query.Calls);
    }

    [Fact]
    public async Task Failed_read_is_failed_with_safe_copy_and_never_a_zero_result()
    {
        var query = new ScriptedHomeQuery();
        using var session = new CrmHrHomeReadSession(query);

        var load = session.LoadAsync();
        query.Fail(new InvalidOperationException("Npgsql: relation \"Parties\" does not exist"));
        await load;

        Assert.Equal(CrmHrHomePhase.Failed, session.Presentation.Phase);
        Assert.Null(session.Presentation.Overview);
        Assert.Equal(CrmHrHomeReadSession.FailureMessage, session.Presentation.FailureMessage);
        Assert.DoesNotContain("Npgsql", session.Presentation.FailureMessage, StringComparison.Ordinal);
        Assert.False(session.Presentation.IsRetrying);
    }

    [Fact]
    public async Task Retry_after_a_failure_reads_again_and_duplicate_or_stale_retries_are_ignored()
    {
        var query = new ScriptedHomeQuery();
        using var session = new CrmHrHomeReadSession(query);
        var load = session.LoadAsync();
        query.Fail(new IOException("offline"));
        await load;
        var failedGeneration = session.Presentation.Generation;

        var retry = session.RetryAsync(failedGeneration);
        Assert.True(session.Presentation.IsRetrying);
        Assert.Equal(CrmHrHomePhase.Failed, session.Presentation.Phase);

        // A duplicate admission while the retry is in flight and a stale generation issue no further request.
        await session.RetryAsync(session.Presentation.Generation);
        await session.RetryAsync(failedGeneration);
        Assert.Equal(2, query.Calls.Count);

        query.Complete(Snapshot);
        await retry;

        Assert.Equal(CrmHrHomePhase.Ready, session.Presentation.Phase);
        Assert.False(session.Presentation.IsRetrying);
        Assert.Equal(2, query.Calls.Count);

        // A retry of a ready overview is not a refresh feature; it is ignored.
        await session.RetryAsync(session.Presentation.Generation);
        Assert.Equal(2, query.Calls.Count);
    }

    [Fact]
    public async Task A_second_load_of_the_same_instance_issues_no_request()
    {
        var query = new ScriptedHomeQuery();
        using var session = new CrmHrHomeReadSession(query);

        var first = session.LoadAsync();
        await session.LoadAsync();
        query.Complete(Snapshot);
        await first;
        await session.LoadAsync();

        Assert.Single(query.Calls);
        Assert.Equal(CrmHrHomePhase.Ready, session.Presentation.Phase);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Disposal_ignores_a_late_outcome_of_a_query_that_ignores_cancellation(bool fail)
    {
        var query = new ScriptedHomeQuery();
        var changes = 0;
        var session = new CrmHrHomeReadSession(query) { Changed = () => { changes++; return Task.CompletedTask; } };
        var load = session.LoadAsync();
        Assert.Equal(1, changes);

        session.Dispose();
        Assert.True(query.Calls[0].IsCancellationRequested);

        if (fail)
        {
            query.Fail(new IOException("late failure"));
        }
        else
        {
            query.Complete(Snapshot);
        }

        await load;

        Assert.Equal(CrmHrHomePhase.Loading, session.Presentation.Phase);
        Assert.Null(session.Presentation.Overview);
        Assert.Null(session.Presentation.FailureMessage);
        Assert.Equal(1, changes);
    }

    [Fact]
    public async Task Separate_instances_keep_separate_state()
    {
        var failing = new ScriptedHomeQuery();
        var succeeding = new ScriptedHomeQuery();
        using var first = new CrmHrHomeReadSession(failing);
        using var second = new CrmHrHomeReadSession(succeeding);

        var firstLoad = first.LoadAsync();
        var secondLoad = second.LoadAsync();
        failing.Fail(new IOException("offline"));
        succeeding.Complete(Snapshot);
        await Task.WhenAll(firstLoad, secondLoad);

        Assert.Equal(CrmHrHomePhase.Failed, first.Presentation.Phase);
        Assert.Equal(CrmHrHomePhase.Ready, second.Presentation.Phase);
        Assert.Equal(42, second.Presentation.Overview!.Totals.Parties);
    }

    [Fact]
    public async Task A_throwing_render_callback_does_not_change_the_accepted_read()
    {
        var query = new ScriptedHomeQuery();
        using var session = new CrmHrHomeReadSession(query) { Changed = () => throw new InvalidOperationException("render failed") };

        var load = session.LoadAsync();
        query.Complete(Snapshot);
        await load;

        Assert.Equal(CrmHrHomePhase.Ready, session.Presentation.Phase);
        Assert.Equal(42, session.Presentation.Overview!.Totals.Parties);
    }

    private static CrmHrHomePartyPreviewModel Party(string name, PartyType type, bool isSensitive = false)
        => new(Guid.NewGuid(), name, type, PartyLifecycleStatus.Active, isSensitive, $"{name} summary", DateTimeOffset.UnixEpoch);

    // Records every request with its token and completes only when the test says so; it never observes cancellation.
    private sealed class ScriptedHomeQuery : ICrmHrHomeQueryService
    {
        private readonly Queue<TaskCompletionSource<CrmHrHomeSnapshotModel>> pending = new();

        public List<CancellationToken> Calls { get; } = [];

        public Task<CrmHrHomeSnapshotModel> GetAsync(CancellationToken cancellationToken = default)
        {
            Calls.Add(cancellationToken);
            var source = new TaskCompletionSource<CrmHrHomeSnapshotModel>();
            pending.Enqueue(source);
            return source.Task;
        }

        public void Complete(CrmHrHomeSnapshotModel snapshot) => pending.Dequeue().SetResult(snapshot);

        public void Fail(Exception exception) => pending.Dequeue().SetException(exception);
    }
}
