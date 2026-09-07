using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Usage;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Pages;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class AgentsOverviewSessionTests {
    [Theory]
    [InlineData(AgentWorkspaceSection.Providers)]
    [InlineData(AgentWorkspaceSection.RequestHistory)]
    public async Task History_demand_reads_only_header(AgentWorkspaceSection section) {
        var reads = new Reads();
        using var session = Session(reads);
        await session.EnsureAsync(section, ProviderUsageWorkloadSelection.Both);
        Assert.Equal(1, reads.HeaderCalls);
        Assert.Equal(0, reads.OverviewCalls);
        Assert.Equal(0, reads.UsageCalls);
        Assert.Null(session.Overview);
        Assert.Null(session.AcceptedUsage);
        await session.EnsureAsync(AgentWorkspaceSection.Overview, ProviderUsageWorkloadSelection.Both);
        Assert.Equal(1, reads.OverviewCalls);
        Assert.Equal(1, reads.UsageCalls);
        Assert.Equal(1, reads.HeaderCalls);
    }

    [Fact]
    public async Task Header_can_publish_while_both_aggregates_are_pending() {
        var overview = new TaskCompletionSource<AgentOverviewSnapshot>();
        var usage = new TaskCompletionSource<ProviderUsageSnapshot>();
        var reads = new Reads { Overview = _ => overview.Task, Usage = (_, _) => new(usage.Task) };
        using var session = Session(reads);
        var load = session.EnsureAsync(AgentWorkspaceSection.Overview, ProviderUsageWorkloadSelection.Both);
        Assert.Equal(7, session.Header?.BoundResourceCount);
        Assert.True(session.OverviewLoading);
        Assert.True(session.UsageLoading);
        overview.SetResult(AgentOverviewSnapshot.Empty);
        usage.SetResult(ProviderUsageSnapshot.Empty(ProviderUsageWorkloadSelection.Both));
        await load;
    }

    [Fact]
    public async Task Same_pending_request_reuses_each_read_lane() {
        var header = new TaskCompletionSource<AgentsHeaderSnapshot>();
        var overview = new TaskCompletionSource<AgentOverviewSnapshot>();
        var usage = new TaskCompletionSource<ProviderUsageSnapshot>();
        var reads = new Reads { Header = _ => header.Task, Overview = _ => overview.Task, Usage = (_, _) => new(usage.Task) };
        using var session = Session(reads);
        var first = session.EnsureAsync(AgentWorkspaceSection.Overview, ProviderUsageWorkloadSelection.Both);
        var echo = session.EnsureAsync(AgentWorkspaceSection.Overview, ProviderUsageWorkloadSelection.Both);
        Assert.Equal(1, reads.HeaderCalls);
        Assert.Equal(1, reads.OverviewCalls);
        Assert.Equal(1, reads.UsageCalls);
        header.SetResult(Reads.HeaderValue());
        overview.SetResult(AgentOverviewSnapshot.Empty);
        usage.SetResult(ProviderUsageSnapshot.Empty(ProviderUsageWorkloadSelection.Both));
        await Task.WhenAll(first, echo);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Scope_replacement_cancels_and_fences_old_result(bool failOld) {
        var pending = new TaskCompletionSource<ProviderUsageSnapshot>();
        var reads = new Reads { Usage = (scope, _) => scope == ProviderUsageWorkloadSelection.Agents
            ? new(pending.Task) : ValueTask.FromResult(ProviderUsageSnapshot.Empty(scope)) };
        using var session = Session(reads);
        var first = session.EnsureAsync(AgentWorkspaceSection.Overview, ProviderUsageWorkloadSelection.Agents);
        var token = reads.UsageToken;
        await session.EnsureAsync(AgentWorkspaceSection.Overview, ProviderUsageWorkloadSelection.SimpleChats);
        Assert.True(token.IsCancellationRequested);
        if (failOld) {
            pending.SetException(new IOException("Private stale failure"));
        } else {
            pending.SetResult(ProviderUsageSnapshot.Empty(ProviderUsageWorkloadSelection.Agents));
        }
        await first;
        Assert.Equal(ProviderUsageWorkloadSelection.SimpleChats, session.AcceptedUsage?.Selection);
        Assert.Null(session.UsageError);
        Assert.False(session.UsageLoading);
    }

    [Fact]
    public async Task Wrong_scope_result_is_rejected_without_relabeling_prior_data() {
        var reads = new Reads();
        using var session = Session(reads);
        await session.EnsureAsync(AgentWorkspaceSection.Overview, ProviderUsageWorkloadSelection.Agents);
        var accepted = session.AcceptedUsage;
        reads.Usage = (_, _) => ValueTask.FromResult(ProviderUsageSnapshot.Empty(ProviderUsageWorkloadSelection.Both));
        await session.EnsureAsync(AgentWorkspaceSection.Overview, ProviderUsageWorkloadSelection.SimpleChats);
        Assert.Same(accepted, session.AcceptedUsage);
        Assert.Null(session.GetAcceptedUsage(ProviderUsageWorkloadSelection.SimpleChats));
        Assert.NotNull(session.UsageError);
        Assert.DoesNotContain("different scope", session.UsageError);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Same_scope_failure_preserves_accepted_data_and_exposes_error(bool usageLane) {
        var reads = new Reads();
        using var session = Session(reads);
        await session.EnsureAsync(AgentWorkspaceSection.Overview, ProviderUsageWorkloadSelection.Both);
        var overview = session.Overview;
        var usage = session.AcceptedUsage;
        if (usageLane) {
            reads.Usage = (_, _) => ValueTask.FromException<ProviderUsageSnapshot>(new IOException("Private usage failure"));
            await session.RetryUsageAsync(ProviderUsageWorkloadSelection.Both);
            Assert.NotNull(session.UsageError);
        } else {
            reads.Overview = _ => Task.FromException<AgentOverviewSnapshot>(new IOException("Private summary failure"));
            await session.RetryOverviewAsync();
            Assert.NotNull(session.OverviewError);
        }
        Assert.Same(overview, session.Overview);
        Assert.Same(usage, session.AcceptedUsage);
    }

    [Fact]
    public async Task Retry_isolated_to_overview_and_clears_only_its_error() {
        var reads = new Reads { Overview = _ => Task.FromException<AgentOverviewSnapshot>(new IOException("Private failure")) };
        using var session = Session(reads);
        await session.EnsureAsync(AgentWorkspaceSection.Overview, ProviderUsageWorkloadSelection.Both);
        Assert.NotNull(session.OverviewError);
        reads.Overview = _ => Task.FromResult(AgentOverviewSnapshot.Empty);
        await session.RetryOverviewAsync();
        Assert.Equal(2, reads.OverviewCalls);
        Assert.Equal(1, reads.HeaderCalls);
        Assert.Equal(1, reads.UsageCalls);
        Assert.Null(session.OverviewError);
        Assert.NotNull(session.Overview);
    }

    [Fact]
    public async Task Old_finally_cannot_clear_newer_overview_request() {
        var first = new TaskCompletionSource<AgentOverviewSnapshot>();
        var second = new TaskCompletionSource<AgentOverviewSnapshot>();
        var reads = new Reads { Overview = _ => first.Task };
        using var session = Session(reads);
        var initial = session.EnsureAsync(AgentWorkspaceSection.Overview, ProviderUsageWorkloadSelection.Both);
        reads.Overview = _ => second.Task;
        var retry = session.RetryOverviewAsync();
        first.SetException(new IOException("Private old failure"));
        await initial;
        Assert.True(session.OverviewLoading);
        Assert.Null(session.OverviewError);
        second.SetResult(AgentOverviewSnapshot.Empty);
        await retry;
        Assert.False(session.OverviewLoading);
    }

    [Fact]
    public async Task History_supersession_allows_new_demand_without_waiting_for_noncooperative_read() {
        var pending = new TaskCompletionSource<AgentOverviewSnapshot>();
        var reads = new Reads { Overview = _ => pending.Task };
        using var session = Session(reads);
        var initial = session.EnsureAsync(AgentWorkspaceSection.Overview, ProviderUsageWorkloadSelection.Both);
        var token = reads.OverviewToken;
        await session.EnsureAsync(AgentWorkspaceSection.Providers, ProviderUsageWorkloadSelection.Both);
        Assert.True(token.IsCancellationRequested);
        reads.Overview = _ => Task.FromResult(AgentOverviewSnapshot.Empty with { Totals = AgentOverviewTotals.Empty with { AgentCount = 12 } });
        await session.EnsureAsync(AgentWorkspaceSection.Overview, ProviderUsageWorkloadSelection.Both);
        pending.SetResult(AgentOverviewSnapshot.Empty);
        await initial;
        Assert.Equal(12, session.Overview?.Totals.AgentCount);
    }

    [Fact]
    public async Task Accepted_collections_are_independent_of_producer_mutations() {
        var teams = new List<AgentTeamOverviewShortcutRow> { new(Guid.NewGuid(), "Accepted team", "", "groups", 2) };
        var consumers = new List<ProviderUsageConsumerRow> { new(ProviderUsageConsumerKind.Agent, "accepted", "Accepted consumer", ProviderUsageTotals.Empty, null) };
        var avatars = new Dictionary<string, string?> { ["accepted"] = "accepted-avatar.jpg" };
        var reads = new Reads {
            Header = _ => Task.FromResult(Reads.HeaderValue() with { AvatarImageUrls = avatars }),
            Overview = _ => Task.FromResult(AgentOverviewSnapshot.Empty with { TeamShortcuts = teams }),
            Usage = (scope, _) => ValueTask.FromResult(ProviderUsageSnapshot.Empty(scope) with { Consumers = consumers })
        };
        using var session = Session(reads);
        await session.EnsureAsync(AgentWorkspaceSection.Overview, ProviderUsageWorkloadSelection.Both);
        teams.Clear();
        consumers.Clear();
        avatars["accepted"] = "changed.jpg";
        Assert.Equal("Accepted team", Assert.Single(session.Overview!.TeamShortcuts).Name);
        Assert.Equal("Accepted consumer", Assert.Single(session.AcceptedUsage!.Consumers).ConsumerName);
        Assert.Equal("accepted-avatar.jpg", session.Header!.AvatarImageUrls["accepted"]);
    }

    [Fact]
    public async Task Disposal_cancels_all_lanes_and_suppresses_late_publication() {
        var header = new TaskCompletionSource<AgentsHeaderSnapshot>();
        var overview = new TaskCompletionSource<AgentOverviewSnapshot>();
        var usage = new TaskCompletionSource<ProviderUsageSnapshot>();
        var reads = new Reads { Header = _ => header.Task, Overview = _ => overview.Task, Usage = (_, _) => new(usage.Task) };
        var publications = 0;
        using var session = new AgentsOverviewSession(reads, NullLogger<AgentsOverviewSession>.Instance, () => {
            publications++;
            return Task.CompletedTask;
        });
        var initial = session.EnsureAsync(AgentWorkspaceSection.Overview, ProviderUsageWorkloadSelection.Both);
        session.Dispose();
        session.Dispose();
        Assert.True(reads.HeaderToken.IsCancellationRequested);
        Assert.True(reads.OverviewToken.IsCancellationRequested);
        Assert.True(reads.UsageToken.IsCancellationRequested);
        header.SetResult(Reads.HeaderValue());
        overview.SetException(new IOException("Late private failure"));
        usage.SetResult(ProviderUsageSnapshot.Empty(ProviderUsageWorkloadSelection.Both));
        await initial;
        Assert.Null(session.Header);
        Assert.Null(session.Overview);
        Assert.Null(session.AcceptedUsage);
        Assert.Equal(0, publications);
    }

    [Fact]
    public async Task Nonowner_cancellation_is_an_explicit_bounded_read_failure() {
        var reads = new Reads { Overview = _ => Task.FromException<AgentOverviewSnapshot>(new OperationCanceledException("Private foreign cancellation")) };
        using var session = Session(reads);
        await session.EnsureAsync(AgentWorkspaceSection.Overview, ProviderUsageWorkloadSelection.Both);
        Assert.NotNull(session.OverviewError);
        Assert.DoesNotContain("Private", session.OverviewError);
        Assert.NotNull(session.AcceptedUsage);
    }

    [Fact]
    public async Task Partial_header_refresh_retains_independent_values_and_marks_failed_part() {
        var reads = new Reads();
        using var session = Session(reads);
        await session.EnsureAsync(AgentWorkspaceSection.Overview, ProviderUsageWorkloadSelection.Both);
        reads.Header = _ => Task.FromResult(Reads.HeaderValue() with { BoundResourceCount = null, Failures = AgentsHeaderFailure.BoundResources | AgentsHeaderFailure.HrAgent });
        await session.RetryHeaderAsync();
        Assert.Equal(7, session.Header?.BoundResourceCount);
        Assert.True(session.Header!.Failures.HasFlag(AgentsHeaderFailure.BoundResources));
        Assert.Equal(1, reads.OverviewCalls);
        Assert.Equal(1, reads.UsageCalls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Header_retry_fences_old_success_and_failure(bool failOld) {
        var old = new TaskCompletionSource<AgentsHeaderSnapshot>();
        var reads = new Reads { Header = _ => old.Task };
        using var session = Session(reads);
        var initial = session.EnsureAsync(AgentWorkspaceSection.Overview, ProviderUsageWorkloadSelection.Both);
        var token = reads.HeaderToken;
        reads.Header = _ => Task.FromResult(Reads.HeaderValue() with { BoundResourceCount = 12 });
        await session.RetryHeaderAsync();
        Assert.True(token.IsCancellationRequested);
        if (failOld) {
            old.SetException(new IOException("Private old header failure"));
        } else {
            old.SetResult(Reads.HeaderValue());
        }
        await initial;
        Assert.Equal(12, session.Header?.BoundResourceCount);
        Assert.Null(session.HeaderError);
        Assert.False(session.HeaderLoading);
    }

    private static AgentsOverviewSession Session(Reads reads) => new(reads, NullLogger<AgentsOverviewSession>.Instance);

    private sealed class Reads : IAgentsWorkspaceQuery {
        public Func<CancellationToken, Task<AgentsHeaderSnapshot>> Header { get; set; } = _ => Task.FromResult(HeaderValue());
        public Func<CancellationToken, Task<AgentOverviewSnapshot>> Overview { get; set; } = _ => Task.FromResult(AgentOverviewSnapshot.Empty);
        public Func<ProviderUsageWorkloadSelection, CancellationToken, ValueTask<ProviderUsageSnapshot>> Usage { get; set; }
            = (scope, _) => ValueTask.FromResult(ProviderUsageSnapshot.Empty(scope));
        public int HeaderCalls { get; private set; }
        public int OverviewCalls { get; private set; }
        public int UsageCalls { get; private set; }
        public CancellationToken HeaderToken { get; private set; }
        public CancellationToken OverviewToken { get; private set; }
        public CancellationToken UsageToken { get; private set; }
        public static AgentsHeaderSnapshot HeaderValue() => new(null, new Dictionary<string, string?>(), 7, AgentsHeaderFailure.HrAgent);

        public Task<AgentsHeaderSnapshot> ReadHeaderAsync(CancellationToken cancellationToken = default) {
            HeaderCalls++;
            HeaderToken = cancellationToken;
            return Header(cancellationToken);
        }

        public Task<AgentOverviewSnapshot> ReadOverviewAsync(CancellationToken cancellationToken = default) {
            OverviewCalls++;
            OverviewToken = cancellationToken;
            return Overview(cancellationToken);
        }

        public ValueTask<ProviderUsageSnapshot> ReadUsageAsync(ProviderUsageWorkloadSelection selection, CancellationToken cancellationToken = default) {
            UsageCalls++;
            UsageToken = cancellationToken;
            return Usage(selection, cancellationToken);
        }
    }
}
