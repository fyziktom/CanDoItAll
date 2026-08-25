using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Usage;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class ProviderUsageWorkloadSelectionTests
{
    [Fact]
    public async Task NoneAndUnknownSelectionAreRejected()
    {
        var service = new ProviderUsageQueryService([]);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            async () => await service.QueryAsync(ProviderUsageWorkloadSelection.None));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            async () => await service.QueryAsync((ProviderUsageWorkloadSelection)8));
    }

    [Fact]
    public void ChatSessionIdDoesNotClassifySimpleChat()
    {
        var workload = ProviderUsageWorkloadClassifier.ClassifyAgentObservation(
            agentId: null,
            executionRunId: null,
            chatSessionId: Guid.NewGuid());

        Assert.Equal(ProviderUsageWorkloadKind.Unknown, workload);
    }
}

public sealed class ProviderUsageAggregationTests
{
    [Fact]
    public async Task Selected_sources_are_read_concurrently_and_reported_deterministically()
    {
        var probe = new ConcurrentReadProbe(expectedReaders: 3);
        var imageContribution = Contribution(
            "relay-image",
            ProviderUsageWorkloadKind.SharedProviderRelay,
            ProviderUsageConsumerKind.SharedProviderRelay,
            "publication-1",
            "Shared image provider",
            "relay-run",
            costUsd: null) with
        {
            Tokens = ProviderUsageTokenCounts.Empty,
            ImageCount = 2
        };
        var service = new ProviderUsageQueryService(
        [
            new ConcurrentSource(
                "z-chat",
                ProviderUsageWorkloadKind.SimpleChat,
                [Contribution(
                    "chat",
                    ProviderUsageWorkloadKind.SimpleChat,
                    ProviderUsageConsumerKind.SimpleChatDefinition,
                    "chat-1",
                    "Chat",
                    "chat-run",
                    2m)],
                probe),
            new ConcurrentSource(
                "a-agent",
                ProviderUsageWorkloadKind.Agent,
                [Contribution(
                    "agent",
                    ProviderUsageWorkloadKind.Agent,
                    ProviderUsageConsumerKind.Agent,
                    "agent-1",
                    "Agent",
                    "agent-run",
                    1m)],
                probe),
            new ConcurrentSource(
                "m-relay",
                ProviderUsageWorkloadKind.SharedProviderRelay,
                [imageContribution],
                probe)
        ]);

        var snapshot = await service.QueryAsync(ProviderUsageWorkloadSelection.All);

        Assert.Equal(3, probe.StartedReaders);
        Assert.Equal(["a-agent", "m-relay", "z-chat"], snapshot.Sources.Select(source => source.SourceName));
        Assert.Equal(3m, snapshot.Totals.KnownCostUsd);
        Assert.Equal(200, snapshot.Totals.Tokens.TotalTokens);
        Assert.Equal(2, snapshot.Totals.ImageCount);
        Assert.Contains(
            typeof(ProviderUsageContribution).GetConstructors(),
            constructor => constructor.GetParameters().Length == 16);
        Assert.Equal(
            16,
            typeof(ProviderUsageContribution).GetMethod("Deconstruct")!.GetParameters().Length);
        Assert.Contains(
            typeof(ProviderUsageTotals).GetConstructors(),
            constructor => constructor.GetParameters().Length == 10);
        Assert.Equal(
            10,
            typeof(ProviderUsageTotals).GetMethod("Deconstruct")!.GetParameters().Length);
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await new ProviderUsageQueryService(
            [
                new FakeSource(
                    "invalid-relay",
                    ProviderUsageWorkloadKind.SharedProviderRelay,
                    [imageContribution with { ImageCount = 0 }])
            ]).QueryAsync(ProviderUsageWorkloadSelection.SharedProviderRelays));
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await new ProviderUsageQueryService(
            [
                new FakeSource(
                    "mixed-relay",
                    ProviderUsageWorkloadKind.SharedProviderRelay,
                    [imageContribution with
                    {
                        Tokens = new ProviderUsageTokenCounts(1, 0, 0, 0, 0, 1)
                    }])
            ]).QueryAsync(ProviderUsageWorkloadSelection.SharedProviderRelays));
    }

    [Fact]
    public async Task AgentsOnlyReturnsOnlyAgentEvidence()
    {
        var service = CreateService();

        var snapshot = await service.QueryAsync(ProviderUsageWorkloadSelection.Agents);

        Assert.Equal(ProviderUsageWorkloadSelection.Agents, snapshot.Selection);
        Assert.Single(snapshot.Consumers);
        Assert.Equal(ProviderUsageConsumerKind.Agent, snapshot.Consumers[0].ConsumerKind);
        Assert.Equal(1m, snapshot.Totals.KnownCostUsd);
    }

    [Fact]
    public async Task SimpleChatsOnlyReturnsOnlyInvocationEvidence()
    {
        var service = CreateService();

        var snapshot = await service.QueryAsync(ProviderUsageWorkloadSelection.SimpleChats);

        Assert.Single(snapshot.Consumers);
        Assert.Equal(ProviderUsageConsumerKind.SimpleChatDefinition, snapshot.Consumers[0].ConsumerKind);
        Assert.Equal(2m, snapshot.Totals.KnownCostUsd);
    }

    [Fact]
    public async Task BothEqualsDeduplicatedSourceSum()
    {
        var service = CreateService(duplicateChatAttempt: true);

        var agents = await service.QueryAsync(ProviderUsageWorkloadSelection.Agents);
        var chats = await service.QueryAsync(ProviderUsageWorkloadSelection.SimpleChats);
        var both = await service.QueryAsync(ProviderUsageWorkloadSelection.Both);

        Assert.Equal(agents.Totals.KnownCostUsd + chats.Totals.KnownCostUsd, both.Totals.KnownCostUsd);
        Assert.Equal(agents.Totals.Tokens.TotalTokens + chats.Totals.Tokens.TotalTokens, both.Totals.Tokens.TotalTokens);
        Assert.Equal(2, both.Totals.UsageObservationCount);
    }

    [Fact]
    public async Task RetriesAddAttemptCostButOnlyOneOperationExecution()
    {
        var retry = Contribution(
            "operation-2:2",
            ProviderUsageWorkloadKind.SimpleChat,
            ProviderUsageConsumerKind.SimpleChatDefinition,
            "chat-1",
            "Chat",
            "operation-2",
            3m) with
        {
            ExecutionOutcome = ProviderUsageExecutionOutcome.Failed
        };
        var first = retry with { ContributionId = "operation-2:1", CostUsd = 2m };
        var service = new ProviderUsageQueryService(
        [
            new FakeSource("chats", ProviderUsageWorkloadKind.SimpleChat, [first, retry])
        ]);

        var snapshot = await service.QueryAsync(ProviderUsageWorkloadSelection.SimpleChats);

        Assert.Equal(5m, snapshot.Totals.KnownCostUsd);
        Assert.Equal(1, snapshot.Totals.ExecutionCount);
        Assert.Equal(1, snapshot.Totals.FailedExecutionCount);
    }

    [Fact]
    public async Task LegacyKnownTokensRemainUnpricedRatherThanFree()
    {
        var legacy = Contribution(
            "legacy",
            ProviderUsageWorkloadKind.SimpleChat,
            ProviderUsageConsumerKind.SimpleChatDefinition,
            "chat-1",
            "Chat",
            "legacy-operation",
            null) with
        {
            UsageCompleteness = ProviderUsageCompleteness.LegacyKnownTokens,
            PricingCompleteness = ProviderUsagePricingCompleteness.Unpriced
        };
        var service = new ProviderUsageQueryService(
        [
            new FakeSource("chats", ProviderUsageWorkloadKind.SimpleChat, [legacy])
        ]);

        var snapshot = await service.QueryAsync(ProviderUsageWorkloadSelection.SimpleChats);

        Assert.Equal(1, snapshot.Totals.UnpricedObservationCount);
        Assert.Equal(100, snapshot.Totals.Tokens.TotalTokens);
        Assert.Equal(0m, snapshot.Totals.KnownCostUsd);
    }

    [Fact]
    public async Task PartialSourceFailureIsVisible()
    {
        var service = new ProviderUsageQueryService(
        [
            new FakeSource("agents", ProviderUsageWorkloadKind.Agent,
            [
                Contribution("agent", ProviderUsageWorkloadKind.Agent, ProviderUsageConsumerKind.Agent, "agent-1", "Agent", "run-1", 1m)
            ]),
            new FakeSource(
                "chats",
                ProviderUsageWorkloadKind.SimpleChat,
                ProviderUsageSourceResult.Failed(
                    "chats",
                    ProviderUsageWorkloadKind.SimpleChat,
                    "database-unavailable",
                    "Simple Chat usage is unavailable.",
                    DateTimeOffset.UnixEpoch))
        ]);

        var snapshot = await service.QueryAsync(ProviderUsageWorkloadSelection.Both);

        Assert.False(snapshot.IsComplete);
        Assert.Equal(1m, snapshot.Totals.KnownCostUsd);
        var failed = Assert.Single(snapshot.Sources, source => source.State == ProviderUsageSourceState.Failed);
        Assert.Equal("database-unavailable", failed.Error?.Code);
    }

    private static ProviderUsageQueryService CreateService(bool duplicateChatAttempt = false)
    {
        var agent = Contribution(
            "agent-observation-1",
            ProviderUsageWorkloadKind.Agent,
            ProviderUsageConsumerKind.Agent,
            "agent-1",
            "Agent",
            "run-1",
            1m);
        var chat = Contribution(
            "operation-1:1",
            ProviderUsageWorkloadKind.SimpleChat,
            ProviderUsageConsumerKind.SimpleChatDefinition,
            "chat-1",
            "Chat",
            "operation-1",
            2m);
        var chats = duplicateChatAttempt ? new[] { chat, chat } : [chat];
        return new ProviderUsageQueryService(
        [
            new FakeSource("agents", ProviderUsageWorkloadKind.Agent, [agent]),
            new FakeSource("chats", ProviderUsageWorkloadKind.SimpleChat, chats)
        ]);
    }

    private static ProviderUsageContribution Contribution(
        string id,
        ProviderUsageWorkloadKind workload,
        ProviderUsageConsumerKind consumerKind,
        string consumerId,
        string consumerName,
        string executionId,
        decimal? costUsd)
    {
        return new(
            id,
            workload,
            consumerKind,
            consumerId,
            consumerName,
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            "Provider",
            ProviderKind.OpenAi,
            "model",
            executionId,
            ProviderUsageExecutionOutcome.Succeeded,
            ProviderUsageCompleteness.Observed,
            costUsd.HasValue
                ? ProviderUsagePricingCompleteness.CalculatedAtExecution
                : ProviderUsagePricingCompleteness.Unpriced,
            new ProviderUsageTokenCounts(60, 10, 0, 40, 0, 100),
            costUsd,
            DateTimeOffset.UnixEpoch);
    }

    private sealed class FakeSource : IProviderUsageProjectionSource
    {
        private readonly ProviderUsageSourceResult _result;

        public FakeSource(
            string sourceName,
            ProviderUsageWorkloadKind workloadKind,
            IReadOnlyList<ProviderUsageContribution> contributions)
            : this(
                sourceName,
                workloadKind,
                new ProviderUsageSourceResult(
                    sourceName,
                    workloadKind,
                    ProviderUsageSourceState.Complete,
                    contributions,
                    DateTimeOffset.UnixEpoch))
        {
        }

        public FakeSource(
            string sourceName,
            ProviderUsageWorkloadKind workloadKind,
            ProviderUsageSourceResult result)
        {
            SourceName = sourceName;
            WorkloadKind = workloadKind;
            _result = result;
        }

        public string SourceName { get; }

        public ProviderUsageWorkloadKind WorkloadKind { get; }

        public ValueTask<ProviderUsageSourceResult> ReadAsync(CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(_result);
        }
    }

    private sealed class ConcurrentSource(
        string sourceName,
        ProviderUsageWorkloadKind workloadKind,
        IReadOnlyList<ProviderUsageContribution> contributions,
        ConcurrentReadProbe probe) : IProviderUsageProjectionSource
    {
        public string SourceName => sourceName;

        public ProviderUsageWorkloadKind WorkloadKind => workloadKind;

        public async ValueTask<ProviderUsageSourceResult> ReadAsync(
            CancellationToken cancellationToken = default)
        {
            await probe.ArriveAsync(cancellationToken);
            return new ProviderUsageSourceResult(
                sourceName,
                workloadKind,
                ProviderUsageSourceState.Complete,
                contributions,
                DateTimeOffset.UnixEpoch);
        }
    }

    private sealed class ConcurrentReadProbe(int expectedReaders)
    {
        private readonly TaskCompletionSource allReadersStarted = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private int startedReaders;

        public int StartedReaders => Volatile.Read(ref startedReaders);

        public async Task ArriveAsync(CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref startedReaders) == expectedReaders)
            {
                allReadersStarted.TrySetResult();
            }

            await allReadersStarted.Task
                .WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);
        }
    }
}
