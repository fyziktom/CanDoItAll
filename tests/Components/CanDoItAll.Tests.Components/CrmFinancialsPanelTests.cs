using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.Charts;
using CanDoItAll.CrmHr.UI.Financials;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.CrmHr.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.CrmHr;

// The production Financials host composed with the real surface and chart; only the query owner is scripted.
public sealed class CrmFinancialsPanelTests
{
    private static readonly Guid AccountA = Guid.Parse("87000000-0000-0000-0000-00000000000a");
    private static readonly Guid AccountB = Guid.Parse("87000000-0000-0000-0000-00000000000b");

    [Fact]
    public void Renders_currency_safe_metrics_period_controls_and_unavailable_sources()
    {
        using var context = CreateContext(new StubFinancialSnapshotQueryService(Snapshot(AccountA)));

        var cut = context.Render<CrmFinancialsPanel>(
            parameters => parameters.Add(component => component.AccountPartyId, AccountA));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains($"{80m:N2} EUR", cut.Markup, StringComparison.Ordinal);
            Assert.Contains($"{150m:N2} USD", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("2 incomplete", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("Distribution unavailable", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("Overdue status unavailable", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("Sold value by month", cut.Markup, StringComparison.Ordinal);
        });
        Assert.Single(cut.FindComponents<CrmHrFinancialsSurface>());
        Assert.Single(cut.FindComponents<CdaChart>());

        cut.Find("[data-testid='crmhr-financials-year']").Click();

        Assert.Contains("Sold value by year", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("100% sold", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Failed_load_shows_retryable_generic_error_without_exception_details()
    {
        using var context = CreateContext(new FailingFinancialSnapshotQueryService());

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

    [Fact]
    public async Task Same_account_rerender_issues_no_read_and_an_account_change_retires_the_pending_read()
    {
        var query = new ScriptedFinancialQuery();
        using var context = CreateContext(query);

        var cut = context.Render<CrmFinancialsPanel>(parameters => parameters.Add(component => component.AccountPartyId, AccountA));
        Assert.Equal("loading", cut.Find("[data-testid='crmhr-financials-panel']").GetAttribute("data-phase"));
        Assert.Single(query.Calls);

        cut.Render(parameters => parameters.Add(component => component.AccountPartyId, AccountA));
        Assert.Single(query.Calls);

        cut.Render(parameters => parameters.Add(component => component.AccountPartyId, AccountB));
        Assert.Equal(2, query.Calls.Count);
        Assert.True(query.Calls[0].Token.IsCancellationRequested);

        // A's late snapshot never becomes B's figures.
        await cut.InvokeAsync(() => query.Complete(0, Snapshot(AccountA)));
        Assert.Equal("loading", cut.Find("[data-testid='crmhr-financials-panel']").GetAttribute("data-phase"));

        await cut.InvokeAsync(() => query.Complete(1, CrmAccountFinancialSnapshot.Empty(AccountB)));
        cut.WaitForAssertion(() => Assert.Equal("ready", cut.Find("[data-testid='crmhr-financials-panel']").GetAttribute("data-phase")));
        Assert.Equal(AccountB.ToString("D"), cut.Find("[data-testid='crmhr-financials-panel']").GetAttribute("data-account-id"));
        Assert.NotNull(cut.Find("[data-testid='crmhr-financials-sold-empty']"));
        Assert.Equal(CrmHrFinancialsPhase.Ready, cut.Instance.Presentation.Phase);
    }

    [Fact]
    public async Task Retry_reads_the_failed_account_once_and_the_period_choice_survives_an_account_change()
    {
        var query = new ScriptedFinancialQuery();
        using var context = CreateContext(query);

        var cut = context.Render<CrmFinancialsPanel>(parameters => parameters.Add(component => component.AccountPartyId, AccountA));
        await cut.InvokeAsync(() => query.Fail(0, new IOException("offline")));
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='crmhr-financials-retry']")));

        cut.Find("[data-testid='crmhr-financials-retry']").Click();
        cut.WaitForAssertion(() => Assert.True(cut.Find("[data-testid='crmhr-financials-retry']").HasAttribute("disabled")));
        Assert.Equal(2, query.Calls.Count);
        await cut.InvokeAsync(() => query.Complete(1, Snapshot(AccountA)));
        cut.WaitForAssertion(() => Assert.Equal("ready", cut.Find("[data-testid='crmhr-financials-panel']").GetAttribute("data-phase")));

        // The retry click's own completion is still queued on the dispatcher, so the period render is awaited.
        cut.Find("[data-testid='crmhr-financials-year']").Click();
        cut.WaitForAssertion(() => Assert.Equal("year", cut.Find("[data-testid='crmhr-financials-panel']").GetAttribute("data-period")));
        Assert.Equal(2, query.Calls.Count);

        cut.Render(parameters => parameters.Add(component => component.AccountPartyId, AccountB));
        await cut.InvokeAsync(() => query.Complete(2, Snapshot(AccountB)));
        cut.WaitForAssertion(() => Assert.Equal(AccountB.ToString("D"), cut.Find("[data-testid='crmhr-financials-panel']").GetAttribute("data-account-id")));
        Assert.Equal("year", cut.Find("[data-testid='crmhr-financials-panel']").GetAttribute("data-period"));
        Assert.Contains("Sold value by year", cut.Markup, StringComparison.Ordinal);
    }

    private static CrmAccountFinancialSnapshot Snapshot(Guid accountId)
        => new(
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

    private static BunitContext CreateContext(ICrmFinancialSnapshotQueryService query)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        context.Services.AddCanDoItAllBaseLib();
        context.Services.AddSingleton(query);
        return context;
    }

    private sealed class StubFinancialSnapshotQueryService(CrmAccountFinancialSnapshot snapshot) : ICrmFinancialSnapshotQueryService
    {
        public Task<CrmAccountFinancialSnapshot> GetAsync(Guid accountPartyId, CancellationToken cancellationToken = default)
            => Task.FromResult(snapshot);
    }

    private sealed class FailingFinancialSnapshotQueryService : ICrmFinancialSnapshotQueryService
    {
        public Task<CrmAccountFinancialSnapshot> GetAsync(Guid accountPartyId, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("secret-provider-detail");
    }

    // Completes only when the test says so and never observes cancellation.
    private sealed class ScriptedFinancialQuery : ICrmFinancialSnapshotQueryService
    {
        private readonly List<TaskCompletionSource<CrmAccountFinancialSnapshot>> pending = [];

        public List<(Guid AccountPartyId, CancellationToken Token)> Calls { get; } = [];

        public Task<CrmAccountFinancialSnapshot> GetAsync(Guid accountPartyId, CancellationToken cancellationToken = default)
        {
            Calls.Add((accountPartyId, cancellationToken));
            var source = new TaskCompletionSource<CrmAccountFinancialSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
            pending.Add(source);
            return source.Task;
        }

        public void Complete(int call, CrmAccountFinancialSnapshot snapshot) => pending[call].SetResult(snapshot);

        public void Fail(int call, Exception exception) => pending[call].SetException(exception);
    }
}
