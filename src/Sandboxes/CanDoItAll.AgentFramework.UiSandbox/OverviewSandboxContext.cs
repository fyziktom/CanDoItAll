using System.Collections.Immutable;
using CanDoItAll.AgentFramework.Usage;

namespace CanDoItAll.AgentFramework.UiSandbox;

public enum OverviewSandboxScenario {
    Baseline, Loading, InitialFailure, StaleOverview, Empty, Ready, HeaderPartial,
    BoundUnavailable, UsageLoading, StaleUsage, ScopePending, WrongScope, Partial,
    UnknownUnpriced, LongContent, DetailPending
}

public sealed record OverviewSandboxScenarioDefinition(OverviewSandboxScenario Scenario, string Token, string Label);

public sealed record OverviewSandboxContext(
    OverviewSandboxScenario Scenario = OverviewSandboxScenario.Baseline,
    CatalogSandboxLayout Layout = CatalogSandboxLayout.Matched,
    ProviderUsageWorkloadSelection Scope = ProviderUsageWorkloadSelection.Both) {
    public static ImmutableArray<OverviewSandboxScenarioDefinition> Scenarios { get; } = [
        new(OverviewSandboxScenario.Baseline, "baseline", "Production baseline"),
        new(OverviewSandboxScenario.Loading, "loading", "Initial loading"),
        new(OverviewSandboxScenario.InitialFailure, "initial-failure", "Overview unavailable"),
        new(OverviewSandboxScenario.StaleOverview, "stale-overview", "Stale Overview"),
        new(OverviewSandboxScenario.Empty, "empty", "Empty"),
        new(OverviewSandboxScenario.Ready, "ready", "Ready"),
        new(OverviewSandboxScenario.HeaderPartial, "header-partial", "HR unavailable"),
        new(OverviewSandboxScenario.BoundUnavailable, "bound-unavailable", "Bound count unavailable"),
        new(OverviewSandboxScenario.UsageLoading, "usage-loading", "Usage loading"),
        new(OverviewSandboxScenario.StaleUsage, "stale-usage", "Stale same-scope usage"),
        new(OverviewSandboxScenario.ScopePending, "scope-pending", "Requested scope pending"),
        new(OverviewSandboxScenario.WrongScope, "wrong-scope", "Wrong-scope result rejected"),
        new(OverviewSandboxScenario.Partial, "partial", "Partial usage"),
        new(OverviewSandboxScenario.UnknownUnpriced, "unknown-unpriced", "Unknown and unpriced usage"),
        new(OverviewSandboxScenario.LongContent, "long-content", "Long labels"),
        new(OverviewSandboxScenario.DetailPending, "detail-pending", "Detail already open")
    ];

    public static OverviewSandboxContext Parse(string? scenario, string? layout, string? scope) => new(
        Scenarios.FirstOrDefault(item => string.Equals(item.Token, scenario?.Trim(), StringComparison.OrdinalIgnoreCase))?.Scenario
            ?? OverviewSandboxScenario.Baseline,
        string.Equals(layout?.Trim(), "flexible", StringComparison.OrdinalIgnoreCase) ? CatalogSandboxLayout.Flexible : CatalogSandboxLayout.Matched,
        scope?.Trim().ToLowerInvariant() switch {
            "agents" => ProviderUsageWorkloadSelection.Agents,
            "chats" => ProviderUsageWorkloadSelection.SimpleChats,
            _ => ProviderUsageWorkloadSelection.Both
        });

    public IReadOnlyDictionary<string, object?> ToQuery() => new Dictionary<string, object?> {
        ["specimen"] = "overview",
        ["scenario"] = Scenarios.Single(item => item.Scenario == Scenario).Token,
        ["layout"] = Layout == CatalogSandboxLayout.Matched ? "matched" : "flexible",
        ["usageScope"] = Scope switch {
            ProviderUsageWorkloadSelection.Agents => "agents",
            ProviderUsageWorkloadSelection.SimpleChats => "chats",
            ProviderUsageWorkloadSelection.Both => "both",
            _ => throw new ArgumentOutOfRangeException(nameof(Scope))
        },
        ["agentId"] = null,
        ["teamId"] = null
    };
}
