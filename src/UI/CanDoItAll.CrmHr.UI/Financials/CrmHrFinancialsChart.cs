using CanDoItAll.Components.Charts;

namespace CanDoItAll.CrmHr.UI.Financials;

// Projects the accepted sold buckets into chart series. The canonical figures stay in the snapshot; this class only
// decides how they are plotted.
public static class CrmHrFinancialsChart
{
    // The x-axis categories: every period in which any currency recognized a sale, in the owner's chronological
    // order, each labelled once.
    public static IReadOnlyList<DateOnly> Periods(CrmHrFinancialsSnapshot snapshot, CrmHrFinancialPeriod period)
        => snapshot.Sold(period)
            .Select(amount => amount.PeriodStart)
            .Distinct()
            .OrderBy(start => start)
            .ToArray();

    // One bar series per currency, ordered by currency code. A category axis positions the n-th point of every
    // series under the n-th category, so each series carries one point per shared period: the recognized amount
    // where the currency sold in that period and a zero-height placeholder where it did not. The placeholder is
    // presentation alignment only; it is not a figure, it never reaches the totals and it is not summed anywhere.
    public static IReadOnlyList<CdaChartSeries> BuildSoldSeries(CrmHrFinancialsSnapshot snapshot, CrmHrFinancialPeriod period)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var periods = Periods(snapshot, period);
        var amounts = snapshot.Sold(period);
        return amounts
            .Select(amount => amount.CurrencyCode)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(currency => currency, StringComparer.Ordinal)
            .Select(currency =>
            {
                var byPeriod = amounts
                    .Where(amount => string.Equals(amount.CurrencyCode, currency, StringComparison.Ordinal))
                    .ToDictionary(amount => amount.PeriodStart, amount => amount.Amount);
                return new CdaChartSeries
                {
                    Name = currency,
                    Type = CdaChartType.Bar,
                    Points = periods
                        .Select(start => new CdaChartPoint(
                            CrmHrFinancialsText.PeriodLabel(start, period),
                            byPeriod.GetValueOrDefault(start, 0m)))
                        .ToArray()
                };
            })
            .ToArray();
    }

    // Chart options are created per call: two surfaces never share one mutable options instance.
    public static CdaChartOptions CreateSoldChartOptions()
        => new()
        {
            Type = CdaChartType.Bar,
            XAxisType = CdaChartAxisType.Category,
            YAxisTitle = "Closed-won amount",
            ShowToolbar = false,
            EnableZoom = false,
            ShowLegend = true,
            LegendPosition = CdaChartLegendPosition.Bottom,
            ValuePrecision = 2,
            TooltipPrecision = 2,
            Palette = CdaChartPalette.Calm
        };
}
