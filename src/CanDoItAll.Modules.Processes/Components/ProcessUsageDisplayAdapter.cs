using System.Globalization;
using CanDoItAll.Infrastructure.Configuration;

namespace CanDoItAll.Modules.Processes;

public enum ProcessUsageCostDisplayKind
{
    KnownActual,
    Estimated,
    UnknownUsage,
    MissingUsage,
    ZeroCost
}

public sealed record ProcessUsageCostDisplay(
    ProcessUsageCostDisplayKind Kind,
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

        return BuildCostDisplay(stats, CurrencyFormatting.Format);
    }

    public static ProcessUsageCostDisplay BuildCostDisplay(
        ProcessLiveStats stats,
        ICurrencyFormatter currencyFormatter)
    {
        ArgumentNullException.ThrowIfNull(currencyFormatter);

        return BuildCostDisplay(stats, currencyFormatter.Format);
    }

    private static ProcessUsageCostDisplay BuildCostDisplay(
        ProcessLiveStats stats,
        Func<decimal, string> formatMoney)
    {
        ArgumentNullException.ThrowIfNull(stats);
        ArgumentNullException.ThrowIfNull(formatMoney);

        var kind = ResolveDisplayKind(stats);
        if (kind == ProcessUsageCostDisplayKind.MissingUsage)
        {
            return new ProcessUsageCostDisplay(
                kind,
                stats.EstimatedCost > 0m ? $"Est. {formatMoney(stats.EstimatedCost)}" : "Usage missing",
                "Provider usage observations are missing for this scope, so exact actual cost is not shown.",
                "warning",
                "Estimated process cost only because provider usage is incomplete.",
                ShowsPreciseActualCost: false);
        }

        if (kind == ProcessUsageCostDisplayKind.UnknownUsage)
        {
            return new ProcessUsageCostDisplay(
                kind,
                "Usage unknown",
                BuildUnknownUsageTooltip(stats),
                "warning",
                "Known cost only; at least one provider usage observation is incomplete.",
                ShowsPreciseActualCost: false);
        }

        if (kind == ProcessUsageCostDisplayKind.Estimated)
        {
            return new ProcessUsageCostDisplay(
                kind,
                $"Est. {formatMoney(stats.EstimatedCost)}",
                BuildEstimatedUsageTooltip(stats, formatMoney),
                "warning",
                "Estimated process cost; actual provider price is not fully known.",
                ShowsPreciseActualCost: false);
        }

        if (kind == ProcessUsageCostDisplayKind.ZeroCost)
        {
            return new ProcessUsageCostDisplay(
                kind,
                "$0",
                BuildZeroCostTooltip(stats),
                "neutral",
                "No provider cost was observed in this scope.",
                ShowsPreciseActualCost: true);
        }

        return new ProcessUsageCostDisplay(
            kind,
            formatMoney(stats.ActualCost),
            BuildCompleteUsageTooltip(stats, formatMoney),
            "danger",
            "Visible process cards, sorted by current attention.",
            ShowsPreciseActualCost: true);
    }

    public static bool ShouldShowPreciseActualCost(ProcessLiveStats stats)
    {
        ArgumentNullException.ThrowIfNull(stats);

        return ResolveDisplayKind(stats) is ProcessUsageCostDisplayKind.KnownActual or ProcessUsageCostDisplayKind.ZeroCost;
    }

    public static string BuildRunCostText(
        ProcessLiveStats stats,
        ProcessLiveRunCard run,
        CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(stats);
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(culture);

        return BuildRunCostText(stats, run, CurrencyFormatting.Format);
    }

    public static string BuildRunCostText(
        ProcessLiveStats stats,
        ProcessLiveRunCard run,
        ICurrencyFormatter currencyFormatter)
    {
        ArgumentNullException.ThrowIfNull(currencyFormatter);

        return BuildRunCostText(stats, run, currencyFormatter.Format);
    }

    private static string BuildRunCostText(
        ProcessLiveStats stats,
        ProcessLiveRunCard run,
        Func<decimal, string> formatMoney)
    {
        ArgumentNullException.ThrowIfNull(stats);
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(formatMoney);

        return ResolveDisplayKind(stats) switch
        {
            ProcessUsageCostDisplayKind.KnownActual => BuildRunActualCostText(run, formatMoney),
            ProcessUsageCostDisplayKind.ZeroCost => "$0",
            ProcessUsageCostDisplayKind.Estimated => run.TreeEstimatedCost > 0m ? $"Est. {formatMoney(run.TreeEstimatedCost)}" : "Estimated",
            ProcessUsageCostDisplayKind.MissingUsage => "Usage missing",
            ProcessUsageCostDisplayKind.UnknownUsage => "Usage unknown",
            _ => "Usage unknown"
        };
    }

    public static string BuildRunCostTone(ProcessLiveStats stats)
    {
        ArgumentNullException.ThrowIfNull(stats);

        return ResolveDisplayKind(stats) switch
        {
            ProcessUsageCostDisplayKind.KnownActual => "neutral",
            ProcessUsageCostDisplayKind.ZeroCost => "neutral",
            _ => "warning"
        };
    }

    private static ProcessUsageCostDisplayKind ResolveDisplayKind(ProcessLiveStats stats)
    {
        if (stats.ProviderUsage.HasUnknownUsage)
        {
            return ProcessUsageCostDisplayKind.UnknownUsage;
        }

        if (stats.ProviderUsage.ObservationCount == 0)
        {
            return stats.TotalTokens > 0 || stats.ToolCalls > 0 || stats.ActualCost > 0m || stats.EstimatedCost > 0m
                ? ProcessUsageCostDisplayKind.MissingUsage
                : ProcessUsageCostDisplayKind.ZeroCost;
        }

        if (stats.ActualCost > 0m || stats.ProviderUsage.KnownCostUsd > 0m)
        {
            return ProcessUsageCostDisplayKind.KnownActual;
        }

        return stats.EstimatedCost > 0m
            ? ProcessUsageCostDisplayKind.Estimated
            : ProcessUsageCostDisplayKind.ZeroCost;
    }

    private static string BuildUnknownUsageTooltip(ProcessLiveStats stats)
    {
        var usage = stats.ProviderUsage;

        return $"{usage.KnownObservationCount:N0} known and {usage.UnknownObservationCount:N0} incomplete provider usage observation(s). Exact actual cost is not shown.";
    }

    private static string BuildEstimatedUsageTooltip(
        ProcessLiveStats stats,
        Func<decimal, string> formatMoney)
    {
        return $"{stats.ProviderUsage.KnownObservationCount:N0} provider usage observation(s), but no priced actual cost was available. Estimated process cost: {formatMoney(stats.EstimatedCost)}.";
    }

    private static string BuildCompleteUsageTooltip(
        ProcessLiveStats stats,
        Func<decimal, string> formatMoney)
    {
        if (stats.ProviderUsage.ObservationCount == 0)
        {
            return "No provider usage was observed in this scope.";
        }

        return $"{stats.ProviderUsage.KnownObservationCount:N0} provider usage observation(s). Estimated process cost: {formatMoney(stats.EstimatedCost)}.";
    }

    private static string BuildZeroCostTooltip(ProcessLiveStats stats)
    {
        return stats.ProviderUsage.ObservationCount == 0
            ? "No provider usage or cost was observed in this scope."
            : $"{stats.ProviderUsage.KnownObservationCount:N0} provider usage observation(s) reported zero calculated cost.";
    }

    private static string BuildRunActualCostText(
        ProcessLiveRunCard run,
        Func<decimal, string> formatMoney)
    {
        var value = formatMoney(run.TreeActualCost);
        return run.DescendantRunCount > 0
            ? $"Total {value}"
            : value;
    }
}
