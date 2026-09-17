using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright.Smoke;

// The CRM Financials projection through the real Web host and PostgreSQL: recognized sales seeded with sparse
// multi-currency periods, the real chart's plotted geometry and legend, the monthly/yearly re-projection, a
// constrained-width layout, and an account switch to an account without sales.
[Collection(PlaywrightCollection.Name)]
public sealed class CrmHrFinancialsBrowserTests
{
    private const string ChartProbe = """
        () => {
            const chart = document.querySelector('[data-testid="crmhr-financials-sold-chart"]');
            if (!chart) { return { present: false }; }
            // Every plotted bar with the series it belongs to, its category index, the value the chart plotted and
            // its real geometry.
            const bars = Array.from(chart.querySelectorAll('path.apexcharts-bar-area')).map(bar => {
                const box = bar.getBBox();
                const series = bar.closest('.apexcharts-series');
                return {
                    series: series ? series.getAttribute('seriesName') : null,
                    index: Number(bar.getAttribute('j')),
                    value: Number(bar.getAttribute('val')),
                    height: box.height,
                    width: box.width,
                    x: box.x
                };
            });
            const legend = Array.from(chart.querySelectorAll('.apexcharts-legend-text')).map(item => item.textContent.trim());
            // An axis label is a <text> with a <tspan> and an accessible <title>; read the visible tspan only.
            const labels = Array.from(chart.querySelectorAll('.apexcharts-xaxis-label')).map(item => (item.querySelector('tspan') ?? item).textContent.trim()).filter(text => text.length > 0);
            const svg = chart.querySelector('svg.apexcharts-svg');
            return {
                present: true,
                state: chart.dataset.cdaChartState,
                bars,
                legend,
                labels,
                svgWidth: svg ? svg.getBoundingClientRect().width : 0,
                svgHeight: svg ? svg.getBoundingClientRect().height : 0,
                overflow: chart.scrollWidth > chart.clientWidth + 1
            };
        }
        """;

    // Resolves once the chart shows exactly the expected category labels, so a probe never reads a half-updated plot.
    private const string LabelsSettled = """
        expected => Array.from(document.querySelectorAll('[data-testid="crmhr-financials-sold-chart"] .apexcharts-xaxis-label'))
            .map(item => (item.querySelector('tspan') ?? item).textContent.trim())
            .filter(text => text.length > 0)
            .join('|') === expected
        """;

    private const string OverflowProbe = """
        () => {
            const panel = document.querySelector('[data-testid="crmhr-financials-panel"]');
            return {
                documentOverflow: document.documentElement.scrollWidth > document.documentElement.clientWidth,
                panelOverflow: panel.scrollWidth > panel.clientWidth + 1,
                panelWidth: panel.clientWidth
            };
        }
        """;

    private readonly PlaywrightAppFixture fixture;

    public CrmHrFinancialsBrowserTests(PlaywrightAppFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public async Task Financials_plot_the_seeded_recognized_sales_re_project_by_year_and_switch_to_an_account_without_sales()
    {
        var artifactsDir = Path.Combine(PlaywrightTestHostPaths.RepositoryRoot, "output", "playwright", "crm-hr-financials");
        Directory.CreateDirectory(artifactsDir);
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var seed = await SeedAsync(suffix);

        await using var context = await fixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 1600, Height = 1000 }
        });
        var page = await context.NewPageAsync();
        var oracle = CrmHrBrowserOracle.Attach(page);

        var response = await oracle.NavigateAsync($"{fixture.BaseUrl}/crm-hr/crm?accountId={seed.SalesAccountId:D}");
        Assert.True(response?.Ok, $"Expected the CRM route to return 2xx, got {(int?)response?.Status}.");
        await PlaywrightAppFixture.CompleteDatabaseStartupAsync(page);
        await page.GetByTestId("crmhr-crm-record-dialog").WaitForAsync(new() { Timeout = 30_000 });
        await Assertions.Expect(page.GetByTestId("crmhr-account-summary")).ToHaveAttributeAsync("data-interactive", "true", new() { Timeout = 30_000 });

        await page.GetByTestId("crmhr-crm-tab-financials").ClickAsync();
        var panel = page.GetByTestId("crmhr-financials-panel");
        await Assertions.Expect(panel).ToHaveAttributeAsync("data-phase", "ready", new() { Timeout = 30_000 });
        await Assertions.Expect(panel).ToHaveAttributeAsync("data-account-id", seed.SalesAccountId.ToString("D"));
        await Assertions.Expect(panel).ToHaveAttributeAsync("data-period", "month");
        var metrics = page.GetByTestId("crmhr-financials-metrics");
        await Assertions.Expect(metrics).ToContainTextAsync("Sold · EUR");
        await Assertions.Expect(metrics).ToContainTextAsync("Sold · USD");
        await Assertions.Expect(metrics).ToContainTextAsync("Unavailable");
        // The totals are the seeded recognized amounts per currency: 12000 + 3200.25 EUR and 2500 USD. The host formats
        // them with its own number format, so the oracle compares the digits only.
        var metricsText = await metrics.InnerTextAsync();
        Assert.Contains("1520025", Digits(metricsText), StringComparison.Ordinal);
        Assert.Contains("250000", Digits(metricsText), StringComparison.Ordinal);
        await Assertions.Expect(page.GetByTestId("crmhr-financials-incomplete")).ToContainTextAsync("1 incomplete");
        await Assertions.Expect(page.GetByTestId("crmhr-financials-distribution-unavailable")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByTestId("crmhr-financials-invoices-unavailable")).ToBeVisibleAsync();

        // The real chart: English month categories in chronological order, one legend entry per currency, and every
        // plotted bar carrying exactly the seeded value of its currency and month.
        var chart = page.GetByTestId("crmhr-financials-sold-chart");
        await Assertions.Expect(chart).ToHaveAttributeAsync("data-cda-chart-state", "ready");
        await Assertions.Expect(chart).ToContainTextAsync("Sold value by month");
        await page.WaitForFunctionAsync(LabelsSettled, "Jan 2025|Mar 2025|Feb 2026", new() { Timeout = 30_000 });
        var monthly = await page.EvaluateAsync<System.Text.Json.JsonElement>(ChartProbe);
        Assert.Equal("ready", monthly.GetProperty("state").GetString());
        Assert.Equal(new[] { "Jan 2025", "Mar 2025", "Feb 2026" }, Strings(monthly, "labels"));
        Assert.Equal(new[] { "EUR", "USD" }, Strings(monthly, "legend"));
        Assert.True(monthly.GetProperty("svgHeight").GetDouble() > 200, "The chart canvas has no height.");
        AssertPlot(
            monthly,
            ("EUR", 0, 12000m),
            ("EUR", 1, 0m),
            ("EUR", 2, 3200.25m),
            ("USD", 0, 0m),
            ("USD", 1, 2500m),
            ("USD", 2, 0m));
        await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(artifactsDir, "01-financials-monthly-1600.png") });

        // Yearly is a local re-projection of the same accepted snapshot: categories and bars change, no new read.
        await page.GetByTestId("crmhr-financials-year").ClickAsync();
        await Assertions.Expect(panel).ToHaveAttributeAsync("data-period", "year");
        await Assertions.Expect(chart).ToContainTextAsync("Sold value by year");
        await page.WaitForFunctionAsync(LabelsSettled, "2025|2026", new() { Timeout = 30_000 });
        var yearly = await page.EvaluateAsync<System.Text.Json.JsonElement>(ChartProbe);
        Assert.Equal(new[] { "2025", "2026" }, Strings(yearly, "labels"));
        Assert.Equal(new[] { "EUR", "USD" }, Strings(yearly, "legend"));
        AssertPlot(
            yearly,
            ("EUR", 0, 12000m),
            ("EUR", 1, 3200.25m),
            ("USD", 0, 2500m),
            ("USD", 1, 0m));
        await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(artifactsDir, "02-financials-yearly-1600.png") });

        // Back to monthly: the update settles on the monthly categories and values again.
        await page.GetByTestId("crmhr-financials-month").ClickAsync();
        await Assertions.Expect(panel).ToHaveAttributeAsync("data-period", "month");
        await page.WaitForFunctionAsync(LabelsSettled, "Jan 2025|Mar 2025|Feb 2026", new() { Timeout = 30_000 });
        var monthlyAgain = await page.EvaluateAsync<System.Text.Json.JsonElement>(ChartProbe);
        AssertPlot(
            monthlyAgain,
            ("EUR", 0, 12000m),
            ("EUR", 1, 0m),
            ("EUR", 2, 3200.25m),
            ("USD", 0, 0m),
            ("USD", 1, 2500m),
            ("USD", 2, 0m));

        // Constrained width: the panel and the chart stay inside their boxes and the plot keeps its values.
        await page.SetViewportSizeAsync(1100, 900);
        await page.WaitForTimeoutAsync(500);
        var overflow = await page.EvaluateAsync<System.Text.Json.JsonElement>(OverflowProbe);
        Assert.False(overflow.GetProperty("documentOverflow").GetBoolean(), "The document overflows horizontally at 1100 px.");
        Assert.False(overflow.GetProperty("panelOverflow").GetBoolean(), "The Financials panel overflows horizontally at 1100 px.");
        var constrained = await page.EvaluateAsync<System.Text.Json.JsonElement>(ChartProbe);
        Assert.False(constrained.GetProperty("overflow").GetBoolean(), "The chart overflows its box at 1100 px.");
        Assert.Equal(3, constrained.GetProperty("bars").EnumerateArray().Count(bar => bar.GetProperty("height").GetDouble() > 0.5));
        await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(artifactsDir, "03-financials-1100.png") });
        await page.SetViewportSizeAsync(1600, 1000);

        // Another account without sales: an accepted empty result, not the previous account's figures.
        response = await oracle.NavigateAsync($"{fixture.BaseUrl}/crm-hr/crm?accountId={seed.EmptyAccountId:D}");
        Assert.True(response?.Ok);
        await PlaywrightAppFixture.CompleteDatabaseStartupAsync(page);
        await page.GetByTestId("crmhr-crm-record-dialog").WaitForAsync(new() { Timeout = 30_000 });
        await Assertions.Expect(page.GetByTestId("crmhr-account-summary")).ToHaveAttributeAsync("data-interactive", "true", new() { Timeout = 30_000 });
        await page.GetByTestId("crmhr-crm-tab-financials").ClickAsync();
        await Assertions.Expect(panel).ToHaveAttributeAsync("data-phase", "ready", new() { Timeout = 30_000 });
        await Assertions.Expect(panel).ToHaveAttributeAsync("data-account-id", seed.EmptyAccountId.ToString("D"));
        await Assertions.Expect(page.GetByTestId("crmhr-financials-sold-empty")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByTestId("crmhr-financials-sold-chart")).ToHaveCountAsync(0);
        await Assertions.Expect(metrics).ToContainTextAsync("No recognized sales");
        Assert.DoesNotContain("Sold · EUR", await metrics.InnerTextAsync(), StringComparison.Ordinal);

        // Every browser-side failure of the journey counts; only the circuit teardown of the journey's own two
        // navigations is kept apart, and it is written next to the screenshots.
        await File.WriteAllLinesAsync(Path.Combine(artifactsDir, "expected-teardown.txt"), oracle.ExpectedTeardown);
        await oracle.AssertCleanAsync();
    }

    private static string[] Strings(System.Text.Json.JsonElement probe, string property)
        => probe.GetProperty(property).EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();

    private static string Digits(string text)
        => new(text.Where(char.IsAsciiDigit).ToArray());

    // The plotted bars against the seeded oracle: one bar per currency and category carrying exactly the seeded
    // value, a zero-height placeholder where the currency has no sale, real geometry for the others, bar heights in
    // the proportion of their values, and categories laid out left to right.
    private static void AssertPlot(System.Text.Json.JsonElement probe, params (string Series, int Index, decimal Value)[] expected)
    {
        var bars = probe.GetProperty("bars").EnumerateArray()
            .Select(bar => (
                Series: bar.GetProperty("series").GetString() ?? string.Empty,
                Index: bar.GetProperty("index").GetInt32(),
                Value: bar.GetProperty("value").GetDecimal(),
                Height: bar.GetProperty("height").GetDouble(),
                Width: bar.GetProperty("width").GetDouble(),
                X: bar.GetProperty("x").GetDouble()))
            .OrderBy(bar => bar.Series, StringComparer.Ordinal)
            .ThenBy(bar => bar.Index)
            .ToArray();

        Assert.Equal(expected, bars.Select(bar => (bar.Series, bar.Index, bar.Value)).ToArray());

        var plotted = bars.Where(bar => bar.Value > 0m).ToArray();
        Assert.All(plotted, bar => Assert.True(bar.Height > 0.5 && bar.Width > 0.5, $"The {bar.Series} bar of category {bar.Index} has no geometry."));
        Assert.All(bars.Where(bar => bar.Value == 0m), bar => Assert.True(bar.Height <= 0.5, $"The {bar.Series} placeholder of category {bar.Index} is plotted with height {bar.Height}."));

        var reference = plotted.MaxBy(bar => bar.Value);
        Assert.All(plotted, bar =>
        {
            var expectedRatio = (double)(bar.Value / reference.Value);
            var actualRatio = bar.Height / reference.Height;
            Assert.True(Math.Abs(expectedRatio - actualRatio) < 0.02, $"The {bar.Series} bar of category {bar.Index} is {actualRatio:F3} of the tallest bar; its value is {expectedRatio:F3} of the largest value.");
        });

        // A placeholder has no box to position; the plotted bars follow their categories from left to right.
        var positions = plotted.OrderBy(bar => bar.Index).ThenBy(bar => bar.Series, StringComparer.Ordinal).Select(bar => bar.X).ToArray();
        Assert.Equal(positions.OrderBy(x => x).ToArray(), positions);
    }

    private async Task<SeededFinancials> SeedAsync(string suffix)
    {
        await using var serviceProvider = await TestApplicationBootstrap.BuildServiceProviderAsync(
            CreateActiveProfile(),
            "CanDoItAll.Tests.Playwright.Seed",
            TestSchemaBootstrapModules.Full,
            new Dictionary<string, string?>
            {
                ["DevelopmentManager:TuningModeEnabled"] = "false"
            });
        await using var scope = serviceProvider.CreateAsyncScope();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

        var salesAccountId = await CreateAccountAsync(partyDirectoryService, $"Financials Account {suffix}", $"financials.{suffix}@example.test");
        var emptyAccountId = await CreateAccountAsync(partyDirectoryService, $"Financials Empty {suffix}", $"financials-empty.{suffix}@example.test");
        var ownerId = await CreateOwnerAsync(partyDirectoryService, $"Financials Owner {suffix}", $"financials-owner.{suffix}@example.test");

        // Recognized sales are the first Won transitions with their recognized amount and currency, dated by UTC:
        // sparse periods per currency and one incomplete Won record without a recognized amount.
        await using (var dbContext = await dbContextFactory.CreateDbContextAsync())
        {
            var januaryEur = Opportunity(salesAccountId, ownerId, $"January EUR {suffix}", "EUR", 12000m);
            var marchUsd = Opportunity(salesAccountId, ownerId, $"March USD {suffix}", "USD", 2500m);
            var februaryEur = Opportunity(salesAccountId, ownerId, $"February EUR {suffix}", "EUR", 3200.25m);
            var incomplete = Opportunity(salesAccountId, ownerId, $"Incomplete {suffix}", "USD", null);
            dbContext.Set<Opportunity>().AddRange(januaryEur, marchUsd, februaryEur, incomplete);
            dbContext.Set<OpportunityStageHistory>().AddRange(
                History(januaryEur.Id, new DateTimeOffset(2025, 1, 15, 9, 0, 0, TimeSpan.Zero), 12000m, "EUR"),
                History(marchUsd.Id, new DateTimeOffset(2025, 3, 10, 9, 0, 0, TimeSpan.Zero), 2500m, "USD"),
                History(februaryEur.Id, new DateTimeOffset(2026, 2, 3, 9, 0, 0, TimeSpan.Zero), 3200.25m, "EUR"),
                History(incomplete.Id, new DateTimeOffset(2026, 2, 20, 9, 0, 0, TimeSpan.Zero), null, "USD"));
            await dbContext.SaveChangesAsync();
        }

        return new SeededFinancials(salesAccountId, emptyAccountId);
    }

    private static Opportunity Opportunity(Guid accountId, Guid ownerId, string title, string currencyCode, decimal? amount)
        => new()
        {
            Id = Guid.NewGuid(),
            AccountPartyId = accountId,
            OwnerPartyId = ownerId,
            Title = title,
            Stage = OpportunityStage.Won,
            CurrencyCode = currencyCode,
            Amount = amount,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

    private static OpportunityStageHistory History(Guid opportunityId, DateTimeOffset changedAtUtc, decimal? recognizedAmount, string recognizedCurrencyCode)
        => new()
        {
            Id = Guid.NewGuid(),
            OpportunityId = opportunityId,
            Stage = OpportunityStage.Won,
            ChangedAtUtc = changedAtUtc,
            ChangedBy = "playwright-tests",
            RecognizedAmount = recognizedAmount,
            RecognizedCurrencyCode = recognizedCurrencyCode
        };

    private TestDatabaseProfile CreateActiveProfile()
    {
        if (string.IsNullOrWhiteSpace(fixture.DatabaseConnectionString))
        {
            throw new InvalidOperationException("Playwright fixture did not expose a database connection string.");
        }

        if (string.IsNullOrWhiteSpace(fixture.StorageWorkspaceRoot))
        {
            throw new InvalidOperationException("Playwright fixture did not expose the storage workspace root.");
        }

        var workspaceRoot = fixture.StorageWorkspaceRoot;
        var profileRoot = Directory.GetParent(workspaceRoot)?.FullName
            ?? throw new InvalidOperationException($"Could not resolve profile root from '{workspaceRoot}'.");
        var environmentRoot = Path.GetFullPath(Path.Combine(profileRoot, "..", ".."));

        return new TestDatabaseProfile(
            "playwright-financials-seed",
            environmentRoot,
            profileRoot,
            TestDatabaseProviderKind.PostgreSql,
            fixture.DatabaseConnectionString,
            workspaceRoot,
            Path.Combine(profileRoot, "manager-artifacts"));
    }

    private static Task<Guid> CreateAccountAsync(PartyDirectoryService partyDirectoryService, string displayName, string email)
        => CreatePartyAsync(partyDirectoryService, displayName, PartyType.Organization, PartyRoleKind.Customer, email);

    private static Task<Guid> CreateOwnerAsync(PartyDirectoryService partyDirectoryService, string displayName, string email)
        => CreatePartyAsync(partyDirectoryService, displayName, PartyType.Person, PartyRoleKind.AccountManager, email);

    private static async Task<Guid> CreatePartyAsync(
        PartyDirectoryService partyDirectoryService,
        string displayName,
        PartyType partyType,
        PartyRoleKind roleKind,
        string email)
    {
        var result = await partyDirectoryService.SavePartyAsync(new PartyEditorModel
        {
            PartyType = partyType,
            LifecycleStatus = PartyLifecycleStatus.Active,
            DisplayName = displayName,
            Summary = $"{displayName} summary",
            LastChangedBy = "playwright-tests",
            Roles =
            [
                new PartyRoleAssignmentEditorModel
                {
                    RoleKind = roleKind,
                    Title = roleKind.ToString(),
                    IsPrimary = true
                }
            ],
            ContactPoints =
            [
                new PartyContactPointEditorModel
                {
                    ContactType = PartyContactType.Email,
                    Label = "Primary email",
                    Value = email,
                    NormalizedValue = email.ToLowerInvariant(),
                    IsPrimary = true,
                    IsPublic = true
                }
            ]
        });

        Assert.True(result.IsSuccess, string.Join(" ", result.Errors.Select(error => error.Message)));
        return result.Value;
    }

    private sealed record SeededFinancials(Guid SalesAccountId, Guid EmptyAccountId);
}
