using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

[Collection(PlaywrightCollection.Name)]
public sealed class AgentMemoryProviderSettingsPlaywrightTests
{
    [Fact]
    public async Task AgentMemorySettings_BindsMultipleProvidersAndKeepsMobileActionsUsable()
    {
        await using var host = await MemoryProviderManagementPlaywrightHost.CreateAsync();
        await using var browser = await host.Playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });
        await using var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize
            {
                Width = 1440,
                Height = 1000
            }
        });
        var page = await context.NewPageAsync();
        var screenshotRoot = Path.Combine(
            PlaywrightTestHostPaths.RepositoryRoot,
            "codex",
            "bundles",
            "candoitall-memory-provider-extraction-bundle",
            "proof",
            "regression",
            "screenshots");
        Directory.CreateDirectory(screenshotRoot);

        try
        {
            await ConfigureHealthyDemoProvidersAsync(page, host.BaseUrl);
            await OpenMemorySettingsAsync(page, host.BaseUrl);

            var providerPicker = page.GetByTestId("agents-catalog-memory-new-provider");
            await Assertions.Expect(providerPicker.Locator("option[value='provider.business-demo']")).ToHaveCountAsync(1);
            await Assertions.Expect(providerPicker.Locator("option[value='provider.programming-demo']")).ToHaveCountAsync(1);

            await AddBindingAsync(page, "business-memory", "provider.business-demo", "Optional");
            await AddBindingAsync(page, "programming-memory", "provider.programming-demo", "Required");

            await Assertions.Expect(page.GetByText("/mem:business-memory", new PageGetByTextOptions { Exact = true })).ToBeVisibleAsync();
            await Assertions.Expect(page.GetByText("/mem:programming-memory", new PageGetByTextOptions { Exact = true })).ToBeVisibleAsync();
            await Assertions.Expect(page.GetByTestId("agents-catalog-memory-requirement-business-memory")).ToHaveValueAsync("Optional");
            await Assertions.Expect(page.GetByTestId("agents-catalog-memory-requirement-programming-memory")).ToHaveValueAsync("Required");

            await page.GetByTestId("agents-catalog-memory-mode").SelectOptionAsync("ExplicitDirective");
            await Assertions.Expect(page.GetByTestId("agents-catalog-memory-tools")).ToBeDisabledAsync();
            await page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = Path.Combine(screenshotRoot, "agent-memory-multiple-providers-explicit-desktop.png"),
                FullPage = false
            });

            await page.SetViewportSizeAsync(390, 900);
            var lastRemove = page.GetByTestId("agents-catalog-memory-remove-programming-memory");
            await lastRemove.ScrollIntoViewIfNeededAsync();
            await lastRemove.ClickAsync(new LocatorClickOptions
            {
                Trial = true
            });

            var bindings = page.GetByTestId("agents-catalog-memory-bindings");
            var hasHorizontalOverflow = await bindings.EvaluateAsync<bool>(
                "element => element.scrollWidth > element.clientWidth + 1");
            Assert.False(hasHorizontalOverflow);
            await AssertInsideViewportAsync(page.GetByTestId("agents-catalog-memory-up-business-memory"), 390);
            await AssertInsideViewportAsync(page.GetByTestId("agents-catalog-memory-down-business-memory"), 390);
            await AssertInsideViewportAsync(page.GetByTestId("agents-catalog-memory-remove-business-memory"), 390);
            await AssertInsideViewportAsync(page.GetByTestId("agents-catalog-memory-up-programming-memory"), 390);
            await AssertInsideViewportAsync(page.GetByTestId("agents-catalog-memory-down-programming-memory"), 390);
            await AssertInsideViewportAsync(lastRemove, 390);

            await page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = Path.Combine(screenshotRoot, "agent-memory-multiple-providers-explicit-mobile.png"),
                FullPage = false
            });
        }
        catch
        {
            await page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = Path.Combine(screenshotRoot, "agent-memory-multiple-providers-failure-state.png"),
                FullPage = true
            });
            throw;
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    private static async Task ConfigureHealthyDemoProvidersAsync(IPage page, string baseUrl)
    {
        var response = await page.GotoAsync($"{baseUrl}/memory");
        Assert.True(response?.Ok);
        await DismissDatabaseProfileDialogAsync(page);
        await page.GetByTestId("memory-ui-zero-provider").WaitForAsync();
        await page.GetByTestId("memory-ui-add-demo-providers").ClickAsync();
        await page.GetByTestId("memory-ui-provider-list").WaitForAsync();
        await page.GetByTestId("memory-provider-provider-programming-demo").ClickAsync();
        await page.GetByTestId("memory-ui-editor-health").SelectOptionAsync("Healthy");
        await page.GetByTestId("memory-ui-save-provider").ClickAsync();
        await Assertions.Expect(page.GetByText("Programming demo memory", new PageGetByTextOptions { Exact = true }).First).ToBeVisibleAsync();
    }

    private static async Task OpenMemorySettingsAsync(IPage page, string baseUrl)
    {
        var response = await page.GotoAsync($"{baseUrl}/agents?tab=agents");
        Assert.True(response?.Ok);
        await DismissDatabaseProfileDialogAsync(page);
        await page.GetByTestId("agents-catalog-new").WaitForAsync();
        await page.GetByTestId("agents-catalog-new").ClickAsync();
        await page.GetByRole(AriaRole.Tab, new PageGetByRoleOptions
        {
            Name = "Memory",
            Exact = true
        }).ClickAsync();
        await page.GetByTestId("agents-catalog-memory-access").WaitForAsync();
        await page.GetByTestId("agents-catalog-memory-mode").SelectOptionAsync("Automatic");
    }

    private static async Task AddBindingAsync(
        IPage page,
        string alias,
        string providerInstanceId,
        string requirement)
    {
        await page.GetByTestId("agents-catalog-memory-new-alias").FillAsync(alias);
        await page.GetByTestId("agents-catalog-memory-new-provider").SelectOptionAsync(providerInstanceId);
        await page.GetByTestId("agents-catalog-memory-new-requirement").SelectOptionAsync(requirement);
        await page.GetByTestId("agents-catalog-memory-add-binding").ClickAsync();
        await page.GetByTestId($"agents-catalog-memory-requirement-{alias}").WaitForAsync();
    }

    private static async Task DismissDatabaseProfileDialogAsync(IPage page)
    {
        var continueButton = page.GetByTestId("database-startup-continue");
        try
        {
            await continueButton.WaitForAsync(new LocatorWaitForOptions
            {
                Timeout = 1_500
            });
        }
        catch (TimeoutException)
        {
            return;
        }

        await continueButton.ClickAsync();
        await continueButton.WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Hidden,
            Timeout = 5_000
        });
    }

    private static async Task AssertInsideViewportAsync(ILocator locator, float viewportWidth)
    {
        var boundingBox = await locator.BoundingBoxAsync();
        Assert.NotNull(boundingBox);
        Assert.True(boundingBox.X >= 0, $"Expected '{locator}' to start inside the viewport.");
        Assert.True(
            boundingBox.X + boundingBox.Width <= viewportWidth,
            $"Expected '{locator}' to end inside the {viewportWidth}px viewport.");
    }
}
