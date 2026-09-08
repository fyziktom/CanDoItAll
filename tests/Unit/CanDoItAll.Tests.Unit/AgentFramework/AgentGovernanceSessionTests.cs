using CanDoItAll.AgentFramework.UI.Governance;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Pages;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class AgentGovernanceSessionTests {
    [Fact]
    public async Task List_is_accepted_before_detail_completes() {
        var pending = new TaskCompletionSource<ExecutionRunDetail>();
        var reads = new Reads { Detail = (_, _) => pending.Task };
        using var session = Create(reads);
        var load = session.SetAgentAsync(Reads.AgentId);
        Assert.Equal(GovernanceReadPhase.Ready, session.ListState.Phase);
        Assert.Equal(GovernanceReadPhase.Loading, session.DetailState.Phase);
        Assert.Equal(Reads.AgentId, session.AcceptedListAgentId);
        Assert.Equal(AgentChatContextAccessState.Ready, session.AccessState);
        Assert.Equal(2, session.Runs.Length);
        pending.SetResult(Reads.DetailValue(Reads.First));
        await load;
        Assert.Equal(Reads.First.Id, session.AcceptedRunId);
    }

    [Fact]
    public async Task Refresh_cannot_repeat_detail_after_newer_manual_selection_during_catalog() {
        var reads = new Reads();
        using var session = Create(reads);
        await session.SetAgentAsync(Reads.AgentId);
        var catalog = new TaskCompletionSource<IReadOnlyList<AgentDefinition>>();
        reads.Catalog = _ => catalog.Task;
        var refresh = session.RefreshAsync();
        await session.SelectRunAsync(Reads.Second.Id);
        var detailReads = reads.DetailCalls;
        var selection = session.SelectionRevision;
        catalog.SetResult(Reads.Agents);
        await refresh;
        Assert.Equal(detailReads, reads.DetailCalls);
        Assert.Equal(selection, session.SelectionRevision);
        Assert.Equal(Reads.Second.Id, session.AcceptedRunId);
    }

    [Fact]
    public async Task Older_catalog_continuation_cannot_start_a_list_for_newer_request() {
        var reads = new Reads();
        var continuation = new TaskCompletionSource();
        var hold = true;
        AgentGovernanceSession? current = null;
        using var session = current = new(reads, NullLogger<AgentGovernanceSession>.Instance, () => {
            if (hold && current!.CatalogState.Phase == GovernanceReadPhase.Ready) {
                hold = false;
                return continuation.Task;
            }
            return Task.CompletedTask;
        });
        var first = session.SetAgentAsync(Reads.AgentId);
        Assert.False(first.IsCompleted);
        await session.RefreshAsync();
        continuation.SetResult();
        await first;
        Assert.Equal(1, reads.ListCalls);
        Assert.Equal(Reads.First.Id, session.AcceptedRunId);
    }

    [Fact]
    public async Task Older_list_finally_cannot_clear_newer_refresh() {
        var reads = new Reads();
        using var session = Create(reads);
        await session.SetAgentAsync(Reads.AgentId);
        var first = new TaskCompletionSource<IReadOnlyList<ExecutionRunRecord>>();
        var second = new TaskCompletionSource<IReadOnlyList<ExecutionRunRecord>>();
        reads.List = (_, _) => first.Task;
        var old = session.RetryRunsAsync();
        reads.List = (_, _) => second.Task;
        var next = session.RetryRunsAsync();
        first.SetException(new IOException("Obsolete read"));
        await old;
        Assert.True(session.IsBusy);
        Assert.Equal(GovernanceReadPhase.Refreshing, session.ListState.Phase);
        Assert.Null(session.ListState.Error);
        second.SetResult([Reads.First, Reads.Second]);
        await next;
        Assert.False(session.IsBusy);
    }

    [Fact]
    public async Task List_retry_does_not_repeat_catalog_or_context_effects() {
        var reads = new Reads();
        using var session = Create(reads);
        await session.SetAgentAsync(Reads.AgentId);
        var catalogReads = reads.CatalogCalls;
        var target = session.TargetRevision;
        reads.List = (_, _) => Task.FromException<IReadOnlyList<ExecutionRunRecord>>(new IOException("List fixture"));
        await session.RetryRunsAsync();
        Assert.Equal(GovernanceReadPhase.Stale, session.ListState.Phase);
        Assert.Equal(AgentChatContextAccessState.Ready, session.AccessState);
        Assert.Equal(target, session.TargetRevision);
        Assert.Equal(catalogReads, reads.CatalogCalls);
        Assert.Equal(Reads.First.Id, session.AcceptedRunId);
    }

    [Fact]
    public async Task Removed_run_can_reappear_without_changing_selection() {
        var reads = new Reads();
        using var session = Create(reads);
        await session.SetAgentAsync(Reads.AgentId);
        await session.SelectRunAsync(Reads.Second.Id);
        reads.List = (_, _) => Task.FromResult<IReadOnlyList<ExecutionRunRecord>>([Reads.First]);
        await session.RetryRunsAsync();
        Assert.Equal(GovernanceReadPhase.Unavailable, session.DetailState.Phase);
        Assert.Equal(Reads.Second.Id, session.DesiredRunId);
        Assert.Null(session.AcceptedRunId);
        reads.List = (_, _) => Task.FromResult<IReadOnlyList<ExecutionRunRecord>>([Reads.First, Reads.Second]);
        await session.RetryRunsAsync();
        Assert.Equal(Reads.Second.Id, session.AcceptedRunId);
        Assert.Equal(GovernanceReadPhase.Ready, session.DetailState.Phase);
    }

    [Fact]
    public async Task Accepted_list_owns_its_collection() {
        var mutable = new List<ExecutionRunRecord> { Reads.First, Reads.Second };
        var reads = new Reads { List = (_, _) => Task.FromResult<IReadOnlyList<ExecutionRunRecord>>(mutable) };
        using var session = Create(reads);
        await session.SetAgentAsync(Reads.AgentId);
        mutable.Clear();
        Assert.Equal(2, session.Runs.Length);
    }

    [Fact]
    public async Task Canceled_current_read_surfaces_failure_when_owner_did_not_cancel() {
        var reads = new Reads { List = (_, _) => Task.FromCanceled<IReadOnlyList<ExecutionRunRecord>>(new CancellationToken(true)) };
        using var session = Create(reads);
        await session.SetAgentAsync(Reads.AgentId);
        Assert.Equal(GovernanceReadPhase.Failed, session.ListState.Phase);
        Assert.False(session.IsBusy);
        Assert.Equal(AgentChatContextAccessState.Ready, session.AccessState);
    }

    public enum Lane { Catalog, List, Detail }

    [Theory]
    [InlineData(Lane.Catalog, false)]
    [InlineData(Lane.Catalog, true)]
    [InlineData(Lane.List, false)]
    [InlineData(Lane.List, true)]
    [InlineData(Lane.Detail, false)]
    [InlineData(Lane.Detail, true)]
    public async Task Disposal_cancels_each_lane_and_fences_noncooperative_completion(Lane lane, bool fail) {
        var completion = new TaskCompletionSource();
        var reads = new Reads();
        CancellationToken captured = default;
        async Task<T> Delayed<T>(CancellationToken token, T value) {
            captured = token;
            await completion.Task;
            return value;
        }
        switch (lane) {
            case Lane.Catalog:
                reads.Catalog = token => Delayed(token, Reads.Agents);
                break;
            case Lane.List:
                reads.List = (_, token) => Delayed<IReadOnlyList<ExecutionRunRecord>>(token, [Reads.First]);
                break;
            case Lane.Detail:
                reads.Detail = (_, token) => Delayed(token, Reads.DetailValue(Reads.First));
                break;
        }
        var notifications = 0;
        using var session = new AgentGovernanceSession(reads, NullLogger<AgentGovernanceSession>.Instance, () => {
            notifications++;
            return Task.CompletedTask;
        });
        var load = session.SetAgentAsync(Reads.AgentId);
        var canceled = 0;
        using var registration = captured.Register(() => canceled++);
        session.Dispose();
        session.Dispose();
        Assert.Equal(1, canceled);
        Assert.True(captured.IsCancellationRequested);
        var delayed = 0;
        using var laterRegistration = captured.Register(() => delayed++);
        Assert.Equal(1, delayed);
        var before = notifications;
        if (fail) {
            completion.SetException(new IOException("Late fixture failure"));
        } else {
            completion.SetResult();
        }
        await load;
        Assert.Equal(before, notifications);
        Assert.Null(session.Detail);
        Assert.Throws<ObjectDisposedException>(() => captured.WaitHandle);
    }

    private static AgentGovernanceSession Create(Reads reads) => new(reads, NullLogger<AgentGovernanceSession>.Instance);

    private sealed class Reads : IAgentGovernanceReads {
        public static readonly Guid AgentId = Guid.NewGuid();
        public static readonly AgentDefinition Agent = new(AgentId, "Agent", "Fixture", "", "", AgentLifecycleStatus.Active,
            null, "model", AgentWorkloadKind.General, AgentChatHistoryMode.FrameworkManaged, 0.2, true, false, "{}", false, "",
            AgentPermissionsPolicy.Default, [], [], DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);
        public static readonly IReadOnlyList<AgentDefinition> Agents = [Agent];
        public static readonly ExecutionRunRecord First = Run("First");
        public static readonly ExecutionRunRecord Second = Run("Second");
        public int CatalogCalls { get; private set; }
        public int ListCalls { get; private set; }
        public int DetailCalls { get; private set; }
        public Func<CancellationToken, Task<IReadOnlyList<AgentDefinition>>> Catalog { get; set; } = _ => Task.FromResult(Agents);
        public Func<Guid?, CancellationToken, Task<IReadOnlyList<ExecutionRunRecord>>> List { get; set; } = (_, _) => Task.FromResult<IReadOnlyList<ExecutionRunRecord>>([First, Second]);
        public Func<Guid, CancellationToken, Task<ExecutionRunDetail>> Detail { get; set; } = (id, _) => Task.FromResult(DetailValue(id == First.Id ? First : Second));
        public Task<IReadOnlyList<AgentDefinition>> ReadAgentsAsync(CancellationToken token) {
            CatalogCalls++;
            return Catalog(token);
        }
        public Task<IReadOnlyList<ExecutionRunRecord>> ReadRunsAsync(Guid? id, CancellationToken token) {
            ListCalls++;
            return List(id, token);
        }
        public Task<ExecutionRunDetail> ReadDetailAsync(Guid id, CancellationToken token) {
            DetailCalls++;
            return Detail(id, token);
        }
        public static ExecutionRunDetail DetailValue(ExecutionRunRecord run) => new(run, null, [], []);
        private static ExecutionRunRecord Run(string title) => new(Guid.NewGuid(), AgentId, null, title, "manual", "", "", "", "", "",
            "{}", "Input", "Result", "Provider", "model", ExecutionState.Completed, RunOutcome.Succeeded,
            DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, null, null, "", null, []);
    }
}
