using CanDoItAll.CrmHr.UI.Financials;

namespace CanDoItAll.Modules.CrmHr.Components;

// Projects the query owner's financial snapshot into the presentation record. Amounts, currencies, buckets and
// their order are carried over unchanged; nothing is aggregated, converted or recomputed here.
public static class CrmFinancialsPresentationMapper
{
    public static CrmHrFinancialsSnapshot ToSnapshot(CrmAccountFinancialSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return new CrmHrFinancialsSnapshot(
            snapshot.AccountPartyId,
            ToAvailability(snapshot.SoldAvailability),
            snapshot.SoldTotals.Select(total => new CrmHrCurrencyTotal(total.CurrencyCode, total.Amount)).ToArray(),
            snapshot.MonthlySold.Select(ToPeriodAmount).ToArray(),
            snapshot.YearlySold.Select(ToPeriodAmount).ToArray(),
            snapshot.IncompleteWonOpportunityCount,
            ToAvailability(snapshot.BoughtAvailability),
            ToAvailability(snapshot.OverdueInvoiceAvailability),
            ToAvailability(snapshot.SoldBoughtDistributionAvailability));
    }

    public static CrmHrFinancialAvailability ToAvailability(FinancialDataAvailability availability)
        => availability switch
        {
            FinancialDataAvailability.Available => CrmHrFinancialAvailability.Available,
            FinancialDataAvailability.Empty => CrmHrFinancialAvailability.Empty,
            _ => CrmHrFinancialAvailability.Unavailable
        };

    private static CrmHrFinancialPeriodAmount ToPeriodAmount(CrmFinancialPeriodAmount amount)
        => new(amount.PeriodStart, amount.CurrencyCode, amount.Amount);
}
