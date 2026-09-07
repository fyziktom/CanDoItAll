using System.Collections.Immutable;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Usage;
using CanDoItAll.Components.Charts;

namespace CanDoItAll.AgentFramework.UI.Overview;

public static class AgentsOverviewPresentation {
    public static AgentsOverviewState Create(AgentOverviewSnapshot? overview, ProviderUsageSnapshot? usage,
        ProviderUsageWorkloadSelection desired, bool overviewLoading, bool usageLoading,
        string? overviewError, string? usageError, IReadOnlyDictionary<string, string?> avatars) {
        if (desired is not (ProviderUsageWorkloadSelection.Agents or ProviderUsageWorkloadSelection.SimpleChats or ProviderUsageWorkloadSelection.Both)) {
            throw new ArgumentOutOfRangeException(nameof(desired));
        }
        var matching = usage?.Selection == desired ? usage : null;
        return new() {
            DesiredScope = desired,
            AcceptedScope = usage?.Selection,
            Totals = overview?.Totals,
            UsageTotals = matching?.Totals,
            Teams = overview?.TeamShortcuts.ToImmutableArray() ?? [],
            Consumers = matching?.Consumers.ToImmutableArray() ?? [],
            Providers = matching?.Providers.OrderByDescending(row => row.Totals.UsageObservationCount).Take(6).ToImmutableArray() ?? [],
            UsageSources = matching?.Sources.Select(source => source.State).ToImmutableArray() ?? [],
            Avatars = avatars.ToImmutableDictionary(StringComparer.OrdinalIgnoreCase),
            OverviewPhase = Phase(overview is not null, overviewLoading, overviewError),
            UsagePhase = Phase(matching is not null, usageLoading, usageError),
            OverviewError = overviewError,
            UsageError = usageError
        };
    }

    public static CdaChartOptions BarOptions() => new() {
        Type = CdaChartType.Bar,
        XAxisType = CdaChartAxisType.Category,
        Unit = "observations",
        YAxisTitle = "Usage observations",
        ShowToolbar = false,
        EnableZoom = false,
        ShowLegend = false,
        ValuePrecision = 0,
        TooltipPrecision = 0,
        Palette = CdaChartPalette.Calm.ToImmutableArray()
    };

    public static CdaChartOptions DistributionOptions() => new() {
        Type = CdaChartType.Donut,
        XAxisType = CdaChartAxisType.Category,
        Unit = "observations",
        ShowToolbar = false,
        EnableZoom = false,
        ShowLegend = true,
        ShowDataLabels = true,
        ValuePrecision = 0,
        TooltipPrecision = 0,
        LegendPosition = CdaChartLegendPosition.Top,
        Palette = CdaChartPalette.Energetic.ToImmutableArray()
    };

    private static AgentsOverviewReadPhase Phase(bool accepted, bool loading, string? error)
        => loading ? accepted ? AgentsOverviewReadPhase.Refreshing : AgentsOverviewReadPhase.Loading
            : error is not null ? accepted ? AgentsOverviewReadPhase.Stale : AgentsOverviewReadPhase.Unavailable
            : accepted ? AgentsOverviewReadPhase.Ready : AgentsOverviewReadPhase.Unavailable;
}
