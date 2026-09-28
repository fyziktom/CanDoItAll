using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Usage;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class ProviderUsageWindowTests {
    private static readonly DateTimeOffset Now = new(2024, 3, 31, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(ProviderUsagePeriod.SevenDays, "2024-03-24T12:00:00Z")]
    [InlineData(ProviderUsagePeriod.FourteenDays, "2024-03-17T12:00:00Z")]
    [InlineData(ProviderUsagePeriod.Month, "2024-02-29T12:00:00Z")]
    [InlineData(ProviderUsagePeriod.Quarter, "2023-12-31T12:00:00Z")]
    [InlineData(ProviderUsagePeriod.Year, "2023-03-31T12:00:00Z")]
    public void Calendar_periods_capture_one_utc_boundary_and_roundtrip(ProviderUsagePeriod period, string from) {
        var service = new ProviderUsageQueryService([], new FixedClock(Now.ToOffset(TimeSpan.FromHours(-4))));
        var query = service.Resolve(ProviderUsageWorkloadSelection.Both, period);
        Assert.Equal(DateTimeOffset.Parse(from, System.Globalization.CultureInfo.InvariantCulture), query.Window.FromUtc);
        Assert.Equal(Now, query.Window.ToUtc);
        Assert.Equal(TimeSpan.Zero, query.Window.ToUtc.Offset);
        Assert.True(query.Window.Contains(query.Window.FromUtc));
        Assert.False(query.Window.Contains(query.Window.FromUtc.AddTicks(-1)));
        Assert.False(query.Window.Contains(Now));
        Assert.Equal(query, JsonSerializer.Deserialize<ProviderUsageQuery>(JsonSerializer.Serialize(query)));
        Assert.Equal(new DateTimeOffset(2023, 2, 28, 12, 0, 0, TimeSpan.Zero),
            new ProviderUsageQuery(ProviderUsageWorkloadSelection.Both, ProviderUsagePeriod.Year, Now.AddMonths(-1)).Window.FromUtc);
    }

    public static IEnumerable<object[]> Selections() =>
        from selection in new[] { ProviderUsageWorkloadSelection.Agents, ProviderUsageWorkloadSelection.SimpleChats, ProviderUsageWorkloadSelection.Both }
        from period in Enum.GetValues<ProviderUsagePeriod>()
        select new object[] { selection, period };

    [Theory]
    [MemberData(nameof(Selections))]
    public async Task Bounded_aggregation_preserves_identity_unknowns_cost_and_64_bit_totals(ProviderUsageWorkloadSelection selection, ProviderUsagePeriod period) {
        var query = new ProviderUsageQuery(selection, period, Now);
        var from = query.Window.FromUtc;
        var agent = Evidence("same", ProviderUsageWorkloadKind.Agent, from);
        var extra = agent with { ContributionId = "second", OccurredAtUtc = Now.AddSeconds(-1), CostUsd = null, PricingCompleteness = ProviderUsagePricingCompleteness.Unpriced };
        var unknown = Evidence("orphan", ProviderUsageWorkloadKind.Unknown, from) with {
            ConsumerKind = ProviderUsageConsumerKind.Unattributed, UsageCompleteness = ProviderUsageCompleteness.UsageUnavailable,
            PricingCompleteness = ProviderUsagePricingCompleteness.Unpriced, CostUsd = null, Tokens = ProviderUsageTokenCounts.Empty
        };
        var agents = new BoundedSource(ProviderUsageWorkloadKind.Agent, [agent, agent, extra, unknown,
            agent with { ContributionId = "before", OccurredAtUtc = from.AddTicks(-1) },
            agent with { ContributionId = "after", OccurredAtUtc = Now }]);
        var chats = new BoundedSource(ProviderUsageWorkloadKind.SimpleChat, [Evidence("same", ProviderUsageWorkloadKind.SimpleChat, from)]);
        var result = await new ProviderUsageQueryService([agents, chats], new FixedClock(Now)).QueryWindowAsync(query);
        var includesAgents = selection != ProviderUsageWorkloadSelection.SimpleChats;
        var includesChats = selection != ProviderUsageWorkloadSelection.Agents;
        Assert.Equal(includesAgents ? 1 : 0, agents.Reads);
        Assert.Equal(includesChats ? 1 : 0, chats.Reads);
        Assert.Equal((includesAgents ? 2 : 0) + (includesChats ? 1 : 0) + (selection == ProviderUsageWorkloadSelection.Both ? 1 : 0), result.Totals.UsageObservationCount);
        Assert.Equal((includesAgents ? 1 : 0) + (includesChats ? 1 : 0) + (selection == ProviderUsageWorkloadSelection.Both ? 1 : 0), result.Totals.ExecutionCount);
        Assert.Equal((includesAgents ? 4_000_000_000L : 0) + (includesChats ? 2_000_000_000L : 0), result.Totals.Tokens.TotalTokens);
        Assert.Equal((includesAgents ? 0.25m : 0) + (includesChats ? 0.25m : 0), result.Totals.KnownCostUsd);
        Assert.Equal(query, result.Query);
        Assert.Equal(Now, result.GeneratedAtUtc);
        Assert.True(result.IsComplete);
        Assert.All(new[] { agents, chats }.Where(source => source.Reads > 0), source => Assert.Same(query.Window, source.Window));
    }

    [Fact]
    public async Task Missing_sources_and_empty_sources_do_not_certify_completeness() {
        var query = new ProviderUsageQuery(ProviderUsageWorkloadSelection.Both, ProviderUsagePeriod.SevenDays, Now);
        var result = await new ProviderUsageQueryService([]).QueryWindowAsync(query);
        Assert.False(result.IsComplete);
        Assert.Equal(2, result.Sources.Count);
        Assert.All(result.Sources, item => Assert.Equal(ProviderUsageSourceState.Failed, item.State));
        Assert.False(ProviderUsageSnapshot.Empty(query.Selection).IsComplete);
    }

    [Fact]
    public async Task Conflicting_bounded_evidence_is_rejected() {
        var query = new ProviderUsageQuery(ProviderUsageWorkloadSelection.Agents, ProviderUsagePeriod.SevenDays, Now);
        var item = Evidence("same", ProviderUsageWorkloadKind.Agent, query.Window.FromUtc);
        var source = new BoundedSource(ProviderUsageWorkloadKind.Agent, [item, item with { CostUsd = 99 }]);
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await new ProviderUsageQueryService([source]).QueryWindowAsync(query));
    }

    [Fact]
    public async Task Profile_generation_change_rejects_a_noncooperative_source_result() {
        var runtime = new MutableRuntime();
        var guard = new ProfileProviderUsageReadContext(runtime);
        var query = new ProviderUsageQuery(ProviderUsageWorkloadSelection.Agents, ProviderUsagePeriod.SevenDays, Now);
        var source = new BoundedSource(ProviderUsageWorkloadKind.Agent, []) { BeforeReturn = () => runtime.Generation++ };
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await new ProviderUsageQueryService([source], context: guard).QueryWindowAsync(query));
        Assert.Equal(1, source.Reads);
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await new ProviderUsageQueryService([source], context: guard).QueryWindowAsync(query));
        Assert.Equal(1, source.Reads);
    }

    private sealed class MutableRuntime : IDatabaseRuntimeState {
        public long Generation { get; set; }
        public DatabaseRuntimeSnapshot GetSnapshot() => new(null, "fixture", Generation);
        public void MarkCurrentProfile(ResolvedDatabaseProfile profile) => throw new NotSupportedException();
    }

    [Theory]
    [InlineData("")]
    [InlineData("all")]
    [InlineData("7D")]
    [InlineData(" 7d ")]
    [InlineData("7d,1y")]
    public void Public_parser_is_strict(string value) => Assert.False(ProviderUsagePeriods.TryParse(value, out _));

    private static ProviderUsageContribution Evidence(string id, ProviderUsageWorkloadKind kind, DateTimeOffset at)
        => new(id, kind, kind == ProviderUsageWorkloadKind.SimpleChat ? ProviderUsageConsumerKind.SimpleChatDefinition : ProviderUsageConsumerKind.Agent,
            "consumer", "Consumer", null, "Provider", ProviderKind.OpenAi, "model", "same-execution",
            ProviderUsageExecutionOutcome.Cancelled, ProviderUsageCompleteness.Observed, ProviderUsagePricingCompleteness.CalculatedAtExecution,
            new(1_500_000_000, 10, 20, 500_000_000, 30, 2_000_000_000), 0.25m, at);

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class BoundedSource(ProviderUsageWorkloadKind kind, IReadOnlyList<ProviderUsageContribution> evidence) : IBoundedProviderUsageProjectionSource {
        public string SourceName => kind.ToString();
        public ProviderUsageWorkloadKind WorkloadKind => kind;
        public int Reads { get; private set; }
        public ProviderUsageWindow? Window { get; private set; }
        public Action? BeforeReturn { get; init; }
        public ValueTask<ProviderUsageSourceResult> ReadAsync(CancellationToken cancellationToken = default) => throw new InvalidOperationException("Unbounded reads are forbidden in this test.");
        public ValueTask<ProviderUsageSourceResult> ReadWindowAsync(ProviderUsageWindow window, CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            Reads++;
            Window = window;
            BeforeReturn?.Invoke();
            return ValueTask.FromResult(new ProviderUsageSourceResult(SourceName, kind, ProviderUsageSourceState.Complete,
                evidence.Where(item => window.Contains(item.OccurredAtUtc)).ToArray(), Now) { Window = window });
        }
    }
}
