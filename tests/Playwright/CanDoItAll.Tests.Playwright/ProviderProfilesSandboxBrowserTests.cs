using System.Text.Json;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

[Trait("Category", "Playwright")]
[Trait("Category", "HostPlatform")]
public sealed class ProviderProfilesSandboxBrowserTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Full_provider_renderer_and_assets_work_independently_on_large_desktop(bool published) {
        await using var host = new IndependentProviderSandboxHost(ProviderSandboxKind.Profiles);
        await host.StartAsync(published);
        using var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        await using var context = await browser.NewContextAsync(new() { ViewportSize = new() { Width = 1920, Height = 1080 } });
        var page = await context.NewPageAsync();
        var errors = new List<string>();
        var assets = new HashSet<string>();
        page.PageError += (_, error) => errors.Add(error);
        page.Response += (_, response) => {
            var path = new Uri(response.Url).AbsolutePath;
            if (response.Status >= 400) {
                errors.Add($"HTTP {response.Status}: {path}");
            }
            if (path.Contains(".css", StringComparison.Ordinal) || path.Contains(".woff", StringComparison.Ordinal) || path.Contains(".js", StringComparison.Ordinal)) {
                assets.Add(path);
            }
        };
        var directory = Path.Combine(PlaywrightTestHostPaths.RepositoryRoot, "output", "playwright", "provider-profiles-pp1",
            $"{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfffZ}-{Guid.NewGuid():N}", published ? "published" : "source");
        Directory.CreateDirectory(directory);
        try {
            await page.GotoAsync(host.BaseUrl);
            var root = page.GetByTestId("provider-sandbox");
            await Assertions.Expect(root).ToHaveAttributeAsync("data-interactive", "true");
            await Assertions.Expect(root.GetByRole(AriaRole.Tab)).ToHaveCountAsync(6);
            await root.GetByTestId("providers-name-input").FillAsync("Žluťoučký 東京 🧭");
            await Assertions.Expect(root.GetByTestId("providers-name-input")).ToBeFocusedAsync();
            await root.GetByTestId("providers-refresh").ClickAsync();
            await Assertions.Expect(root.GetByTestId("providers-name-input")).ToHaveValueAsync("Žluťoučký 東京 🧭");
            await Shot("connection");
            await Tab("prices");
            await root.GetByTestId("provider-pricing-input-0").FillAsync("invalid");
            await Tab("runtime");
            await root.GetByTestId("providers-config-json").FillAsync("{invalid");
            await root.GetByTestId("providers-notes").FillAsync("Unblurred notes");
            await Shot("invalid-runtime");
            await Tab("prices");
            await Assertions.Expect(root.GetByTestId("provider-pricing-input-0")).ToHaveValueAsync("invalid");
            await root.GetByTestId("provider-pricing-input-0").FillAsync("2.5");
            await Assertions.Expect(root.GetByTestId("provider-pricing-long-output-0")).ToHaveValueAsync("7");
            Assert.True(await root.GetByTestId("provider-pricing-table").EvaluateAsync<bool>("element => element.scrollWidth > element.clientWidth"));
            await root.GetByTestId("provider-pricing-long-output-0").FocusAsync();
            await Shot("prices-scrolled");
            await Tab("runtime");
            await root.GetByTestId("providers-config-json").FillAsync("{\"extension\":{\"retained\":true}}");
            await Assertions.Expect(root.GetByTestId("providers-notes")).ToHaveValueAsync("Unblurred notes");
            await Tab("thinking");
            await root.GetByTestId("provider-thinking-search").FillAsync("qwen3:8b");
            await root.GetByRole(AriaRole.Button, new() { Name = "Edit thinking for qwen3:8b", Exact = true }).ClickAsync();
            var dialog = page.GetByTestId("provider-thinking-dialog");
            await dialog.GetByTestId("thinking-automatic").UncheckAsync();
            await dialog.GetByTestId("thinking-supported").UncheckAsync();
            await Assertions.Expect(dialog.GetByTestId("thinking-apply")).ToBeInViewportAsync(new() { Ratio = 1 });
            await Shot("thinking-dialog");
            await dialog.GetByTestId("thinking-apply").ClickAsync();
            await Assertions.Expect(dialog).ToHaveCountAsync(0);
            await Assertions.Expect(root.GetByTestId("providers-save")).ToBeInViewportAsync(new() { Ratio = 1 });
            await root.GetByTestId("providers-save").ClickAsync();
            await Assertions.Expect(root.GetByTestId("fixture-outcome")).ToContainTextAsync("saved");
            await root.GetByTestId("open-second-editor").ClickAsync();
            var second = page.GetByTestId("provider-second-editor");
            await second.GetByTestId("providers-name-input").FillAsync("Independent second draft");
            await page.Keyboard.PressAsync("Escape");
            await Assertions.Expect(second).ToHaveCountAsync(0);
            await Tab("connection");
            await Assertions.Expect(root.GetByTestId("providers-name-input")).ToHaveValueAsync("Žluťoučký 東京 🧭");
            foreach (var scenario in new[] { "Loading", "Empty", "Large", "Missing", "PartialSecrets", "ReadOnly", "Warning", "Unknown" }) {
                await root.GetByTestId("provider-scenario").SelectOptionAsync(scenario);
                await Assertions.Expect(root).ToHaveAttributeAsync("data-scenario", scenario);
                if (scenario == "Loading") {
                    await Assertions.Expect(root.GetByText("Loading provider catalog", new() { Exact = true })).ToBeVisibleAsync();
                    await root.GetByTestId("providers-refresh").ClickAsync();
                }
                if (scenario == "Large") {
                    await root.GetByTestId("providers-search").FillAsync("Provider 80");
                    await Assertions.Expect(root.GetByTestId("providers-filter-count")).ToHaveTextAsync("1 / 80");
                }
                if (scenario == "Missing") {
                    await root.GetByTestId("providers-load-retry").ClickAsync();
                }
                if (scenario == "PartialSecrets") {
                    await Assertions.Expect(root.GetByTestId("providers-secret-warning")).ToBeVisibleAsync();
                    await Assertions.Expect(root.GetByTestId("providers-save")).ToBeEnabledAsync();
                }
                if (scenario == "ReadOnly") {
                    await Assertions.Expect(root.GetByTestId("providers-name-input")).ToBeDisabledAsync();
                    await Assertions.Expect(root.GetByTestId("providers-save")).ToHaveCountAsync(0);
                }
                if (scenario == "Warning") {
                    await root.GetByTestId("providers-reconcile").ClickAsync();
                    await Assertions.Expect(root.GetByTestId("providers-save")).ToBeEnabledAsync();
                }
                if (scenario == "Unknown") {
                    await root.GetByTestId("providers-verify").ClickAsync();
                    await Assertions.Expect(root.GetByTestId("providers-retry-verified")).ToBeVisibleAsync();
                }
                await Shot(scenario);
            }
            await root.GetByTestId("provider-scenario").SelectOptionAsync("Held");
            await Assertions.Expect(root).ToHaveAttributeAsync("data-scenario", "Held");
            await root.GetByTestId("providers-name-input").FillAsync("Captured fixture");
            await root.GetByTestId("providers-save").ClickAsync();
            await Assertions.Expect(root.GetByTestId("providers-save")).ToBeDisabledAsync();
            await root.GetByTestId("providers-name-input").FillAsync("Later typing");
            await root.GetByTestId("release-held").ClickAsync();
            await Assertions.Expect(root.GetByTestId("providers-save")).ToBeEnabledAsync();
            await Assertions.Expect(root.GetByTestId("providers-name-input")).ToHaveValueAsync("Later typing");
            Assert.True(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth + 1"));
            Assert.True(await page.EvaluateAsync<bool>("document.fonts.check('16px \"Material Symbols Rounded\"')"));
            Assert.Contains(assets, path => path.Contains("Providers.UiSandbox", StringComparison.Ordinal) && path.Contains(".css", StringComparison.Ordinal));
            Assert.Contains(assets, path => path.Contains("BaseLib", StringComparison.Ordinal));
            Assert.Empty(errors);
            await File.WriteAllTextAsync(Path.Combine(directory, "assets.json"), JsonSerializer.Serialize(assets.Order()));
        } catch {
            await page.ScreenshotAsync(new() { Path = Path.Combine(directory, "failure.png"), FullPage = true });
            throw;
        }
        Assert.DoesNotContain("canonical runtime database", host.Logs, StringComparison.OrdinalIgnoreCase);
        Task Tab(string section) => page.GetByTestId("provider-sandbox").GetByTestId($"provider-editor-tab-{section}").ClickAsync();
        Task Shot(string name) => page.ScreenshotAsync(new() { Path = Path.Combine(directory, name + ".png") });
    }
}
