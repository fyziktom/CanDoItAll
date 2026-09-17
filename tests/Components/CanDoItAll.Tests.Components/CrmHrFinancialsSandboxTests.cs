using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.Charts;
using CanDoItAll.CrmHr.UI.Financials;
using CanDoItAll.CrmHr.UiSandbox.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.CrmHr;

public sealed class CrmHrFinancialsSandboxTests : IDisposable
{
    private readonly BunitContext context = new();

    public CrmHrFinancialsSandboxTests()
    {
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        context.Services.AddCanDoItAllBaseLib();
    }

    [Theory]
    [InlineData("populated", "crmhr-financials-sold-chart")]
    [InlineData("single-currency", "crmhr-financials-sold-chart")]
    [InlineData("empty", "crmhr-financials-sold-empty")]
    [InlineData("incomplete", "crmhr-financials-incomplete")]
    [InlineData("loading", "crmhr-financials-loading")]
    [InlineData("failed", "crmhr-financials-retry")]
    [InlineData("long-labels", "crmhr-financials-sold-chart")]
    public void Query_restores_scenarios_with_the_real_surface(string scenario, string testId)
    {
        var cut = Render(scenario);

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find($"[data-testid='{testId}']")));
        Assert.Equal(scenario, cut.Find("[data-testid='crmhr-sandbox-frame']").GetAttribute("data-scenario"));
        Assert.NotNull(cut.FindComponent<CrmHrFinancialsSurface>());
    }

    [Fact]
    public void Populated_scenario_plots_one_aligned_series_per_currency_and_period_switching_re_projects_locally()
    {
        var cut = Render("populated");
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='crmhr-financials-sold-chart']")));

        var chart = Assert.Single(cut.FindComponents<CdaChart>());
        Assert.Equal(new[] { "EUR", "GBP", "USD" }, chart.Instance.Series.Select(series => series.Name));
        var categories = chart.Instance.Series[0].Points.Select(point => point.Category).ToArray();
        var expected = new[] { new DateOnly(2025, 1, 1), new DateOnly(2025, 2, 1), new DateOnly(2025, 3, 1), new DateOnly(2025, 11, 1), new DateOnly(2026, 1, 1), new DateOnly(2026, 2, 1) }
            .Select(start => start.ToString("MMM yyyy"))
            .ToArray();
        Assert.Equal(expected, categories);
        Assert.All(chart.Instance.Series, series => Assert.Equal(categories, series.Points.Select(point => point.Category)));

        cut.Find("[data-testid='crmhr-financials-year']").Click();

        cut.WaitForAssertion(() => Assert.Equal("year", cut.Find("[data-testid='crmhr-financials-panel']").GetAttribute("data-period")));
        Assert.Equal("Period: Year", cut.Find("[data-testid='crmhr-sandbox-intent']").TextContent);
        var yearly = Assert.Single(cut.FindComponents<CdaChart>());
        Assert.Equal("Sold value by year", yearly.Instance.Title);
        Assert.All(yearly.Instance.Series, series => Assert.Equal(new[] { "2025", "2026" }, series.Points.Select(point => point.Category)));
    }

    [Fact]
    public void Failed_scenario_retries_locally_into_the_populated_snapshot()
    {
        var cut = Render("failed");
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='crmhr-financials-retry']")));
        Assert.Empty(cut.FindComponents<CdaChart>());

        cut.Find("[data-testid='crmhr-financials-retry']").Click();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='crmhr-financials-sold-chart']")));
        Assert.StartsWith("Retry: generation ", cut.Find("[data-testid='crmhr-sandbox-intent']").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Empty_scenario_shows_no_sales_and_no_fabricated_figure()
    {
        var cut = Render("empty");

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='crmhr-financials-sold-empty']")));
        Assert.Empty(cut.FindComponents<CdaChart>());
        Assert.Contains(CrmHrFinancialsText.NoRecognizedSalesValue, cut.Markup, StringComparison.Ordinal);
        Assert.Contains(CrmHrFinancialsText.UnavailableValue, cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("100%", cut.Markup, StringComparison.Ordinal);
    }

    public void Dispose() => context.Dispose();

    private IRenderedComponent<Routes> Render(string scenario)
    {
        context.Services.GetRequiredService<NavigationManager>().NavigateTo($"/crm-hr/financials?scenario={scenario}&layout=matched");
        return context.Render<Routes>();
    }
}
