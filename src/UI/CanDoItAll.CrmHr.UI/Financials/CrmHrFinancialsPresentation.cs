namespace CanDoItAll.CrmHr.UI.Financials;

public enum CrmHrFinancialsPhase
{
    Loading,
    Ready,
    Failed
}

// Availability of one financial figure as the query owner reports it: a value exists, the account has none, or the
// source does not exist yet. Unavailable is never rendered as zero.
public enum CrmHrFinancialAvailability
{
    Available,
    Empty,
    Unavailable
}

public enum CrmHrFinancialPeriod
{
    Month,
    Year
}

public sealed record CrmHrCurrencyTotal(string CurrencyCode, decimal Amount);

// One recognized bucket: the UTC period start the query owner grouped by, its currency and the recognized amount.
public sealed record CrmHrFinancialPeriodAmount(DateOnly PeriodStart, string CurrencyCode, decimal Amount);

// The account's financial snapshot as the query owner accepted it: totals per currency, the monthly and yearly
// buckets in the owner's order, the count of incomplete won records and the availability of the figures that do not
// exist yet. Nothing here is aggregated, converted or recomputed by the renderer.
public sealed record CrmHrFinancialsSnapshot(
    Guid AccountPartyId,
    CrmHrFinancialAvailability SoldAvailability,
    IReadOnlyList<CrmHrCurrencyTotal> SoldTotals,
    IReadOnlyList<CrmHrFinancialPeriodAmount> MonthlySold,
    IReadOnlyList<CrmHrFinancialPeriodAmount> YearlySold,
    int IncompleteWonCount,
    CrmHrFinancialAvailability BoughtAvailability,
    CrmHrFinancialAvailability OverdueInvoiceAvailability,
    CrmHrFinancialAvailability SoldBoughtDistributionAvailability)
{
    public IReadOnlyList<CrmHrCurrencyTotal> SoldTotals { get; init; } = SoldTotals?.ToArray() ?? throw new ArgumentNullException(nameof(SoldTotals));

    public IReadOnlyList<CrmHrFinancialPeriodAmount> MonthlySold { get; init; } = MonthlySold?.ToArray() ?? throw new ArgumentNullException(nameof(MonthlySold));

    public IReadOnlyList<CrmHrFinancialPeriodAmount> YearlySold { get; init; } = YearlySold?.ToArray() ?? throw new ArgumentNullException(nameof(YearlySold));

    public IReadOnlyList<CrmHrFinancialPeriodAmount> Sold(CrmHrFinancialPeriod period)
        => period == CrmHrFinancialPeriod.Month ? MonthlySold : YearlySold;
}

// What the Financials surface renders: the read phase for the current account, the accepted snapshot, the safe
// failure copy and the period the operator chose. The generation identifies the read a retry may target.
public sealed record CrmHrFinancialsPresentation(
    long Generation,
    CrmHrFinancialsPhase Phase,
    CrmHrFinancialsSnapshot? Snapshot,
    string? FailureMessage,
    CrmHrFinancialPeriod Period,
    bool IsRetrying)
{
    public static CrmHrFinancialsPresentation CreateLoading(long generation, CrmHrFinancialPeriod period)
        => new(generation, CrmHrFinancialsPhase.Loading, null, null, period, IsRetrying: false);

    public static CrmHrFinancialsPresentation CreateReady(long generation, CrmHrFinancialsSnapshot snapshot, CrmHrFinancialPeriod period)
        => new(generation, CrmHrFinancialsPhase.Ready, snapshot ?? throw new ArgumentNullException(nameof(snapshot)), null, period, IsRetrying: false);

    public static CrmHrFinancialsPresentation CreateFailed(long generation, string failureMessage, CrmHrFinancialPeriod period, bool isRetrying = false)
        => new(generation, CrmHrFinancialsPhase.Failed, null, failureMessage ?? throw new ArgumentNullException(nameof(failureMessage)), period, isRetrying);
}

// Everything the Financials surface can ask its host to do: read the failed account again, or project the accepted
// snapshot by another period. A period change is a local projection; it issues no read.
public abstract record CrmHrFinancialsIntent
{
    private CrmHrFinancialsIntent()
    {
    }

    public sealed record Retry(long Generation) : CrmHrFinancialsIntent;

    public sealed record SetPeriod(CrmHrFinancialPeriod Period) : CrmHrFinancialsIntent;
}

public static class CrmHrFinancialsText
{
    public const string UnavailableValue = "Unavailable";
    public const string NoRecognizedSalesValue = "No recognized sales";
    public const string LoadingValue = "…";

    public static string FormatAmount(CrmHrCurrencyTotal total) => $"{total.Amount:N2} {total.CurrencyCode}";

    public static string FormatCount(int count) => count.ToString("N0");

    public static string ChartTitle(CrmHrFinancialPeriod period)
        => period == CrmHrFinancialPeriod.Month ? "Sold value by month" : "Sold value by year";

    // The human-readable label of a bucket, as the module rendered it before the extraction.
    public static string PeriodLabel(DateOnly periodStart, CrmHrFinancialPeriod period)
        => period == CrmHrFinancialPeriod.Month
            ? periodStart.ToString("MMM yyyy")
            : periodStart.Year.ToString();

    public static string PeriodToken(CrmHrFinancialPeriod period)
        => period == CrmHrFinancialPeriod.Month ? "month" : "year";
}
