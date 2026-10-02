using System.Text.Json;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

[Trait("Category", "Playwright")]
[Trait("Category", "HostPlatform")]
public sealed class AgentEditorSandboxBrowserTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Complete_editor_runs_independently_with_real_children_assets_and_two_lifetimes(bool published) {
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
        var directory = Path.Combine(PlaywrightTestHostPaths.RepositoryRoot, "output", "playwright", "agent-editor-a2",
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
            await editor.GetByTestId("agents-catalog-auto-approval").CheckAsync();
            await Assertions.Expect(page.GetByTestId("agents-auto-approval-cancel")).ToBeInViewportAsync(new() { Ratio = 1 });
            await Shot("auto-approval-confirmation");
            await page.GetByTestId("agents-auto-approval-cancel").ClickAsync();
            await Assertions.Expect(editor.GetByTestId("agents-catalog-auto-approval")).Not.ToBeCheckedAsync();
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
            await editor.GetByTestId("agents-catalog-delete").ClickAsync();
            await Assertions.Expect(page.GetByTestId("agents-catalog-delete-cancel")).ToBeInViewportAsync(new() { Ratio = 1 });
            await Shot("delete-confirmation");
            await page.GetByTestId("agents-catalog-delete-cancel").ClickAsync();
            await Tab("Memory");
            var alias = editor.GetByTestId("agents-catalog-memory-new-alias");
            await alias.FillAsync("team");
            await editor.GetByTestId("agents-catalog-memory-new-provider").SelectOptionAsync("team-fixture");
            Assert.True(await alias.EvaluateAsync<bool>("element => element.form === null"));
            await alias.PressAsync("Enter");
            await Assertions.Expect(editor.GetByTestId("scenario-counts")).ToContainTextAsync("writes: 1");
            await editor.GetByTestId("agents-catalog-memory-add-binding").ClickAsync();
            await Assertions.Expect(editor.GetByTestId("agents-catalog-memory-remove-team")).ToBeVisibleAsync();
            await alias.FillAsync("Unadded 東京");
            await Shot("memory");
            await Tab("Project Structure Access");
            await editor.GetByTestId("agents-catalog-project-structure-load").ClickAsync();
            await editor.GetByTestId("agents-catalog-project-structure-projects").Locator("input").First.CheckAsync();
            await editor.GetByTestId("agents-catalog-project-structure-task-write").CheckAsync();
            await Assertions.Expect(editor.GetByTestId("agents-catalog-project-structure-all")).Not.ToBeCheckedAsync();
            await Shot("project-access");
            await Tab("Workspace Tools");
            await editor.GetByTestId("agents-catalog-workspace-write").CheckAsync();
            var root = editor.GetByTestId("agents-catalog-workspace-external-roots-input");
            var fixtureRoot = Path.Combine(Path.GetTempPath(), "agent-editor-fixture", "東京");
            await root.FillAsync(fixtureRoot);
            Assert.True(await root.EvaluateAsync<bool>("element => element.form === null"));
            await root.PressAsync("Enter");
            await Assertions.Expect(editor.GetByTestId("scenario-counts")).ToContainTextAsync("writes: 1");
            await editor.GetByTestId("agents-catalog-workspace-external-roots-add").ClickAsync();
            await Assertions.Expect(editor.GetByTestId("agents-catalog-workspace-external-roots-table")).ToContainTextAsync(fixtureRoot);
            await editor.GetByTestId("agents-catalog-storage-read").CheckAsync();
            await editor.GetByTestId("agents-catalog-storage-selection-choose").ClickAsync();
            var storage = page.GetByTestId("agents-catalog-storage-selection-dialog-shell");
            await Assertions.Expect(storage).ToBeVisibleAsync();
            await page.GetByTestId("agents-catalog-storage-selection-dialog-option-11111111110011001100111111111111").ClickAsync();
            await Assertions.Expect(page.GetByTestId("agents-catalog-storage-selection-dialog-option-22222222220022002200222222222222")).ToBeDisabledAsync();
            await Shot("storage-picker");
            await page.GetByTestId("agents-catalog-storage-selection-dialog-apply").ClickAsync();
            await Assertions.Expect(editor.GetByTestId("scenario-counts")).ToContainTextAsync("writes: 1");
            await editor.GetByTestId("agents-catalog-workspace-scripts").CheckAsync();
            await page.GetByTestId("agents-workspace-scripts-confirmation-cancel").ClickAsync();
            await Assertions.Expect(editor.GetByTestId("agents-catalog-workspace-scripts")).Not.ToBeCheckedAsync();
            await editor.GetByTestId("agents-catalog-workspace-scripts").CheckAsync();
            await page.GetByTestId("agents-workspace-scripts-confirmation-risk-acknowledgement").CheckAsync();
            await Shot("script-confirmation");
            await page.GetByTestId("agents-workspace-scripts-confirmation-confirm").ClickAsync();
            await editor.GetByTestId("agents-catalog-workspace-scripts-environment").CheckAsync();
            await page.GetByTestId("agents-workspace-environment-confirmation-cancel").ClickAsync();
            await Assertions.Expect(editor.GetByTestId("agents-catalog-workspace-scripts-environment")).Not.ToBeCheckedAsync();
            await Shot("workspace-tools");
            await Tab("Secrets");
            await editor.GetByTestId("agents-catalog-secret-list").Locator("input").First.CheckAsync();
            await Shot("secrets");
            await Tab("Process Access");
            await editor.GetByTestId("agents-catalog-process-write").CheckAsync();
            await Assertions.Expect(editor).ToContainTextAsync("Process definition selection is unavailable");
            await Shot("process-access");
            await Tab("Capabilities");
            await editor.GetByTestId("agents-details-capability-toggle").First.ClickAsync();
            await Assertions.Expect(editor.GetByTestId("scenario-counts")).ToContainTextAsync("writes: 2");
            await Assertions.Expect(editor.GetByTestId("scenario-committed")).ToContainTextAsync("Žluťoučký 東京");
            await editor.GetByTestId("agents-details-capability-verify").First.ClickAsync();
            await Assertions.Expect(editor.GetByTestId("scenario-counts")).ToContainTextAsync("writes: 2");
            await Shot("capabilities");
            await Tab("Memory");
            await Assertions.Expect(alias).ToHaveValueAsync("Unadded 東京");
            Assert.Equal(1, await editor.Locator("form").CountAsync());
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
            await Tab("Project Structure Access");
            await editor.GetByTestId("agents-catalog-project-structure-load").ClickAsync();
            await Assertions.Expect(editor.GetByTestId("agents-catalog-project-structure-projects").Locator("input")).ToHaveCountAsync(100);
            await Shot("large-project-list");
            await Tab("Capabilities");
            await editor.GetByTestId("agents-details-capability-search").FillAsync("Fixture capability 89");
            await Assertions.Expect(editor.GetByTestId("agents-details-capability-toggle")).ToHaveCountAsync(1);
            await Shot("large-filtered-capabilities");
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
            await Assertions.Expect(editor.GetByTestId("scenario-counts")).ToHaveTextAsync("Synthetic writes: 1; reads: 2");
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
        async Task Shot(string name) {
            Assert.True(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= window.innerWidth && window.devicePixelRatio === 1"));
            await page.ScreenshotAsync(new() { Path = Path.Combine(directory, name + ".png") });
        }
    }
}
