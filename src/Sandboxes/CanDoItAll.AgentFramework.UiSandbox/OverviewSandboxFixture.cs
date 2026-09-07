using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.UI.Overview;
using CanDoItAll.AgentFramework.Usage;

namespace CanDoItAll.AgentFramework.UiSandbox;

public static class OverviewSandboxFixture {
    private static AgentsOverviewState Baseline { get; } = LoadBaseline();

    public static AgentsOverviewState Create(OverviewSandboxContext context) {
        var state = Baseline with { DesiredScope = context.Scope, AcceptedScope = context.Scope };
        if (context.Scope == ProviderUsageWorkloadSelection.SimpleChats) {
            state = state with {
                Consumers = state.Consumers.Select(row => row with {
                    ConsumerKind = ProviderUsageConsumerKind.SimpleChatDefinition,
                    ConsumerId = "sample-chat:" + row.ConsumerId,
                    ConsumerName = "Chat: " + row.ConsumerName
                }).ToImmutableArray()
            };
        }
        return context.Scenario switch {
            OverviewSandboxScenario.Loading => state with {
                Totals = null, UsageTotals = null, AcceptedScope = null, Consumers = [], Providers = [], Teams = [],
                OverviewPhase = AgentsOverviewReadPhase.Loading, UsagePhase = AgentsOverviewReadPhase.Loading, HeaderLoading = true
            },
            OverviewSandboxScenario.InitialFailure => state with {
                Totals = null, Teams = [], OverviewPhase = AgentsOverviewReadPhase.Unavailable,
                OverviewError = "The sample agent summary is unavailable. Retry the Overview read."
            },
            OverviewSandboxScenario.StaleOverview => state with {
                OverviewPhase = AgentsOverviewReadPhase.Stale,
                OverviewError = "The sample summary refresh failed. Previously accepted totals remain visible."
            },
            OverviewSandboxScenario.Empty => state with {
                Totals = AgentOverviewTotals.Empty, UsageTotals = ProviderUsageTotals.Empty,
                Consumers = [], Providers = [], Teams = []
            },
            OverviewSandboxScenario.HeaderPartial => state with {
                HeaderWarning = "The sample HR agent is unavailable. Overview and usage remain available."
            },
            OverviewSandboxScenario.BoundUnavailable => state with {
                HeaderWarning = "The sample bound-resource count is unavailable. HR and Overview remain available."
            },
            OverviewSandboxScenario.UsageLoading => state with {
                UsageTotals = null, AcceptedScope = null, Consumers = [], Providers = [], UsagePhase = AgentsOverviewReadPhase.Loading
            },
            OverviewSandboxScenario.StaleUsage => state with {
                UsagePhase = AgentsOverviewReadPhase.Stale,
                UsageError = "The sample usage refresh failed. Previously accepted same-scope evidence remains visible."
            },
            OverviewSandboxScenario.ScopePending => Mismatched(state) with { UsagePhase = AgentsOverviewReadPhase.Loading },
            OverviewSandboxScenario.WrongScope => Mismatched(state) with {
                UsagePhase = AgentsOverviewReadPhase.Unavailable,
                UsageError = "The sample result did not match the requested scope. Retry the requested scope."
            },
            OverviewSandboxScenario.Partial => state with { UsageSources = [ProviderUsageSourceState.Complete, ProviderUsageSourceState.Failed] },
            OverviewSandboxScenario.UnknownUnpriced => state with {
                UsageTotals = state.UsageTotals! with { PricedObservationCount = 3, UnpricedObservationCount = 1 }
            },
            OverviewSandboxScenario.LongContent => state with {
                Consumers = state.Consumers.Select(row => row with {
                    ConsumerName = row.ConsumerName + " with a deliberately long research and delivery responsibility label"
                }).ToImmutableArray(),
                Providers = state.Providers.Select(row => row with {
                    ProviderName = row.ProviderName + " with a deliberately long local inference provider label"
                }).ToImmutableArray(),
                Teams = state.Teams.Select(row => row with {
                    Name = row.Name + " with a deliberately long cross-functional team name"
                }).ToImmutableArray()
            },
            OverviewSandboxScenario.DetailPending => state with { OpenDetails = [AgentsOverviewDetail.Consumers, AgentsOverviewDetail.Providers, AgentsOverviewDetail.Models] },
            OverviewSandboxScenario.Baseline or OverviewSandboxScenario.Ready => state,
            _ => throw new ArgumentOutOfRangeException(nameof(context))
        };
    }

    private static AgentsOverviewState Mismatched(AgentsOverviewState state) => state with {
        AcceptedScope = state.DesiredScope == ProviderUsageWorkloadSelection.Agents
            ? ProviderUsageWorkloadSelection.SimpleChats : ProviderUsageWorkloadSelection.Agents,
        UsageTotals = null, Consumers = [], Providers = []
    };

    private static AgentsOverviewState LoadBaseline() {
        using var stream = typeof(OverviewSandboxFixture).Assembly.GetManifestResourceStream("OverviewFixture.json")
            ?? throw new InvalidOperationException("The frozen Overview fixture is missing.");
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return JsonSerializer.Deserialize<AgentsOverviewState>(stream, options)
            ?? throw new InvalidOperationException("The frozen Overview fixture is invalid.");
    }
}
