using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright.Smoke;

[Collection(PlaywrightCollection.Name)]
public sealed class CorePortabilityBrowserSmokeTests(PlaywrightAppFixture fixture)
{
    [Fact]
    [Trait("Category", "UnixPortabilityBrowserSmoke")]
    public async Task Runtime_capabilities_page_reports_ready_without_desktop_features()
    {
        await using var context = await fixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize
            {
                Width = 1440,
                Height = 900
            }
        });
        IPage page = await context.NewPageAsync();

        IResponse? response = await page.GotoAsync($"{fixture.BaseUrl}/settings/runtime-capabilities");

        Assert.NotNull(response);
        Assert.True(response!.Ok, $"Expected runtime capabilities page to return 2xx, got {(int)response.Status}.");
        ILocator readiness = page.GetByTestId("runtime-readiness-summary");
        await readiness.WaitForAsync();
        await Assertions.Expect(readiness).ToContainTextAsync("Mandatory runtime capabilities are ready.");
        await page.GetByTestId("runtime-capability-grid").WaitForAsync();
        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
    }
}
