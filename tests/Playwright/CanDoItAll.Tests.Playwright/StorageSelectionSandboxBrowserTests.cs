using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

[Trait("Category", "Playwright")]
[Trait("Category", "HostPlatform")]
public sealed class StorageSelectionSandboxBrowserTests {
    private const string Alpha = "11111111111111111111111111111111";
    private const string Disabled = "22222222222222222222222222222222";
    private const string ReadOnly = "33333333333333333333333333333333";
    private const string Missing = "44444444444444444444444444444444";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Real_nested_picker_handles_policy_large_catalog_failure_and_late_owned_reads(bool published) {
        await using var host = new StorageSelectionSandboxHost();
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
        await page.GetByTestId("selection-open-primary").ClickAsync();
        await Assertions.Expect(page.GetByTestId("selection-parent-dialog-primary").GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true })).ToBeFocusedAsync();
        await Assertions.Expect(page.GetByTestId("selection-parent-counts")).ToContainTextAsync("owner reads: 0");
        var notes = page.GetByTestId("selection-primary-notes");
        await notes.FillAsync("Unblurred parent draft");
        await page.GetByTestId("selection-primary-choose").ClickAsync();
        await Assertions.Expect(page.GetByTestId("selection-primary-dialog-shell").GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true })).ToBeFocusedAsync();
        var search = page.GetByTestId("selection-primary-dialog-picker-search");
        await search.FillAsync("Customer documents");
        await Assertions.Expect(page.GetByTestId("selection-primary-dialog-option-" + ReadOnly)).ToHaveCountAsync(0);
        await Assertions.Expect(search).ToHaveValueAsync("Customer documents");
        await search.EvaluateAsync("element => element.setSelectionRange(8,8)");
        await Assertions.Expect(search).ToBeFocusedAsync();
        Assert.Equal(8, await search.EvaluateAsync<int>("element => element.selectionStart"));
        await page.Keyboard.PressAsync("ArrowLeft");
        await Assertions.Expect(search).ToBeFocusedAsync();
        Assert.Equal(7, await search.EvaluateAsync<int>("element => element.selectionStart"));
        var option = page.GetByTestId("selection-primary-dialog-option-" + Alpha);
        await option.FocusAsync();
        await option.PressAsync("Space");
        await Assertions.Expect(option).ToHaveAttributeAsync("aria-pressed", "true");
        await Shot("nested");
        await page.GetByTestId("selection-primary-dialog-apply").ClickAsync();
        await Assertions.Expect(notes).ToHaveValueAsync("Unblurred parent draft");
        await Assertions.Expect(page.GetByTestId("selection-parent-counts")).ToContainTextAsync("Applied lists: 1; owner reads: 1");
        await page.GetByTestId("selection-allow-all").CheckAsync();
        await Assertions.Expect(page.GetByTestId("selection-primary-choose")).ToBeDisabledAsync();
        await Assertions.Expect(page.GetByTestId("selection-primary-selected-row-" + Alpha)).ToBeVisibleAsync();
        await page.GetByTestId("selection-allow-all").UncheckAsync();
        await page.GetByTestId("selection-primary-choose").ClickAsync();
        await page.GetByTestId("selection-primary-dialog-option-" + ReadOnly).ClickAsync();
        await page.Keyboard.PressAsync("Escape");
        await Assertions.Expect(page.GetByTestId("selection-primary-dialog-shell")).ToHaveCountAsync(0);
        await Assertions.Expect(page.GetByTestId("selection-primary-parent")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByTestId("selection-primary-selected-row-" + ReadOnly)).ToHaveCountAsync(0);
        await page.GetByTestId("selection-primary-choose").ClickAsync();
        await page.GetByTestId("selection-primary-dialog-option-" + ReadOnly).ClickAsync();
        await Assertions.Expect(page.GetByTestId("selection-primary-dialog-option-" + ReadOnly)).ToHaveAttributeAsync("aria-pressed", "true");
        await page.GetByTestId("selection-primary-dialog-shell").Locator(".cda-dialog__backdrop").ClickAsync(new() { Position = new() { X = 5, Y = 5 } });
        await Assertions.Expect(page.GetByTestId("selection-primary-dialog-shell")).ToHaveCountAsync(0);
        await Assertions.Expect(page.GetByTestId("selection-primary-parent")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByTestId("selection-primary-selected-row-" + ReadOnly)).ToHaveCountAsync(0);
        await Assertions.Expect(page.GetByTestId("selection-parent-counts")).ToContainTextAsync("Applied lists: 1;");
        await page.GetByTestId("selection-primary-close").ClickAsync();

        await page.GetByTestId("selection-scenario").SelectOptionAsync("Large");
        await page.GetByTestId("selection-reset").ClickAsync();
        await page.GetByTestId("selection-open-saved").ClickAsync();
        await page.GetByTestId("selection-primary-choose").ClickAsync();
        var results = page.GetByTestId("selection-primary-dialog-picker-results");
        await page.GetByTestId("selection-primary-dialog-option-" + Alpha).WaitForAsync();
        Assert.True(await results.EvaluateAsync<bool>("element => element.scrollHeight > element.clientHeight && element.clientHeight > 100 && element.clientHeight < 750"));
        Assert.True(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth + 1"));
        await Shot("large");
        await page.GetByTestId("selection-primary-dialog-option-" + Disabled).ClickAsync();
        await Assertions.Expect(page.GetByTestId("selection-primary-dialog-option-" + Disabled)).ToBeDisabledAsync();
        await page.GetByTestId("selection-primary-dialog-option-" + Missing).ClickAsync();
        await Assertions.Expect(page.GetByTestId("selection-primary-dialog-option-" + Missing)).ToHaveCountAsync(0);
        await page.GetByTestId("selection-primary-dialog-option-" + ReadOnly).ClickAsync();
        await Assertions.Expect(page.GetByTestId("selection-primary-dialog-option-" + ReadOnly)).ToHaveAttributeAsync("aria-pressed", "true");
        await page.GetByTestId("selection-primary-dialog-apply").ClickAsync();
        await Assertions.Expect(page.GetByTestId("selection-primary-dialog-shell")).ToHaveCountAsync(0);
        await Shot("large-applied");
        await Assertions.Expect(page.GetByTestId("selection-primary-selected-row-" + ReadOnly)).ToContainTextAsync("Read only");
        await Assertions.Expect(page.GetByTestId("selection-primary-selected-row-" + Disabled)).ToHaveCountAsync(0);
        await page.GetByTestId("selection-primary-close").ClickAsync();

        await page.GetByTestId("selection-scenario").SelectOptionAsync("Failure");
        await page.GetByTestId("selection-reset").ClickAsync();
        await page.GetByTestId("selection-open-saved").ClickAsync();
        await page.GetByTestId("selection-primary-choose").ClickAsync();
        await page.GetByTestId("selection-primary-dialog-retry").WaitForAsync();
        await Assertions.Expect(page.GetByTestId("selection-primary-dialog-apply")).ToBeDisabledAsync();
        await Assertions.Expect(page.GetByTestId("selection-primary-dialog-shell")).Not.ToContainTextAsync("Private scenario diagnostic");
        await Shot("failure");
        await page.Keyboard.PressAsync("Alt+Shift+C");
        await page.GetByTestId("selection-primary-dialog-retry").ClickAsync();
        await Assertions.Expect(page.GetByTestId("selection-primary-dialog-option-" + Missing)).ToHaveAttributeAsync("aria-pressed", "true");
        await page.GetByTestId("selection-primary-dialog-cancel").ClickAsync();
        await page.GetByTestId("selection-primary-close").ClickAsync();

        await page.GetByTestId("selection-hold").CheckAsync();
        await page.GetByTestId("selection-ignore-cancellation").CheckAsync();
        await page.GetByTestId("selection-open-primary").ClickAsync();
        await page.GetByTestId("selection-primary-choose").ClickAsync();
        await Assertions.Expect(page.GetByTestId("selection-primary-dialog-apply")).ToBeDisabledAsync();
        await page.Keyboard.PressAsync("Alt+Shift+B");
        await page.GetByTestId("selection-secondary-choose").ClickAsync();
        await page.Keyboard.PressAsync("Alt+Shift+A");
        await Assertions.Expect(page.GetByTestId("selection-parent-dialog-primary")).ToHaveCountAsync(0);
        await Assertions.Expect(page.GetByTestId("selection-primary-dialog-shell")).ToHaveCountAsync(0);
        await Assertions.Expect(page.GetByTestId("selection-secondary-dialog-apply")).ToBeDisabledAsync();
        await page.Keyboard.PressAsync("Alt+Shift+R");
        await Assertions.Expect(page.GetByTestId("selection-secondary-dialog-apply")).ToBeDisabledAsync();
        await page.Keyboard.PressAsync("Alt+Shift+R");
        await page.Keyboard.PressAsync("Alt+Shift+R");
        await page.GetByTestId("selection-secondary-dialog-option-" + Alpha).WaitForAsync();
        await Shot("two-owners");
        await page.GetByTestId("selection-secondary-dialog-cancel").ClickAsync();
        await Assertions.Expect(page.GetByTestId("selection-secondary-parent")).ToBeVisibleAsync();
        await page.GetByTestId("selection-secondary-close").ClickAsync();
        Assert.True(await page.EvaluateAsync<bool>("document.fonts.check('16px \"Material Symbols Rounded\"')"));
        Assert.True(assets.Count >= 4, "Real shared styles and fonts must be loaded.");
        await context.GrantPermissionsAsync(["clipboard-read", "clipboard-write"]);
        await page.EvaluateAsync("async () => { const module = await import('/_content/CanDoItAll.Components.BaseLib/js/copyButton.js'); await module.copyText('Harmless selection asset proof'); }");
        Assert.Equal("Harmless selection asset proof", await page.EvaluateAsync<string>("navigator.clipboard.readText()"));
        Assert.Empty(errors);
        Assert.DoesNotContain("Unhandled exception", host.Logs, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("canonical runtime database", host.Logs, StringComparison.OrdinalIgnoreCase);

        async Task Shot(string name) {
            var directory = Path.Combine(PlaywrightTestHostPaths.RepositoryRoot, "output", "playwright", "workspace-storage-selection-ui");
            Directory.CreateDirectory(directory);
            await page.ScreenshotAsync(new() { Path = Path.Combine(directory, (published ? "published-" : "source-") + name + ".png"), FullPage = true });
        }
    }
}
