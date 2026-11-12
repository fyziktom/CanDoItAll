using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

[Trait("Category", "Playwright")]
[Trait("Category", "HostPlatform")]
public sealed class StorageRecoverySandboxBrowserTests {
    private const string Placement = "1988b7b4c92c41cca946b30261ae28ae";
    private const string FollowUp = "72996c6e3d324ca49d60fd4dd5b9730f";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Real_renderer_handles_both_feeds_workflow_results_errors_and_keyboard_close(bool published) {
        await using var host = new StorageRecoverySandboxHost();
        await host.StartAsync(published);
        using var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        await using var context = await browser.NewContextAsync(new() { ViewportSize = new() { Width = 1600, Height = 1000 } });
        var page = await context.NewPageAsync();
        var errors = new List<string>();
        var assets = new HashSet<string>();
        page.PageError += (_, error) => errors.Add(error);
        page.Console += (_, message) => {
            if (message.Type == "error") {
                errors.Add(message.Text);
            }
        };
        page.Response += (_, response) => {
            if (response.Status >= 400) {
                errors.Add($"HTTP {response.Status}: {new Uri(response.Url).AbsolutePath}");
            }
            if (response.Url.Contains(".css", StringComparison.Ordinal) || response.Url.Contains(".woff", StringComparison.Ordinal)) {
                assets.Add(response.Url);
            }
        };
        await page.GotoAsync(host.BaseUrl);
        await page.GetByTestId("recovery-ready").WaitForAsync(new() { State = WaitForSelectorState.Attached });
        await Assertions.Expect(page.GetByText("Storage Recovery scenarios", new() { Exact = true })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByText("Simulation using the production renderer. No backend, provider, upload or model is registered.", new() { Exact = true })).ToBeVisibleAsync();
        await Open("WorkflowPrepared");
        await page.GetByTestId("storage-recovery-inspect-" + FollowUp).ClickAsync();
        var action = page.GetByTestId("storage-recovery-complete-workflow-asset");
        await action.FocusAsync();
        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(page.GetByTestId("storage-recovery-acknowledged")).ToContainTextAsync("ReceiptRecorded");
        await Assertions.Expect(action).ToHaveCountAsync(0);
        await Shot("workflow");
        await Close();
        await Assertions.Expect(page.GetByTestId("recovery-simulation-count")).ToContainTextAsync("1");

        await Open("WorkflowReceipt");
        await page.GetByTestId("storage-recovery-inspect-" + FollowUp).ClickAsync();
        await page.GetByTestId("storage-recovery-record-workflow-receipt").ClickAsync();
        await Assertions.Expect(page.GetByTestId("storage-recovery-acknowledged")).ToContainTextAsync("ReceiptRecorded");
        await Close();

        await Open("EmptyOwnerPage");
        await Assertions.Expect(page.GetByTestId("storage-recovery-inspect-" + FollowUp)).ToHaveCountAsync(0);
        await page.GetByTestId("storage-recovery-next-owners").ClickAsync();
        await Assertions.Expect(page.GetByTestId("storage-recovery-inspect-" + FollowUp)).ToBeVisibleAsync();
        await Close();
        await Assertions.Expect(page.GetByTestId("recovery-simulation-count")).ToContainTextAsync("0");

        await Open("RefreshFailure");
        await page.GetByTestId("storage-recovery-inspect-" + Placement).ClickAsync();
        await page.GetByTestId("storage-recovery-reconcile").ClickAsync();
        await Assertions.Expect(page.GetByTestId("storage-recovery-error")).ToContainTextAsync("acknowledged result");
        await Assertions.Expect(page.GetByTestId("storage-recovery-acknowledged")).ToContainTextAsync("Recorded");
        await Shot("refresh-failure");
        await page.GetByTestId("storage-recovery-refresh").FocusAsync();
        await page.Keyboard.PressAsync("Alt+Shift+O");
        await Assertions.Expect(page.GetByTestId("recovery-scenario-state")).ToContainTextAsync("Observation failure: False");
        await page.GetByTestId("storage-recovery-refresh").ClickAsync();
        await Assertions.Expect(page.GetByTestId("storage-recovery-error")).ToHaveCountAsync(0);
        await Close();
        await Assertions.Expect(page.GetByTestId("recovery-simulation-count")).ToContainTextAsync("1");

        await Open("ReadOnly");
        await page.GetByTestId("storage-recovery-inspect-" + Placement).ClickAsync();
        await Assertions.Expect(page.GetByTestId("storage-recovery-dialog")).ToContainTextAsync("Read-only access");
        await Assertions.Expect(page.GetByTestId("storage-recovery-reconcile")).ToHaveCountAsync(0);
        await Close();
        await Open("Denied");
        await Assertions.Expect(page.GetByTestId("storage-recovery-error")).ToContainTextAsync("does not allow");
        await Assertions.Expect(page.GetByTestId("storage-recovery-selected-intent")).ToHaveCountAsync(0);
        await Close();
        await Open("Loading", waitForReady: false);
        await Assertions.Expect(page.GetByTestId("storage-recovery-refresh")).ToBeDisabledAsync();
        await page.Keyboard.PressAsync("Alt+Shift+R");
        await Assertions.Expect(page.GetByTestId("storage-recovery-refresh")).ToBeEnabledAsync();
        await Close();
        Assert.True(assets.Count >= 4, "Shared parity CSS and material fonts must be loaded.");
        Assert.True(await page.EvaluateAsync<bool>("document.fonts.check('16px \"Material Symbols Rounded\"')"));
        Assert.Empty(errors);
        Assert.DoesNotContain("Unhandled exception", host.Logs, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("canonical runtime database", host.Logs, StringComparison.OrdinalIgnoreCase);

        async Task Open(string scenario, bool waitForReady = true) {
            await page.GetByTestId("recovery-scenario").SelectOptionAsync(scenario);
            await page.GetByTestId("recovery-open-primary").ClickAsync();
            await page.GetByTestId("storage-recovery-dialog").WaitForAsync();
            await page.WaitForFunctionAsync("() => document.querySelector('[data-testid=storage-recovery-dialog]')?.contains(document.activeElement)");
            if (waitForReady) {
                await Assertions.Expect(page.GetByTestId("storage-recovery-refresh")).ToBeEnabledAsync();
            }
            Assert.True(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth + 1"));
            var footer = page.GetByTestId("storage-recovery-close");
            await page.EvaluateAsync("() => new Promise(resolve => requestAnimationFrame(resolve))");
            await footer.FocusAsync();
            await Assertions.Expect(footer).ToBeFocusedAsync();
            await Assertions.Expect(footer).ToBeInViewportAsync();
        }
        async Task Close() {
            await page.Keyboard.PressAsync("Escape");
            await Assertions.Expect(page.GetByTestId("storage-recovery-dialog")).ToHaveCountAsync(0);
        }
        async Task Shot(string name) {
            var directory = Path.Combine(PlaywrightTestHostPaths.RepositoryRoot, "output", "playwright", "workspace-recovery-ui");
            Directory.CreateDirectory(directory);
            await page.ScreenshotAsync(new() { Path = Path.Combine(directory, (published ? "published-" : "source-") + name + ".png"), FullPage = true });
        }
    }
}
