using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

[Trait("Category", "Playwright")]
[Trait("Category", "HostPlatform")]
public sealed class DataSourcesSandboxBrowserTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Profile_raw_fields_transfer_lifetime_and_assets_work_without_a_backend(bool published) {
        await using var host = new DataSourcesSandboxHost();
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
        await page.GetByTestId("data-sources-ready").WaitForAsync(new() { State = WaitForSelectorState.Attached });
        await Assertions.Expect(page.GetByText("Data Sources scenarios", new() { Exact = true })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByText("Simulation using the actual profile and transfer renderers. No database, driver, credential store or process is registered.", new() { Exact = true })).ToBeVisibleAsync();
        var create = page.GetByTestId("database-profile-new-postgres");
        await Assertions.Expect(create).ToBeInViewportAsync(new() { Ratio = 1 });
        Assert.True(await create.EvaluateAsync<bool>("e => e.scrollWidth <= e.clientWidth + 1"));
        var name = page.GetByTestId("database-profile-name");
        await name.FillAsync("Žlutý draft 東京");
        await name.PressAsync("Tab");
        await name.FocusAsync();
        await name.EvaluateAsync("e => e.setSelectionRange(3,3)");
        await page.GetByTestId("database-profile-refresh").EvaluateAsync("e => e.click()");
        await Assertions.Expect(name).ToHaveValueAsync("Žlutý draft 東京");
        await Assertions.Expect(name).ToBeFocusedAsync();
        Assert.Equal(3, await name.EvaluateAsync<int>("e => e.selectionStart"));
        var port = page.GetByTestId("database-profile-postgres-port");
        await port.FillAsync(string.Empty);
        await page.GetByTestId("database-profile-row-20000000000000000000000000000001").ClickAsync();
        await Assertions.Expect(port).ToHaveValueAsync(string.Empty);
        await page.GetByTestId("database-profile-save").ClickAsync();
        await Assertions.Expect(page.GetByTestId("data-sources-command-count")).ToContainTextAsync("0");
        var save = page.GetByTestId("database-profile-save");
        await Assertions.Expect(save).ToBeInViewportAsync(new() { Ratio = 1 });
        Assert.True(await save.EvaluateAsync<bool>("e => e.scrollWidth <= e.clientWidth + 1"));
        await Shot("raw-fields");

        await Scenario("PartialTransfer");
        await page.GetByTestId("database-profile-row-20000000000000000000000000000002").ClickAsync();
        await page.GetByTestId("database-profile-transfer-settings").ClickAsync();
        var dialog = page.GetByTestId("database-transfer-dialog");
        await page.WaitForFunctionAsync("() => document.querySelector('[data-testid=database-transfer-dialog]')?.contains(document.activeElement)");
        await Assertions.Expect(dialog.GetByTestId("database-transfer-apply")).ToBeDisabledAsync();
        await dialog.GetByTestId("database-transfer-item-workspace-default-provider").Locator("input").CheckAsync();
        await dialog.GetByTestId("database-transfer-item-ai-agents").Locator("input").CheckAsync();
        var apply = dialog.GetByTestId("database-transfer-apply");
        await apply.FocusAsync();
        await Assertions.Expect(apply).ToBeInViewportAsync();
        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(dialog.GetByTestId("database-transfer-result")).ToContainTextAsync("outcomes retained");
        await Assertions.Expect(dialog.GetByTestId("database-transfer-group-result-workspace-default-provider")).ToContainTextAsync("Copied one");
        await Assertions.Expect(dialog.GetByTestId("database-transfer-group-result-ai-agents")).ToContainTextAsync("Later group failed");
        await Shot("partial-groups");
        await page.Keyboard.PressAsync("Escape");
        await Assertions.Expect(dialog).ToHaveCountAsync(0);
        await page.GetByTestId("data-sources-reopen").ClickAsync();
        await Assertions.Expect(page.GetByTestId("database-operation-unknown")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByTestId("database-profile-save")).ToBeDisabledAsync();

        await Scenario("DelayedWrite");
        await page.GetByTestId("database-profile-save").ClickAsync();
        await Assertions.Expect(page.GetByTestId("database-operation-pending")).ToBeVisibleAsync();
        await page.GetByTestId("data-sources-reopen").ClickAsync();
        await Assertions.Expect(page.GetByTestId("database-profile-save")).ToBeDisabledAsync();
        await page.GetByTestId("data-sources-release").ClickAsync();
        await Assertions.Expect(page.GetByTestId("database-profile-save")).ToBeEnabledAsync();
        await Assertions.Expect(page.GetByTestId("data-sources-command-count")).ToContainTextAsync("1");
        await Scenario("Locked");
        await Assertions.Expect(page.GetByTestId("database-profile-new-postgres")).ToBeDisabledAsync();
        await Assertions.Expect(page.GetByTestId("database-profile-save")).ToBeDisabledAsync();
        await Scenario("PendingRestart");
        await Assertions.Expect(page.GetByTestId("database-data-sources-summary")).ToContainTextAsync("Pending restart");
        await Scenario("Unavailable");
        await Assertions.Expect(page.GetByTestId("database-profile-read-error").First).ToBeVisibleAsync();
        Assert.True(assets.Count >= 4);
        Assert.True(await page.EvaluateAsync<bool>("document.fonts.check('16px \"Material Symbols Rounded\"')"));
        Assert.Empty(errors);
        Assert.DoesNotContain("canonical runtime database", host.Logs, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Unhandled exception", host.Logs, StringComparison.OrdinalIgnoreCase);

        Task Scenario(string scenario) => page.GetByTestId("data-sources-scenario").SelectOptionAsync(scenario);
        async Task Shot(string name) {
            var directory = Path.Combine(PlaywrightTestHostPaths.RepositoryRoot, "output", "playwright", "workspace-data-sources-ui");
            Directory.CreateDirectory(directory);
            await page.ScreenshotAsync(new() { Path = Path.Combine(directory, (published ? "published-" : "source-") + name + ".png"), FullPage = true });
        }
    }
}
