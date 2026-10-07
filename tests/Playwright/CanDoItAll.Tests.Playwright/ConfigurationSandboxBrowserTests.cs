using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

[Trait("Category", "Playwright")]
[Trait("Category", "HostPlatform")]
public sealed class ConfigurationSandboxBrowserTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Actual_schema_fields_preserve_raw_drafts_references_and_assets(bool published) {
        await using var host = new ConfigurationSandboxHost();
        await host.StartAsync(published);
        using var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 1600, Height = 1000 } });
        var errors = new List<string>();
        var styles = new HashSet<string>();
        page.PageError += (_, message) => errors.Add(message);
        page.Console += (_, message) => {
            if (message.Type == "error") {
                errors.Add(message.Text);
            }
        };
        page.Response += (_, response) => {
            if (response.Status >= 400) {
                errors.Add($"HTTP {response.Status}: {new Uri(response.Url).AbsolutePath}");
            }
            if (response.Url.Contains(".css", StringComparison.Ordinal)) {
                styles.Add(response.Url);
            }
        };
        await page.GotoAsync(host.BaseUrl);
        await page.GetByTestId("configuration-ready").WaitForAsync(new() { State = WaitForSelectorState.Attached });
        var title = page.GetByTestId("configuration-field-title");
        await title.FillAsync("Žlutý draft 東京");
        await title.EvaluateAsync("e => e.setSelectionRange(4,4)");
        await page.GetByTestId("configuration-rerender").EvaluateAsync("e => e.click()");
        await Assertions.Expect(title).ToBeFocusedAsync();
        Assert.Equal(4, await title.EvaluateAsync<int>("e => e.selectionStart"));
        await Assertions.Expect(title).ToHaveValueAsync("Žlutý draft 東京");
        await page.GetByTestId("configuration-field-count").FillAsync(" 1e- ");
        await page.GetByTestId("configuration-field-payload").FillAsync("{ unfinished");
        await page.GetByTestId("configuration-field-enabled").CheckAsync();
        await page.GetByTestId("configuration-field-mode").SelectOptionAsync("write");
        var secret = page.GetByTestId("configuration-field-secret");
        await Assertions.Expect(secret).ToHaveValueAsync("33333333-3333-3333-3333-333333333333");
        await Assertions.Expect(secret).ToContainTextAsync("Unavailable secret");
        await page.GetByTestId("configuration-validate").ClickAsync();
        await Assertions.Expect(page.Locator(".workflow-canvas-error")).ToHaveCountAsync(2);
        await Assertions.Expect(page.GetByTestId("configuration-field-count")).ToHaveValueAsync(" 1e- ");
        await Assertions.Expect(page.GetByTestId("configuration-field-payload")).ToHaveValueAsync("{ unfinished");
        await Assertions.Expect(page.GetByTestId("configuration-field-enabled")).ToBeCheckedAsync();
        await secret.SelectOptionAsync("22222222-2222-2222-2222-222222222222");
        await Assertions.Expect(secret).ToHaveValueAsync("22222222-2222-2222-2222-222222222222");
        var output = Path.Combine(PlaywrightTestHostPaths.RepositoryRoot, "output", "playwright", "configuration-ui");
        Directory.CreateDirectory(output);
        await page.ScreenshotAsync(new() { Path = Path.Combine(output, published ? "published-fields.png" : "source-fields.png"), FullPage = true });
        await page.GetByTestId("configuration-acquire").ClickAsync();
        await Assertions.Expect(title).ToHaveValueAsync(string.Empty);
        await Assertions.Expect(page.Locator(".workflow-canvas-error")).ToHaveCountAsync(0);
        Assert.True(styles.Count >= 4);
        Assert.Empty(errors);
        Assert.DoesNotContain("canonical runtime database", host.Logs, StringComparison.OrdinalIgnoreCase);
    }
}
