using System.ComponentModel;

namespace CanDoItAll.AgentFramework.Usage;

[Description("Rolling usage period as a JSON integer; wire query values are 7d, 14d, 1m, 1q and 1y.")]
public enum ProviderUsagePeriod { SevenDays, FourteenDays, Month, Quarter, Year }

[Description("Resolved half-open UTC usage interval: FromUtc is inclusive and ToUtc is exclusive.")]
public sealed record ProviderUsageWindow {
    [Description("Inclusive UTC occurrence lower boundary.")]
    public DateTimeOffset FromUtc { get; }
    [Description("Exclusive UTC occurrence upper boundary.")]
    public DateTimeOffset ToUtc { get; }

    public ProviderUsageWindow(DateTimeOffset fromUtc, DateTimeOffset toUtc) {
        if (fromUtc >= toUtc) {
            throw new ArgumentOutOfRangeException(nameof(fromUtc), "The usage interval must be nonempty.");
        }
        FromUtc = fromUtc.ToUniversalTime();
        ToUtc = toUtc.ToUniversalTime();
    }

    public bool Contains(DateTimeOffset occurrence) => occurrence >= FromUtc && occurrence < ToUtc;
}

[Description("Accepted workload and rolling period resolved once against a single UTC upper boundary.")]
public sealed record ProviderUsageQuery {
    [Description("Selected workload flags as a JSON integer.")]
    public ProviderUsageWorkloadSelection Selection { get; }
    [Description("Rolling period as a JSON integer; Month, Quarter and Year use calendar arithmetic.")]
    public ProviderUsagePeriod Period { get; }
    [Description("Exact shared occurrence interval for every selected source and dialog.")]
    public ProviderUsageWindow Window { get; }

    [System.Text.Json.Serialization.JsonConstructor]
    public ProviderUsageQuery(ProviderUsageWorkloadSelection selection, ProviderUsagePeriod period, ProviderUsageWindow window)
        : this(selection, period, (window ?? throw new ArgumentNullException(nameof(window))).ToUtc) {
        if (Window != window) {
            throw new ArgumentException("The interval does not match the selected rolling period.", nameof(window));
        }
    }

    public ProviderUsageQuery(ProviderUsageWorkloadSelection selection, ProviderUsagePeriod period, DateTimeOffset toUtc) {
        if (selection is not (ProviderUsageWorkloadSelection.Agents or ProviderUsageWorkloadSelection.SimpleChats or ProviderUsageWorkloadSelection.Both)) {
            throw new ArgumentOutOfRangeException(nameof(selection));
        }
        toUtc = toUtc.ToUniversalTime();
        var fromUtc = period switch {
            ProviderUsagePeriod.SevenDays => toUtc.AddDays(-7),
            ProviderUsagePeriod.FourteenDays => toUtc.AddDays(-14),
            ProviderUsagePeriod.Month => toUtc.AddMonths(-1),
            ProviderUsagePeriod.Quarter => toUtc.AddMonths(-3),
            ProviderUsagePeriod.Year => toUtc.AddYears(-1),
            _ => throw new ArgumentOutOfRangeException(nameof(period))
        };
        Selection = selection;
        Period = period;
        Window = new(fromUtc, toUtc);
    }
}

public static class ProviderUsagePeriods {
    public const string QueryKey = "usagePeriod";
    public const string ScopeQueryKey = "usageScope";

    public static bool TryParse(string? value, out ProviderUsagePeriod period) {
        period = value switch {
            null or "7d" => ProviderUsagePeriod.SevenDays,
            "14d" => ProviderUsagePeriod.FourteenDays,
            "1m" => ProviderUsagePeriod.Month,
            "1q" => ProviderUsagePeriod.Quarter,
            "1y" => ProviderUsagePeriod.Year,
            _ => (ProviderUsagePeriod)(-1)
        };
        return Enum.IsDefined(period);
    }

    public static bool TryParseSelection(string? value, out ProviderUsageWorkloadSelection selection) {
        selection = value switch {
            null or "both" => ProviderUsageWorkloadSelection.Both,
            "agents" => ProviderUsageWorkloadSelection.Agents,
            "simple-chats" => ProviderUsageWorkloadSelection.SimpleChats,
            _ => ProviderUsageWorkloadSelection.None
        };
        return selection != ProviderUsageWorkloadSelection.None;
    }

    public static string ToWireValue(this ProviderUsagePeriod period) => period switch {
        ProviderUsagePeriod.SevenDays => "7d",
        ProviderUsagePeriod.FourteenDays => "14d",
        ProviderUsagePeriod.Month => "1m",
        ProviderUsagePeriod.Quarter => "1q",
        ProviderUsagePeriod.Year => "1y",
        _ => throw new ArgumentOutOfRangeException(nameof(period))
    };

    public static string Label(this ProviderUsagePeriod period) => period switch {
        ProviderUsagePeriod.SevenDays => "Last 7d",
        ProviderUsagePeriod.FourteenDays => "Last 14d",
        ProviderUsagePeriod.Month => "Last 1m",
        ProviderUsagePeriod.Quarter => "Last 3m",
        ProviderUsagePeriod.Year => "Last 1y",
        _ => throw new ArgumentOutOfRangeException(nameof(period))
    };
}

public interface IBoundedProviderUsageProjectionSource : IProviderUsageProjectionSource {
    ValueTask<ProviderUsageSourceResult> ReadWindowAsync(ProviderUsageWindow window, CancellationToken cancellationToken = default);
}

public interface IProviderUsageReadContext {
    void EnsureCurrent();
}
