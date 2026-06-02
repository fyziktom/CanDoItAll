using System.Globalization;

namespace CanDoItAll.Modules.Processes;

public sealed record ProcessUsageCostDisplay(
    string Value,
    string TooltipText,
    string Tone,
    string ChartDescription,
    bool ShowsPreciseActualCost);

public static class ProcessUsageDisplayAdapter
{
    public static ProcessUsageCostDisplay BuildCostDisplay(
        ProcessLiveStats stats,
        CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(stats);
        ArgumentNullException.ThrowIfNull(culture);

        if (HasIncompleteUsage(stats))
        {
            return new ProcessUsageCostDisplay(
                "Incomplete",
                BuildIncompleteUsageTooltip(stats),
                "warning",
                "Estimated process cost only because provider usage is incomplete.",
                ShowsPreciseActualCost: false);
        }

        return new ProcessUsageCostDisplay(
            FormatMoney(stats.ActualCost, culture),
            BuildCompleteUsageTooltip(stats, culture),
            "danger",
            "Visible process cards, sorted by current attention.",
            ShowsPreciseActualCost: true);
    }

    public static bool ShouldShowPreciseActualCost(ProcessLiveStats stats)
    {
        ArgumentNullException.ThrowIfNull(stats);

        return !HasIncompleteUsage(stats);
    }

    public static string BuildRunCostText(
        ProcessLiveStats stats,
        ProcessLiveRunCard run,
        CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(stats);
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(culture);

        return ShouldShowPreciseActualCost(stats)
            ? FormatMoney(run.ActualCost, culture)
            : "Usage incomplete";
    }

    public static string BuildRunCostTone(ProcessLiveStats stats)
    {
        ArgumentNullException.ThrowIfNull(stats);

        return ShouldShowPreciseActualCost(stats) ? "neutral" : "warning";
    }

    private static bool HasIncompleteUsage(ProcessLiveStats stats)
    {
        return stats.ProviderUsage.HasUnknownUsage ||
               stats.ProviderUsage.ObservationCount == 0 &&
               (stats.TotalTokens > 0 || stats.ToolCalls > 0 || stats.ActualCost > 0m);
    }

    private static string BuildIncompleteUsageTooltip(ProcessLiveStats stats)
    {
        var usage = stats.ProviderUsage;
        if (usage.ObservationCount == 0)
        {
            return "Provider usage observations are missing for this scope, so exact actual cost is not shown.";
        }

        return $"{usage.KnownObservationCount:N0} known and {usage.UnknownObservationCount:N0} incomplete provider usage observation(s). Exact actual cost is not shown.";
    }

    private static string BuildCompleteUsageTooltip(
        ProcessLiveStats stats,
        CultureInfo culture)
    {
        if (stats.ProviderUsage.ObservationCount == 0)
        {
            return "No provider usage was observed in this scope.";
        }

        return $"{stats.ProviderUsage.KnownObservationCount:N0} provider usage observation(s). Estimated process cost: {FormatMoney(stats.EstimatedCost, culture)}.";
    }

    private static string FormatMoney(decimal value, CultureInfo culture)
    {
        return value == 0m
            ? "$0"
            : value.ToString("C", culture);
    }
}
