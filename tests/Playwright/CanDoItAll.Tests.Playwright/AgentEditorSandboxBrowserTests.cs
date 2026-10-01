using System.Text.Json;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

[Trait("Category", "Playwright")]
[Trait("Category", "HostPlatform")]
public sealed class AgentEditorSandboxBrowserTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Core_editor_runs_independently_with_real_widgets_assets_and_two_lifetimes(bool published) {
        await using var host = new AgentEditorSandboxHost();
        await host.StartAsync(published);
        using var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true, Channel = "chromium" });
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
        var directory = Path.Combine(PlaywrightTestHostPaths.RepositoryRoot, "output", "playwright", "agent-editor-a1",
            $"{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfffZ}-{Guid.NewGuid():N}", published ? "published" : "source");
        Directory.CreateDirectory(directory);
        try {
            await page.GotoAsync(host.BaseUrl);
            await Assertions.Expect(page.GetByTestId("editor-sandbox")).ToHaveAttributeAsync("data-interactive", "true");
            var editor = page.GetByTestId("editor-a");
            await Assertions.Expect(editor.GetByRole(AriaRole.Tab)).ToHaveCountAsync(10);
            var name = editor.GetByTestId("agents-catalog-name");
            await name.FillAsync("Žluťoučký 東京 🧭 before blur");
            await name.PressAsync("Enter");
            await Assertions.Expect(editor.GetByTestId("scenario-submission")).ToHaveTextAsync("Captured name: Žluťoučký 東京 🧭 before blur");
            await Assertions.Expect(editor.GetByTestId("scenario-counts")).ToContainTextAsync("writes: 1");
            await Shot("identity");
            await Tab("Runtime");
            await editor.GetByTestId("agents-catalog-thinking-effort").SelectOptionAsync(new SelectOptionValue { Label = "None" });
            await Assertions.Expect(editor.GetByTestId("agents-catalog-thinking-effort").Locator("option:checked")).ToHaveTextAsync("None");
            await editor.GetByTestId("agents-catalog-thinking-effort").SelectOptionAsync(new SelectOptionValue { Label = "Provider default" });
            await editor.GetByTestId("agents-catalog-model-override").CheckAsync();
            await editor.GetByTestId("agents-catalog-model").FillAsync("custom-deployment 東京");
            await editor.GetByTestId("agents-catalog-provider").SelectOptionAsync(new SelectOptionValue { Label = "Fixture shared catalog" });
            await Assertions.Expect(editor.GetByTestId("agents-catalog-model-override")).ToHaveCountAsync(0);
            await Assertions.Expect(editor.GetByTestId("agents-catalog-model-choice")).ToBeVisibleAsync();
            await Shot("runtime");
            await Tab("Images");
            await editor.GetByTestId("agents-catalog-image-generation-enabled").CheckAsync();
            await editor.GetByTestId("agents-catalog-image-generation-project-assets").CheckAsync();
            await editor.GetByTestId("agents-catalog-image-generation-provider").SelectOptionAsync(new SelectOptionValue { Label = "Fixture reasoning provider" });
            await Assertions.Expect(editor.GetByTestId("agents-catalog-image-model-choice")).ToBeEnabledAsync();
            await Shot("images");
            await Tab("Voice");
            await editor.GetByTestId("agents-catalog-voice-enabled").CheckAsync();
            await editor.GetByTestId("agents-catalog-voice-override").SelectOptionAsync("alloy");
            await editor.GetByTestId("agents-catalog-voice-override").SelectOptionAsync(string.Empty);
            var save = editor.GetByTestId("agents-catalog-save");
            await save.ScrollIntoViewIfNeededAsync();
            await Assertions.Expect(save).ToBeInViewportAsync(new() { Ratio = 1 });
            await save.FocusAsync();
            await Assertions.Expect(save).ToBeFocusedAsync();
            await Shot("voice-footer");
            foreach (var section in new[] { "Memory", "Project Structure Access", "Workspace Tools", "Secrets", "Process Access", "Capabilities" }) {
                await Tab(section);
                await Assertions.Expect(editor).ToContainTextAsync("retained production section");
            }
            await Scenario("UnknownEffort");
            await Tab("Runtime");
            await Assertions.Expect(save).ToBeDisabledAsync();
            await editor.GetByTestId("agents-catalog-thinking-effort").SelectOptionAsync(new SelectOptionValue { Label = "Provider default" });
            await Assertions.Expect(save).ToBeEnabledAsync();
            await Scenario("UnavailableModel");
            await Tab("Runtime");
            await Assertions.Expect(editor.GetByTestId("agents-catalog-model-selector")).ToContainTextAsync("Unavailable shared model");
            await Shot("unavailable-shared-model");
            await Scenario("LargeCatalog");
            await Tab("Runtime");
            await Assertions.Expect(editor.GetByTestId("agents-catalog-provider").Locator("option")).ToHaveCountAsync(153);
            await editor.GetByTestId("agents-catalog-provider").SelectOptionAsync(new SelectOptionValue { Label = "Fixture provider 149 · 東京" });
            await Scenario("LoadFailure");
            await Assertions.Expect(editor.Locator("form")).ToHaveCountAsync(0);
            await editor.GetByTestId("agents-details-retry-load").ClickAsync();
            await Assertions.Expect(editor.Locator("form")).ToHaveCountAsync(1);
            await Scenario("ProviderFailure");
            await Tab("Runtime");
            await Assertions.Expect(editor).ToContainTextAsync("Injected provider catalog failure");
            await Scenario("RefreshFailure");
            await save.ClickAsync();
            await Assertions.Expect(save).ToBeDisabledAsync();
            await editor.GetByTestId("agents-editor-retry-refresh").ClickAsync();
            await Assertions.Expect(editor.GetByTestId("scenario-counts")).ToHaveTextAsync("Synthetic writes: 1; reads: 1");
            await Scenario("UnknownResult");
            await save.ClickAsync();
            await Assertions.Expect(save).ToBeDisabledAsync();
            await Shot("unknown-write");
            await Scenario("Representative");
            await page.GetByTestId("editor-two").ClickAsync();
            var other = page.GetByTestId("editor-b");
            await editor.GetByRole(AriaRole.Button, new() { Name = "Hold save", Exact = true }).ClickAsync();
            await editor.GetByTestId("agents-catalog-name").FillAsync("Captured A");
            await save.ClickAsync();
            await Assertions.Expect(save).ToBeDisabledAsync();
            await other.GetByTestId("agents-catalog-name").FillAsync("Independent B");
            await editor.GetByRole(AriaRole.Button, new() { Name = "Reopen editor", Exact = true }).ClickAsync();
            await Assertions.Expect(other.GetByTestId("agents-catalog-name")).ToHaveValueAsync("Independent B");
            await Assertions.Expect(editor.GetByTestId("scenario-counts")).ToContainTextAsync("writes: 0");
            await Shot("independent-lifetimes");
            Assert.Empty(errors);
            Assert.Contains(assets, path => path.Contains("material-symbols", StringComparison.Ordinal));
            Assert.Contains(assets, path => path.Contains(".woff", StringComparison.Ordinal));
            Assert.Contains(assets, path => path.Contains("CanDoItAll.AgentFramework.Editor.UiSandbox", StringComparison.Ordinal) && path.Contains(".css", StringComparison.Ordinal));
            Assert.True(await page.EvaluateAsync<bool>("document.fonts.check('16px \"Material Symbols Rounded\"')"));
            Assert.DoesNotContain("canonical runtime database", host.Logs, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Unhandled exception", host.Logs, StringComparison.OrdinalIgnoreCase);
            await File.WriteAllTextAsync(Path.Combine(directory, "served-assets.json"), JsonSerializer.Serialize(assets.Order().ToArray()));

            Task Tab(string label) => editor.GetByRole(AriaRole.Tab, new() { Name = label, Exact = true }).ClickAsync();
            async Task Scenario(string value) {
                await page.GetByTestId("editor-scenario").SelectOptionAsync(value);
                await page.GetByTestId("editor-reset").ClickAsync();
            }
        } catch {
            await Shot("failure");
            await File.WriteAllTextAsync(Path.Combine(directory, "failure.txt"), await page.Locator("body").InnerTextAsync());
            throw;
        }
        Task Shot(string name) => page.ScreenshotAsync(new() { Path = Path.Combine(directory, name + ".png") });
    }
}
