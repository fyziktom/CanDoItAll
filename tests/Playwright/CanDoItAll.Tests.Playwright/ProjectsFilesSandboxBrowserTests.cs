using System.Text.Json;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

[Trait("Category", "Playwright")]
[Trait("Category", "HostPlatform")]
public sealed class ProjectsFilesSandboxBrowserTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Both_independent_surfaces_render_real_content_and_assets_without_backend_dependencies(bool published) {
        await using var host = new ProjectsFilesSandboxHost();
        await host.StartAsync(published);
        using var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true, Channel = "chromium" });
        await using var context = await browser.NewContextAsync(new() { ViewportSize = new() { Width = 1920, Height = 1080 } });
        var page = await context.NewPageAsync();
        var errors = new List<string>();
        var assets = new HashSet<string>();
        page.PageError += (_, error) => errors.Add(error);
        page.Response += (_, response) => {
            string path = new Uri(response.Url).AbsolutePath;
            if (response.Status >= 400) {
                errors.Add($"HTTP {response.Status}: {path}");
            }
            if (path.Contains(".css", StringComparison.Ordinal) || path.Contains(".woff", StringComparison.Ordinal) || path.Contains(".js", StringComparison.Ordinal)) {
                assets.Add(path);
            }
        };
        string directory = Path.Combine(PlaywrightTestHostPaths.RepositoryRoot, "output", "playwright", "projects-files-p2", published ? "published" : "source");
        Directory.CreateDirectory(directory);
        try {
            await page.GotoAsync(host.BaseUrl);
            await Assertions.Expect(page.GetByTestId("files-sandbox")).ToHaveAttributeAsync("data-interactive", "true");
            var pane = page.GetByTestId("project-files-portfolio-pane");
            await Assertions.Expect(pane.Locator(".ft-file-browser__item-main")).ToHaveCountAsync(7);
            await page.GetByTestId("files-open-dialog").ClickAsync();
            var dialog = page.GetByTestId("project-files-dialog");
            await Assertions.Expect(dialog.Locator(".ft-file-browser__item-main")).ToHaveCountAsync(7);
            await dialog.Locator(".ft-file-browser__item-main").Filter(new() { HasText = "notes.txt" }).PressAsync("Enter");
            await Assertions.Expect(dialog.GetByTestId("interaction-text-view")).ToContainTextAsync("Žluťoučký 東京");
            await VisibleAsync(dialog);
            await Shot("independent-dialog");
            await dialog.GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true }).ClickAsync();
            await Assertions.Expect(dialog).ToHaveCountAsync(0);
            await Assertions.Expect(pane.Locator(".ft-file-browser__item-main")).ToHaveCountAsync(7);
            foreach (var file in new[] { "notes.txt", "README.md", "data.json", "diagram.mermaid", "diagram.svg", "image.png", "manual.pdf" }) {
                var item = pane.Locator(".ft-file-browser__item-main").Filter(new() { HasText = file });
                await item.FocusAsync();
                await Assertions.Expect(item).ToBeFocusedAsync();
                await item.PressAsync("Enter");
                await VisibleAsync(pane);
                string viewer = file switch {
                    "image.png" => "interaction-image-view",
                    "manual.pdf" => "interaction-pdf-view",
                    "diagram.svg" => "interaction-browser-view",
                    "diagram.mermaid" => "files-fixture-mermaid",
                    "README.md" => "interaction-markdown-view",
                    _ => "interaction-text-view"
                };
                await Assertions.Expect(pane.GetByTestId(viewer)).ToBeVisibleAsync();
                if (file == "image.png") {
                    Assert.True(await pane.GetByTestId(viewer).Locator("img").EvaluateAsync<bool>("image => image.complete && image.naturalWidth > 0 && image.naturalHeight > 0"));
                }
                if (file == "diagram.mermaid") {
                    await Assertions.Expect(pane.GetByTestId(viewer).Locator("svg")).ToBeVisibleAsync();
                }
                if (file is "diagram.svg" or "manual.pdf") {
                    var element = pane.GetByTestId(viewer).Locator(file == "manual.pdf" ? "object" : "iframe");
                    string prefix = await element.EvaluateAsync<string>("async e => { const data = await (await fetch(e.data || e.src)).arrayBuffer(); return new TextDecoder().decode(data.slice(0, 40)); }");
                    Assert.StartsWith(file == "manual.pdf" ? "%PDF-1.4" : "<svg", prefix);
                }
                await Shot(file.Replace('.', '-'));
                await pane.GetByTestId("project-files-portfolio-back").ClickAsync();
            }
            await pane.GetByRole(AriaRole.Button, new() { Name = "Actions for notes.txt", Exact = true }).ClickAsync();
            var menu = pane.GetByRole(AriaRole.Group, new() { Name = "Actions for notes.txt", Exact = true });
            await Assertions.Expect(menu).ToBeInViewportAsync(new() { Ratio = 1 });
            await Shot("action-menu");
            await menu.GetByRole(AriaRole.Button, new() { Name = "Download", Exact = true }).ClickAsync();
            await Assertions.Expect(pane).ToContainTextAsync("Simulated: Download");
            await page.GetByTestId("files-fail-read").ClickAsync();
            await Assertions.Expect(pane).ToContainTextAsync("Injected fixture source read failure.");
            await pane.GetByRole(AriaRole.Button, new() { Name = "Retry", Exact = true }).ClickAsync();
            await Assertions.Expect(pane.Locator(".ft-file-browser__item-main")).ToHaveCountAsync(7);
            await page.GetByTestId("files-hold").ClickAsync();
            await pane.GetByTestId("project-files-portfolio-refresh").ClickAsync();
            await page.GetByTestId("files-remove-source").ClickAsync();
            await page.GetByTestId("files-release").ClickAsync();
            await Assertions.Expect(pane).ToContainTextAsync("1 synthetic source(s)");
            await Assertions.Expect(pane.Locator(".ft-file-browser__item-main")).ToHaveCountAsync(7);
            await Scenario("Large");
            await Assertions.Expect(pane.Locator(".ft-file-browser__list tbody tr")).ToHaveCountAsync(50);
            await pane.GetByLabel("Search scope", new() { Exact = true }).SelectOptionAsync("Progressive");
            await pane.GetByLabel("Search files and folders", new() { Exact = true }).FillAsync("notes-219");
            await Assertions.Expect(pane.Locator(".ft-file-browser__item-main")).ToHaveCountAsync(1);
            await Assertions.Expect(pane).ToContainTextAsync("220 inspected");
            await Shot("large-search");
            await Scenario("Empty");
            await Assertions.Expect(pane).ToContainTextAsync("No projects match the shared filters");
            await Scenario("MissingSource");
            await Assertions.Expect(pane).ToContainTextAsync("This synthetic source is no longer available.");
            await Shot("unavailable-source");
            Assert.Contains(assets, path => path.Contains("material-symbols", StringComparison.Ordinal));
            Assert.Contains(assets, path => path.Contains(".woff", StringComparison.Ordinal));
            Assert.Contains(assets, path => path.Contains("CanDoItAll.Projects.Files.UiSandbox", StringComparison.Ordinal) && path.Contains(".css", StringComparison.Ordinal));
            Assert.True(await page.EvaluateAsync<bool>("document.fonts.check('16px \"Material Symbols Rounded\"')"));
            Assert.Empty(errors);
            Assert.DoesNotContain("canonical runtime database", host.Logs, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Unhandled exception", host.Logs, StringComparison.OrdinalIgnoreCase);
            await File.WriteAllTextAsync(Path.Combine(directory, "served-assets.json"), JsonSerializer.Serialize(assets.Order().ToArray()));
        } catch {
            await Shot("failure");
            await File.WriteAllTextAsync(Path.Combine(directory, "failure.txt"), await page.Locator("body").InnerTextAsync());
            throw;
        }

        Task Shot(string name) => page.ScreenshotAsync(new() { Path = Path.Combine(directory, name + ".png") });
        async Task Scenario(string value) {
            await page.GetByTestId("files-scenario").SelectOptionAsync(value);
            await page.GetByTestId("files-reset").ClickAsync();
        }
    }

    private static async Task VisibleAsync(ILocator surface) {
        await Assertions.Expect(surface.GetByTestId("file-interaction")).ToBeVisibleAsync();
        var bounds = await surface.GetByTestId("file-interaction").BoundingBoxAsync();
        Assert.NotNull(bounds);
        Assert.True(bounds.Width >= 640);
        await Assertions.Expect(surface.GetByTestId("interaction-mode-edit")).ToBeDisabledAsync();
    }
}
