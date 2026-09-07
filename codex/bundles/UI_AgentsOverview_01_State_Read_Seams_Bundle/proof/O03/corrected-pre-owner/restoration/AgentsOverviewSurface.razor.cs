using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Usage;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.Charts;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components.Overview;

public partial class AgentsOverviewSurface {
    [Parameter, EditorRequired]
    public AgentsOverviewState State { get; set; } = default!;

    [Parameter]
    public EventCallback<AgentsOverviewIntent> Intent { get; set; }

    private readonly CdaChartOptions ProviderUsageBarChartOptions = AgentsOverviewPresentation.BarOptions();
    private readonly CdaChartOptions ProviderUsageDistributionChartOptions = AgentsOverviewPresentation.DistributionOptions();
    private bool hasOverviewLoaded => State.HasOverview;
    private bool hasUsageLoaded => State.HasUsage;
    private bool isUsageLoading => State.UsageLoading;
    private bool isOverviewLoading => State.OverviewLoading;
    private bool hasOverviewLoadError => State.OverviewError is not null;
    private string? overviewLoadError => State.OverviewError;
    private string? usageLoadError => State.UsageError;
    private bool HasUsagePartial => State.UsagePartial;
    private ProviderUsageWorkloadSelection usageSelection => State.DesiredScope;
    private string UsageChartEmptyText => !hasUsageLoaded
        ? isUsageLoading ? "Usage evidence is loading." : "Usage evidence is unavailable for this scope. Retry the usage read."
        : "No provider usage has been recorded in this scope.";

    private IReadOnlyList<SecondaryTabItem> UsageScopeTabs =>
    [
        new(nameof(ProviderUsageWorkloadSelection.Agents), "Agents"),
        new(nameof(ProviderUsageWorkloadSelection.SimpleChats), "Chats"),
        new(nameof(ProviderUsageWorkloadSelection.Both), "Both")
    ];

    private string UsageScopeKey => usageSelection.ToString();

    private string UsageScopeLabel => usageSelection switch {
        ProviderUsageWorkloadSelection.Agents => "Agents",
        ProviderUsageWorkloadSelection.SimpleChats => "Chats",
        ProviderUsageWorkloadSelection.Both => "Agents and Chats",
        _ => throw new ArgumentOutOfRangeException(nameof(usageSelection), usageSelection, "Unknown usage scope.")
    };

    private IReadOnlyList<OverviewMetricBadge> OverviewMetricBadges =>
    [
        new(
            "Agents",
            ResolveOverviewValue((State.Totals ?? AgentOverviewTotals.Empty).AgentCount),
            "groups",
            "info",
            "Organization-scoped technical runtime records.",
            "agents-overview-metric-agents"),
        new(
            "Teams",
            ResolveOverviewValue((State.Totals ?? AgentOverviewTotals.Empty).TeamCount),
            "hub",
            "success",
            "Agent teams available from the technical catalog.",
            "agents-overview-metric-teams"),
        new(
            "Providers",
            ResolveOverviewValue((State.Totals ?? AgentOverviewTotals.Empty).ProviderCount),
            "cloud",
            "accent",
                "Provider profiles executed through AgentFramework.",
            "agents-overview-metric-providers"),
        new(
            "Capabilities",
            ResolveOverviewValue((State.Totals ?? AgentOverviewTotals.Empty).CapabilityCount),
            "extension",
            "warning",
            "Reusable skills, MCP servers, and other runtime capabilities.",
            "agents-overview-metric-capabilities"),
        new(
            "Sessions",
            ResolveOverviewValue((State.Totals ?? AgentOverviewTotals.Empty).SessionCount),
            "forum",
            "neutral",
            "Chat and runtime sessions associated with the workspace.",
            "agents-overview-metric-sessions"),
        new(
            "Usage",
            ResolveUsageValue((State.UsageTotals ?? ProviderUsageTotals.Empty).UsageObservationCount),
            "monitor_heart",
            "info",
            $"Usage observations for {UsageScopeLabel}.",
            "agents-overview-metric-usage"),
        new(
            "Tokens",
            ResolveUsageTokens((State.UsageTotals ?? ProviderUsageTotals.Empty).Tokens.TotalTokens),
            "token",
            "success",
            $"Known token usage for {UsageScopeLabel}.",
            "agents-overview-metric-tokens"),
        new(
            "Cost",
            ResolveUsageCost(),
            "paid",
            "danger",
            $"Known execution-time provider cost for {UsageScopeLabel}; unpriced observations are never treated as free.",
            "agents-overview-metric-cost")
    ];

    private IReadOnlyList<ProviderUsageProviderRow> OverviewProviderRows =>
        !State.HasUsage ? [] : State.Providers
            .OrderByDescending(item => item.Totals.UsageObservationCount)
            .Take(6)
            .ToArray();

    private IReadOnlyList<ProviderUsageConsumerRow> TopUsageConsumers =>
        !State.HasUsage ? [] : State.Consumers
            .OrderByDescending(item => item.Totals.ExecutionCount)
            .ThenByDescending(item => item.Totals.KnownCostUsd)
            .Take(5)
            .ToArray();

    private IReadOnlyList<ProviderUsageConsumerRow> TopFailingConsumers =>
        !State.HasUsage ? [] : State.Consumers
            .Where(item => item.Totals.FailedExecutionCount > 0)
            .OrderByDescending(item => item.Totals.FailedExecutionCount)
            .ThenByDescending(item => item.Totals.ExecutionCount)
            .Take(5)
            .ToArray();

    private IReadOnlyDictionary<string, string?> OverviewConsumerAvatarImageUrls =>
        State.Avatars;

    private IReadOnlyList<CdaChartSeries> ProviderUsageBarSeries =>
        OverviewProviderRows.Count == 0
            ? []
            :
            [
                new CdaChartSeries {
                    Name = "Provider usage",
                    Type = CdaChartType.Bar,
                    Points = OverviewProviderRows
                        .Select(item => new CdaChartPoint(
                            AgentUsageDisplay.TrimLabel(item.ProviderName, 22),
                            item.Totals.UsageObservationCount))
                        .ToArray()
                }
            ];

    private IReadOnlyList<CdaChartSeries> ProviderUsageDistributionSeries =>
        OverviewProviderRows.Count == 0
            ? []
            :
            [
                new CdaChartSeries {
                    Name = "Provider share",
                    Type = CdaChartType.Donut,
                    Points = OverviewProviderRows
                        .Select(item => new CdaChartPoint(
                            AgentUsageDisplay.TrimLabel(item.ProviderName, 22),
                            item.Totals.UsageObservationCount))
                        .ToArray()
                }
            ];

    private static string ResolveOverviewMetricBadgeClass(string tone) {
        return tone switch {
            "success" => "agents-overview-stat-badge agents-overview-stat-badge--success",
            "warning" => "agents-overview-stat-badge agents-overview-stat-badge--warning",
            "danger" => "agents-overview-stat-badge agents-overview-stat-badge--danger",
            "accent" => "agents-overview-stat-badge agents-overview-stat-badge--accent",
            "neutral" => "agents-overview-stat-badge agents-overview-stat-badge--neutral",
            _ => "agents-overview-stat-badge agents-overview-stat-badge--info"
        };
    }

    private string ResolveOverviewValue(int value) {
        return hasOverviewLoaded ? AgentUsageDisplay.FormatCount(value) : isOverviewLoading ? "..." : "\u2014";
    }

    private string ResolveUsageValue(int value)
        => hasUsageLoaded ? AgentUsageDisplay.FormatCount(value) : isUsageLoading ? "..." : "\u2014";

    private string ResolveUsageTokens(int value)
        => hasUsageLoaded ? AgentUsageDisplay.FormatTokens(value) : isUsageLoading ? "..." : "\u2014";

    private string ResolveUsageCost() {
        if (!hasUsageLoaded) {
            return isUsageLoading ? "..." : "\u2014";
        }

        var knownCost = AgentUsageDisplay.FormatCost((State.UsageTotals ?? ProviderUsageTotals.Empty).KnownCostUsd);
        return (State.UsageTotals ?? ProviderUsageTotals.Empty).UnpricedObservationCount == 0
            ? knownCost
            : (State.UsageTotals ?? ProviderUsageTotals.Empty).PricedObservationCount == 0
                ? "Unpriced"
                : $"{knownCost} + {(State.UsageTotals ?? ProviderUsageTotals.Empty).UnpricedObservationCount:N0} unpriced";
    }

    private Task HandleUsageScopeChangedAsync(string key) => Intent.InvokeAsync(new AgentsOverviewIntent.SelectUsage(key switch {
        nameof(ProviderUsageWorkloadSelection.Agents) => ProviderUsageWorkloadSelection.Agents,
        nameof(ProviderUsageWorkloadSelection.SimpleChats) => ProviderUsageWorkloadSelection.SimpleChats,
        nameof(ProviderUsageWorkloadSelection.Both) => ProviderUsageWorkloadSelection.Both,
        _ => throw new ArgumentOutOfRangeException(nameof(key))
    }));

    private Task RetryOverviewAsync() => Intent.InvokeAsync(new AgentsOverviewIntent.RetryOverview());
    private Task RetryUsageAsync() => Intent.InvokeAsync(new AgentsOverviewIntent.RetryUsage());
    private Task RetryHeaderAsync() => Intent.InvokeAsync(new AgentsOverviewIntent.RetryHeader());
    private Task OpenDetailAsync(AgentsOverviewDetail detail) => State.CanOpen(detail)
        ? Intent.InvokeAsync(new AgentsOverviewIntent.OpenDetail(detail, State.DesiredScope)) : Task.CompletedTask;
    private Task OpenAgentsForTeamAsync(Guid id) => Intent.InvokeAsync(new AgentsOverviewIntent.OpenTeam(id));

    private sealed record OverviewMetricBadge(string Label, string Value, string Icon, string Tone, string TooltipText, string TestId);
}
