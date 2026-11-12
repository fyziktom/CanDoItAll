using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.UI.Shell;
using CanDoItAll.AgentFramework.UI.Usage;
using CanDoItAll.AgentFramework.Usage;
using CanDoItAll.Components.BaseLib;

namespace CanDoItAll.AgentFramework.UiSandbox;

public enum CompletionScenario { Ready, Loading, Error, Empty, Partial, Unknown, Long, Retired }

public static class CompletionSandboxFixture {
    public static AgentsShellState Shell(long scope = 1, string tab = "overview") => new(new(scope, tab),
        "40", "4", "12", "9", "2", "1", null, false, "HR Agent", null, true, false,
        [new("overview", "Overview"), new("agents", "Agents", "40"), new("simple-chats", "Simple Chats"),
         new("providers", "Providers", "4"), new("request-history", "Request history"), new("voice", "Voice"),
         new("floating-chat", "Floating chat"), new("chat", "Chat"), new("capabilities", "Capabilities"),
         new("governance", "Governance"), new("diagnostics", "Diagnostics")], false, false);

    public static UsageDetailState Usage(CompletionScenario scenario = CompletionScenario.Ready,
        ProviderUsageWorkloadSelection selection = ProviderUsageWorkloadSelection.Both,
        ProviderUsagePeriod period = ProviderUsagePeriod.SevenDays) {
        var now = new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);
        var query = new ProviderUsageQuery(selection, period, now);
        var totals = ProviderUsageTotals.Empty with {
            ExecutionCount = 6, FailedExecutionCount = 1, UsageObservationCount = 8,
            UnknownUsageObservationCount = 2, UnpricedObservationCount = 3, KnownCostUsd = 0.012m,
            Tokens = new(120, 0, 0, 30, 0, 150)
        };
        var consumerKind = selection == ProviderUsageWorkloadSelection.SimpleChats
            ? ProviderUsageConsumerKind.SimpleChatDefinition : ProviderUsageConsumerKind.Agent;
        var consumers = Enumerable.Range(1, 12).Select(index => new ProviderUsageConsumerRow(consumerKind,
            $"consumer-{index}", scenario == CompletionScenario.Long ? $"Consumer {index} with a deliberately long research and delivery responsibility label" : $"Consumer {index}",
            totals with { ExecutionCount = index, UsageObservationCount = index + 2, KnownUsageObservationCount = index,
                PricedObservationCount = index - 1 }, now.AddMinutes(-index))).ToArray();
        if (scenario == CompletionScenario.Unknown) {
            consumers = consumers.Select(row => row with { Totals = row.Totals with {
                KnownUsageObservationCount = 0, UnknownUsageObservationCount = row.Totals.UsageObservationCount,
                PricedObservationCount = 0, UnpricedObservationCount = row.Totals.UsageObservationCount,
                Tokens = ProviderUsageTokenCounts.Empty, KnownCostUsd = 0m
            } }).ToArray();
        }
        var providers = new[] {
            new ProviderUsageProviderRow(Guid.Parse("668f86a7-ea2b-4078-9d11-81c7d3346fce"), "Same provider label", ProviderKind.OpenAi,
                Sum(consumers.Take(6)), now.AddMinutes(-1)),
            new ProviderUsageProviderRow(Guid.Parse("158f5bbb-cd1e-4d77-a530-c5826e2f9a5b"), "Same provider label", ProviderKind.OpenAi,
                Sum(consumers.Skip(6)), now.AddMinutes(-7))
        };
        var sources = new[] {
            new ProviderUsageSourceStatus("sample-agents", ProviderUsageWorkloadKind.Agent, ProviderUsageSourceState.Complete, now, null),
            new ProviderUsageSourceStatus("sample-chats", ProviderUsageWorkloadKind.SimpleChat, ProviderUsageSourceState.Complete, now, null)
        }.Where(source => selection.Includes(source.WorkloadKind)).ToArray();
        var snapshot = new ProviderUsageSnapshot(selection, Sum(consumers), consumers, providers,
            providers.Select((provider, index) => new ProviderUsageModelRow(provider.ProviderProfileId, provider.ProviderName, provider.ProviderKind,
                $"opaque-model/{index + 1}:revision", provider.Totals, provider.LastUsedAtUtc)).ToArray(), sources, now) { Query = query, GeneratedAtUtc = now };
        snapshot = scenario switch {
            CompletionScenario.Empty => snapshot with { Totals = ProviderUsageTotals.Empty, Consumers = [], Providers = [], Models = [] },
            CompletionScenario.Partial => snapshot with { Sources = sources.Select(source => source with { State = ProviderUsageSourceState.Partial }).ToArray() },
            _ => snapshot
        };
        return new(Guid.NewGuid(), query) {
            Loading = scenario == CompletionScenario.Loading,
            Retired = scenario == CompletionScenario.Retired,
            Error = scenario == CompletionScenario.Error ? "The sample source is unavailable. Retry this accepted interval." : null,
            Snapshot = scenario is CompletionScenario.Loading or CompletionScenario.Error or CompletionScenario.Retired ? null : snapshot
        };
    }

    private static ProviderUsageTotals Sum(IEnumerable<ProviderUsageConsumerRow> consumers) {
        var rows = consumers.Select(consumer => consumer.Totals).ToArray();
        return new(rows.Sum(row => row.ExecutionCount), rows.Sum(row => row.FailedExecutionCount), 0,
            rows.Sum(row => row.UsageObservationCount), rows.Sum(row => row.KnownUsageObservationCount),
            rows.Sum(row => row.UnknownUsageObservationCount), rows.Sum(row => row.PricedObservationCount),
            rows.Sum(row => row.UnpricedObservationCount),
            new(rows.Sum(row => row.Tokens.InputTokens), 0, 0, rows.Sum(row => row.Tokens.OutputTokens), 0, rows.Sum(row => row.Tokens.TotalTokens)),
            rows.Sum(row => row.KnownCostUsd));
    }
}
