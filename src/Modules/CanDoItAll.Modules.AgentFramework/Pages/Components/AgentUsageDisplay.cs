using System.Globalization;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components;

internal static class AgentUsageDisplay
{
    public static string FormatCount(int value)
    {
        return value.ToString("N0", CultureInfo.CurrentCulture);
    }

    public static string FormatTokens(int value)
    {
        return value.ToString("N0", CultureInfo.CurrentCulture);
    }

    public static string FormatCost(decimal value)
    {
        if (value <= 0m)
        {
            return "$0";
        }

        return value < 1m
            ? FormattableString.Invariant($"${value:0.0000}")
            : FormattableString.Invariant($"${value:0.00}");
    }

    public static string FormatLastUsed(DateTimeOffset? value)
    {
        return value.HasValue
            ? value.Value.LocalDateTime.ToString("g", CultureInfo.CurrentCulture)
            : "Never";
    }

    public static string FormatUsageShare(decimal value, decimal total)
    {
        if (total <= 0m)
        {
            return "0%";
        }

        return $"{Math.Round(value / total * 100m, 1):0.#}%";
    }

    public static string TrimLabel(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Unknown";
        }

        var trimmed = value.Trim();
        if (trimmed.Length <= maxLength)
        {
            return trimmed;
        }

        return string.Concat(trimmed.AsSpan(0, Math.Max(1, maxLength - 3)), "...");
    }

    public static decimal ResolveProviderUsageValue(ProviderOverviewUsageRow row)
    {
        return row.UsageObservationCount;
    }

    public static decimal ResolveModelUsageValue(ModelOverviewUsageRow row)
    {
        return row.UsageObservationCount;
    }
}
