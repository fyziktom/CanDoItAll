using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.CrmHr;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class CrmFinancialSnapshotQueryIntegrationTests
{
    [Fact]
    public async Task Snapshot_uses_first_won_transition_groups_currencies_and_marks_incomplete_records()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var service = scope.ServiceProvider.GetRequiredService<ICrmFinancialSnapshotQueryService>();
        var accountId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();

        await using (var dbContext = await dbContextFactory.CreateDbContextAsync())
        {
            dbContext.Set<Party>().AddRange(
                CreateParty(accountId, PartyType.Organization, "Northwind"),
                CreateParty(ownerId, PartyType.Person, "Olivia Owner"));

            var mayUsd = CreateOpportunity(accountId, ownerId, "May USD", OpportunityStage.Won, "usd", 100m);
            var juneUsd = CreateOpportunity(accountId, ownerId, "June USD", OpportunityStage.Won, "USD", 50m);
            var juneEur = CreateOpportunity(accountId, ownerId, "June EUR", OpportunityStage.Won, "eur", 80m);
            var missingAmount = CreateOpportunity(accountId, ownerId, "Missing amount", OpportunityStage.Won, "USD", null);
            var missingHistory = CreateOpportunity(accountId, ownerId, "Missing history", OpportunityStage.Won, "USD", 20m);
            var invalidCurrency = CreateOpportunity(accountId, ownerId, "Invalid currency", OpportunityStage.Won, "US", 25m);
            var proposal = CreateOpportunity(accountId, ownerId, "Proposal", OpportunityStage.Proposal, "USD", 999m);
            var laterLost = CreateOpportunity(accountId, ownerId, "Won then lost", OpportunityStage.Lost, "USD", 5m);
            mayUsd.Amount = 900m;
            mayUsd.CurrencyCode = "GBP";
            dbContext.Set<Opportunity>().AddRange(
                mayUsd,
                juneUsd,
                juneEur,
                missingAmount,
                missingHistory,
                invalidCurrency,
                proposal,
                laterLost);
            dbContext.Set<OpportunityStageHistory>().AddRange(
                CreateHistory(mayUsd.Id, OpportunityStage.Won, new DateTimeOffset(2026, 5, 10, 8, 0, 0, TimeSpan.Zero), 100m, "USD"),
                CreateHistory(mayUsd.Id, OpportunityStage.Won, new DateTimeOffset(2026, 5, 12, 8, 0, 0, TimeSpan.Zero), 700m, "EUR"),
                CreateHistory(juneUsd.Id, OpportunityStage.Won, new DateTimeOffset(2026, 6, 1, 9, 0, 0, TimeSpan.Zero), 50m, "USD"),
                CreateHistory(juneEur.Id, OpportunityStage.Won, new DateTimeOffset(2026, 6, 15, 10, 0, 0, TimeSpan.Zero), 80m, "EUR"),
                CreateHistory(missingAmount.Id, OpportunityStage.Won, new DateTimeOffset(2026, 6, 20, 10, 0, 0, TimeSpan.Zero), null, "USD"),
                CreateHistory(invalidCurrency.Id, OpportunityStage.Won, new DateTimeOffset(2026, 6, 21, 10, 0, 0, TimeSpan.Zero), 25m, "US"),
                CreateHistory(proposal.Id, OpportunityStage.Proposal, new DateTimeOffset(2026, 4, 1, 10, 0, 0, TimeSpan.Zero)),
                CreateHistory(laterLost.Id, OpportunityStage.Won, new DateTimeOffset(2026, 7, 1, 10, 0, 0, TimeSpan.Zero), 30m, "USD"),
                CreateHistory(laterLost.Id, OpportunityStage.Lost, new DateTimeOffset(2026, 7, 2, 10, 0, 0, TimeSpan.Zero)));
            await dbContext.SaveChangesAsync();
        }

        var snapshot = await service.GetAsync(accountId);

        Assert.Equal(FinancialDataAvailability.Available, snapshot.SoldAvailability);
        Assert.Equal(
            [
                new CrmCurrencyAmount("EUR", 80m),
                new CrmCurrencyAmount("USD", 180m)
            ],
            snapshot.SoldTotals);
        Assert.Equal(
            [
                new CrmFinancialPeriodAmount(new DateOnly(2026, 5, 1), "USD", 100m),
                new CrmFinancialPeriodAmount(new DateOnly(2026, 6, 1), "EUR", 80m),
                new CrmFinancialPeriodAmount(new DateOnly(2026, 6, 1), "USD", 50m),
                new CrmFinancialPeriodAmount(new DateOnly(2026, 7, 1), "USD", 30m)
            ],
            snapshot.MonthlySold);
        Assert.Equal(
            [
                new CrmFinancialPeriodAmount(new DateOnly(2026, 1, 1), "EUR", 80m),
                new CrmFinancialPeriodAmount(new DateOnly(2026, 1, 1), "USD", 180m)
            ],
            snapshot.YearlySold);
        Assert.Equal(3, snapshot.IncompleteWonOpportunityCount);
        Assert.Equal(FinancialDataAvailability.Unavailable, snapshot.BoughtAvailability);
        Assert.Equal(FinancialDataAvailability.Unavailable, snapshot.OverdueInvoiceAvailability);
        Assert.Equal(FinancialDataAvailability.Unavailable, snapshot.SoldBoughtDistributionAvailability);
    }

    [Fact]
    public async Task Snapshot_distinguishes_empty_accounts_and_rejects_unknown_accounts()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var service = scope.ServiceProvider.GetRequiredService<ICrmFinancialSnapshotQueryService>();
        var accountId = Guid.NewGuid();

        await using (var dbContext = await dbContextFactory.CreateDbContextAsync())
        {
            dbContext.Set<Party>().Add(CreateParty(accountId, PartyType.Organization, "Empty account"));
            await dbContext.SaveChangesAsync();
        }

        var snapshot = await service.GetAsync(accountId);

        Assert.Equal(FinancialDataAvailability.Empty, snapshot.SoldAvailability);
        Assert.Empty(snapshot.SoldTotals);
        Assert.Equal(
            FinancialDataAvailability.Unavailable,
            snapshot.SoldBoughtDistributionAvailability);
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => service.GetAsync(Guid.NewGuid()));
    }

    private static Party CreateParty(Guid id, PartyType partyType, string displayName)
    {
        return new Party
        {
            Id = id,
            PartyType = partyType,
            LifecycleStatus = PartyLifecycleStatus.Active,
            DisplayName = displayName,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };
    }

    private static Opportunity CreateOpportunity(
        Guid accountId,
        Guid ownerId,
        string title,
        OpportunityStage stage,
        string currencyCode,
        decimal? amount)
    {
        return new Opportunity
        {
            Id = Guid.NewGuid(),
            AccountPartyId = accountId,
            OwnerPartyId = ownerId,
            Title = title,
            Stage = stage,
            CurrencyCode = currencyCode,
            Amount = amount,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };
    }

    private static OpportunityStageHistory CreateHistory(
        Guid opportunityId,
        OpportunityStage stage,
        DateTimeOffset changedAtUtc,
        decimal? recognizedAmount = null,
        string recognizedCurrencyCode = "")
    {
        return new OpportunityStageHistory
        {
            Id = Guid.NewGuid(),
            OpportunityId = opportunityId,
            Stage = stage,
            ChangedAtUtc = changedAtUtc,
            ChangedBy = "integration-tests",
            RecognizedAmount = recognizedAmount,
            RecognizedCurrencyCode = recognizedCurrencyCode
        };
    }
}
