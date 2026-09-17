using CanDoItAll.CrmHr.UI.Financials;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.CrmHr.Components;

namespace CanDoItAll.Tests.Unit.CrmHr;

// The projection of the query owner's financial snapshot into the presentation record: values and order carried over.
public sealed class CrmFinancialsPresentationMapperTests
{
    [Fact]
    public void Maps_totals_buckets_incomplete_count_and_availabilities_without_recomputing_anything()
    {
        var accountId = Guid.NewGuid();
        var snapshot = new CrmAccountFinancialSnapshot(
            accountId,
            FinancialDataAvailability.Available,
            [new CrmCurrencyAmount("EUR", 80m), new CrmCurrencyAmount("USD", 150m)],
            [
                new CrmFinancialPeriodAmount(new DateOnly(2026, 5, 1), "USD", 100m),
                new CrmFinancialPeriodAmount(new DateOnly(2026, 6, 1), "EUR", 80m),
                new CrmFinancialPeriodAmount(new DateOnly(2026, 6, 1), "USD", 50m)
            ],
            [
                new CrmFinancialPeriodAmount(new DateOnly(2026, 1, 1), "EUR", 80m),
                new CrmFinancialPeriodAmount(new DateOnly(2026, 1, 1), "USD", 150m)
            ],
            2,
            FinancialDataAvailability.Unavailable,
            FinancialDataAvailability.Unavailable,
            FinancialDataAvailability.Unavailable);

        var presentation = CrmFinancialsPresentationMapper.ToSnapshot(snapshot);

        Assert.Equal(accountId, presentation.AccountPartyId);
        Assert.Equal(CrmHrFinancialAvailability.Available, presentation.SoldAvailability);
        Assert.Equal(new[] { ("EUR", 80m), ("USD", 150m) }, presentation.SoldTotals.Select(total => (total.CurrencyCode, total.Amount)));
        Assert.Equal(
            new[] { (new DateOnly(2026, 5, 1), "USD", 100m), (new DateOnly(2026, 6, 1), "EUR", 80m), (new DateOnly(2026, 6, 1), "USD", 50m) },
            presentation.MonthlySold.Select(amount => (amount.PeriodStart, amount.CurrencyCode, amount.Amount)));
        Assert.Equal(
            new[] { (new DateOnly(2026, 1, 1), "EUR", 80m), (new DateOnly(2026, 1, 1), "USD", 150m) },
            presentation.YearlySold.Select(amount => (amount.PeriodStart, amount.CurrencyCode, amount.Amount)));
        Assert.Equal(2, presentation.IncompleteWonCount);
        Assert.Equal(CrmHrFinancialAvailability.Unavailable, presentation.BoughtAvailability);
        Assert.Equal(CrmHrFinancialAvailability.Unavailable, presentation.OverdueInvoiceAvailability);
        Assert.Equal(CrmHrFinancialAvailability.Unavailable, presentation.SoldBoughtDistributionAvailability);
    }

    [Fact]
    public void An_empty_snapshot_maps_to_an_empty_sold_availability_with_no_buckets()
    {
        var accountId = Guid.NewGuid();

        var presentation = CrmFinancialsPresentationMapper.ToSnapshot(CrmAccountFinancialSnapshot.Empty(accountId));

        Assert.Equal(accountId, presentation.AccountPartyId);
        Assert.Equal(CrmHrFinancialAvailability.Empty, presentation.SoldAvailability);
        Assert.Empty(presentation.SoldTotals);
        Assert.Empty(presentation.MonthlySold);
        Assert.Empty(presentation.YearlySold);
        Assert.Equal(0, presentation.IncompleteWonCount);
    }

    [Theory]
    [InlineData(FinancialDataAvailability.Available, CrmHrFinancialAvailability.Available)]
    [InlineData(FinancialDataAvailability.Empty, CrmHrFinancialAvailability.Empty)]
    [InlineData(FinancialDataAvailability.Unavailable, CrmHrFinancialAvailability.Unavailable)]
    public void Availability_maps_one_to_one(FinancialDataAvailability source, CrmHrFinancialAvailability expected)
        => Assert.Equal(expected, CrmFinancialsPresentationMapper.ToAvailability(source));
}
