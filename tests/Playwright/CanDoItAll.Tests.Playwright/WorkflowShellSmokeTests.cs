using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

[Collection(PlaywrightCollection.Name)]
public sealed class WorkflowShellSmokeTests
{
    private readonly PlaywrightAppFixture fixture;

    public WorkflowShellSmokeTests(PlaywrightAppFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public async Task Workflow_shell_creates_and_runs_starter_preview_on_large_screen()
    {
        var artifactDirectory = Path.Combine(
            PlaywrightTestHostPaths.RepositoryRoot,
            "codex",
            "bundles",
            "skill-tool-mcp-isolation-template-migration",
            "proof",
            "SB11",
            "screenshots");
        Directory.CreateDirectory(artifactDirectory);

        await using var context = await fixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize
            {
                Width = 1600,
                Height = 1000
            }
        });
        var page = await context.NewPageAsync();

        var response = await page.GotoAsync($"{fixture.BaseUrl}/agents/workflows");
        Assert.NotNull(response);
        Assert.True(response!.Ok, $"Expected /agents/workflows to return 2xx, got {(int)response.Status}.");
        await DismissStartupModalIfPresentAsync(page);
        await page.GetByTestId("workflows-tabs").WaitForAsync();
        await page.GetByTestId("workflows-create-starter").WaitForAsync();
        await page.GetByTestId("workflows-create-starter").ClickAsync();

        await page.GetByTestId("workflows-tab-workflows").ClickAsync();
        await page.GetByTestId("workflows-catalog").WaitForAsync();
        await page.GetByTestId("workflows-catalog-item").First.WaitForAsync();

        await page.GetByTestId("workflows-tab-history").ClickAsync();
        await page.GetByTestId("workflows-run-test").WaitForAsync();
        await page.GetByTestId("workflows-run-test").ClickAsync();
        await ExpectTextContainsAsync(page.GetByTestId("workflows-test-result"), "Succeeded", timeoutMs: 30_000);
        await page.GetByTestId("workflows-run-event").First.WaitForAsync();
        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(artifactDirectory, "workflow-shell-runtime-large.png"),
            FullPage = true
        });

        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
    }

    private static async Task DismissStartupModalIfPresentAsync(IPage page, float timeoutMs = 1_500)
    {
        var startupDialog = page.GetByTestId("database-startup-modal");
        try
        {
            await startupDialog.WaitForAsync(new LocatorWaitForOptions
            {
                Timeout = timeoutMs
            });
        }
        catch (TimeoutException)
        {
            return;
        }

        await page.GetByTestId("database-startup-continue").ClickAsync();
        await startupDialog.WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Detached
        });
    }

    private static async Task ExpectTextContainsAsync(ILocator locator, string expectedValue, int timeoutMs)
    {
        var timeoutAt = DateTimeOffset.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTimeOffset.UtcNow < timeoutAt)
        {
            if ((await locator.InnerTextAsync()).Contains(expectedValue, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            await Task.Delay(200);
        }

        throw new TimeoutException($"Timed out waiting for text '{expectedValue}'.");
    }
}
