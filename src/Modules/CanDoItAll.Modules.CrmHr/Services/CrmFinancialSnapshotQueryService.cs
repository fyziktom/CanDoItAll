using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.CrmHr;

public enum FinancialDataAvailability
{
    Available,
    Empty,
    Unavailable
}

public sealed record CrmCurrencyAmount(
    string CurrencyCode,
    decimal Amount);

public sealed record CrmFinancialPeriodAmount(
    DateOnly PeriodStart,
    string CurrencyCode,
    decimal Amount);

public sealed record CrmAccountFinancialSnapshot(
    Guid AccountPartyId,
    FinancialDataAvailability SoldAvailability,
    IReadOnlyList<CrmCurrencyAmount> SoldTotals,
    IReadOnlyList<CrmFinancialPeriodAmount> MonthlySold,
    IReadOnlyList<CrmFinancialPeriodAmount> YearlySold,
    int IncompleteWonOpportunityCount,
    FinancialDataAvailability BoughtAvailability,
    FinancialDataAvailability OverdueInvoiceAvailability,
    FinancialDataAvailability SoldBoughtDistributionAvailability)
{
    public static CrmAccountFinancialSnapshot Empty(Guid accountPartyId)
    {
        return new CrmAccountFinancialSnapshot(
            accountPartyId,
            FinancialDataAvailability.Empty,
            [],
            [],
            [],
            0,
            FinancialDataAvailability.Unavailable,
            FinancialDataAvailability.Unavailable,
            FinancialDataAvailability.Unavailable);
    }
}

public interface ICrmFinancialSnapshotQueryService
{
    Task<CrmAccountFinancialSnapshot> GetAsync(
        Guid accountPartyId,
        CancellationToken cancellationToken = default);
}

public sealed class CrmFinancialSnapshotQueryService(
    IDbContextFactory<AppDbContext> dbContextFactory) : ICrmFinancialSnapshotQueryService
{
    public async Task<CrmAccountFinancialSnapshot> GetAsync(
        Guid accountPartyId,
        CancellationToken cancellationToken = default)
    {
        if (accountPartyId == Guid.Empty)
        {
            throw new ArgumentException("An account is required for a financial snapshot.", nameof(accountPartyId));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var accountExists = await dbContext.Set<Party>()
            .AsNoTracking()
            .AnyAsync(
                party => party.Id == accountPartyId && party.PartyType == PartyType.Organization,
                cancellationToken);
        if (!accountExists)
        {
            throw new KeyNotFoundException($"CRM account '{accountPartyId}' was not found.");
        }

        var recognitionCandidates = await dbContext.Set<Opportunity>()
            .AsNoTracking()
            .Where(opportunity => opportunity.AccountPartyId == accountPartyId)
            .Where(opportunity =>
                opportunity.Stage == OpportunityStage.Won ||
                dbContext.Set<OpportunityStageHistory>().Any(history =>
                    history.OpportunityId == opportunity.Id &&
                    history.Stage == OpportunityStage.Won))
            .Select(opportunity => new
            {
                opportunity.Id,
                Recognition = dbContext.Set<OpportunityStageHistory>()
                    .Where(history =>
                        history.OpportunityId == opportunity.Id &&
                        history.Stage == OpportunityStage.Won)
                    .OrderBy(history => history.ChangedAtUtc)
                    .ThenBy(history => history.Id)
                    .Select(history => new
                    {
                        history.ChangedAtUtc,
                        history.RecognizedAmount,
                        history.RecognizedCurrencyCode
                    })
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);
        if (recognitionCandidates.Count == 0)
        {
            return CrmAccountFinancialSnapshot.Empty(accountPartyId);
        }

        var validSales = new List<ValidSale>(recognitionCandidates.Count);
        foreach (var candidate in recognitionCandidates)
        {
            var recognition = candidate.Recognition;
            if (recognition is null)
            {
                continue;
            }

            var currencyCode = NormalizeCurrencyCode(recognition.RecognizedCurrencyCode);
            if (recognition.RecognizedAmount is not decimal recognizedAmount ||
                currencyCode is null)
            {
                continue;
            }

            validSales.Add(new ValidSale(
                recognizedAmount,
                currencyCode,
                recognition.ChangedAtUtc.ToUniversalTime()));
        }

        var incompleteCount = recognitionCandidates.Count - validSales.Count;

        var soldTotals = validSales
            .GroupBy(sale => sale.CurrencyCode, StringComparer.Ordinal)
            .Select(group => new CrmCurrencyAmount(group.Key, group.Sum(sale => sale.Amount)))
            .OrderBy(total => total.CurrencyCode, StringComparer.Ordinal)
            .ToList();
        var monthlySold = validSales
            .GroupBy(sale => new
            {
                sale.WonAtUtc.Year,
                sale.WonAtUtc.Month,
                sale.CurrencyCode
            })
            .Select(group => new CrmFinancialPeriodAmount(
                new DateOnly(group.Key.Year, group.Key.Month, 1),
                group.Key.CurrencyCode,
                group.Sum(sale => sale.Amount)))
            .OrderBy(amount => amount.PeriodStart)
            .ThenBy(amount => amount.CurrencyCode, StringComparer.Ordinal)
            .ToList();
        var yearlySold = validSales
            .GroupBy(sale => new
            {
                sale.WonAtUtc.Year,
                sale.CurrencyCode
            })
            .Select(group => new CrmFinancialPeriodAmount(
                new DateOnly(group.Key.Year, 1, 1),
                group.Key.CurrencyCode,
                group.Sum(sale => sale.Amount)))
            .OrderBy(amount => amount.PeriodStart)
            .ThenBy(amount => amount.CurrencyCode, StringComparer.Ordinal)
            .ToList();

        return new CrmAccountFinancialSnapshot(
            accountPartyId,
            validSales.Count == 0
                ? FinancialDataAvailability.Empty
                : FinancialDataAvailability.Available,
            soldTotals,
            monthlySold,
            yearlySold,
            incompleteCount,
            FinancialDataAvailability.Unavailable,
            FinancialDataAvailability.Unavailable,
            FinancialDataAvailability.Unavailable);
    }

    private static string? NormalizeCurrencyCode(string? currencyCode)
    {
        var normalized = currencyCode?.Trim().ToUpperInvariant() ?? string.Empty;
        return normalized is [var first, var second, var third]
            && char.IsAsciiLetter(first)
            && char.IsAsciiLetter(second)
            && char.IsAsciiLetter(third)
            ? normalized
            : null;
    }

    private sealed record ValidSale(
        decimal Amount,
        string CurrencyCode,
        DateTimeOffset WonAtUtc);
}
