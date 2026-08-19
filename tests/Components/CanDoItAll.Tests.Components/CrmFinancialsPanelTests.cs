using Bunit;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.CrmHr.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.CrmHr;

public sealed class CrmFinancialsPanelTests
{
    [Fact]
    public void Renders_currency_safe_metrics_period_controls_and_unavailable_sources()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        var accountId = Guid.NewGuid();
        context.Services.AddSingleton<ICrmFinancialSnapshotQueryService>(
            new StubFinancialSnapshotQueryService(
                new CrmAccountFinancialSnapshot(
                    accountId,
                    FinancialDataAvailability.Available,
                    [
                        new CrmCurrencyAmount("EUR", 80m),
                        new CrmCurrencyAmount("USD", 150m)
                    ],
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
                    FinancialDataAvailability.Unavailable)));

        var cut = context.Render<CrmFinancialsPanel>(
            parameters => parameters.Add(component => component.AccountPartyId, accountId));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains($"{80m:N2} EUR", cut.Markup, StringComparison.Ordinal);
            Assert.Contains($"{150m:N2} USD", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("2 incomplete", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("Distribution unavailable", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("Overdue status unavailable", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("Sold value by month", cut.Markup, StringComparison.Ordinal);
        });

        cut.Find("[data-testid='crmhr-financials-year']").Click();

        Assert.Contains("Sold value by year", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("100% sold", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Failed_load_shows_retryable_generic_error_without_exception_details()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        context.Services.AddSingleton<ICrmFinancialSnapshotQueryService>(
            new FailingFinancialSnapshotQueryService());

        var cut = context.Render<CrmFinancialsPanel>(
            parameters => parameters.Add(component => component.AccountPartyId, Guid.NewGuid()));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Commercial results could not be loaded", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("financial projection is unavailable", cut.Markup, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("secret-provider-detail", cut.Markup, StringComparison.Ordinal);
            Assert.NotEmpty(cut.FindAll("[data-testid='crmhr-financials-retry']"));
        });
    }

    private sealed class StubFinancialSnapshotQueryService(
        CrmAccountFinancialSnapshot snapshot) : ICrmFinancialSnapshotQueryService
    {
        public Task<CrmAccountFinancialSnapshot> GetAsync(
            Guid accountPartyId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(snapshot);
        }
    }

    private sealed class FailingFinancialSnapshotQueryService : ICrmFinancialSnapshotQueryService
    {
        public Task<CrmAccountFinancialSnapshot> GetAsync(
            Guid accountPartyId,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("secret-provider-detail");
        }
    }
}
