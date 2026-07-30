using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

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

    private async Task WaitForWorkforceAsync(IPage page, int timeoutMs = 30_000)
    {
        var timeoutAt = DateTimeOffset.UtcNow.AddMilliseconds(timeoutMs);
        var workforceMarker = page.GetByText("Workforce workspace", new PageGetByTextOptions
        {
            Exact = true
        });
        var startupDialog = page.GetByTestId("database-startup-modal");

        while (DateTimeOffset.UtcNow < timeoutAt)
        {
            if (await workforceMarker.IsVisibleAsync())
            {
                return;
            }

            if (await startupDialog.IsVisibleAsync())
            {
                await page.GetByTestId("database-startup-continue").ClickAsync();
                await startupDialog.WaitForAsync(new LocatorWaitForOptions
                {
                    State = WaitForSelectorState.Detached
                });
            }

            await Task.Delay(100);
        }

        throw new TimeoutException(
            $"Timed out waiting for the workforce workspace.{Environment.NewLine}{fixture.GetLogSnapshot()}");
    }
}
