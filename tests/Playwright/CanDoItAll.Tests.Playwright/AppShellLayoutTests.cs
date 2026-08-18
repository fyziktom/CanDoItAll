using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright.Visual;

[Collection(PlaywrightCollection.Name)]
public sealed class AppShellLayoutTests
{
    private const float MaximumShellBottomGap = 16;
    private readonly PlaywrightAppFixture fixture;

    public AppShellLayoutTests(PlaywrightAppFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public async Task Standard_page_surface_tracks_the_effective_viewport_after_resize()
    {
        await using var context = await fixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize
            {
                Width = 1536,
                Height = 864
            }
        });
        var page = await context.NewPageAsync();

        await page.GotoAsync($"{fixture.BaseUrl}/crm-hr/workforce");
        await WaitForWorkforceAsync(page);

        var initialSurfaceHeight = await AssertSurfaceFillsViewportAsync(page);

        await page.SetViewportSizeAsync(1920, 1080);
        var expandedSurfaceHeight = await AssertSurfaceFillsViewportAsync(page);

        Assert.True(
            expandedSurfaceHeight > initialSurfaceHeight,
            $"Expected the shell surface to grow after the effective viewport expanded, but it changed from {initialSurfaceHeight}px to {expandedSurfaceHeight}px.");
    }

    [Fact]
    public async Task Workforce_catalog_and_results_use_the_remaining_viewport_after_resize()
    {
        await using var context = await fixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize
            {
                Width = 1536,
                Height = 864
            }
        });
        var page = await context.NewPageAsync();

        await page.GotoAsync($"{fixture.BaseUrl}/crm-hr/workforce");
        await WaitForWorkforceAsync(page);

        var initial = await AssertWorkforceCatalogFillsSurfaceAsync(page);

        await page.SetViewportSizeAsync(1920, 1080);
        var expanded = await AssertWorkforceCatalogFillsSurfaceAsync(page);

        Assert.True(
            expanded.CatalogHeight > initial.CatalogHeight,
            $"Expected the workforce catalog to grow after the viewport expanded, but it changed from {initial.CatalogHeight}px to {expanded.CatalogHeight}px.");
        Assert.True(
            expanded.ResultsHeight > initial.ResultsHeight,
            $"Expected the bounded results region to grow after the viewport expanded, but it changed from {initial.ResultsHeight}px to {expanded.ResultsHeight}px.");
    }

    private static async Task<float> AssertSurfaceFillsViewportAsync(IPage page)
    {
        var surface = page.Locator(".cda-shell-body-surface");
        await surface.WaitForAsync();

        var surfaceBounds = await surface.BoundingBoxAsync();
        Assert.NotNull(surfaceBounds);

        var viewportHeight = await page.EvaluateAsync<float>("() => window.innerHeight");
        var bottomGap = viewportHeight - (surfaceBounds.Y + surfaceBounds.Height);

        Assert.InRange(bottomGap, 0, MaximumShellBottomGap);
        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());

        return surfaceBounds.Height;
    }

    private static async Task<WorkforceLayoutBounds> AssertWorkforceCatalogFillsSurfaceAsync(IPage page)
    {
        var catalog = page.GetByTestId("crmhr-workforce-catalog");
        await catalog.WaitForAsync();

        var surfaceBounds = await page.Locator(".cda-shell-body-surface").BoundingBoxAsync();
        var catalogBounds = await catalog.BoundingBoxAsync();
        var resultsBounds = await page.GetByTestId("crmhr-workforce-results").BoundingBoxAsync();
        var pagerBounds = await page.GetByTestId("crmhr-workforce-pager").BoundingBoxAsync();

        Assert.NotNull(surfaceBounds);
        Assert.NotNull(catalogBounds);
        Assert.NotNull(resultsBounds);
        Assert.NotNull(pagerBounds);

        var surfaceBottom = surfaceBounds.Y + surfaceBounds.Height;
        var catalogBottom = catalogBounds.Y + catalogBounds.Height;
        var catalogBottomInset = surfaceBottom - catalogBottom;
        Assert.InRange(catalogBottomInset, 0, 40);
        Assert.True(
            resultsBounds.Y + resultsBounds.Height <= pagerBounds.Y + 1,
            "Expected the pager to remain below the bounded results scroll region.");

        var documentHeight = await page.EvaluateAsync<float>("() => document.body.scrollHeight");
        var viewportHeight = await page.EvaluateAsync<float>("() => window.innerHeight");
        Assert.Equal(viewportHeight, documentHeight);
        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());

        return new WorkforceLayoutBounds(catalogBounds.Height, resultsBounds.Height);
    }

    private async Task WaitForWorkforceAsync(IPage page, int timeoutMs = 30_000)
    {
        var timeoutAt = DateTimeOffset.UtcNow.AddMilliseconds(timeoutMs);
        var startupSettleAt = DateTimeOffset.UtcNow.AddSeconds(4);
        var workforceMarker = page.GetByText("Workforce workspace", new PageGetByTextOptions
        {
            Exact = true
        });
        var workforceCatalog = page.GetByTestId("crmhr-workforce-catalog");
        var startupDialog = page.GetByTestId("database-startup-modal");

        while (DateTimeOffset.UtcNow < timeoutAt)
        {
            if (await startupDialog.IsVisibleAsync())
            {
                await page.GetByTestId("database-startup-continue").ClickAsync();
                await startupDialog.WaitForAsync(new LocatorWaitForOptions
                {
                    State = WaitForSelectorState.Detached
                });
                continue;
            }

            if (DateTimeOffset.UtcNow >= startupSettleAt &&
                await workforceMarker.IsVisibleAsync() &&
                await workforceCatalog.IsVisibleAsync())
            {
                return;
            }

            await Task.Delay(100);
        }

        throw new TimeoutException(
            $"Timed out waiting for the workforce workspace.{Environment.NewLine}{fixture.GetLogSnapshot()}");
    }

    private readonly record struct WorkforceLayoutBounds(
        float CatalogHeight,
        float ResultsHeight);
}
