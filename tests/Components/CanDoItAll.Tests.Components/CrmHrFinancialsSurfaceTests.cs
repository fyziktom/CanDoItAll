using System.Globalization;
using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.Charts;
using CanDoItAll.CrmHr.UI.Financials;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.CrmHr;

// Controlled rendering of the Financials surface: no query service, module runtime or account model is involved,
// and the real chart component is composed with the projected series.
public sealed class CrmHrFinancialsSurfaceTests
{
    private static readonly Guid AccountId = Guid.Parse("86000000-0000-0000-0000-000000000001");

    [Fact]
    public void Loading_phase_shows_the_loading_state_and_no_figure()
    {
        using var context = CreateContext();

        var cut = context.Render<CrmHrFinancialsSurface>(parameters => parameters
            .Add(component => component.Presentation, CrmHrFinancialsPresentation.CreateLoading(1, CrmHrFinancialPeriod.Month)));

        var root = cut.Find("[data-testid='crmhr-financials-panel']");
        Assert.Equal("loading", root.GetAttribute("data-phase"));
        Assert.Equal("none", root.GetAttribute("data-account-id"));
        Assert.NotNull(cut.Find("[data-testid='crmhr-financials-loading']"));
        Assert.Empty(cut.FindAll("[data-testid='crmhr-financials-metrics']"));
        Assert.Empty(cut.FindComponents<CdaChart>());
        Assert.DoesNotContain("Unavailable", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Failed_phase_offers_a_retry_that_names_the_failed_read_and_is_blocked_while_retrying() {
        using var context = CreateContext();
        var intents = new List<CrmHrFinancialsIntent>();

        var cut = context.Render<CrmHrFinancialsSurface>(parameters => parameters
            .Add(component => component.Presentation, CrmHrFinancialsPresentation.CreateFailed(7, "The financial projection is unavailable. Retry after checking the CRM data source.", CrmHrFinancialPeriod.Month))
            .Add(component => component.Intent, intent => intents.Add(intent)));

        Assert.Equal("failed", cut.Find("[data-testid='crmhr-financials-panel']").GetAttribute("data-phase"));
        Assert.Contains("Commercial results could not be loaded", cut.Find("[data-testid='crmhr-financials-failed']").TextContent, StringComparison.Ordinal);
        Assert.Contains("financial projection is unavailable", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(cut.FindComponents<CdaChart>());

        await cut.Find("[data-testid='crmhr-financials-retry']").ClickAsync();
        Assert.Equal(7, Assert.IsType<CrmHrFinancialsIntent.Retry>(Assert.Single(intents)).Generation);

        cut.Render(parameters => parameters
            .Add(component => component.Presentation, CrmHrFinancialsPresentation.CreateFailed(8, "still failing", CrmHrFinancialPeriod.Month, isRetrying: true)));
        Assert.True(cut.Find("[data-testid='crmhr-financials-retry']").HasAttribute("disabled"));
        await cut.Find("[data-testid='crmhr-financials-retry']").ClickAsync();
        Assert.Single(intents);
    }

    [Fact]
    public async Task Ready_snapshot_renders_currency_separated_metrics_unavailable_sources_the_incomplete_note_and_the_aligned_chart() {
        using var context = CreateContext();
        var intents = new List<CrmHrFinancialsIntent>();

        var cut = context.Render<CrmHrFinancialsSurface>(parameters => parameters
            .Add(component => component.Presentation, CrmHrFinancialsPresentation.CreateReady(3, Snapshot(incomplete: 2), CrmHrFinancialPeriod.Month))
            .Add(component => component.Intent, intent => intents.Add(intent)));

        var root = cut.Find("[data-testid='crmhr-financials-panel']");
        Assert.Equal("ready", root.GetAttribute("data-phase"));
        Assert.Equal(AccountId.ToString("D"), root.GetAttribute("data-account-id"));
        Assert.Equal("month", root.GetAttribute("data-period"));
        var metrics = cut.FindComponents<MetricCard>().Select(card => (card.Instance.Label, card.Instance.Value)).ToArray();
        Assert.Equal(
            new[]
            {
                ("Sold · EUR", $"{80m:N2} EUR"),
                ("Sold · USD", $"{150m:N2} USD"),
                ("Bought", CrmHrFinancialsText.UnavailableValue),
                ("Overdue invoices", CrmHrFinancialsText.UnavailableValue),
                ("Incomplete won records", "2")
            },
            metrics);
        Assert.Contains("2 incomplete", cut.Find("[data-testid='crmhr-financials-incomplete']").TextContent, StringComparison.Ordinal);
        Assert.Contains("Distribution unavailable", cut.Find("[data-testid='crmhr-financials-distribution-unavailable']").TextContent, StringComparison.Ordinal);
        Assert.Contains("Overdue status unavailable", cut.Find("[data-testid='crmhr-financials-invoices-unavailable']").TextContent, StringComparison.Ordinal);
        Assert.DoesNotContain("100% sold", cut.Markup, StringComparison.OrdinalIgnoreCase);

        var chart = Assert.Single(cut.FindComponents<CdaChart>());
        Assert.Equal("Sold value by month", chart.Instance.Title);
        Assert.Equal(new[] { "EUR", "USD" }, chart.Instance.Series.Select(series => series.Name));
        var monthLabels = new[] { "May 2026", "Jun 2026" };
        Assert.All(chart.Instance.Series, series => Assert.Equal(monthLabels, series.Points.Select(point => point.Category)));
        Assert.Equal(new[] { 0m, 80m }, chart.Instance.Series[0].Points.Select(point => point.Value));
        Assert.Equal(new[] { 100m, 50m }, chart.Instance.Series[1].Points.Select(point => point.Value));
        Assert.False(chart.Instance.Options.ShowToolbar);
        Assert.False(chart.Instance.Options.EnableZoom);
        Assert.True(chart.Instance.Options.ShowLegend);

        await cut.Find("[data-testid='crmhr-financials-year']").ClickAsync();
        Assert.Equal(CrmHrFinancialPeriod.Year, Assert.IsType<CrmHrFinancialsIntent.SetPeriod>(Assert.Single(intents)).Period);
        // The active period is not re-requested.
        await cut.Find("[data-testid='crmhr-financials-month']").ClickAsync();
        Assert.Single(intents);

        cut.Render(parameters => parameters
            .Add(component => component.Presentation, CrmHrFinancialsPresentation.CreateReady(3, Snapshot(incomplete: 2), CrmHrFinancialPeriod.Year)));
        Assert.Equal("year", cut.Find("[data-testid='crmhr-financials-panel']").GetAttribute("data-period"));
        var yearly = Assert.Single(cut.FindComponents<CdaChart>());
        Assert.Equal("Sold value by year", yearly.Instance.Title);
        Assert.All(yearly.Instance.Series, series => Assert.Equal(new[] { "2026" }, series.Points.Select(point => point.Category)));
        Assert.Equal(new[] { 80m }, yearly.Instance.Series[0].Points.Select(point => point.Value));
        Assert.Equal(new[] { 150m }, yearly.Instance.Series[1].Points.Select(point => point.Value));
    }

    [Fact]
    public void An_accepted_empty_sales_result_shows_the_no_sales_copy_without_a_chart_and_without_fabricated_figures()
    {
        using var context = CreateContext();
        var snapshot = new CrmHrFinancialsSnapshot(
            AccountId,
            CrmHrFinancialAvailability.Empty,
            [],
            [],
            [],
            IncompleteWonCount: 0,
            CrmHrFinancialAvailability.Unavailable,
            CrmHrFinancialAvailability.Unavailable,
            CrmHrFinancialAvailability.Unavailable);

        var cut = context.Render<CrmHrFinancialsSurface>(parameters => parameters
            .Add(component => component.Presentation, CrmHrFinancialsPresentation.CreateReady(1, snapshot, CrmHrFinancialPeriod.Month)));

        Assert.Equal("ready", cut.Find("[data-testid='crmhr-financials-panel']").GetAttribute("data-phase"));
        var metrics = cut.FindComponents<MetricCard>().Select(card => (card.Instance.Label, card.Instance.Value)).ToArray();
        Assert.Equal(
            new[]
            {
                ("Sold", CrmHrFinancialsText.NoRecognizedSalesValue),
                ("Bought", CrmHrFinancialsText.UnavailableValue),
                ("Overdue invoices", CrmHrFinancialsText.UnavailableValue),
                ("Incomplete won records", "0")
            },
            metrics);
        Assert.NotNull(cut.Find("[data-testid='crmhr-financials-sold-empty']"));
        Assert.Empty(cut.FindComponents<CdaChart>());
        Assert.Empty(cut.FindAll("[data-testid='crmhr-financials-incomplete']"));
        Assert.DoesNotContain("0,00", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("0.00", cut.Markup, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("cs-CZ")]
    [InlineData("ja-JP")]
    public void The_rendered_chart_categories_are_english_under_a_non_english_server_culture(string ambient)
    {
        var previousCulture = CultureInfo.CurrentCulture;
        var previousUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(ambient);
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(ambient);
            using var context = CreateContext();

            var cut = context.Render<CrmHrFinancialsSurface>(parameters => parameters
                .Add(component => component.Presentation, CrmHrFinancialsPresentation.CreateReady(1, Snapshot(incomplete: 0), CrmHrFinancialPeriod.Month)));

            var chart = Assert.Single(cut.FindComponents<CdaChart>());
            Assert.All(chart.Instance.Series, series => Assert.Equal(new[] { "May 2026", "Jun 2026" }, series.Points.Select(point => point.Category)));
            // The plotted values are the owner's decimals, untouched by the label policy.
            Assert.Equal(new[] { 0m, 80m }, chart.Instance.Series[0].Points.Select(point => point.Value));
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUiCulture;
        }
    }

    [Fact]
    public void Two_surfaces_never_share_chart_options()
    {
        using var context = CreateContext();
        var presentation = CrmHrFinancialsPresentation.CreateReady(1, Snapshot(incomplete: 0), CrmHrFinancialPeriod.Month);

        var first = context.Render<CrmHrFinancialsSurface>(parameters => parameters.Add(component => component.Presentation, presentation));
        var second = context.Render<CrmHrFinancialsSurface>(parameters => parameters.Add(component => component.Presentation, presentation));

        Assert.NotSame(
            Assert.Single(first.FindComponents<CdaChart>()).Instance.Options,
            Assert.Single(second.FindComponents<CdaChart>()).Instance.Options);
    }

    private static CrmHrFinancialsSnapshot Snapshot(int incomplete)
        => new(
            AccountId,
            CrmHrFinancialAvailability.Available,
            [new CrmHrCurrencyTotal("EUR", 80m), new CrmHrCurrencyTotal("USD", 150m)],
            [
                new CrmHrFinancialPeriodAmount(new DateOnly(2026, 5, 1), "USD", 100m),
                new CrmHrFinancialPeriodAmount(new DateOnly(2026, 6, 1), "EUR", 80m),
                new CrmHrFinancialPeriodAmount(new DateOnly(2026, 6, 1), "USD", 50m)
            ],
            [
                new CrmHrFinancialPeriodAmount(new DateOnly(2026, 1, 1), "EUR", 80m),
                new CrmHrFinancialPeriodAmount(new DateOnly(2026, 1, 1), "USD", 150m)
            ],
            incomplete,
            CrmHrFinancialAvailability.Unavailable,
            CrmHrFinancialAvailability.Unavailable,
            CrmHrFinancialAvailability.Unavailable);

    private static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        context.Services.AddCanDoItAllBaseLib();
        return context;
    }
}
