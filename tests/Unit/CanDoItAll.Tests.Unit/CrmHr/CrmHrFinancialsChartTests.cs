using CanDoItAll.Components.Charts;
using CanDoItAll.CrmHr.UI.Financials;

namespace CanDoItAll.Tests.Unit.CrmHr;

// The chart projection of the accepted sold buckets: series aligned by period key, placeholders kept out of the
// figures, options never shared between instances.
public sealed class CrmHrFinancialsChartTests
{
    [Fact]
    public void Sparse_currencies_share_one_chronological_category_axis_and_placeholders_never_touch_the_totals()
    {
        var snapshot = Snapshot(
            monthly:
            [
                new(new DateOnly(2025, 1, 1), "EUR", 12000m),
                new(new DateOnly(2025, 2, 1), "USD", 4500.5m),
                new(new DateOnly(2025, 3, 1), "EUR", 8000m),
                new(new DateOnly(2025, 3, 1), "USD", 2500m),
                new(new DateOnly(2026, 1, 1), "USD", 15000m)
            ],
            yearly:
            [
                new(new DateOnly(2025, 1, 1), "EUR", 20000m),
                new(new DateOnly(2025, 1, 1), "USD", 7000.5m),
                new(new DateOnly(2026, 1, 1), "USD", 15000m)
            ]);

        var series = CrmHrFinancialsChart.BuildSoldSeries(snapshot, CrmHrFinancialPeriod.Month);

        Assert.Equal(new[] { "EUR", "USD" }, series.Select(item => item.Name));
        Assert.All(series, item => Assert.Equal(CdaChartType.Bar, item.Type));
        // Month labels are English interface words under every server culture; the expectation is written out by hand.
        var categories = new[] { "Jan 2025", "Feb 2025", "Mar 2025", "Jan 2026" };
        Assert.All(series, item => Assert.Equal(categories, item.Points.Select(point => point.Category)));
        Assert.Equal(new[] { 12000m, 0m, 8000m, 0m }, series[0].Points.Select(point => point.Value));
        Assert.Equal(new[] { 0m, 4500.5m, 2500m, 15000m }, series[1].Points.Select(point => point.Value));

        // The zero-height points are alignment only: the snapshot's totals and buckets are exactly what the owner sent.
        Assert.Equal(new[] { ("EUR", 20000m), ("USD", 22000.5m) }, snapshot.SoldTotals.Select(total => (total.CurrencyCode, total.Amount)));
        Assert.Equal(5, snapshot.MonthlySold.Count);
        Assert.DoesNotContain(snapshot.MonthlySold, amount => amount.Amount == 0m);
    }

    [Fact]
    public void Yearly_projection_uses_the_yearly_buckets_and_year_labels()
    {
        var snapshot = Snapshot(
            monthly: [new(new DateOnly(2025, 3, 1), "EUR", 1m)],
            yearly:
            [
                new(new DateOnly(2024, 1, 1), "EUR", 10m),
                new(new DateOnly(2026, 1, 1), "EUR", 30m),
                new(new DateOnly(2026, 1, 1), "GBP", 5m)
            ]);

        var series = CrmHrFinancialsChart.BuildSoldSeries(snapshot, CrmHrFinancialPeriod.Year);

        Assert.Equal(new[] { "2024", "2026" }, CrmHrFinancialsChart.Periods(snapshot, CrmHrFinancialPeriod.Year).Select(start => CrmHrFinancialsText.PeriodLabel(start, CrmHrFinancialPeriod.Year)));
        Assert.Equal(new[] { "EUR", "GBP" }, series.Select(item => item.Name));
        Assert.Equal(new[] { 10m, 30m }, series[0].Points.Select(point => point.Value));
        Assert.Equal(new[] { 0m, 5m }, series[1].Points.Select(point => point.Value));
        Assert.Equal(new[] { "2024", "2026" }, series[1].Points.Select(point => point.Category));
    }

    [Fact]
    public void An_empty_snapshot_yields_no_series_and_options_are_never_shared()
    {
        var snapshot = Snapshot(monthly: [], yearly: []);

        Assert.Empty(CrmHrFinancialsChart.BuildSoldSeries(snapshot, CrmHrFinancialPeriod.Month));
        Assert.Empty(CrmHrFinancialsChart.Periods(snapshot, CrmHrFinancialPeriod.Year));

        var first = CrmHrFinancialsChart.CreateSoldChartOptions();
        var second = CrmHrFinancialsChart.CreateSoldChartOptions();
        Assert.NotSame(first, second);
        Assert.Equal(CdaChartType.Bar, first.Type);
        Assert.Equal(CdaChartAxisType.Category, first.XAxisType);
        Assert.False(first.ShowToolbar);
        Assert.False(first.EnableZoom);
        Assert.True(first.ShowLegend);
        Assert.Equal(CdaChartLegendPosition.Bottom, first.LegendPosition);
        Assert.Equal(2, first.ValuePrecision);
        Assert.Equal(2, first.TooltipPrecision);
    }

    private static CrmHrFinancialsSnapshot Snapshot(IReadOnlyList<CrmHrFinancialPeriodAmount> monthly, IReadOnlyList<CrmHrFinancialPeriodAmount> yearly)
        => new(
            Guid.NewGuid(),
            monthly.Count == 0 ? CrmHrFinancialAvailability.Empty : CrmHrFinancialAvailability.Available,
            yearly
                .GroupBy(amount => amount.CurrencyCode, StringComparer.Ordinal)
                .Select(group => new CrmHrCurrencyTotal(group.Key, group.Sum(amount => amount.Amount)))
                .OrderBy(total => total.CurrencyCode, StringComparer.Ordinal)
                .ToArray(),
            monthly,
            yearly,
            IncompleteWonCount: 0,
            CrmHrFinancialAvailability.Unavailable,
            CrmHrFinancialAvailability.Unavailable,
            CrmHrFinancialAvailability.Unavailable);
}
