using System.Text.Json;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

public sealed class Sb04GeneratedAppBrowserValidationTests
{
    private const string UrlEnvironmentVariable = "CANDOITALL_SB04_BROWSER_URL";
    private const string OutputRootEnvironmentVariable = "CANDOITALL_SB04_BROWSER_OUTPUT_ROOT";
    private const string ScenarioKeyEnvironmentVariable = "CANDOITALL_SB04_BROWSER_SCENARIO_KEY";
    private const string InteractiveControlSelector = "button:not([disabled]):visible, input:not([type=hidden]):not([disabled]):visible, select:not([disabled]):visible, textarea:not([disabled]):visible, [role=button]:visible, a[href]:visible";
    private const string InteractiveControlCssSelector = "button:not([disabled]), input:not([type=hidden]):not([disabled]), select:not([disabled]), textarea:not([disabled]), [role=button], a[href]";

    [Fact]
    [Trait("Category", "SB04")]
    public async Task Generated_app_supports_desktop_and_mobile_browser_validation()
    {
        var url = Environment.GetEnvironmentVariable(UrlEnvironmentVariable);
        var outputRoot = Environment.GetEnvironmentVariable(OutputRootEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(outputRoot))
        {
            return;
        }

        var scenarioKey = Environment.GetEnvironmentVariable(ScenarioKeyEnvironmentVariable) ?? "unknown-scenario";
        Directory.CreateDirectory(outputRoot);
        Directory.CreateDirectory(Path.Combine(outputRoot, "screenshots"));

        using var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });

        var results = new List<BrowserViewportValidationResult>();
        try
        {
            foreach (var viewport in BrowserViewportSpec.All)
            {
                results.Add(await ValidateViewportAsync(browser, url, outputRoot, scenarioKey, viewport));
                await WriteSummaryAsync(outputRoot, results);
            }
        }
        catch
        {
            await WriteSummaryAsync(outputRoot, results);
            throw;
        }
    }

    private static async Task<BrowserViewportValidationResult> ValidateViewportAsync(
        IBrowser browser,
        string url,
        string outputRoot,
        string scenarioKey,
        BrowserViewportSpec viewport)
    {
        var consoleMessages = new List<string>();
        var pageErrors = new List<string>();
        var networkResponses = new List<string>();
        var failedResponses = new List<string>();
        var failedRequests = new List<string>();
        await using var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true,
            IsMobile = viewport.IsMobile,
            ViewportSize = new ViewportSize
            {
                Width = viewport.Width,
                Height = viewport.Height
            }
        });
        var page = await context.NewPageAsync();
        page.Console += (_, message) => consoleMessages.Add($"{message.Type}: {message.Text}");
        page.PageError += (_, error) => pageErrors.Add(error);
        page.RequestFailed += (_, request) => failedRequests.Add($"{request.Failure} {request.Url}");
        page.Response += (_, response) =>
        {
            networkResponses.Add($"{response.Status} {response.Url}");
            if (response.Status >= 400)
            {
                failedResponses.Add($"{response.Status} {response.Url}");
            }
        };

        var response = await page.GotoAsync(url, new PageGotoOptions
        {
            WaitUntil = WaitUntilState.Load,
            Timeout = 45_000
        });
        Assert.NotNull(response);
        Assert.True(response!.Status is >= 200 and < 400, $"Expected generated app route to return 2xx/3xx, got {(int)response.Status} for {url}.");
        try
        {
            await WaitForRenderedApplicationAsync(page);
        }
        catch
        {
            await WriteBrowserDiagnosticFilesAsync(outputRoot, viewport, consoleMessages, pageErrors, networkResponses, failedResponses, failedRequests);
            await page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = Path.Combine(outputRoot, "screenshots", $"{viewport.Name}-render-timeout.png"),
                FullPage = false
            });
            await File.WriteAllTextAsync(
                Path.Combine(outputRoot, $"browser-dom-{viewport.Name}-render-timeout.html"),
                await page.ContentAsync());
            throw;
        }

        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(outputRoot, "screenshots", $"{viewport.Name}-initial.png"),
            FullPage = false
        });
        await WriteBrowserDiagnosticFilesAsync(outputRoot, viewport, consoleMessages, pageErrors, networkResponses, failedResponses, failedRequests);

        var interactiveControls = page.Locator(InteractiveControlSelector);
        var interactiveCount = await interactiveControls.CountAsync();
        Assert.True(interactiveCount > 0, $"Expected generated app '{scenarioKey}' to expose at least one interactive browser control for {viewport.Name} validation.");

        var interactionSucceeded = true;
        string? interactionFailure = null;
        try
        {
            await interactiveControls.First.ClickAsync(new LocatorClickOptions { Timeout = 10_000 });
            await page.WaitForTimeoutAsync(500);
        }
        catch (PlaywrightException ex)
        {
            interactionSucceeded = false;
            interactionFailure = ex.Message;
        }

        await WriteBrowserDiagnosticFilesAsync(outputRoot, viewport, consoleMessages, pageErrors, networkResponses, failedResponses, failedRequests);
        Assert.True(interactionSucceeded, $"Expected first generated app control to be clickable for {viewport.Name}. {interactionFailure}");
        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(outputRoot, "screenshots", $"{viewport.Name}-after-interaction.png"),
            FullPage = false
        });

        await page.ReloadAsync(new PageReloadOptions
        {
            WaitUntil = WaitUntilState.Load,
            Timeout = 45_000
        });
        await WaitForRenderedApplicationAsync(page);
        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(outputRoot, "screenshots", $"{viewport.Name}-after-reload.png"),
            FullPage = false
        });

        var bodyText = await page.Locator("body").InnerTextAsync(new LocatorInnerTextOptions { Timeout = 15_000 });
        await WriteBrowserDiagnosticFilesAsync(outputRoot, viewport, consoleMessages, pageErrors, networkResponses, failedResponses, failedRequests);

        var consoleErrors = consoleMessages
            .Where(message => message.StartsWith("error:", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var ignoredConsoleErrors = consoleErrors
            .Where(IsIgnoredGeneratedAppBrowserError)
            .ToArray();
        var blockingConsoleErrors = consoleErrors
            .Where(message => !IsIgnoredGeneratedAppBrowserError(message))
            .ToArray();
        var ignoredPageErrors = pageErrors
            .Where(IsIgnoredGeneratedAppBrowserError)
            .ToArray();
        var blockingPageErrors = pageErrors
            .Where(error => !IsIgnoredGeneratedAppBrowserError(error))
            .ToArray();
        Assert.True(blockingConsoleErrors.Length == 0, $"Generated app '{scenarioKey}' emitted console errors for {viewport.Name}:{Environment.NewLine}{string.Join(Environment.NewLine, blockingConsoleErrors)}");
        Assert.True(blockingPageErrors.Length == 0, $"Generated app '{scenarioKey}' emitted page errors for {viewport.Name}:{Environment.NewLine}{string.Join(Environment.NewLine, blockingPageErrors)}");
        Assert.True(failedResponses.Count == 0, $"Generated app '{scenarioKey}' returned failed browser responses for {viewport.Name}:{Environment.NewLine}{string.Join(Environment.NewLine, failedResponses)}");
        Assert.True(failedRequests.Count == 0, $"Generated app '{scenarioKey}' had failed browser requests for {viewport.Name}:{Environment.NewLine}{string.Join(Environment.NewLine, failedRequests)}");
        Assert.DoesNotContain("An unhandled error has occurred", bodyText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Loading", bodyText, StringComparison.OrdinalIgnoreCase);

        return new BrowserViewportValidationResult(
            scenarioKey,
            viewport.Name,
            viewport.Width,
            viewport.Height,
            page.Url,
            await page.TitleAsync(),
            bodyText.Length,
            interactiveCount,
            consoleMessages.Count,
            consoleErrors.Length,
            ignoredConsoleErrors.Length,
            blockingConsoleErrors.Length,
            pageErrors.Count,
            ignoredPageErrors.Length,
            blockingPageErrors.Length,
            failedResponses.Count,
            failedRequests.Count,
            DateTimeOffset.UtcNow);
    }

    private static async Task WriteSummaryAsync(
        string outputRoot,
        IReadOnlyCollection<BrowserViewportValidationResult> results)
    {
        var summaryPath = Path.Combine(outputRoot, "browser-validation-summary.json");
        await File.WriteAllTextAsync(
            summaryPath,
            JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static async Task WriteBrowserDiagnosticFilesAsync(
        string outputRoot,
        BrowserViewportSpec viewport,
        IEnumerable<string> consoleMessages,
        IEnumerable<string> pageErrors,
        IEnumerable<string> networkResponses,
        IEnumerable<string> failedResponses,
        IEnumerable<string> failedRequests)
    {
        await File.WriteAllLinesAsync(Path.Combine(outputRoot, $"browser-console-{viewport.Name}.txt"), consoleMessages);
        await File.WriteAllLinesAsync(Path.Combine(outputRoot, $"browser-page-errors-{viewport.Name}.txt"), pageErrors);
        await File.WriteAllLinesAsync(Path.Combine(outputRoot, $"browser-network-{viewport.Name}.txt"), networkResponses);
        await File.WriteAllLinesAsync(Path.Combine(outputRoot, $"browser-failed-responses-{viewport.Name}.txt"), failedResponses);
        await File.WriteAllLinesAsync(Path.Combine(outputRoot, $"browser-failed-requests-{viewport.Name}.txt"), failedRequests);
    }

    private static bool IsIgnoredGeneratedAppBrowserError(string message) =>
        message.Contains("Failed to register a ServiceWorker", StringComparison.OrdinalIgnoreCase)
        || message.Contains("ServiceWorker script evaluation failed", StringComparison.OrdinalIgnoreCase);

    private static async Task WaitForRenderedApplicationAsync(IPage page)
    {
        await page.WaitForFunctionAsync(
            """
            selector => {
                const hasVisibleControl = Array
                    .from(document.querySelectorAll(selector))
                    .some(element => {
                        const style = window.getComputedStyle(element);
                        const bounds = element.getBoundingClientRect();
                        return style.visibility !== 'hidden'
                            && style.display !== 'none'
                            && bounds.width > 0
                            && bounds.height > 0;
                    });
                const appText = document.getElementById('app')?.innerText ?? '';
                return hasVisibleControl && !/Loading/i.test(appText);
            }
            """,
            InteractiveControlCssSelector,
            new PageWaitForFunctionOptions { Timeout = 45_000 });
    }

    private sealed record BrowserViewportSpec(
        string Name,
        int Width,
        int Height,
        bool IsMobile)
    {
        public static IReadOnlyList<BrowserViewportSpec> All { get; } =
        [
            new("desktop", 1440, 960, false),
            new("mobile", 390, 844, true)
        ];
    }

    private sealed record BrowserViewportValidationResult(
        string ScenarioKey,
        string Viewport,
        int Width,
        int Height,
        string FinalUrl,
        string Title,
        int BodyTextLength,
        int InteractiveControlCount,
        int ConsoleMessageCount,
        int ConsoleErrorCount,
        int IgnoredConsoleErrorCount,
        int BlockingConsoleErrorCount,
        int PageErrorCount,
        int IgnoredPageErrorCount,
        int BlockingPageErrorCount,
        int FailedResponseCount,
        int FailedRequestCount,
        DateTimeOffset CapturedAtUtc);
}
