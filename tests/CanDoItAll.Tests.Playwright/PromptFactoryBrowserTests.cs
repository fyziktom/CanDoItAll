using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

public sealed partial class AppSmokeTests
{
    [Fact]
    [Trait("Surface", "PromptFactory")]
    public async Task Prompt_factory_canvas_surface_loads_and_exposes_shared_chrome()
    {
        await using var context = await fixture.Browser.NewContextAsync();
        var page = await context.NewPageAsync();

        await CreateProjectAsync(page, "Playwright Prompt Factory", "Review");

        var response = await page.GotoAsync($"{fixture.BaseUrl}/prompt-factory");
        Assert.NotNull(response);
        Assert.True(response!.Ok, $"Expected /prompt-factory to return 2xx, got {(int)response.Status}.");

        await page.WaitForSelectorAsync("text=Prompt session workbench");
        await page.WaitForSelectorAsync("text=Prompt Factory tabs");
        await page.WaitForTimeoutAsync(1000);
        await page.Locator(".cw-workbench-shell").WaitForAsync();
        await AssertSharedChromeVisibleAsync(page);
        await OpenCanvasHelpAsync(page);
        await CloseCanvasHelpAsync(page);
        await page.Locator(".pf-page-tab").Filter(new() { HasText = "Assembly" }).First.ClickAsync();
        await page.WaitForSelectorAsync("text=Assembly workspace");
        await page.GetByTestId("prompt-factory-build").First.WaitForAsync();
        await page.GetByTestId("prompt-factory-build").First.ClickAsync();
        await page.Locator(".pf-page-tab").Filter(new() { HasText = "Review" }).First.ClickAsync();
        await page.WaitForSelectorAsync("text=Prompt review");
        await page.WaitForSelectorAsync("text=Select a project before building a prompt.");
        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
    }
}
