using CanDoItAll.CrmHr.UI.Financials;

namespace CanDoItAll.CrmHr.UiSandbox;

public enum CrmHrFinancialsSandboxScenario
{
    Populated,
    SingleCurrency,
    Empty,
    Incomplete,
    Loading,
    Failed,
    LongLabels
}

public sealed record CrmHrFinancialsSandboxContext(
    CrmHrFinancialsSandboxScenario Scenario = CrmHrFinancialsSandboxScenario.Populated,
    CrmHrHomeSandboxLayout Layout = CrmHrHomeSandboxLayout.Matched)
{
    public static CrmHrFinancialsSandboxContext Parse(string? scenario, string? layout)
        => new(ParseScenario(scenario), CrmHrHomeSandboxContext.ParseLayout(layout));

    public static CrmHrFinancialsSandboxScenario ParseScenario(string? token)
        => Enum.GetValues<CrmHrFinancialsSandboxScenario>()
            .FirstOrDefault(scenario => string.Equals(Token(scenario), token?.Trim(), StringComparison.OrdinalIgnoreCase));

    public static string Token(CrmHrFinancialsSandboxScenario scenario)
        => string.Concat(scenario.ToString().Select((character, index) =>
            char.IsUpper(character) && index > 0 ? $"-{char.ToLowerInvariant(character)}" : char.ToLowerInvariant(character).ToString()));

    public IReadOnlyDictionary<string, object?> ToQuery() => new Dictionary<string, object?>
    {
        ["scenario"] = Token(Scenario),
        ["layout"] = CrmHrHomeSandboxContext.Token(Layout)
    };
}

// Deterministic local state for the real Financials surface; no query service, timer, persistence or CRM write is
// involved. Every figure is synthetic and every retry or period action updates local state only.
public sealed class CrmHrFinancialsSandboxFixture
{
    private static readonly Guid AccountId = Guid.Parse("53000000-0000-0000-0000-000000000001");

    private CrmHrFinancialsSandboxScenario scenario = CrmHrFinancialsSandboxScenario.Populated;
    private CrmHrFinancialPeriod period = CrmHrFinancialPeriod.Month;
    private long generation;
    private bool retried;

    public CrmHrFinancialsPresentation Presentation { get; private set; } = CrmHrFinancialsPresentation.CreateReady(0, Populated(), CrmHrFinancialPeriod.Month);

    public string IntentLog { get; private set; } = "No intent yet.";

    public void Apply(CrmHrFinancialsSandboxScenario next)
    {
        scenario = next;
        retried = false;
        IntentLog = "No intent yet.";
        Presentation = Build(++generation);
    }

    // The surface's intents update local state only: a retry of the failed scenario resolves to the populated
    // snapshot, a period change re-projects the same snapshot.
    public void Handle(CrmHrFinancialsIntent intent)
    {
        switch (intent)
        {
            case CrmHrFinancialsIntent.SetPeriod setPeriod:
                period = setPeriod.Period;
                Presentation = Presentation with { Period = period };
                IntentLog = $"Period: {period}";
                break;
            case CrmHrFinancialsIntent.Retry retry when retry.Generation == Presentation.Generation && scenario == CrmHrFinancialsSandboxScenario.Failed:
                IntentLog = $"Retry: generation {retry.Generation}";
                retried = true;
                Presentation = CrmHrFinancialsPresentation.CreateReady(++generation, Populated(), period);
                break;
            case CrmHrFinancialsIntent.Retry retry:
                IntentLog = $"Retry ignored: generation {retry.Generation}";
                break;
        }
    }

    private CrmHrFinancialsPresentation Build(long current)
        => scenario switch
        {
            CrmHrFinancialsSandboxScenario.Loading => CrmHrFinancialsPresentation.CreateLoading(current, period),
            CrmHrFinancialsSandboxScenario.Failed when !retried => CrmHrFinancialsPresentation.CreateFailed(
                current,
                "The financial projection is unavailable. Retry after checking the CRM data source.",
                period),
            CrmHrFinancialsSandboxScenario.SingleCurrency => CrmHrFinancialsPresentation.CreateReady(current, SingleCurrency(), period),
            CrmHrFinancialsSandboxScenario.Empty => CrmHrFinancialsPresentation.CreateReady(current, Empty(), period),
            CrmHrFinancialsSandboxScenario.Incomplete => CrmHrFinancialsPresentation.CreateReady(current, Populated() with { IncompleteWonCount = 3 }, period),
            CrmHrFinancialsSandboxScenario.LongLabels => CrmHrFinancialsPresentation.CreateReady(current, LongLabels(), period),
            _ => CrmHrFinancialsPresentation.CreateReady(current, Populated(), period)
        };

    // Sparse currencies over two years: no currency sells in every month, so the chart must align by period key.
    public static CrmHrFinancialsSnapshot Populated()
        => Snapshot(
            [
                Amount(2025, 1, "EUR", 12000m),
                Amount(2025, 2, "USD", 4500.5m),
                Amount(2025, 3, "EUR", 8000m),
                Amount(2025, 3, "USD", 2500m),
                Amount(2025, 11, "GBP", 900m),
                Amount(2026, 1, "USD", 15000m),
                Amount(2026, 2, "EUR", 3200.25m)
            ],
            incomplete: 0);

    private static CrmHrFinancialsSnapshot SingleCurrency()
        => Snapshot(
            [
                Amount(2026, 1, "EUR", 1000m),
                Amount(2026, 2, "EUR", 2000m),
                Amount(2026, 3, "EUR", 3000m)
            ],
            incomplete: 0);

    private static CrmHrFinancialsSnapshot Empty()
        => new(
            AccountId,
            CrmHrFinancialAvailability.Empty,
            [],
            [],
            [],
            IncompleteWonCount: 0,
            CrmHrFinancialAvailability.Unavailable,
            CrmHrFinancialAvailability.Unavailable,
            CrmHrFinancialAvailability.Unavailable);

    // Eighteen consecutive months in two currencies with large amounts: long category axes and wide legends.
    private static CrmHrFinancialsSnapshot LongLabels()
        => Snapshot(
            Enumerable.Range(0, 18)
                .SelectMany(index =>
                {
                    var start = new DateOnly(2024, 7, 1).AddMonths(index);
                    return new[]
                    {
                        Amount(start.Year, start.Month, "EUR", 1_250_000m + index * 37_500m),
                        Amount(start.Year, start.Month, "USD", 980_000m + index * 12_250.75m)
                    };
                })
                .ToArray(),
            incomplete: 12);

    private static CrmHrFinancialsSnapshot Snapshot(IReadOnlyList<CrmHrFinancialPeriodAmount> monthly, int incomplete)
        => new(
            AccountId,
            monthly.Count == 0 ? CrmHrFinancialAvailability.Empty : CrmHrFinancialAvailability.Available,
            monthly
                .GroupBy(amount => amount.CurrencyCode, StringComparer.Ordinal)
                .Select(group => new CrmHrCurrencyTotal(group.Key, group.Sum(amount => amount.Amount)))
                .OrderBy(total => total.CurrencyCode, StringComparer.Ordinal)
                .ToArray(),
            monthly,
            monthly
                .GroupBy(amount => (amount.PeriodStart.Year, amount.CurrencyCode))
                .Select(group => new CrmHrFinancialPeriodAmount(new DateOnly(group.Key.Year, 1, 1), group.Key.CurrencyCode, group.Sum(amount => amount.Amount)))
                .OrderBy(amount => amount.PeriodStart)
                .ThenBy(amount => amount.CurrencyCode, StringComparer.Ordinal)
                .ToArray(),
            incomplete,
            CrmHrFinancialAvailability.Unavailable,
            CrmHrFinancialAvailability.Unavailable,
            CrmHrFinancialAvailability.Unavailable);

    private static CrmHrFinancialPeriodAmount Amount(int year, int month, string currency, decimal amount)
        => new(new DateOnly(year, month, 1), currency, amount);
}
